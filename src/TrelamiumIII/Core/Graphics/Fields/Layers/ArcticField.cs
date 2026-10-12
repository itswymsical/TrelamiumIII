using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>Freezing fog from Frostbark weapons: buoyant, runny and short-lived once unfed.</summary>
public sealed class ArcticField : FieldLayer
{
    public override int LoadOrder => 85;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.16f;

    protected override float DefaultViscosity => 0.30f;

    protected override float BuoyancyForce => 0.11f;

    protected override float GravityScale => 0.012f;

    protected override float Drag => 0.972f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 2.5f;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.45f),
        Threshold = 0.22f,
        NoiseScale = 0.2f,
        NoiseStrength = 0.32f,
        Drift = new Vector2(0.05f, -0.09f),
        FlowStrength = 0.20f,
        Bands = 4f,
        BodyAlpha = 0.68f,
        Shadow = Vector3.One,
        Body = new Vector3(0.72f, 0.8f, 0.95f),
        Core = new Vector3(0.2f, 0.57f, 0.95f),
        Outline = new Vector3(0.8f, 0.95f, 1.00f),
        OutlineAlpha = 0.75f,
    };

    protected override void ApplyLighting(ref FieldNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, 0.09f * alpha, 0.16f * alpha, 0.24f * alpha);
    }

    /// <summary>Slows against surfaces.</summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.9f;
    }

    /// <summary>Slows and drains life fast in liquid.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.8f;
        node.Life -= 4f;
    }
}
