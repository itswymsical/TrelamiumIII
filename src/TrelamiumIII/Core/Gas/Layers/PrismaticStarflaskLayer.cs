using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Gas.Layers;

public class PrismaticStarflaskLayer : ChemicalGasLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.ScreenWobble.Asset;
    protected override string TexturePath => Assets.Textures.VFX.BloomFlare.KEY;
    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 1.18f;
    protected override float DefaultViscosity => 0.33f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.0f;
    protected override float BuoyancyForce => 0.14f;
    protected override float Drag => 0.9998f;
    protected override float LifeDecay => 0.85f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Kinetic;
    public override float ChemicalDamage => 0.4f;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 4f;
    protected override BlendState CompositeBlendState => BlendState.Additive;

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void BindAdditionalTextures(GraphicsDevice device)
    {
        device.Textures[1] = noiseTex.Value;
        device.SamplerStates[1] = SamplerState.PointClamp;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * .004f);
        effect.Parameters["uIntensity"]?.SetValue(0.005f);

        effect.Parameters["uScale"]?.SetValue(Main.screenPosition * 0.005f);
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);
    }

    protected override void ApplyLighting(ref GasNode node)
    {
        Lighting.AddLight(node.Position, TorchID.Ice);

        Lighting.AddLight(node.Position, TorchID.Rainbow);
    }

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {

        base.OnNPCContact(ref node, npc);
        if (!npc.friendly)
            npc.AddBuff(BuffID.Bleeding, 30);
    }
}