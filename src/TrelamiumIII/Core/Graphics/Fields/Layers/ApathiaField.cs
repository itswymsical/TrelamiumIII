using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>
///     Neutral-density dark gas shed by the Eye of Cthulhu's remains, and the vortex that swallows them.
///     Palette runs from void to a cold pale rim (the ends of the menu ink nebula).
/// </summary>
public sealed class ApathiaField : FieldLayer
{
    public override int LoadOrder => 10;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.5f;

    protected override float DefaultViscosity => 0.72f;

    protected override float BuoyancyForce => 0.02f;

    protected override float GravityScale => 0.02f;

    protected override float Drag => 0.965f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.2f;

    /// <summary>Raised so the vortex ring feed doesn't starve the shroud around the remains.</summary>
    protected override int BaseMaxNodes => 560;

    /// <summary>Lit high because the palette is near-black; ambient light would take the rim down with it.</summary>
    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.Turbulence_08.Asset,
        WorldLight = new Vector3(0.78f),
        Threshold = 0.165f,
        NoiseScale = 1.05f,
        NoiseStrength = 0.20f,
        Drift = new Vector2(-0.014f, -0.022f),
        FlowStrength = 0.08f,
        Bands = 3f,
        BodyAlpha = 0.94f,
        Shadow = new Vector3(0.014f, 0.015f, 0.022f),
        Body = new Vector3(0.055f, 0.050f, 0.095f),
        Core = new Vector3(0.20f, 0.19f, 0.32f),
        Outline = new Vector3(0.70f, 0.71f, 0.85f),
        OutlineAlpha = 0.90f,
    };

    /// <summary>Emits no light.</summary>
    protected override void ApplyLighting(ref FieldNode node) { }

    /// <summary>Ink pools against a surface instead of creeping along it.</summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity = Math.Min(node.Viscosity * 1.04f, 1f);
        node.Velocity *= 0.88f;
    }

    /// <summary>Slows and drains life in liquid.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.9f;
        node.Life -= 1f;
    }

    private static ApathiaField? Layer => FieldSystem.GetLayer<ApathiaField>();

    public static void Bloom(Vector2 center, float radius, int count = 22)
    {
        if (Layer is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.6f);

            layer.SpawnNode(
                center + Main.rand.NextVector2Circular(radius * 0.35f, radius * 0.35f),
                velocity,
                radius: Main.rand.NextFloat(16f, 34f),
                life: Main.rand.NextFloat(160f, 240f)
            );
        }
    }

    /// <summary>
    ///     One shroud node around a drifting piece of the remains, inheriting most of its velocity.
    ///     Called once per tick per piece; call frequency sets the density.
    /// </summary>
    public static void Shroud(Vector2 position, Vector2 velocity, float intensity)
    {
        if (Layer is not { } layer)
            return;

        intensity = MathHelper.Clamp(intensity, 0f, 1f);

        layer.SpawnNode(
            position + Main.rand.NextVector2Circular(7f, 7f),
            velocity * 0.9f + Main.rand.NextVector2Circular(0.3f, 0.3f),
            radius: MathHelper.Lerp(9f, 21f, intensity) * Main.rand.NextFloat(0.8f, 1.2f),
            life: MathHelper.Lerp(38f, 70f, intensity)
        );
    }

    /// <summary>A wisp shed behind a piece moving fast enough to leave a trail.</summary>
    public static void Trail(Vector2 position, Vector2 velocity, float radius)
    {
        Layer?.SpawnNode(
            position + Main.rand.NextVector2Circular(4f, 4f),
            velocity * 0.55f + Main.rand.NextVector2Circular(0.2f, 0.2f),
            radius: radius * Main.rand.NextFloat(0.75f, 1.15f),
            life: Main.rand.NextFloat(30f, 52f)
        );
    }

    /// <summary>
    ///     Spawns a vortex node on a ring that tightens as <paramref name="progress" /> goes 0..1, with
    ///     summed inward and tangential velocity (spin dominates wide, the pull dominates tight).
    /// </summary>
    public static void Vortex(Vector2 center, float progress, float radius = 150f, float spin = 1f)
    {
        if (Layer is not { } layer)
            return;

        progress = MathHelper.Clamp(progress, 0f, 1f);

        float ring = radius * MathHelper.Lerp(1f, 0.22f, progress);
        Vector2 offset = Main.rand.NextVector2CircularEdge(ring, ring);

        Vector2 inward = -offset / MathHelper.Lerp(26f, 11f, progress);
        Vector2 tangent = new Vector2(-offset.Y, offset.X).SafeNormalize(Vector2.Zero)
                          * (spin * MathHelper.Lerp(2.6f, 5.4f, progress));

        layer.SpawnNode(
            center + offset,
            inward + tangent,
            radius: MathHelper.Lerp(13f, 24f, progress) * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(26f, 44f),
            density: 0.5f,
            viscosity: 0.1f
        );
    }

    /// <summary>Drops every live node at once, for the moment the vortex closes.</summary>
    public static void Clear() => Layer?.ClearNodes();
}
