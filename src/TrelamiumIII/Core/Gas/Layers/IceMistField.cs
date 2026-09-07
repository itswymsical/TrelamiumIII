using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;

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
    ///     <see cref="ThermiteLayer" /> for what was wrong with that.
    /// </remarks>
    private const float CONVERSION_COVERAGE = 1.6f;

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
    public override float ChemicalDamage => 0.3f;
    public override int AfflictionDuration => 60;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 3f;


    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);

        int stride = Math.Max(1, gasNodes.Count / 16);

        Vector3 light = new Vector3(0.22f); // ambient floor; the real light rides in the field per node

        effect.Parameters["WorldLight"]?.SetValue(light);

        // Field target is screen / PixelScale(2): one field texel = one 2px vanilla pixel.
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(
            2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Airy mist: softer erosion than the spores, slow lateral roll.
        effect.Parameters["Threshold"]?.SetValue(0.14f);
        effect.Parameters["NoiseScale"]?.SetValue(1.4f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.18f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.05f, 0.015f));

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.8f);

        // Frost palette: steel-blue shadow through pale frost to a near-white core,
        // with a bright icy rim (bright outlines read better than dark ink).
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.36f, 0.52f, 0.68f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.62f, 0.80f, 0.92f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.88f, 0.97f, 1.0f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.85f, 0.96f, 1.0f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.85f);
    }

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.Frostburn, 30);
    }

    protected override void OnPlayerContact(ref GasNode node, Player player) { }
    protected override void OnTileContact(ref GasNode node, int tileX, int tileY, Tile tile) { }

    /// <summary>An arm of the burst that catches something leaves frostburn on it.</summary>
    /// <remarks>
    /// What <c>HyperboreanFlaskBurstProjectile.OnHitNPC</c> did. Moved onto the layer because it is
    /// a property of being hit by flash-frozen mist rather than of the flask that threw it, and
    /// because the burst carriers it lived on no longer exist.
    /// </remarks>
    protected override void OnEmitterHitNPC(ref GasEmitter emitter, NPC npc)
    {
        base.OnEmitterHitNPC(ref emitter, npc);

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
    protected override void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner)
    {
        if (density < CONVERSION_COVERAGE)
            return;

        int drain = (int)MathHelper.Clamp(density * 4f, 2f, 14f);

        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.BreakableIce, LiquidID.Water, drain, amountLeft: 200);
    }
}