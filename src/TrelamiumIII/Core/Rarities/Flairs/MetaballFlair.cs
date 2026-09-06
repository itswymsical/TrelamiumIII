using ReLogic.Content;
using TrelamiumIII.Core.Graphics.Metaballs;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>
///     The look of one gas field: the colour ramp, the erosion, and the rim.
/// </summary>
/// <remarks>
///     Every field in the mod goes through <c>GasFbm.fx</c>, which is fully parameterised, so a new
///     kind of gas is a table of numbers rather than a new shader. The presets are starting points,
///     not a closed set -- <c>Smoke with { Threshold = 0.3f }</c> is a legal and normal thing to
///     write.
/// </remarks>
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

        // Guarded rather than passed straight through: the FBM sampler cannot take a null texture,
        // and the asset is still null on the frames before it finishes loading.
        if (Noise() is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
    }
}

/// <summary>
///     Gas, smoke or flame billowing off the item -- around the tooltip name, and off the item where
///     it lies on the ground.
/// </summary>
/// <remarks>
///     <para>
///         Two fields, because the two surfaces are genuinely different systems and pretending
///         otherwise is what makes this hard. The tooltip is fed by a <see cref="UiMetaballField" />
///         in interface space, composited over the interface. A dropped item is fed into one of the
///         mod's existing world <see cref="MetaballLayer" />s, which already handle tile collision,
///         world lighting and the depth band a cloud in the world belongs in -- none of which a
///         tooltip has any use for.
///     </para>
///     <para>
///         The world half is opt-in through <see cref="WorldLayer" /> precisely because those layers
///         are shared: feeding one means the rarity's gas is drawn with that layer's look, not
///         <see cref="Look" />. Point it at a layer whose look matches, or leave it null and let the
///         gas live on the tooltip alone.
///     </para>
/// </remarks>
public sealed class MetaballFlair : RarityFlair
{
    /// <summary>The gas shader. Lazy, for the same load-order reason as everywhere else.</summary>
    public Func<Asset<Effect>> Shader { get; init; } = static () => Assets.Shaders.GasFbm.Asset;

    /// <summary>The soft radial falloff stamped once per blob.</summary>
    public Func<Asset<Texture2D>> Stamp { get; init; } = static () => Assets.Textures.VFX.RadialGradient.Asset;

    public GasLook Look { get; init; } = GasLook.Smoke;

    /// <summary>Blobs added per frame while the tooltip is open. Fractional rates are honoured.</summary>
    public float SpawnRate { get; init; } = 1.5f;

    public float Radius { get; init; } = 9f;

    public float Life { get; init; } = 34f;

    /// <summary>Radius added per tick, for smoke that swells as it rises.</summary>
    public float Growth { get; init; } = 0.12f;

    /// <summary>Upward acceleration per tick. Negative sinks, for something heavier than air.</summary>
    public float Buoyancy { get; init; } = 0.06f;

    /// <summary>Spread of the initial velocity.</summary>
    public Vector2 Scatter { get; init; } = new(0.7f, 0.5f);

    /// <summary>Pixels of vertical offset from the name's baseline the gas is born at.</summary>
    public float NameOffsetY { get; init; } = 6f;

    /// <summary>Whether the gas also pours off items sitting in inventory slots.</summary>
    /// <remarks>
    ///     Off by default. A slot draw runs for every visible slot of every open interface, so a
    ///     chest full of one rarity would feed the field a few hundred blobs a frame and reduce the
    ///     whole thing to a grey wash.
    /// </remarks>
    public bool InSlots { get; init; }

    /// <summary>
    ///     The world layer a dropped item feeds, or null to keep the gas on the tooltip.
    /// </summary>
    public Func<MetaballLayer?>? WorldLayer { get; init; }

    /// <summary>Blobs per tick from a dropped item.</summary>
    public float WorldSpawnRate { get; init; } = 0.5f;

    public float WorldRadius { get; init; } = 7f;

    public float WorldLife { get; init; } = 30f;

    private UiMetaballField? uiField;
    private float carry;
    private float worldCarry;

    /// <summary>The composite runs full-screen and immediate, so the renderer keeps out of it.</summary>
    public override bool ManagesOwnBatch => true;

    public override void Unload()
    {
        uiField?.Release();
        uiField = null;
    }

    /// <summary>
    ///     Feeds the field, then composites it behind the name.
    /// </summary>
    /// <remarks>
    ///     Spawning during a draw hook rather than an update is deliberate: the tooltip's position is
    ///     only known while it is being drawn, and it is the position that decides where the gas is
    ///     born. Simulation still happens on the update tick, so the blobs move once per tick rather
    ///     than once per frame.
    /// </remarks>
    public override void UnderName(in RarityNameContext ctx)
    {
        if (Main.dedServ)
            return;

        var live = Field;

        Emit(live, ctx.Position + new Vector2(0f, ctx.Size.Y + NameOffsetY), ctx.Size.X, SpawnRate, ref carry);

        live.Composite(ctx.SpriteBatch);
    }

    public override void UnderSprite(in RaritySpriteContext ctx)
    {
        if (Main.dedServ || !InSlots || ctx.InWorld)
            return;

        var live = Field;

        Emit(live, ctx.Center + new Vector2(-ctx.SpriteSize.X * 0.5f, ctx.SpriteSize.Y * 0.35f),
            ctx.SpriteSize.X, SpawnRate, ref carry);

        live.Composite(ctx.SpriteBatch);
    }

    /// <summary>Feeds the world layer for an item lying on the ground.</summary>
    public override void UpdateWorldItem(Item item)
    {
        if (Main.dedServ || WorldLayer?.Invoke() is not { } layer)
            return;

        worldCarry += WorldSpawnRate;

        while (worldCarry >= 1f)
        {
            worldCarry--;

            Vector2 spawn = item.Center + Main.rand.NextVector2Circular(item.width * 0.4f, item.height * 0.4f);
            Vector2 velocity = Main.rand.NextVector2Circular(Scatter.X, Scatter.Y) - Vector2.UnitY * 0.3f;

            layer.SpawnNode(spawn, velocity, WorldRadius, WorldLife);
        }
    }

    /// <summary>
    ///     Adds this frame's blobs along <paramref name="width" /> from <paramref name="origin" />.
    /// </summary>
    /// <remarks>
    ///     <paramref name="carried" /> holds the fractional remainder between frames, so a rate below
    ///     one blob per frame produces an even trickle instead of rounding to nothing.
    /// </remarks>
    private void Emit(UiMetaballField live, Vector2 origin, float width, float rate, ref float carried)
    {
        carried += rate;

        while (carried >= 1f)
        {
            carried--;

            Vector2 position = origin + new Vector2(Main.rand.NextFloat(0f, MathF.Max(width, 1f)), 0f);
            Vector2 velocity = Main.rand.NextVector2Circular(Scatter.X, Scatter.Y);

            live.Spawn(position, velocity, Radius, Life, Growth);
        }
    }

    private UiMetaballField Field => uiField ??= new UiMetaballField(Shader, Stamp)
    {
        Buoyancy = Buoyancy,
        ApplyParameters = Look.Apply,
    };
}
