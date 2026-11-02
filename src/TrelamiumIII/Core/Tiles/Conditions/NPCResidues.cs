using System.Collections.Generic;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>What an NPC leaves on the ground: which condition, in what colour, and when.</summary>
/// <param name="Condition">An <see cref="ITileCondition" /> type. Build one with <see cref="Of{T}" />.</param>
public sealed record NPCResidue(Type Condition)
{
    public static NPCResidue Of<T>() where T : class, ITileCondition => new(typeof(T));

    /// <summary>The colour each mark takes from the NPC, or null for the condition's own.</summary>
    public Func<NPC, Color?>? Tint { get; init; }

    /// <summary>Ticks each mark lasts, or a negative value for the condition's own.</summary>
    public float Duration { get; init; } = -1f;

    /// <summary>Pixels around its feet marked when it lands hard. Zero for none.</summary>
    public float LandingRadius { get; init; } = 20f;

    public float LandingIntensity { get; init; } = 0.4f;

    /// <summary>Fall speed, in pixels per tick, that counts as landing hard rather than stepping down.</summary>
    public float LandingSpeed { get; init; } = 5f;

    /// <summary>Pixels marked around it, past its own size, when it dies. Zero for none.</summary>
    public float DeathRadius { get; init; } = 28f;

    public float DeathIntensity { get; init; } = 0.85f;
}

/// <summary>
///     Lets NPCs mark the terrain: slimes splatter gel in their own colour when they land or burst,
///     lava slimes leave magma.
/// </summary>
/// <remarks>
///     <para>
///         Register a residue for one NPC type with <see cref="Register" />, or for a whole family
///         with <see cref="RegisterAIStyle" />; a type wins over its AI style, and
///         <see cref="Exclude" /> stops a type leaving anything at all. Modded NPC types are
///         assigned by <c>SetStaticDefaults</c>, so register those from there or later.
///     </para>
///     <para>
///         Nothing is synced. Landing and dying are simulated on every machine already, so each
///         lays its own marks.
///     </para>
/// </remarks>
public sealed class NPCResidues : GlobalNPC
{
    private static readonly Dictionary<int, NPCResidue?> byType = [];

    private static readonly Dictionary<int, NPCResidue> byAIStyle = [];

    public static void Register(int npcType, NPCResidue residue) => byType[npcType] = residue;

    public static void RegisterAIStyle(int aiStyle, NPCResidue residue) => byAIStyle[aiStyle] = residue;

    /// <summary>Stops <paramref name="npcType" /> leaving anything, including what its AI style would.</summary>
    public static void Exclude(int npcType) => byType[npcType] = null;

    public static NPCResidue? For(NPC npc)
        => byType.TryGetValue(npc.type, out NPCResidue? residue) ? residue : byAIStyle.GetValueOrDefault(npc.aiStyle);

    public override void Load()
    {
        RegisterAIStyle(NPCAIStyleID.Slime, NPCResidue.Of<SlimedCondition>() with { Tint = SlimeTint });

        // Critters that happen to hop with the slime AI. They have no gel to leave.
        Exclude(NPCID.Grasshopper);
        Exclude(NPCID.GoldGrasshopper);

        Register(NPCID.LavaSlime, NPCResidue.Of<MagmaCondition>() with
        {
            LandingIntensity = 0.75f,
            DeathIntensity = 1f,
        });
    }

    public override void Unload()
    {
        byType.Clear();
        byAIStyle.Clear();
    }

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && For(entity) is not null;

    /// <summary>
    ///     Before the AI, velocity is what collision left last tick and <c>oldVelocity</c> what it
    ///     was going into it, so a fall that was just stopped dead reads as one tick of exactly that.
    /// </summary>
    public override bool PreAI(NPC npc)
    {
        if (For(npc) is { LandingRadius: > 0f } residue && npc.velocity.Y == 0f && npc.oldVelocity.Y >= residue.LandingSpeed)
            Mark(npc, residue, npc.Bottom + new Vector2(0f, 8f), residue.LandingRadius, residue.LandingIntensity);

        return true;
    }

    /// <summary>Clients and single player. A server has no hit effects to run, so it marks from <see cref="OnKill" />.</summary>
    public override void HitEffect(NPC npc, NPC.HitInfo hit)
    {
        if (npc.life <= 0 && Main.netMode != NetmodeID.Server)
            Burst(npc);
    }

    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.Server)
            Burst(npc);
    }

    private static void Burst(NPC npc)
    {
        if (For(npc) is { DeathRadius: > 0f } residue)
            Mark(npc, residue, npc.Center, residue.DeathRadius + Math.Max(npc.width, npc.height) * 0.5f, residue.DeathIntensity);
    }

    private static void Mark(NPC npc, NPCResidue residue, Vector2 center, float radius, float intensity)
    {
        if (TileConditionSystem.Get(residue.Condition) is not { } condition)
            return;

        TileConditionSystem.ApplyCircle(condition, center, radius, intensity, duration: residue.Duration, tint: residue.Tint?.Invoke(npc));
    }

    /// <summary>
    ///     A slime's own colour: the tint vanilla draws it with, or for the slimes whose texture is
    ///     already coloured, a hand-picked match.
    /// </summary>
    private static Color? SlimeTint(NPC npc)
    {
        if (npc.type == NPCID.RainbowSlime)
            return Main.DiscoColor;

        if (npc.color.A > 0)
            return npc.color with { A = 255 };

        return npc.type switch
        {
            NPCID.JungleSlime or NPCID.SpikedJungleSlime => new Color(110, 200, 40),
            NPCID.IceSlime or NPCID.SpikedIceSlime => new Color(150, 220, 255),
            NPCID.SandSlime => new Color(220, 190, 100),
            NPCID.DungeonSlime => new Color(110, 110, 150),
            NPCID.CorruptSlime => new Color(130, 80, 160),
            NPCID.Crimslime => new Color(200, 40, 50),
            NPCID.IlluminantSlime => new Color(240, 90, 220),
            NPCID.ToxicSludge => new Color(100, 200, 40),
            NPCID.GoldenSlime => new Color(255, 210, 60),
            NPCID.ShimmerSlime => new Color(180, 140, 255),
            _ => null,
        };
    }
}
