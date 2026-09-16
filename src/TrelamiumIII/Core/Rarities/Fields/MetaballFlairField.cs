using ReLogic.Content;
using System.Collections.Generic;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>The look of one gas field for <c>GasFbm.fx</c>: colour ramp, erosion and rim. Presets are starting points for <c>with</c>.</summary>
public sealed record GasLook
{
    /// <summary>Sparse edge of the ramp.</summary>
    public Color Shadow { get; init; } = new(40, 40, 46);

    /// <summary>The bulk of the body.</summary>
    public Color Body { get; init; } = new(90, 90, 100);

    /// <summary>Densest centre.</summary>
    public Color Core { get; init; } = new(150, 150, 160);

    public Color Outline { get; init; } = new(24, 24, 28);

    public float OutlineAlpha { get; init; } = 0.75f;

    public float BodyAlpha { get; init; } = 0.68f;

    /// <summary>Field level where gas exists, after noise erosion. Lower merges into broader sheets.</summary>
    public float Threshold { get; init; } = 0.22f;

    /// <summary>Field units from <see cref="Threshold" /> to a fully shaded interior. See <see cref="FieldStamp.DepthRange" />.</summary>
    public float DepthRange { get; init; } = FieldStamp.DepthRange;

    public float NoiseScale { get; init; } = 0.2f;

    /// <summary>How hard the noise eats the field edge. 0 leaves clean metaballs.</summary>
    public float NoiseStrength { get; init; } = 0.32f;

    /// <summary>Scroll of the noise, in uv per second.</summary>
    public Vector2 Drift { get; init; } = new(0.05f, -0.09f);

    /// <summary>Posterisation steps in the body shade. Three or four reads as vanilla.</summary>
    public float Bands { get; init; } = 4f;

    /// <summary>The tiling noise the erosion is built from.</summary>
    public Func<Asset<Texture2D>> Noise { get; init; } = static () => Assets.Textures.Noise.Milky_2.Asset;

    public static GasLook Smoke => new();

    public static GasLook Flame => new()
    {
        Shadow = new Color(120, 30, 10),
        Body = new Color(230, 110, 30),
        Core = new Color(255, 230, 150),
        Outline = new Color(255, 190, 90),
        OutlineAlpha = 0.9f,
        BodyAlpha = 0.8f,
        Threshold = 0.2f,
        NoiseStrength = 0.45f,
        Drift = new Vector2(0.03f, -0.28f),
        Bands = 3f,
    };

    public static GasLook Spore => new()
    {
        Shadow = new Color(40, 70, 30),
        Body = new Color(110, 160, 70),
        Core = new Color(190, 220, 140),
        Outline = new Color(30, 45, 25),
        Threshold = 0.24f,
        Drift = new Vector2(0.04f, -0.06f),
    };

    public static GasLook Frost => new()
    {
        Shadow = new Color(120, 150, 200),
        Body = new Color(185, 210, 240),
        Core = Color.White,
        Outline = Color.White,
        OutlineAlpha = 0.7f,
        Threshold = 0.2f,
        NoiseStrength = 0.28f,
        Drift = new Vector2(0.05f, -0.11f),
    };

    internal void Apply(Effect effect)
    {
        effect.Parameters["Threshold"]?.SetValue(Threshold);
        effect.Parameters["DepthRange"]?.SetValue(DepthRange);
        effect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["NoiseStrength"]?.SetValue(NoiseStrength);
        effect.Parameters["NoiseDrift"]?.SetValue(Drift);
        effect.Parameters["Bands"]?.SetValue(Bands);
        effect.Parameters["BodyAlpha"]?.SetValue(BodyAlpha);
        effect.Parameters["ShadowColor"]?.SetValue(Shadow.ToVector3());
        effect.Parameters["BodyColor"]?.SetValue(Body.ToVector3());
        effect.Parameters["CoreColor"]?.SetValue(Core.ToVector3());
        effect.Parameters["OutlineColor"]?.SetValue(Outline.ToVector3());
        effect.Parameters["OutlineAlpha"]?.SetValue(OutlineAlpha);

        // The FBM sampler cannot take a null texture, and the asset is null until it loads.
        if (Noise() is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
    }
}

/// <summary>
///     Gas, smoke or flame off text: soft blobs summed into a half-resolution target and cut at a threshold so neighbours fuse.
///     World drops feed <see cref="WorldLayer" />, which draws with that layer's own look. Text and world only.
/// </summary>
public sealed class MetaballFlairField : FlairField
{
    /// <summary>Screen pixels per field texel; <c>GasFbm.fx</c> quantises its decisions to this grid.</summary>
    private const int PIXEL_SCALE = 2;

    /// <summary>Ticks with no blobs before the target is freed, so a hover that flickers off does not churn it.</summary>
    private const int IDLE_TICKS_BEFORE_RELEASE = 120;

    private struct Node
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Life;
        public float MaxLife;
        public float Growth;

        public readonly float Alpha => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);
    }

    private readonly List<Node> nodes = [];

    private FieldRenderTarget? target;
    private Action<SpriteBatch>? stampNodes;
    private Matrix transform = Matrix.Identity;
    private int idleTicks;

    /// <summary>The gas shader, resolved lazily for load order.</summary>
    public Func<Asset<Effect>> Shader { get; init; } = static () => Assets.Shaders.GasFbm.Asset;

    /// <summary>The soft radial falloff stamped once per blob.</summary>
    public Func<Asset<Texture2D>> Stamp { get; init; } = static () => Assets.Textures.VFX.RadialGradient.Asset;

    public GasLook Look { get; init; } = GasLook.Smoke;

    /// <summary>Blob radius at spawn, in pixels.</summary>
    public float Radius { get; init; } = 9f;

    /// <summary>Blob lifetime, in ticks.</summary>
    public float Life { get; init; } = 34f;

    /// <summary>Radius added per tick, for smoke that swells as it rises.</summary>
    public float Growth { get; init; } = 0.12f;

    /// <summary>Upward acceleration per tick. Negative sinks.</summary>
    public float Buoyancy { get; init; } = 0.06f;

    /// <summary>Per-tick velocity multiplier.</summary>
    public float Drag { get; init; } = 0.94f;

    /// <summary>Spread of the initial velocity.</summary>
    public Vector2 Scatter { get; init; } = new(0.7f, 0.5f);

    /// <summary>Live blobs before <see cref="VFXConfig.MetaballCountMultiplier" />; past it the oldest go.</summary>
    public int MaxNodes { get; init; } = 220;

    /// <summary>The world layer a dropped item feeds, or null to keep the gas on text.</summary>
    public Func<FieldLayer?>? WorldLayer { get; init; }

    public float WorldRadius { get; init; } = 7f;

    public float WorldLife { get; init; } = 30f;

    public override FieldSurfaces Supported => FieldSurfaces.Text | FieldSurfaces.World;

    protected override void Spawn(in FieldEmitter emitter)
    {
        transform = emitter.Transform;

        int budget = Math.Max((int)(MaxNodes * ModContent.GetInstance<VFXConfig>().MetaballCountMultiplier), 1);
        int excess = nodes.Count - budget + 1;

        if (excess > 0)
            nodes.RemoveRange(0, Math.Min(excess, nodes.Count));

        nodes.Add(new Node
        {
            Position = emitter.RandomPoint(),
            Velocity = Main.rand.NextVector2Circular(Scatter.X, Scatter.Y) * emitter.Scale,
            Radius = Radius * emitter.Scale,
            Life = Life,
            MaxLife = Life,
            Growth = Growth * emitter.Scale,
        });
    }

    protected override void Update()
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            Node node = nodes[i];

            node.Velocity.Y -= Buoyancy;
            node.Velocity *= Drag;
            node.Position += node.Velocity;
            node.Radius += node.Growth;
            node.Life--;

            if (node.Life <= 0f)
                nodes.RemoveAt(i);
            else
                nodes[i] = node;
        }

        if (nodes.Count > 0)
        {
            idleTicks = 0;
            return;
        }

        if (target is not null && ++idleTicks >= IDLE_TICKS_BEFORE_RELEASE)
        {
            target.Free();
            target = null;
            idleTicks = 0;
        }
    }

    protected internal override void Accumulate()
    {
        if (nodes.Count == 0)
            return;

        target ??= new FieldRenderTarget(PIXEL_SCALE);

        // Additive: overlapping blobs have to sum for neighbours to fuse.
        target.Build(transform, BlendState.Additive, stampNodes ??= StampNodes);
    }

    protected internal override void Draw(SpriteBatch spriteBatch, in FieldEmitter emitter)
    {
        if (emitter.Surface != FieldSurfaces.Text || !FirstDrawThisFrame())
            return;

        if (nodes.Count == 0 || target?.Current is not { } field || Shader() is not { Value: { } effect })
            return;

        ApplySharedParameters(effect, field);
        Look.Apply(effect);

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);

        // A full-screen quad in raw screen pixels; the shader discards wherever the field is empty.
        RarityBatch.Restart(spriteBatch, new SpriteBatchState(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, effect, Matrix.Identity));

        spriteBatch.Draw(field, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, PIXEL_SCALE, SpriteEffects.None, 0f);

        RarityBatch.Restart(spriteBatch, home);
    }

    protected internal override void SpawnWorld(Item item)
    {
        if (WorldLayer?.Invoke() is not { } layer)
            return;

        for (int i = RollCount(WorldRate); i > 0; i--)
        {
            Vector2 position = item.Center + Main.rand.NextVector2Circular(item.width * 0.4f, item.height * 0.4f);
            Vector2 velocity = Main.rand.NextVector2Circular(Scatter.X, Scatter.Y) - Vector2.UnitY * 0.3f;

            layer.SpawnNode(position, velocity, WorldRadius, WorldLife);
        }
    }

    protected internal override void Release()
    {
        target?.Free();
        target = null;
        nodes.Clear();
    }

    private void StampNodes(SpriteBatch spriteBatch)
    {
        if (Stamp() is not { Value: { } texture })
            return;

        Vector2 origin = texture.Size() * 0.5f;

        foreach (Node node in nodes)
        {
            // Plain coverage; the overdrive that fuses neighbours is FieldStamp.Gain, applied by the shader.
            // Flat: text has no world light to carry.
            spriteBatch.Draw(texture, node.Position, null, FieldStamp.Flat(node.Alpha), 0f, origin, node.Radius * 2f / texture.Width,
                SpriteEffects.None, 0f);
        }
    }

    /// <summary>Screen uniforms. Unit zoom and zero offset turn GasFbm's world-locked noise into screen-locked noise.</summary>
    private static void ApplySharedParameters(Effect effect, Texture2D field)
    {
        FieldLayer.ApplyScreenParameters(effect, PIXEL_SCALE);

        // Overridden after the shared block rather than instead of it. This field is drawn in
        // interface space, where there is no camera to undo and no world light to stand in, so the
        // two uniforms that locate the noise in the world are pinned to their identity values.
        effect.Parameters["Zoom"]?.SetValue(Vector2.One);
        effect.Parameters["WorldOffset"]?.SetValue(Vector2.Zero);
        effect.Parameters["WorldLight"]?.SetValue(Vector3.One);

        // The shared block sizes a texel against the screen; this target is sized against itself.
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(1f / field.Width, 1f / field.Height));
    }
}
