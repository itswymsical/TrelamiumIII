using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

public sealed class MagnoliaField : FieldLayer
{
    public override int LoadOrder => 70;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.16f;

    protected override float DefaultViscosity => 0.30f;

    protected override float BuoyancyForce => 0.11f;

    protected override float GravityScale => 0.012f;

    protected override float Drag => 0.972f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 2.5f;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.Milky_2.Asset;
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

        Vector3 light = new Vector3(0.45f);
        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["Threshold"]?.SetValue(0.22f);
        effect.Parameters["NoiseScale"]?.SetValue(0.2f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.32f);

        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.05f, -0.09f));
        
        effect.Parameters["FlowStrength"]?.SetValue(0.14f);

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.68f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(1f, 1f, 1f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.72f, 0.8f, 0.95f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(1f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(1f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.75f);
    }

    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.9f;
    }

    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.8f;
        node.Life -= 4f;
    }
}
