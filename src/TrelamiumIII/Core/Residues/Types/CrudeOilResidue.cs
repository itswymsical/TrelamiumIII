using ModLiquidLib.ModLoader;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>
///     A black, oily coat over the terrain, left by crude itself or poured from something. When heat reaches it, it burns
///     along whatever oil is next to it and the tiles blow. See <see cref="Detonation" />.
/// </summary>
public sealed class CrudeOilResidue : FilmResidue
{
    /// <summary>
    ///     <see cref="ResidueInstance.Variant" /> for oil that seeped out of a pool rather than being poured. It goes up the same,
    ///     but leaves the ground standing, or a spark at the edge of a lake would dig the lake out.
    /// </summary>
    public const byte SEEPED = 1;

    private const float PLAYER_DRAG = 0.95f;

    private const float NPC_DRAG = 0.93f;

    /// <summary>Vanilla's debuff for burning hotter, so anything that wades through it and then catches fire suffers for it.</summary>
    private const int OILED_TICKS = 5 * 60;

    /// <summary>Life lost per probe to a full cell of water, which floats it off.</summary>
    private const float WASH_PER_PROBE = 24f;

    protected override FilmLook Look { get; } = new()
    {
        Threshold = 0.2f,
        NoiseScale = 0.05f,
        NoiseStrength = 0.16f,
        NoiseDrift = new Vector2(0.003f, 0.012f),
        Bands = 5f,
        ShadowColor = new Vector3(0.01f, 0.01f, 0.02f),
        BodyColor = new Vector3(0.05f, 0.04f, 0.07f),
        CoreColor = new Vector3(0.11f, 0.085f, 0.15f),
        BodyAlpha = 0.92f,
        OutlineColor = new Vector3(0.2f, 0.16f, 0.28f),
        OutlineAlpha = 0.8f,
        HighlightColor = new Vector3(0.55f, 0.45f, 0.75f),
        HighlightAlpha = 0.55f,
        SurfaceLip = 2f,
    };

    public override float Duration => 40f * 60f;

    public override int MaxTiles => 1500;

    public override int FadeInTicks => 12;

    public override int FadeOutTicks => 40;

    public override ResidueElement ElementsOf(in ResidueInstance instance) => ResidueElement.Volatile;

    /// <summary>Heavy and slow, and it stays on you.</summary>
    public override Bodies.BodyFlow BodyFlow => new(Rise: 0f, Fall: 0.05f, Creep: 0.02f, Sag: 0.02f, Decay: 0.0015f, Reach: 0.35f);

    public override Bodies.BodyShed? BodyShed => new(Rate: 0.07f, Radius: 4.5f, Life: 40f, Launch: new Vector2(0f, 0.05f), Scatter: 0.08f, Pull: new Vector2(0f, 0.08f), Drag: 0.98f, Cling: 0.15f);

    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Strength * 0.06f;

        return this;
    }

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        if (FirstContact(player))
            player.velocity.X *= MathHelper.Lerp(1f, PLAYER_DRAG, instance.Strength);
    }

    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        if (npc.noTileCollide || !FirstContact(npc))
            return;

        npc.velocity.X *= MathHelper.Lerp(1f, NPC_DRAG, instance.Strength);

        if (!npc.friendly)
            ContactDebuff(npc, BuffID.Oiled, OILED_TICKS);
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
        {
            BurnedResidue.Ignite(instance.X, instance.Y, BurnProfile.Fuse, instance.Strength, instance.Owner);

            return;
        }

        // Still in the pool it came from, so it's going nowhere.
        if (liquidType == LiquidLoader.LiquidType<OilLiquids>())
        {
            instance.Life = instance.MaxLife;

            return;
        }

        instance.Life -= WASH_PER_PROBE * amount / 255f;
    }

    /// <summary>Whether the liquid at a cell leaves this behind rather than only a stain. Sunflower oil is food.</summary>
    public static bool SeepsFrom(int x, int y)
        => Main.tile[x, y].LiquidType == LiquidLoader.LiquidType<OilLiquids>() && OilLiquids.Instance.InternalLiqAt(x, y) != OilLiquids.Instance.Sunflower;

    /// <summary>
    ///     Keeps a tile beside a pool coated, as a <see cref="ResidueRule" /> does, so each machine lays its own. Nothing seeps
    ///     back onto a tile that's still burning, or it would go up again at once for as long as the pool lasts.
    /// </summary>
    public static bool Seep(int i, int j, float intensity = 1f)
    {
        if (ResidueSystem.HasElement(i, j, ResidueElement.Heat))
            return false;

        if (ResidueSystem.TryGet<CrudeOilResidue>(i, j, out ResidueInstance existing) && existing.Fade > 0.5f)
            return true;

        return ResidueSystem.Apply<CrudeOilResidue>(i, j, intensity, origin: ResidueOrigin.Local, variant: SEEPED);
    }

    /// <summary>Pours it over everything solid within <paramref name="radius" /> pixels, thinning toward the edge.</summary>
    public static void Pour(Vector2 center, float radius, float intensity = 1f, int owner = ResidueInstance.NoOwner)
        => ResidueSystem.ApplyCircle<CrudeOilResidue>(center, radius, intensity, owner: owner);
}
