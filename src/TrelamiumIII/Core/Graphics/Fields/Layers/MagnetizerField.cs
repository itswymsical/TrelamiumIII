using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

public sealed class MagnetizerField : FieldLayer
{
    private const float PULL = 0.55f;

    private const float CURL = 0.22f;

    private const float MAX_SPEED = 13f;

    private const float ARRIVAL = 18f;

    public override int LoadOrder => 90;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.5f;

    protected override float DefaultViscosity => 0.35f;

    protected override float BuoyancyForce => 0.02f;

    protected override float GravityScale => 0.14f;

    protected override float Drag => 0.995f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 3f;

    protected override int BaseMaxNodes => 160;

    private Vector3 tint = new(0.6f, 0.6f, 0.65f);

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.38f),
        Threshold = 0.3f,
        NoiseScale = 1.7f,
        NoiseStrength = 0.2f,
        Drift = new Vector2(-0.07f, 0.04f),
        FlowStrength = 0.20f,
        Bands = 4f,
        BodyAlpha = 0.62f,
        OutlineAlpha = 0.8f,
    };

    private static MagnetizerField? Layer => FieldSystem.GetLayer<MagnetizerField>();

    /// <summary>Eases the stream colour toward <paramref name="colour" />.</summary>
    public static void SetOre(Color colour)
    {
        if (Layer is not { } layer)
            return;

        layer.tint = Vector3.Lerp(layer.tint, colour.ToVector3(), 0.12f);
    }

    /// <summary>
    ///     Adds one blob to the stream, torn off at <paramref name="position" /> and pulled toward
    ///     <paramref name="host" />.
    /// </summary>
    public static void Stream(Entity host, Vector2 position, Vector2 velocity, float radius, float life, int gripTicks)
    {
        Layer?.SpawnAnchoredNode(host, position, velocity, radius, life, gripTicks);
    }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        if (node.AnchorHost is { active: true } host && node.AnchorTicks > 0)
        {
            node.AnchorTicks--;

            Vector2 toHost = host.Center - node.Position;
            float distance = toHost.Length();

            if (distance <= ARRIVAL)
            {
                node.Life = 0f;
                return;
            }

            Vector2 pull = toHost / distance;

            // Curl toward the side of the pull the node is already crossing.
            float side = Math.Sign(pull.X * node.Velocity.Y - pull.Y * node.Velocity.X);

            node.Velocity += pull * PULL + pull.RotatedBy(MathHelper.PiOver2) * (CURL * side);

            if (node.Velocity.LengthSquared() > MAX_SPEED * MAX_SPEED)
                node.Velocity = node.Velocity.SafeNormalize(Vector2.Zero) * MAX_SPEED;

            node.Position += node.Velocity;
            node.Life -= LifeDecay;

            return;
        }

        // Cleared so the base update doesn't release the node and add host momentum again.
        node.AnchorHost = null;
        node.AnchorTicks = 0;

        base.UpdateNode(ref node, index);
    }

    /// <summary>Ramp derived from the current ore tint.</summary>
    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["ShadowColor"]?.SetValue(tint * 0.3f);
        effect.Parameters["BodyColor"]?.SetValue(tint * 0.72f);
        effect.Parameters["CoreColor"]?.SetValue(Vector3.Lerp(tint, Vector3.One, 0.4f));
        effect.Parameters["OutlineColor"]?.SetValue(Vector3.Lerp(tint, Vector3.One, 0.72f));
    }

    protected override void ApplyLighting(ref FieldNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, tint.X * 0.14f * alpha, tint.Y * 0.14f * alpha, tint.Z * 0.14f * alpha);
    }
}
