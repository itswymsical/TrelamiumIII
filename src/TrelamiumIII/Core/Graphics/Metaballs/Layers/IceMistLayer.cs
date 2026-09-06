using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Metaballs.Layers;

/// <summary>
///     The freezing fog thrown off by Frostbark weapons. Lighter and thinner than
///     <see cref="SporeLayer" />: it rises rather than settles, and burns off quickly once it stops
///     being fed, so a charged swing reads as a puff of breath rather than a lingering cloud.
/// </summary>
public sealed class IceMistLayer : MetaballLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    // Lighter than air, so the mist lifts off the flail head instead of pooling under it.
    protected override float DefaultDensity => 0.16f;

    // Runnier than spores -- the cloud should keep spreading rather than hold a shape.
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
            noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        base.Unload();
        noiseTexture = null;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        // Guarded rather than passed straight through: the FBM sampler cannot take a null texture,
        // and the asset is still null on the frames before it finishes loading.
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Floored higher than the spore cloud -- ice mist reads as self-lit frost, and the pale
        // palette turns to mud the moment the ambient light drops.
        Vector3 light = new Vector3(0.45f); // ambient floor; the real light rides in the field per node
        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Lower threshold than the spore layer: mist should merge into broad sheets rather than
        // resolve into distinct blobs.
        effect.Parameters["Threshold"]?.SetValue(0.22f);
        effect.Parameters["NoiseScale"]?.SetValue(0.2f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.32f);

        // Drifts up and to the side faster than spores, so the fog keeps curling while it fades.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.05f, -0.09f));

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.68f);

        // Deep glacial blue in shadow up through pale cyan to a near-white core, rimmed in frost.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(1f, 1f, 1f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.72f, 0.8f, 0.95f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.2f, 0.57f, 0.95f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.8f, 0.95f, 1.00f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.75f);
    }

    protected override void ApplyLighting(ref MetaballNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, 0.09f * alpha, 0.16f * alpha, 0.24f * alpha);
    }

    /// <summary>Mist creeps along a surface it touches instead of bouncing off it.</summary>
    protected override void OnTileContact(ref MetaballNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.9f;
    }

    /// <summary>Water condenses the fog straight out of the air.</summary>
    protected override void OnLiquidContact(ref MetaballNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.8f;
        node.Life -= 4f;
    }
}
