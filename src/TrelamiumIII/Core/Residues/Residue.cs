using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues;

/// <summary>
///     Base for every <see cref="IResidue" />: a default for each member of the contract, plus
///     the handful of drawing and sampling helpers a residue normally wants.
/// </summary>
/// <remarks>
///     <para>
///         Derived types are discovered and registered automatically by
///         <see cref="ResidueSystem" />, so a new residue is one file and nothing else. It
///         needs a parameterless constructor and it must not keep per-tile state: see
///         <see cref="IResidue" /> for why.
///     </para>
///     <para>
///         A coat that is only a palette and some rules for what touches it -- slime, mud, a stain --
///         should extend <see cref="FilmResidue" /> instead, which does the drawing.
///     </para>
/// </remarks>
public abstract class Residue : IResidue
{
    private readonly HashSet<Entity> touchedThisTick = [];

    /// <summary>
    ///     True the first time <paramref name="entity" /> touches this residue this tick, false
    ///     after.
    /// </summary>
    /// <remarks>
    ///     Contact hooks run once per marked tile under the hitbox, so a player standing across three
    ///     slimed tiles is slowed three times over. Anything that should happen once per entity --
    ///     drag, a debuff, a sound -- belongs behind this.
    /// </remarks>
    protected bool FirstContact(Entity entity) => touchedThisTick.Add(entity);

    /// <summary>
    ///     Gives an NPC a debuff from standing in this. Every machine runs contacts, and in multiplayer every
    ///     <see cref="NPC.AddBuff" /> is packets to everyone, so each only asks once what it knows of has run below half.
    /// </summary>
    protected static void ContactDebuff(NPC npc, int buffType, int ticks)
    {
        if (Main.netMode != NetmodeID.SinglePlayer && npc.FindBuffIndex(buffType) is >= 0 and var index && npc.buffTime[index] > ticks / 2)
            return;

        npc.AddBuff(buffType, ticks);
    }

    /// <summary>Forgets this tick's contacts. Called by the system before <see cref="UpdateGlobal" />.</summary>
    internal void ResetContacts() => touchedThisTick.Clear();

    public virtual string Name => GetType().Name;

    public virtual float Duration => 8f * 60f;

    public virtual int MaxTiles => 1024;

    public virtual void Load() { }

    public virtual void Unload() { }

    public virtual bool CanApplyTo(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);

        return tile.HasTile && Main.tileSolid[tile.TileType];
    }

    public virtual void OnApplied(ref ResidueInstance instance) { }

    public virtual void OnExpired(in ResidueInstance instance) { }

    public virtual bool CanRemain(in ResidueInstance instance) => instance.Tile.HasTile;

    public virtual void UpdateTile(ref ResidueInstance instance) { }

    public virtual void UpdateGlobal() { }

    public virtual void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount) { }

    public virtual void OnPlayerContact(ref ResidueInstance instance, Player player) { }

    public virtual void OnNPCContact(ref ResidueInstance instance, NPC npc) { }

    public virtual void OnProjectileContact(ref ResidueInstance instance, Projectile projectile) { }

    public virtual ResidueElement ElementsOf(in ResidueInstance instance) => ResidueElement.None;

    public virtual float PotencyOf(in ResidueInstance instance) => instance.Strength;

    public virtual void Diminish(ref ResidueInstance instance, float share) => instance.Life *= 1f - share;

    public virtual ResidueRenderMode RenderMode => ResidueRenderMode.None;

    public virtual BlendState BlendState => BlendState.AlphaBlend;

    public virtual int CanvasPixelScale => 2;

    public virtual int FadeInTicks => 8;

    public virtual int FadeOutTicks => 12;

    public virtual Asset<Effect>? Shader => null;

    public virtual TileCanvasSample EncodeCanvas(in ResidueInstance instance) => new(instance.Strength);

    public virtual void ApplyShaderParameters(Effect effect) { }

    private const float BODY_DETAIL_HEIGHT = 52f;

    private static readonly Point[] SilhouetteOnly = [Point.Zero];

    public virtual Point[] BodyReach => SilhouetteOnly;

    public virtual Bodies.BodyFlow BodyFlow => new();

    public virtual Bodies.BodyShed? BodyShed => null;

    /// <summary>Nothing by default: most of what rubs off is worth saying so.</summary>
    public virtual IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = 0f;

        return null;
    }

    /// <summary>The ground's own encoding, so a shader reads the same channels off a body. Thickens fast.</summary>
    public virtual TileCanvasSample EncodeBody(float amount, int age, Color tint)
    {
        var instance = new ResidueInstance
        {
            Life = 1f,
            MaxLife = 1f,
            Intensity = MathHelper.Clamp(amount * 3f, 0f, 1f),
            TotalAge = age,
            Tint = tint,
        };

        return EncodeCanvas(in instance);
    }

    public virtual void ApplyBodyParameters(Effect effect, float gravDir, float height) => ApplyShaderParameters(effect);

    /// <summary>What to scale a noise's cycles per tile by, so that a body three tiles tall keeps its detail and a boss gets it broader rather than finer.</summary>
    protected static float BodyDetail(float height) => MathHelper.Clamp(BODY_DETAIL_HEIGHT / height, 0.3f, 1f);

    public virtual void UpdateBody(Entity body, float fill) { }

    /// <summary>
    ///     Breaks the tile under this mark, the way an explosion would, if an explosion could. Ends the mark if it does.
    /// </summary>
    /// <remarks>
    ///     Clients leave it to the server, which sends the result. Call this when the tile is really going, not to ask
    ///     whether it could: a tile's own <c>CanExplode</c> may change the tile as it answers, as modded grass does.
    /// </remarks>
    /// <returns>Whether the tile is gone.</returns>
    protected static bool TryDestroyTile(ref ResidueInstance instance)
    {
        if (!TryBreakTile(instance.X, instance.Y))
            return false;

        instance.Life = 0f;

        return true;
    }

    /// <inheritdoc cref="TryDestroyTile" />
    internal static bool TryBreakTile(int x, int y)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !Framing.GetTileSafely(x, y).HasTile)
            return false;

        if (!Main.projectile[0].CanExplodeTile(x, y) || !WorldGen.CanKillTile(x, y))
            return false;

        WorldGen.KillTile(x, y, noItem: true);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, x, y, 1);

        return true;
    }

    /// <summary>Whether the tile is close enough to the camera to be worth spending anything on.</summary>
    /// <remarks>
    ///     Per-tile update runs for the whole batch, on and off screen alike, because the simulation
    ///     has to stay consistent. Dust, light and sound dont, and a thousand tiles all rolling for
    ///     an ember two screens away is pure cost.
    /// </remarks>
    protected static bool OnScreen(in ResidueInstance instance, float padding = 64f)
    {
        Vector2 relative = instance.WorldCenter - Main.screenPosition;

        return relative.X >= -padding
               && relative.Y >= -padding
               && relative.X <= Main.screenWidth + padding
               && relative.Y <= Main.screenHeight + padding;
    }
}
