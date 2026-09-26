using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Content.Items.Rusted;

namespace TrelamiumIII.Core.Gas.Layers;

public class ThermiteFlaskField : ChemicalGasField
{
    private const float CONVERSION_COVERAGE = 2.5f;

    /// <summary>Pixels of terrain charred where an arm of the burst strikes it.</summary>
    private const float SCORCH_RADIUS = 26f;

    /// <summary>Molten liquid; the brightest of the pooling layers, so last of them.</summary>
    public override int LoadOrder => 40;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.BloomFlare.KEY;
    protected override int GasType => GasTypeID.Liquid;
    protected override float DefaultDensity => 1.0f;
    protected override float DefaultViscosity => 0.2f;
    protected override float EnclosedLifePenalty => 0.5f;
    protected override float GravityScale => 0.33f;
    protected override float BuoyancyForce => 0.5f;
    protected override float Drag => 0.9956f;
 
    protected override float LifeDecay => 0.35f;

    protected override float DefaultTemperature => 2400f;

    protected override Vector2 TemperatureRange => new(700f, 2400f);

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        base.UpdateNode(ref node, index);

        node.Temperature = MathHelper.Lerp(700f, 2400f, node.Alpha);
    }

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 1;

    private static readonly ClingBehaviour Spatter = new SplashCling(6, 8, 16.5f, 200f, SoundID.Item74);

    public override ClingBehaviour? ClingBehaviour => Spatter;

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();

        noiseTex = Assets.Textures.Noise.Melt_03.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);


        Vector3 light = new Vector3(0.55f); // ambient floor; the real light rides in the field per node

        effect.Parameters["WorldLight"]?.SetValue(light);


        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.05f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.08f));

        effect.Parameters["FlowStrength"]?.SetValue(0.20f);

        effect.Parameters["TemperatureBlend"]?.SetValue(0.55f);
        effect.Parameters["HotColor"]?.SetValue(new Vector3(1f, 0.93f, 0.72f));
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.45f));

        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.8f);

        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.94f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.72f, 0.38f, 0.01f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.75f, 0.22f, 0.01f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.94f, 0.21f, 0f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.95f, 0.15f, 0f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(1f, 0.77f, 0.22f));
        effect.Parameters["HighlightAlpha"]?.SetValue(0.9f);
    }

    protected override void ApplyLighting(ref FieldNode node)
    {
        Dust.NewDust(node.Position / 16, 16, 16, DustID.Lava);
        Lighting.AddLight(node.Position, new Vector3(1f, 0.77f, 0.22f));
    }

    // BindAdditionalTextures used to pin the noise into sampler register 1 by hand, which was
    // redundant with the uNoiseTex parameter above and became actively wrong once the shader gained
    // a second sampler: registers are assigned in declaration order, so writing one by index now
    // lands on whichever texture happens to sit there -- the attribute target, whose flow would
    // then be read out of a noise texture. The effect parameter binds it to the right register.

    /// <summary>Thermite left on a floor keeps burning as a thermal residue.</summary>
    protected override ChemicalReactionTileCondition? Residue => TileConditionSystem.Get<ThermalResidueCondition>();

    /// <summary>
    /// Just over one node's worth, so a spray that merely passes over terrain leaves nothing and
    /// only thermite that has actually settled starts working on it.
    /// </summary>
    protected override float TileFieldThreshold => 1.15f;

    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.OnFire, 120);
    }

    /// <summary>Per-node response only: the node thickens and slows in liquid, nothing else.</summary>
    /// <remarks>
    /// The conversions this used to do live in <see cref="OnLiquidField"/> now. Run from here they
    /// fired once per node per frame, which meant a single invisible tail-end node was enough to
    /// flash a full tile of water to obsidian -- and three of them did it three times over.
    /// </remarks>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        base.OnLiquidContact(ref node, tileX, tileY, tile);

        node.Life *= 0.99f;
    }

    /// <summary>
    /// Boils off a pool the thermite is genuinely sitting in, leaving the appropriate slag.
    /// </summary>
    /// <remarks>
    /// Drain scales with coverage, so a thin film takes its time and a deep pour goes quickly, and
    /// each pass only removes a slice -- the pool visibly recedes instead of vanishing on the frame
    /// of contact.
    /// </remarks>
    protected override void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (density < CONVERSION_COVERAGE)
            return;

        int drain = (int)MathHelper.Clamp(density * 5f, 3f, 20f);

        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.Obsidian, LiquidID.Water, drain, amountLeft: 50);
        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, (ushort)ModContent.TileType<RustedIronOre>(), LiquidID.Lava, drain, amountLeft: 50);
        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.CrispyHoneyBlock, LiquidID.Honey, drain, amountLeft: 50);
    }

    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity *= 1.01f;
        node.Velocity *= 0.89f;
    }

    /// <summary>Molten metal chars whatever it lands on, then carries on bouncing.</summary>
    /// <remarks>
    ///     Lives on the layer rather than on the flask, which is where the burst projectile used to
    ///     keep it. Scorching is something thermite does; it should not have to be restated by every
    ///     future weapon that throws some.
    /// </remarks>
    protected override bool OnEmitterBounce(ref GasEmitter emitter, Vector2 normal, int tileX, int tileY)
    {
        BurnedCondition.Scorch(emitter.Position, SCORCH_RADIUS, profile: BurnProfile.Thermal);

        return true;
    }
}
