using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Metaballs.Layers;

/// <summary>
///     Ore drawn up the Lodestone Magnetizer's intake: a stream of molten-looking blobs travelling
///     from the rock being broken back to the gun, in the colour of whatever is being broken.
/// </summary>
/// <remarks>
///     <para>
///         Metaballs rather than particles because the stream has to read as one continuous flow of
///         material. A line of separate sprites is a line of separate sprites however tightly it is
///         packed; a thresholded density field fuses in the gaps and comes apart again where the
///         stream thins, which is what makes it look like stuff rather than like an emitter.
///     </para>
///     <para>
///         The other two layers settle or rise under their own physics. This one is pulled, so
///         <see cref="UpdateNode" /> is replaced rather than extended: an anchored node here homes on
///         its host instead of riding at a fixed offset from it, and only falls back to the base
///         class's drift once the pull lets go.
///     </para>
///     <para>
///         <b>One colour for the whole layer.</b> The composite is a single full-screen draw, so the
///         shader gets one palette per frame however many nodes there are -- the same constraint that
///         makes <see cref="MetaballLayer.SampleAverageLight" /> a layer-wide average. Two players
///         mining different ores at once therefore share the newest colour. That is a fair trade for
///         one draw call, and it costs nothing in the case that actually happens.
///     </para>
/// </remarks>
public sealed class MagnetizerLayer : MetaballLayer
{
    /// <summary>Inward acceleration per tick while a node is in the grip.</summary>
    private const float PULL = 0.55f;

    /// <summary>
    ///     Sideways acceleration, applied along whichever side of the pull the node is already
    ///     drifting toward. Ore that flies dead straight down the axis reads as a conveyor; letting
    ///     each blob keep curving the way it happens to be going makes the stream spiral in.
    /// </summary>
    private const float CURL = 0.22f;

    private const float MAX_SPEED = 13f;

    /// <summary>Distance at which a node has arrived and is consumed.</summary>
    private const float ARRIVAL = 18f;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    /// <summary>Neutral. Ore in a magnetic grip should neither sink nor float out of the stream.</summary>
    protected override float DefaultDensity => 0.5f;

    protected override float DefaultViscosity => 0.35f;

    protected override float BuoyancyForce => 0.02f;

    protected override float GravityScale => 0.14f;

    /// <summary>Almost none. The pull is what moves these, and drag only fights it.</summary>
    protected override float Drag => 0.995f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 3f;

    /// <summary>
    ///     Lower than the gas layers on purpose. The stream is thin by design -- it crosses the tiles
    ///     the player is trying to see, and a mining tool that hides its own workings is a bad one.
    /// </summary>
    protected override int BaseMaxNodes => 160;

    private Asset<Texture2D>? noiseTexture;

    private Vector3 tint = new(0.6f, 0.6f, 0.65f);

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

    private static MagnetizerLayer? Layer => MetaballSystem.GetLayer<MagnetizerLayer>();

    /// <summary>
    ///     Sets the colour the stream is drawn in. Eased rather than assigned, so sweeping the beam
    ///     off copper and onto stone is a slide instead of a cut.
    /// </summary>
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
    /// <remarks>
    ///     Called once per blob wanted, per tick -- the caller decides how heavy the stream is by how
    ///     often it asks, the same way <see cref="SporeLayer.Cling" /> works.
    /// </remarks>
    /// <param name="gripTicks">
    ///     How long the pull holds. Past it the blob is released and finishes its life falling, which
    ///     is what a chunk that failed to make it back to the gun should do.
    /// </param>
    public static void Stream(Entity host, Vector2 position, Vector2 velocity, float radius, float life, int gripTicks)
    {
        Layer?.SpawnAnchoredNode(host, position, velocity, radius, life, gripTicks);
    }

    protected override void UpdateNode(ref MetaballNode node, int index)
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

            // Whichever side of the pull the node is already crossing. Derived from its own motion
            // rather than rolled per node, because a node struct has nowhere to keep a roll -- and
            // deriving it means the curl never fights the velocity it is bending.
            float side = Math.Sign(pull.X * node.Velocity.Y - pull.Y * node.Velocity.X);

            node.Velocity += pull * PULL + pull.RotatedBy(MathHelper.PiOver2) * (CURL * side);

            if (node.Velocity.LengthSquared() > MAX_SPEED * MAX_SPEED)
                node.Velocity = node.Velocity.SafeNormalize(Vector2.Zero) * MAX_SPEED;

            node.Position += node.Velocity;
            node.Life -= LifeDecay;

            return;
        }

        // Cleared first: the base class would otherwise treat a dead host or an expired grip as
        // something to release, and hand the node its host's momentum a second time.
        node.AnchorHost = null;
        node.AnchorTicks = 0;

        base.UpdateNode(ref node, index);
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        // Guarded rather than passed straight through: the FBM sampler cannot take a null texture,
        // and the asset is still null on the frames before it finishes loading.
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Floored, but not far. Ore in the grip glows a little from being torn apart, and a stream
        // that vanishes in an unlit cave stops telling the player the tool is working -- which is
        // most of what it is there for.
        Vector3 light = new Vector3(0.38f); // ambient floor; the real light rides in the field per node
        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Highest threshold of the three layers. Spores and mist want to merge into sheets; ore
        // wants to stay lumps, because lumps are what is being carried.
        effect.Parameters["Threshold"]?.SetValue(0.3f);
        effect.Parameters["NoiseScale"]?.SetValue(1.7f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.2f);

        // Drifts against the flow, so the grain of each blob churns while the blob itself travels.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.07f, 0.04f));

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.62f);

        // The whole ramp is built off the one ore colour, so a stream of gold and a stream of lead
        // differ in hue without either of them losing the shading that makes it read as solid.
        effect.Parameters["ShadowColor"]?.SetValue(tint * 0.3f);
        effect.Parameters["BodyColor"]?.SetValue(tint * 0.72f);
        effect.Parameters["CoreColor"]?.SetValue(Vector3.Lerp(tint, Vector3.One, 0.4f));
        effect.Parameters["OutlineColor"]?.SetValue(Vector3.Lerp(tint, Vector3.One, 0.72f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
    }

    protected override void ApplyLighting(ref MetaballNode node)
    {
        float alpha = node.Alpha;

        Lighting.AddLight(node.Position, tint.X * 0.14f * alpha, tint.Y * 0.14f * alpha, tint.Z * 0.14f * alpha);
    }
}
