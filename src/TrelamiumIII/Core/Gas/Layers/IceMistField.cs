using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class IceMistField : ChemicalGasField
{
    /// <summary>
    ///     Pooled coverage a tile of liquid needs before the mist sitting on it freezes over.
    /// </summary>
    /// <remarks>
    ///     Lower than thermite's, in the same proportion the two layers' tile thresholds differ:
    ///     mist is thin, and a frost that only formed under a dense cloud would almost never appear.
    ///     Replaces a <c>HyperboreanFlaskProjectile.shattered</c> static -- see
    ///     <see cref="ThermiteFlaskField" /> for what was wrong with that.
    /// </remarks>
    private const float CONVERSION_COVERAGE = 1.6f;

    /// <summary>Thin frost; reads as haze over whatever it drifts across.</summary>
    public override int LoadOrder => 80;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;
    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 0.35f;
    protected override float DefaultViscosity => 0.25f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.04f;
    protected override float BuoyancyForce => 0.1f;
    protected override float Drag => 0.936f;
    protected override float LifeDecay => 0.75f;
    protected override BlendState CompositeBlendState => BlendState.AlphaBlend;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 60;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 3;


    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.22f),
        Threshold = 0.14f,
        NoiseScale = 1.4f,
        NoiseStrength = 0.18f,
        Drift = new Vector2(0.05f, 0.015f),
        FlowStrength = 0.20f,
        Bands = 8f,
        BodyAlpha = 0.8f,
        Shadow = new Vector3(0.36f, 0.52f, 0.68f),
        Body = new Vector3(0.62f, 0.80f, 0.92f),
        Core = new Vector3(0.88f, 0.97f, 1.0f),
        Outline = new Vector3(0.85f, 0.96f, 1.0f),
        OutlineAlpha = 0.85f,
    };

    protected override int ContactBuff => BuffID.Frostburn;

    protected override int ContactBuffTicks => 30;

    protected override void OnPlayerContact(ref FieldNode node, Player player) { }
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile) { }

    /// <summary>An arm of the burst that catches something leaves frostburn on it.</summary>
    /// <remarks>
    /// What <c>HyperboreanFlaskBurstProjectile.OnHitNPC</c> did. Moved onto the layer because it is
    /// a property of being hit by flash-frozen mist rather than of the flask that threw it, and
    /// because the burst carriers it lived on no longer exist.
    /// </remarks>
    protected override void OnEmitterHitNPC(ref GasEmitter emitter, NPC npc)
    {
        if (!Strike(in emitter, npc))
            return;

        emitter.Payload?.Hit(npc, this);
        npc.AddBuff(BuffID.Frostburn, 60);
    }

    /// <summary>Mist that settles leaves a rime on the surface.</summary>
    protected override ChemicalReactionTileCondition? Residue => TileConditionSystem.Get<CryonicResidueCondition>();

    /// <summary>
    /// Lower than thermite's: mist is thin, and a frost that only forms under a dense cloud would
    /// almost never appear.
    /// </summary>
    protected override float TileFieldThreshold => 0.7f;

    /// <summary>
    /// Freezes over a pool the mist has actually settled on.
    /// </summary>
    /// <remarks>
    /// Moved off the per-node liquid hook for the same reason thermite's conversions were: run
    /// there, one node clipping the surface froze a full tile of water on the frame it touched it.
    /// </remarks>
    protected override void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (density < CONVERSION_COVERAGE)
            return;

        int drain = (int)MathHelper.Clamp(density * 4f, 2f, 14f);

        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.BreakableIce, LiquidID.Water, drain, amountLeft: 200);
    }
}
