using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

public sealed class SporeField : FieldLayer
{
    public override int LoadOrder => 65;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.28f;

    protected override float DefaultViscosity => 0.55f;

    protected override float BuoyancyForce => 0.075f;

    protected override float GravityScale => 0.03f;

    protected override float Drag => 0.982f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.5f;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.3f),
        Threshold = 0.16f,
        NoiseScale = 1.35f,
        NoiseStrength = 0.24f,
        Drift = new Vector2(0.03f, -0.06f),
        FlowStrength = 0.14f,
        Bands = 5f,
        BodyAlpha = 0.86f,
        Shadow = new Vector3(0.28f, 0.10f, 0.05f),
        Body = new Vector3(0.74f, 0.32f, 0.10f),
        Core = new Vector3(0.94f, 0.63f, 0.18f),
        Outline = new Vector3(1.00f, 0.75f, 0.32f),
        OutlineAlpha = 0.85f,
    };

    protected override void ApplyLighting(ref FieldNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, 0.20f * alpha, 0.11f * alpha, 0.03f * alpha);
    }

    /// <summary>Thickens and slows against surfaces.</summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity = Math.Min(node.Viscosity * 1.02f, 1f);
        node.Velocity *= 0.95f;
    }

    /// <summary>Slows and drains life in liquid.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.85f;
        node.Life -= 3f;
    }

    private static SporeField? Layer => FieldSystem.GetLayer<SporeField>();

    /// <summary>A tight spray along <paramref name="direction" />, anchored to <paramref name="host" />.</summary>
    public static void Splat(NPC host, Vector2 position, Vector2 direction, int anchorTicks)
    {
        if (Layer is not { } layer)
            return;

        Vector2 spray = direction.SafeNormalize(Vector2.UnitY);

        for (int i = 0; i < 10; i++)
        {
            Vector2 velocity = spray.RotatedByRandom(0.9f) * Main.rand.NextFloat(0.6f, 2.4f)
                               + Main.rand.NextVector2Circular(0.5f, 0.5f);

            layer.SpawnAnchoredNode(
                host,
                position + Main.rand.NextVector2Circular(6f, 6f),
                velocity,
                radius: Main.rand.NextFloat(9f, 19f),
                life: Main.rand.NextFloat(45f, 80f),
                anchorTicks: anchorTicks
            );
        }
    }

    /// <summary>
    ///     One anchored node on <paramref name="host" />, inheriting most of its velocity.
    ///     <paramref name="intensity" /> (0..1) scales coverage and node size.
    /// </summary>
    public static void Cling(Entity host, float intensity)
    {
        if (Layer is not { } layer || host is not { active: true })
            return;

        intensity = MathHelper.Clamp(intensity, 0f, 1f);

        float spreadX = host.width * MathHelper.Lerp(0.25f, 0.55f, intensity);
        float spreadY = host.height * MathHelper.Lerp(0.25f, 0.55f, intensity);

        Vector2 position = host.Center + Main.rand.NextVector2Circular(spreadX, spreadY);

        Vector2 velocity = host.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);

        layer.SpawnAnchoredNode(
            host,
            position,
            velocity,
            radius: MathHelper.Lerp(7f, 18f, intensity) * Main.rand.NextFloat(0.75f, 1.25f),
            life: MathHelper.Lerp(30f, 55f, intensity),
            anchorTicks: 20
        );
    }

    /// <summary>A long-lived, upward-biased burst cloud.</summary>
    public static void Burst(Vector2 position, float radius)
    {
        if (Layer is not { } layer)
            return;

        int count = Main.rand.Next(18, 26);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.5f, 7.5f);
            velocity.Y -= 0.6f;

            layer.SpawnNode(
                position + Main.rand.NextVector2Circular(radius * 0.2f, radius * 0.2f),
                velocity,
                radius: Main.rand.NextFloat(12f, 26f),
                life: Main.rand.NextFloat(130f, 190f)
            );
        }
    }

    /// <summary>
    ///     One node drawn inward from a ring that tightens as <paramref name="progress" /> goes 0..1,
    ///     timed to reach the centre as it expires. Call frequency sets the density.
    /// </summary>
    public static void Coalesce(Vector2 center, float progress, float radius = 32f)
    {
        if (Layer is not { } layer)
            return;

        progress = MathHelper.Clamp(progress, 0f, 1f);

        float ring = radius * MathHelper.Lerp(1f, 0.3f, progress);
        Vector2 offset = Main.rand.NextVector2CircularEdge(ring, ring);

        Vector2 velocity = -offset / 15f;

        layer.SpawnNode(
            center + offset,
            velocity,
            radius: MathHelper.Lerp(5f, 12f, progress) * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(14f, 22f),
            density: 0.5f
        );
    }

    public static void Trail(Vector2 position, Vector2 velocity, float radius)
    {
        Layer?.SpawnNode(
            position + Main.rand.NextVector2Circular(3f, 3f),
            velocity + new Vector2(0f, -0.15f) + Main.rand.NextVector2Circular(0.25f, 0.25f),
            radius: radius * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(35f, 60f)
        );
    }

    public static void TileSplat(Vector2 position, Vector2 direction)
    {
        if (Layer is not { } layer)
            return;

        Vector2 rebound = (-direction).SafeNormalize(-Vector2.UnitY);

        for (int i = 0; i < 7; i++)
        {
            Vector2 velocity = rebound.RotatedByRandom(1.1f) * Main.rand.NextFloat(0.5f, 2f);

            layer.SpawnNode(
                position + Main.rand.NextVector2Circular(5f, 5f),
                velocity,
                radius: Main.rand.NextFloat(8f, 16f),
                life: Main.rand.NextFloat(50f, 90f)
            );
        }
    }
}