using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class PoisonGasField : ChemicalGasField
{
    private const int POISON_TICKS = 120;

    /// <summary>Gas.</summary>
    public override int LoadOrder => 60;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.InkMote.KEY;

    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 1.00f;

    protected override float DefaultViscosity => 0.58f;

    protected override float EnclosedLifePenalty => 1.5f;

    protected override float GravityScale => 0.0f;

    protected override float BuoyancyForce => 0.68f;

    protected override float Drag => 0.9998f;

    protected override float LifeDecay => 0.35f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    public override int ChemicalDamage => 1;

    public override int AfflictionDuration => 90;

    public override float AfflictionRate => 30f;

    public override int ReactionChance => 0;

    public override int ReactionDamage => 0;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.24f),
        Threshold = 0.16f,
        NoiseScale = 1.25f,
        NoiseStrength = 0.22f,
        Drift = new Vector2(-0.075f, 0.09f),
        FlowStrength = 0.14f,
        Bands = 8f,
        BodyAlpha = 0.88f,
        Shadow = new Vector3(0.20f, 0.08f, 0.24f),
        Body = new Vector3(0.46f, 0.70f, 0.12f),
        Core = new Vector3(0.68f, 0.90f, 0.22f),
        Outline = new Vector3(0.80f, 1.00f, 0.36f),
        OutlineAlpha = 0.8f,
    };

    protected override void ApplyLighting(ref FieldNode node)
        => Lighting.AddLight(node.Position, 0.10f * node.Alpha, 0.20f * node.Alpha, 0.04f * node.Alpha);

    protected override int ContactBuff => BuffID.Poisoned;

    protected override int ContactBuffTicks => POISON_TICKS;
}
