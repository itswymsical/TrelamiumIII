using System.Collections.Generic;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues;

/// <summary>What an NPC leaves on the ground: which residue, in what colour, and when.</summary>
/// <param name="Residue">An <see cref="IResidue" /> type. Build one with <see cref="Of{T}" />.</param>
public sealed record NPCTrail(Type Residue)
{
    public static NPCTrail Of<T>() where T : class, IResidue => new(typeof(T));

    /// <summary>The colour each mark takes from the NPC, or null for the residue's own.</summary>
    public Func<NPC, Color?>? Tint { get; init; }

    /// <summary>Ticks each mark lasts, or a negative value for the residue's own.</summary>
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
///         Register a trail for one NPC type with <see cref="Register" />, or for a whole family
///         with <see cref="RegisterAIStyle" />; a type wins over its AI style, and
///         <see cref="Exclude" /> stops a type leaving anything at all. Modded NPC types are
///         assigned by <c>SetStaticDefaults</c>, so register those from there or later.
///     </para>
///     <para>
///         Nothing is synced. Landing and dying are simulated on every machine already, so each
///         lays its own marks.
///     </para>
/// </remarks>
public sealed class NPCTrails : GlobalNPC
{
    private static readonly Dictionary<int, NPCTrail?> byType = [];

    private static readonly Dictionary<int, NPCTrail> byAIStyle = [];

    public static void Register(int npcType, NPCTrail trail) => byType[npcType] = trail;

    public static void RegisterAIStyle(int aiStyle, NPCTrail trail) => byAIStyle[aiStyle] = trail;

    /// <summary>Stops <paramref name="npcType" /> leaving anything, including what its AI style would.</summary>
    public static void Exclude(int npcType) => byType[npcType] = null;

    public static NPCTrail? For(NPC npc)
        => byType.TryGetValue(npc.type, out NPCTrail? trail) ? trail : byAIStyle.GetValueOrDefault(npc.aiStyle);

    public override void Load()
    {
        RegisterAIStyle(NPCAIStyleID.Slime, NPCTrail.Of<SlimedResidue>() with { Tint = SlimeTint });

        // Critters that happen to hop with the slime AI. They have no gel to leave.
        Exclude(NPCID.Grasshopper);
        Exclude(NPCID.GoldGrasshopper);

        Register(NPCID.LavaSlime, NPCTrail.Of<MagmaResidue>() with
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
        if (For(npc) is { LandingRadius: > 0f } trail && npc.velocity.Y == 0f && npc.oldVelocity.Y >= trail.LandingSpeed)
            Mark(npc, trail, npc.Bottom + new Vector2(0f, 8f), trail.LandingRadius, trail.LandingIntensity);

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
        if (For(npc) is { DeathRadius: > 0f } trail)
            Mark(npc, trail, npc.Center, trail.DeathRadius + Math.Max(npc.width, npc.height) * 0.5f, trail.DeathIntensity);
    }

    private static void Mark(NPC npc, NPCTrail trail, Vector2 center, float radius, float intensity)
    {
        if (ResidueSystem.Get(trail.Residue) is not { } residue)
            return;

        ResidueSystem.ApplyCircle(residue, center, radius, intensity, duration: trail.Duration, tint: trail.Tint?.Invoke(npc));
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
