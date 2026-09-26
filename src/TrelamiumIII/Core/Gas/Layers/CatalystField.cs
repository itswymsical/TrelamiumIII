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

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTexture = null;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // A high floor: the solvent glows, so it should read even in the dark.
        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.45f));

        effect.Parameters["Threshold"]?.SetValue(0.13f);
        effect.Parameters["NoiseScale"]?.SetValue(0.9f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.08f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.05f));

        effect.Parameters["FlowStrength"]?.SetValue(0.1f);
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.5f));

        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.8f);

        effect.Parameters["Bands"]?.SetValue(7f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.9f);

        // Sampled from the Catalyzer Gun's brass tubing.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.52f, 0.36f, 0.17f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.80f, 0.52f, 0.20f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.98f, 0.77f, 0.55f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.71f, 0.45f, 0.14f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.85f);
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(0.98f, 0.87f, 0.75f));
        effect.Parameters["HighlightAlpha"]?.SetValue(0.8f);
    }

    protected override void ApplyLighting(ref FieldNode node)
        => Lighting.AddLight(node.Position, 0.30f * node.Alpha, 0.19f * node.Alpha, 0.06f * node.Alpha);
}
