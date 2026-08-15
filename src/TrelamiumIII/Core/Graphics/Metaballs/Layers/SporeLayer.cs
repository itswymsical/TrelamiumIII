using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Metaballs.Layers;

public sealed class SporeLayer : MetaballLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.28f;

    protected override float DefaultViscosity => 0.55f;

    protected override float BuoyancyForce => 0.075f;

    protected override float GravityScale => 0.03f;

    protected override float Drag => 0.982f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.5f;

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

        // Floored so the cloud stays legible in an unlit cave; it emits its own light anyway, and
        // spores that vanish into the dark stop communicating that an enemy is infected.
        Vector3 light = Vector3.Max(SampleAverageLight(), new Vector3(0.3f));
        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.35f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.24f);

        // Drifts up and slightly across, so the body of the cloud boils in place while the
        // silhouette keeps changing.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.03f, -0.06f));

        effect.Parameters["Bands"]?.SetValue(5f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.86f);

        // Fungore's cap :3 rust through burnt orange to a pale amber core, rimmed in gold.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.28f, 0.10f, 0.05f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.74f, 0.32f, 0.10f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.94f, 0.63f, 0.18f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(1.00f, 0.75f, 0.32f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.85f);
    }

    protected override void ApplyLighting(ref MetaballNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, 0.20f * alpha, 0.11f * alpha, 0.03f * alpha);
    }

    /// <summary>
    ///     Spores landing on tiles pack down and creep along the surface.
    /// </summary>
    protected override void OnTileContact(ref MetaballNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity = Math.Min(node.Viscosity * 1.02f, 1f);
        node.Velocity *= 0.95f;
    }

    /// <summary>Water washes the spores out fast.</summary>
    protected override void OnLiquidContact(ref MetaballNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.85f;
        node.Life -= 3f;
    }

    private static SporeLayer? Layer => MetaballSystem.GetLayer<SporeLayer>();

    /// <summary>
    ///     The wet splat of a sac bursting against a target: a tight spray thrown along
    ///     <paramref name="direction" />, anchored to the host so it rides along.
    /// </summary>
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
    ///     Clings a cloud that adheres to an infected host, NPC or player alike.
    ///     <paramref name="intensity" /> runs 0..1 with the stack count, and scales how much of the
    ///     body the cloud covers.
    /// </summary>
    public static void Cling(Entity host, float intensity)
    {
        if (Layer is not { } layer || host is not { active: true })
            return;

        intensity = MathHelper.Clamp(intensity, 0f, 1f);

        float spreadX = host.width * MathHelper.Lerp(0.25f, 0.55f, intensity);
        float spreadY = host.height * MathHelper.Lerp(0.25f, 0.55f, intensity);

        Vector2 position = host.Center + Main.rand.NextVector2Circular(spreadX, spreadY);

        // Inherits most of the host's motion so the puff is already travelling with it on the
        // frame it appears, rather than being left behind and catching up.
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

    /// <summary>
    ///     The bloom: an infected host coming apart into a cloud that hangs long enough to be a
    ///     hazard marker for everything it just infected.
    /// </summary>
    public static void Burst(Vector2 position, float radius)
    {
        if (Layer is not { } layer)
            return;

        int count = Main.rand.Next(18, 26);

        for (int i = 0; i < count; i++)
        {
            // Biased upward: the cloud should loom over the host, not wash off it.
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
    ///     The opposite of a burst: spores drawn inward on a point until they set into something
    ///     solid. <paramref name="progress" /> runs 0..1 and tightens the ring as it climbs.
    /// </summary>
    /// <remarks>
    ///     Called once per node wanted, per tick -- the caller decides how thick the cloud gets by
    ///     how often it asks, the same way <see cref="Cling(Entity, float)" /> works.
    /// </remarks>
    public static void Coalesce(Vector2 center, float progress, float radius = 32f)
    {
        if (Layer is not { } layer)
            return;

        progress = MathHelper.Clamp(progress, 0f, 1f);

        float ring = radius * MathHelper.Lerp(1f, 0.3f, progress);
        Vector2 offset = Main.rand.NextVector2CircularEdge(ring, ring);

        // Aimed at the centre and timed to roughly arrive there as it expires, so the cloud reads as
        // being pulled in rather than merely drawn smaller each tick.
        Vector2 velocity = -offset / 15f;

        layer.SpawnNode(
            center + offset,
            velocity,
            radius: MathHelper.Lerp(5f, 12f, progress) * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(14f, 22f),
            // Neutral, so nothing rises or sinks out of the knot on its way in.
            density: 0.5f
        );
    }

    /// <summary>
    ///     A single wisp shed by something still leaking spores -- a tumbling husk, a kicked chunk.
    /// </summary>
    public static void Trail(Vector2 position, Vector2 velocity, float radius)
    {
        Layer?.SpawnNode(
            position + Main.rand.NextVector2Circular(3f, 3f),
            velocity + new Vector2(0f, -0.15f) + Main.rand.NextVector2Circular(0.25f, 0.25f),
            radius: radius * Main.rand.NextFloat(0.8f, 1.2f),
            life: Main.rand.NextFloat(35f, 60f)
        );
    }

    /// <summary>A sac that missed, breaking against terrain. Small, brief, no anchor.</summary>
    public static void TileSplat(Vector2 position, Vector2 direction)
    {
        if (Layer is not { } layer)
            return;

        // Away from the surface it hit, so the puff blows back at the thrower rather than into
        // the wall where it would be culled immediately.
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
