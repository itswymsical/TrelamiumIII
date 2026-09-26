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

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.24f));


        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.25f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.075f, 0.09f));

        // Drifting cloud: the texture travels with it, so a puff blown sideways carries its own detail instead of swimming through a world-locked one.
        effect.Parameters["FlowStrength"]?.SetValue(0.14f);

        effect.Parameters["Bands"]?.SetValue(8f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.88f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.20f, 0.08f, 0.24f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.46f, 0.70f, 0.12f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.68f, 0.90f, 0.22f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.80f, 1.00f, 0.36f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
    }

    protected override void ApplyLighting(ref FieldNode node)
        => Lighting.AddLight(node.Position, 0.10f * node.Alpha, 0.20f * node.Alpha, 0.04f * node.Alpha);

    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.Poisoned, POISON_TICKS);
    }
}
