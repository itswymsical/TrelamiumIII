using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields.Layers;

/// <summary>
///     The dark gas that bleeds off the Eye of Cthulhu's remains, and the throat of the vortex that
///     eventually swallows them.
/// </summary>
/// <remarks>
///     <para>
///         The odd one out among the layers: <see cref="SporeLayer" /> and <see cref="IceMistLayer" />
///         are lit clouds, and this is a <em>hole</em>. Its palette runs from void to a cold pale rim,
///         so the field reads as an absence with an edge rather than as smoke with a colour, which is
///         what lets it sit under the Illuminant Ink vortex instead of looking like a second,
///         differently-coloured cloud beside it.
///     </para>
///     <para>
///         Density sits at neutral by default. Ink neither rises like spores nor lifts like frost --
///         it hangs where it was shed, which is the whole read: the remains are not smoking, they are
///         leaking something that does not care about gravity.
///     </para>
/// </remarks>
public sealed class InkLayer : FieldLayer
{
    /// <summary>Lowest: ink is an absence, and everything else reads against it.</summary>
    public override int LoadOrder => 10;

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

    /// <summary>
    ///     Larger than the default budget. The vortex stage feeds this every tick off a ring, and a
    ///     400-node ceiling starves the shroud around the remains to pay for it.
    /// </summary>
    protected override int BaseMaxNodes => 560;

    private Asset<Texture2D>? noiseTexture;

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

    protected override void ApplyShaderParameters(Effect effect)
    {

        // Guarded rather than passed straight through: the FBM sampler cannot take a null texture,
        // and the asset is still null on the frames before it finishes loading.
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Floored high, which looks backwards for something this dark until you remember GasFbm
        // multiplies the whole palette by this. The body is already near-black by colour; letting
        // ambient light drive it as well would take the rim down with it, and the silhouette would
        // lose its edge in exactly the places this plays -- a night-time arena, or the cavern layer.
        // So the layer lights itself and ignores the room.
        Vector3 light = new Vector3(0.78f); // ambient floor; the real light rides in the field per node
        effect.Parameters["WorldLight"]?.SetValue(light);


        // Lowest threshold of the three layers: ink should fuse into one continuous mass early, so
        // the remains sit inside a single silhouette rather than a cluster of separate blobs.
        effect.Parameters["Threshold"]?.SetValue(0.165f);

        // Coarse and strong. Fine erosion reads as smoke; at this scale the outline tears in big
        // slow lobes, which is what sells it as liquid rather than gas.
        effect.Parameters["NoiseScale"]?.SetValue(1.05f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.20f);

        // Almost still, and drifting very slightly up and left against the vortex's own spin so the
        // two motions never lock into one rotation.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.014f, -0.022f));

        // Barely moves, so the drag is slight -- enough that the mass reads as churning rather than as a pattern it is sliding under.
        effect.Parameters["FlowStrength"]?.SetValue(0.08f);

        // Three bands, not five. Ink has no interior detail worth resolving -- void, mass, rim.
        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.94f);

        // Void, through a cold indigo mass, to a pale rim. Deliberately the same two ends the menu
        // nebula runs between (InkStyle.InkLow and InkStyle.InkHigh) so the cutscene's gas and its
        // vortex read as one material.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.014f, 0.015f, 0.022f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.055f, 0.050f, 0.095f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.20f, 0.19f, 0.32f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.70f, 0.71f, 0.85f));
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
    protected override void ApplyLighting(ref FieldNode node) { }

    /// <summary>Ink pools against a surface instead of creeping along it.</summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity = Math.Min(node.Viscosity * 1.04f, 1f);
        node.Velocity *= 0.88f;
    }

    /// <summary>Water does not wash ink out; it disperses into it and goes on being dark.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Velocity *= 0.9f;
        node.Life -= 1f;
    }

    private static InkLayer? Layer => FieldSystem.GetLayer<InkLayer>();

    /// <summary>
    ///     The first billow, thrown the moment the remains come apart.
    /// </summary>
    public static void Bloom(Vector2 center, float radius, int count = 22)
    {
        if (Layer is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            // Slow and omnidirectional. A biased burst reads as an explosion; this should read as
            // something welling outward.
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
    ///     The shroud clinging to one drifting piece of the remains.
    /// </summary>
    /// <remarks>
    ///     Called once per tick per piece, so the caller sets how heavy the shroud gets by how often
    ///     it asks -- the same contract <see cref="SporeLayer.Cling" /> uses.
    ///     <para>
    ///         Takes a bare position and velocity rather than an <see cref="Entity" />, because the
    ///         pieces are particles and not entities, so there is no host for
    ///         <see cref="FieldLayer.SpawnAnchoredNode" /> to ride. Inheriting most of the piece's
    ///         motion buys the same thing: the node is already travelling with it on the frame it
    ///         appears, instead of being left behind and catching up.
    ///     </para>
    /// </remarks>
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
    ///     The vortex throat: ink drawn in off a ring that tightens as <paramref name="progress" />
    ///     climbs 0..1.
    /// </summary>
    /// <remarks>
    ///     Given a tangential kick as well as an inward one, so the field winds as it collapses and
    ///     the intake reads as a drain rather than as a cloud being scaled down. The two are summed
    ///     rather than blended: out at the wide ring the spin dominates and nodes visibly orbit, and
    ///     by the time the ring has tightened the inward term has grown enough to actually land them.
    /// </remarks>
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
            // Neutral and slack: nothing should sink out of the throat or lag behind the spin.
            density: 0.5f,
            viscosity: 0.1f
        );
    }

    /// <summary>Drops every live node at once, for the moment the vortex closes.</summary>
    public static void Clear() => Layer?.ClearNodes();
}
