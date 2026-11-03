using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class BloodField : ChemicalGasField
{
    /// <summary>Liquid; over gel so a splash on a puddle still reads.</summary>
    public override int LoadOrder => 30;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.Bloom.KEY;
    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 1.2f;
    protected override float DefaultViscosity => 0.67f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.09f;
    protected override float BuoyancyForce => 0.5f;
    protected override float Drag => 0.9956f;
    protected override float LifeDecay => 0.85f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 4;

    private static readonly ClingBehaviour Splatter = new SplashCling(6, 8, 16.5f, 140f, SoundID.Item74, shake: 1);

    public override ClingBehaviour? ClingBehaviour => Splatter;

    /// <summary>
    ///     Leaves nothing on the terrain it lands on.
    /// </summary>
    protected override ChemicalResidue? Residue => null;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.18f),
        Threshold = 0.12f,
        NoiseScale = 0.75f,
        NoiseStrength = 0.04f,
        Drift = new Vector2(0f, -0.08f),
        FlowStrength = 0.08f,
        NoiseStretch = new Vector2(1f, 0.45f),
        BodyAlphaFloor = 0.8f,
        Bands = 8f,
        BodyAlpha = 0.94f,
        Shadow = new Vector3(0.72f, 0.08f, 0.08f),
        Body = new Vector3(0.55f, 0.07f, 0.03f),
        Core = new Vector3(0.34f, 0.05f, 0.02f),
        Outline = new Vector3(0.76f, 0.01f, 0.02f),
        OutlineAlpha = 0.8f,
        Highlight = new Vector3(0.95f, 0.30f, 0.25f),
        HighlightAlpha = 0.9f,
    };

    protected override int ContactBuff => BuffID.BloodButcherer;

    protected override int ContactBuffTicks => 30;
}