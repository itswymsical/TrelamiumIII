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

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.Milky_2.Asset,
        WorldLight = new Vector3(0.45f),
        Threshold = 0.22f,
        NoiseScale = 0.2f,
        NoiseStrength = 0.32f,
        Drift = new Vector2(0.05f, -0.09f),
        FlowStrength = 0.14f,
        Bands = 4f,
        BodyAlpha = 0.68f,
        Shadow = Vector3.One,
        Body = new Vector3(0.72f, 0.8f, 0.95f),
        Core = Vector3.One,
        Outline = Vector3.One,
        OutlineAlpha = 0.75f,
    };

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
