using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

/// <summary>
///     Catalytic solvent: an amber liquid that falls and pools. It deals no damage of its own; contact catalyzes,
///     amplifying every other chemical on the target.
/// </summary>
public class CatalystField : ChemicalGasField
{
    /// <summary>Liquid; over blood and under thermite, the brightest pooling layer.</summary>
    public override int LoadOrder => 35;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.Bloom.KEY;
    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 1.1f;
    protected override float DefaultViscosity => 0.5f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.1f;
    protected override float BuoyancyForce => 0.45f;
    protected override float Drag => 0.995f;
    protected override float LifeDecay => 0.9f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Solute;
    public override int ChemicalDamage => 0;
    public override int Concentration => 0;
    public override float Catalysis => 0.15f;
    public override int ReactionChance => 0;
    public override int ReactionDamage => 0;

    /// <summary>Brass, from the Catalyzer Gun's tubing; self-lit, so it reads in the dark.</summary>
    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.45f),
        Threshold = 0.13f,
        NoiseScale = 0.9f,
        NoiseStrength = 0.08f,
        Drift = new Vector2(0f, -0.05f),
        FlowStrength = 0.1f,
        NoiseStretch = new Vector2(1f, 0.5f),
        BodyAlphaFloor = 0.8f,
        Bands = 7f,
        BodyAlpha = 0.9f,
        Shadow = new Vector3(0.52f, 0.36f, 0.17f),
        Body = new Vector3(0.80f, 0.52f, 0.20f),
        Core = new Vector3(0.98f, 0.77f, 0.55f),
        Outline = new Vector3(0.71f, 0.45f, 0.14f),
        OutlineAlpha = 0.85f,
        Highlight = new Vector3(0.98f, 0.87f, 0.75f),
        HighlightAlpha = 0.8f,
    };

    protected override void ApplyLighting(ref FieldNode node)
        => Lighting.AddLight(node.Position, 0.30f * node.Alpha, 0.19f * node.Alpha, 0.06f * node.Alpha);
}
