using ReLogic.Content;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>
///     In-game version of the menu ink mote fields.
/// </summary>
public sealed class InikiField : FieldLayer
{
    public override int LoadOrder => 12;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.InkMote.KEY;

    protected override float DefaultDensity => 0.5f;

    protected override float DefaultViscosity => 0.5f;

    protected override float BuoyancyForce => 0.015f;

    protected override float GravityScale => 0.015f;

    protected override float Drag => 0.955f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.5f;

    protected override int BaseMaxNodes => 260;

    protected override float StampStretch => InkStyle.MoteSpikeStretch;

    protected override float FlowReferenceSpeed => 4f;

    private Asset<Texture2D>? noiseTexture;

    private Asset<Texture2D>? glintTexture;

    private static Vector2? magnet;
    private static float magnetRadius;
    private static float magnetForce;

    public override void Load()
    {
        base.Load();

        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
        glintTexture = Assets.Textures.VFX.Bloom.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTexture = null;
        glintTexture = null;
        magnet = null;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.78f));

        effect.Parameters["Threshold"]?.SetValue(InkStyle.MoteThreshold);
        effect.Parameters["Bands"]?.SetValue(InkStyle.MoteBands);
        effect.Parameters["BodyAlpha"]?.SetValue(InkStyle.MoteBodyAlpha);

        effect.Parameters["NoiseScale"]?.SetValue(InkStyle.MoteWobbleScale);
        effect.Parameters["NoiseStrength"]?.SetValue(0.06f);
        effect.Parameters["NoiseDrift"]?.SetValue(InkStyle.MoteWobbleDrift);

        effect.Parameters["FlowStrength"]?.SetValue(0.10f);

        effect.Parameters["ShadowColor"]?.SetValue(InkStyle.MoteShadowColor);
        effect.Parameters["BodyColor"]?.SetValue(InkStyle.MoteBodyColor);
        effect.Parameters["CoreColor"]?.SetValue(InkStyle.MoteCoreColor);
        effect.Parameters["OutlineColor"]?.SetValue(InkStyle.MoteRimColor);
        effect.Parameters["OutlineAlpha"]?.SetValue(InkStyle.MoteRimAlpha);
    }

    protected override void ApplyLighting(ref FieldNode node) { }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        ApplyMagnet(ref node);

        base.UpdateNode(ref node, index);
    }

    /// <summary>
    ///     Pulls a bead toward the active magnet, easing off inside its core.
    /// </summary>
    /// <remarks>
    ///     Both falloffs are the menu's, and the second is the one that matters: at full strength
    ///     all the way to the centre a bead is driven through the magnet, the direction to it flips,
    ///     and it buzzes across the point forever. Scaled down through the core it is a spring, and
    ///     the settle damping leaves it overdamped.
    /// </remarks>
    private static void ApplyMagnet(ref FieldNode node)
    {
        if (magnet is not { } point || node.IsAnchored)
            return;

        Vector2 toMagnet = point - node.Position;
        float distance = toMagnet.Length();

        if (distance <= 0.001f || distance >= magnetRadius)
            return;

        float pull = 1f - distance / magnetRadius;
        float grip = MathHelper.Clamp(distance / InkStyle.MagnetCore, 0f, 1f);
        Vector2 direction = toMagnet / distance;

        node.Velocity += direction * (magnetForce * pull * pull * grip);

        if (grip < 1f)
            node.Velocity *= MathHelper.Lerp(InkStyle.MagnetSettle, 1f, grip);
    }

    /// <summary>
    ///     The specular glint, the one part of a bead a threshold cannot produce.
    /// </summary>
    /// <remarks>
    ///     Offset toward a fixed upper-left light rather than rotating per bead as the menu's does.
    ///     A scene has a world to be lit by, and highlights that all agree on one direction read as
    ///     a surface; highlights that each spin read as sparkle.
    /// </remarks>
    protected override void DrawOverlay(SpriteBatch spriteBatch)
    {
        if (glintTexture is not { Value: { } texture })
            return;

        Vector2 origin = texture.Size() * 0.5f;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix);

        foreach (var node in nodes)
        {
            float radius = node.Radius;
            Vector2 glint = node.Position - Main.screenPosition + new Vector2(-radius * 0.3f, -radius * 0.3f);

            spriteBatch.Draw(
                texture,
                glint,
                null,
                Color.White * (node.Alpha * InkStyle.MoteGlint),
                0f,
                origin,
                radius * 0.5f / texture.Width,
                SpriteEffects.None,
                0f);
        }

        spriteBatch.End();
    }

    private static InikiField? Layer => FieldSystem.GetLayer<InikiField>();

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
    ///     Puts a scatter of beads into the world.
    /// </summary>
    /// <param name="spread">Radius of the scatter. Beads land anywhere inside it.</param>
    public static void Beads(Vector2 center, float spread, int count = 10, float life = 300f)
    {
        if (Layer is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            layer.SpawnNode(
                center + Main.rand.NextVector2Circular(spread, spread),
                Main.rand.NextVector2Circular(0.5f, 0.5f),
                radius: Main.rand.NextFloat(InkStyle.MoteRadiusMin, InkStyle.MoteRadiusMax) * 0.6f,
                life: life * Main.rand.NextFloat(0.8f, 1.2f));
        }
    }
    public static void Trail(Vector2 position, Vector2 velocity, float radius)
    {
        Layer?.SpawnNode(
            position + Main.rand.NextVector2Circular(4f, 4f),
            velocity * 0.55f + Main.rand.NextVector2Circular(0.2f, 0.2f),
            radius: radius * Main.rand.NextFloat(0.75f, 1.15f),
            life: Main.rand.NextFloat(30f, 52f)
        );
    }
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
    /// <summary>One bead, thrown. For a drip, a flick, or a trail behind something.</summary>
    public static void Drip(Vector2 position, Vector2 velocity, float radius, float life = 240f)
    {
        Layer?.SpawnNode(position, velocity, radius, life);
    }

    /// <summary>Beads clinging to a host, which release and fall away after <paramref name="ticks" />.</summary>
    public static void Cling(Entity host, int count = 8, int ticks = 120, float life = 220f)
    {
        if (Layer is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            layer.SpawnAnchoredNode(
                host,
                host.Center + Main.rand.NextVector2Circular(host.width * 0.5f, host.height * 0.5f),
                Main.rand.NextVector2Circular(0.3f, 0.3f),
                radius: Main.rand.NextFloat(InkStyle.MoteRadiusMin, InkStyle.MoteRadiusMax) * 0.45f,
                life: life,
                anchorTicks: ticks);
        }
    }

    /// <summary>
    ///     Sets the world point every bead reaches for, until <see cref="Demagnetize" />.
    /// </summary>
    /// <remarks>
    ///     One magnet, not a list: a scene has one thing the ink is answering at a time, and two
    ///     attractors cancel into beads that sit still between them.
    /// </remarks>
    public static void Magnetize(Vector2 world, float radius = 320f, float force = 0.55f)
    {
        magnet = world;
        magnetRadius = MathF.Max(radius, 1f);
        magnetForce = force;
    }

    public static void Demagnetize() => magnet = null;

    /// <summary>Drops every bead at once, for a cut that has to land clean.</summary>
    public static void Clear() => Layer?.ClearNodes();
}
