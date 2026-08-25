using ReLogic.Content;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Metaballs;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Core.Gas.Layers;

public sealed class StarLayer : GasLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    /// <summary>Neutral: ink hangs rather than rising or sinking. See the class remarks.</summary>
    protected override float DefaultDensity => 0.5f;

    /// <summary>Thick. The cloud should hold a shape and loom, not disperse like a puff.</summary>
    protected override float DefaultViscosity => 0.72f;

    protected override float BuoyancyForce => 0.02f;

    protected override float GravityScale => 0.02f;

    /// <summary>Heavier drag than the other layers, so a node settles into a slow billow.</summary>
    protected override float Drag => 0.965f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.2f;

    private Asset<Texture2D>? noiseTexture;

    protected override int GasType => GasTypeID.Plasma;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.Turbulence_08.Asset;
    }

    public override void Unload()
    {
        base.Unload();
        noiseTexture = null;
    }

    private Vector3 GetRGB(float speed = 2.0f, float sat = 1.0f, float bright = 1.0f)
    {
        float time = (float)Main.timeForVisualEffects / 60f;
        float t = time * speed;

        float r = (MathF.Sin(t) + 1f) * 0.5f;
        float g = (MathF.Sin(t + 2.0944f) + 1f) * 0.5f;
        float b = (MathF.Sin(t + 4.1888f) + 1f) * 0.5f;

        Vector3 rawRainbow = new Vector3(r, g, b);

        Vector3 color = Vector3.Lerp(Vector3.One, rawRainbow, sat);

        return color * bright;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        Vector3 light = Vector3.Max(SampleAverageLight(), new Vector3(0.78f));
        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        effect.Parameters["Threshold"]?.SetValue(0.165f);

        effect.Parameters["NoiseScale"]?.SetValue(1.05f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.20f);

        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.014f, -0.022f));

        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.94f);

        effect.Parameters["ShadowColor"]?.SetValue(GetRGB(speed: 2, sat: 0.4f, bright: 0.6f));
        effect.Parameters["BodyColor"]?.SetValue(GetRGB(speed: 4f));
        effect.Parameters["CoreColor"]?.SetValue(GetRGB(speed: 4f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(1f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.90f);
    }

    /// <summary>
    ///     Ink takes light out of the room rather than adding any.
    /// </summary>
    /// <remarks>
    ///     <see cref="Lighting" /> has no way to subtract, so there is nothing to do here -- but the
    ///     override is left explicit because every other layer lights itself, and a reader finding
    ///     this one empty should know that is the intent rather than an omission.
    /// </remarks>
    protected override void ApplyLighting(ref GasNode node) 
    {
        Lighting.AddLight(node.Position, TorchID.Rainbow);
    }

    protected override void UpdateGasNode(ref GasNode node, int index)
    {
        base.UpdateGasNode(ref node, index);
        if (Main.rand.NextFloat() < 0.025f)
        {
            Vector2 offset = Main.rand.NextVector2Circular(14f, 14f);

            ParticleSystem.Spawn<InkSparkParticle>(
                node.Position,
                node.Velocity * 0.4f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                timeLeft: (int)node.Life / 3,
                scale: Main.rand.NextFloat(0.5f, 0.75f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.05f, 0.05f)
            );
        }
    }

    //private static InkLayer? Layer => MetaballSystem.GetLayer<InkLayer>();

    ///// <summary>
    /////     The first billow, thrown the moment the remains come apart.
    ///// </summary>
    //public static void Bloom(Vector2 center, float radius, int count = 22)
    //{
    //    if (Layer is not { } layer)
    //        return;

    //    for (int i = 0; i < count; i++)
    //    {
    //        // Slow and omnidirectional. A biased burst reads as an explosion; this should read as
    //        // something welling outward.
    //        Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.6f);

    //        layer.SpawnNode(
    //            center + Main.rand.NextVector2Circular(radius * 0.35f, radius * 0.35f),
    //            velocity,
    //            radius: Main.rand.NextFloat(16f, 34f),
    //            life: Main.rand.NextFloat(160f, 240f)
    //        );
    //    }
    //}

    ///// <summary>
    /////     The shroud clinging to one drifting piece of the remains.
    ///// </summary>
    ///// <remarks>
    /////     Called once per tick per piece, so the caller sets how heavy the shroud gets by how often
    /////     it asks -- the same contract <see cref="SporeLayer.Cling" /> uses.
    /////     <para>
    /////         Takes a bare position and velocity rather than an <see cref="Entity" />, because the
    /////         pieces are particles and not entities, so there is no host for
    /////         <see cref="MetaballLayer.SpawnAnchoredNode" /> to ride. Inheriting most of the piece's
    /////         motion buys the same thing: the node is already travelling with it on the frame it
    /////         appears, instead of being left behind and catching up.
    /////     </para>
    ///// </remarks>
    //public static void Shroud(Vector2 position, Vector2 velocity, float intensity)
    //{
    //    if (Layer is not { } layer)
    //        return;

    //    intensity = MathHelper.Clamp(intensity, 0f, 1f);

    //    layer.SpawnNode(
    //        position + Main.rand.NextVector2Circular(7f, 7f),
    //        velocity * 0.9f + Main.rand.NextVector2Circular(0.3f, 0.3f),
    //        radius: MathHelper.Lerp(9f, 21f, intensity) * Main.rand.NextFloat(0.8f, 1.2f),
    //        life: MathHelper.Lerp(38f, 70f, intensity)
    //    );
    //}

    ///// <summary>A wisp shed behind a piece moving fast enough to leave a trail.</summary>
    //public static void Trail(Vector2 position, Vector2 velocity, float radius)
    //{
    //    Layer?.SpawnNode(
    //        position + Main.rand.NextVector2Circular(4f, 4f),
    //        velocity * 0.55f + Main.rand.NextVector2Circular(0.2f, 0.2f),
    //        radius: radius * Main.rand.NextFloat(0.75f, 1.15f),
    //        life: Main.rand.NextFloat(30f, 52f)
    //    );
    //}

    ///// <summary>
    /////     The vortex throat: ink drawn in off a ring that tightens as <paramref name="progress" />
    /////     climbs 0..1.
    ///// </summary>
    ///// <remarks>
    /////     Given a tangential kick as well as an inward one, so the field winds as it collapses and
    /////     the intake reads as a drain rather than as a cloud being scaled down. The two are summed
    /////     rather than blended: out at the wide ring the spin dominates and nodes visibly orbit, and
    /////     by the time the ring has tightened the inward term has grown enough to actually land them.
    ///// </remarks>
    //public static void Vortex(Vector2 center, float progress, float radius = 150f, float spin = 1f)
    //{
    //    if (Layer is not { } layer)
    //        return;

    //    progress = MathHelper.Clamp(progress, 0f, 1f);

    //    float ring = radius * MathHelper.Lerp(1f, 0.22f, progress);
    //    Vector2 offset = Main.rand.NextVector2CircularEdge(ring, ring);

    //    Vector2 inward = -offset / MathHelper.Lerp(26f, 11f, progress);
    //    Vector2 tangent = new Vector2(-offset.Y, offset.X).SafeNormalize(Vector2.Zero)
    //                      * (spin * MathHelper.Lerp(2.6f, 5.4f, progress));

    //    layer.SpawnNode(
    //        center + offset,
    //        inward + tangent,
    //        radius: MathHelper.Lerp(13f, 24f, progress) * Main.rand.NextFloat(0.8f, 1.2f),
    //        life: Main.rand.NextFloat(26f, 44f),
    //        // Neutral and slack: nothing should sink out of the throat or lag behind the spin.
    //        density: 0.5f,
    //        viscosity: 0.1f
    //    );
    //}

    ///// <summary>Drops every live node at once, for the moment the vortex closes.</summary>
    //public static void Clear() => Layer?.ClearNodes();
}
