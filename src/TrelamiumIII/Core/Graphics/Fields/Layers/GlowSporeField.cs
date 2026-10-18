using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>Glowing mushroom spore mist from Argie's sprayer: light, quick to thin out, lit blue. Visual only.</summary>
public sealed class GlowSporeField : FieldLayer
{
    public override int LoadOrder => 66;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.2f;

    protected override float DefaultViscosity => 0.4f;

    protected override float BuoyancyForce => 0.06f;

    protected override float GravityScale => 0.02f;

    protected override float Drag => 0.95f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 2f;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.5f),
        Threshold = 0.18f,
        NoiseScale = 1.1f,
        NoiseStrength = 0.26f,
        Drift = new Vector2(0.03f, -0.05f),
        FlowStrength = 0.16f,
        Bands = 5f,
        BodyAlpha = 0.72f,
        Shadow = new Vector3(0.05f, 0.10f, 0.32f),
        Body = new Vector3(0.20f, 0.42f, 0.95f),
        Core = new Vector3(0.55f, 0.85f, 1.00f),
        Outline = new Vector3(0.75f, 0.95f, 1.00f),
        OutlineAlpha = 0.8f,
    };

    protected override void ApplyLighting(ref FieldNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, 0.05f * alpha, 0.14f * alpha, 0.30f * alpha);
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

    /// <summary>One puff of mist carried along <paramref name="velocity" />.</summary>
    public static void Puff(Vector2 position, Vector2 velocity, float radius)
    {
        FieldSystem.GetLayer<GlowSporeField>()?.SpawnNode(
            position + Main.rand.NextVector2Circular(4f, 4f),
            velocity + Main.rand.NextVector2Circular(0.3f, 0.3f),
            radius: radius * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(30f, 48f)
        );
    }
}
