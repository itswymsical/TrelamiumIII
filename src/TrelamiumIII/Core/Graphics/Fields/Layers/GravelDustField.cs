using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>Dust scuffed off highland gravel paths: heavy, hugs the ground, and settles within a second.</summary>
public sealed class GravelDustField : FieldLayer
{
    public override int LoadOrder => 60;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.55f;

    protected override float DefaultViscosity => 0.35f;

    protected override float BuoyancyForce => 0.03f;

    protected override float GravityScale => 0.02f;

    protected override float Drag => 0.95f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 3f;

    protected override int BaseMaxNodes => 160;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.Grainy_01.Asset,
        WorldLight = new Vector3(0.45f),
        Threshold = 0.26f,
        NoiseScale = 0.25f,
        NoiseStrength = 0.45f,
        Drift = new Vector2(0.03f, -0.02f),
        FlowStrength = 0.18f,
        Bands = 3f,
        BodyAlpha = 0.5f,
        Shadow = new Vector3(0.43f, 0.39f, 0.35f),
        Body = new Vector3(0.64f, 0.6f, 0.53f),
        Core = new Vector3(0.78f, 0.75f, 0.68f),
        Outline = new Vector3(0.72f, 0.69f, 0.62f),
        OutlineAlpha = 0.35f,
    };

    /// <summary>Drags along the ground it skims.</summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.85f;
    }

    /// <summary>Wets out at once.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Life -= 8f;
    }
}
