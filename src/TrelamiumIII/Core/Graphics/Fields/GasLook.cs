using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     The material of a field drawn through <c>GasFbm.fx</c>: colour ramp, erosion, rim, liquid surface and hot ramp.
///     Defaults match <see cref="FieldLayer.ApplyScreenParameters" />. Presets are starting points for <c>with</c>.
/// </summary>
public sealed record GasLook
{
    /// <summary>Sparse edge of the ramp.</summary>
    public Vector3 Shadow { get; init; } = Rgb(40, 40, 46);

    /// <summary>The bulk of the body.</summary>
    public Vector3 Body { get; init; } = Rgb(90, 90, 100);

    /// <summary>Densest centre.</summary>
    public Vector3 Core { get; init; } = Rgb(150, 150, 160);

    public Vector3 Outline { get; init; } = Rgb(24, 24, 28);

    public float OutlineAlpha { get; init; } = 0.75f;

    public float BodyAlpha { get; init; } = 0.68f;

    /// <summary>Alpha of the thinnest band, as a fraction of <see cref="BodyAlpha" />.</summary>
    public float BodyAlphaFloor { get; init; } = 0.75f;

    /// <summary>Field level where the material exists, after noise erosion. Lower merges into broader sheets.</summary>
    public float Threshold { get; init; } = 0.22f;

    /// <summary>Field units from <see cref="Threshold" /> to a fully shaded interior.</summary>
    public float DepthRange { get; init; } = FieldStamp.DepthRange;

    public float NoiseScale { get; init; } = 0.2f;

    /// <summary>How hard the noise eats the field edge. 0 leaves clean metaballs.</summary>
    public float NoiseStrength { get; init; } = 0.32f;

    /// <summary>Scroll of the noise, in uv per second.</summary>
    public Vector2 Drift { get; init; } = new(0.05f, -0.09f);

    /// <summary>Noise anisotropy. Below 1 on y streaks it vertically, for liquids.</summary>
    public Vector2 NoiseStretch { get; init; } = Vector2.One;

    /// <summary>Posterisation steps in the body shade. Three or four reads as vanilla.</summary>
    public float Bands { get; init; } = 4f;

    /// <summary>How far node flow advects the noise. 0 locks it to the world.</summary>
    public float FlowStrength { get; init; }

    /// <summary>Ambient light floor the palette is multiplied by, or null to leave the caller's value.</summary>
    public Vector3? WorldLight { get; init; }

    /// <summary>Colour of the liquid surface line.</summary>
    public Vector3 Highlight { get; init; } = Vector3.One;

    /// <summary>Strength of the liquid surface line. 0 for gas.</summary>
    public float HighlightAlpha { get; init; }

    /// <summary>How far node temperature blends the ramp toward the hot colours. 0 disables it.</summary>
    public float TemperatureBlend { get; init; }

    public Vector3 HotShadow { get; init; }

    public Vector3 HotBody { get; init; }

    public Vector3 HotCore { get; init; }

    /// <summary>The tiling noise the erosion is built from.</summary>
    public Func<Asset<Texture2D>> Noise { get; init; } = static () => Assets.Textures.Noise.Milky_2.Asset;

    public static GasLook Smoke => new();

    public static GasLook Flame => new()
    {
        Shadow = Rgb(120, 30, 10),
        Body = Rgb(230, 110, 30),
        Core = Rgb(255, 230, 150),
        Outline = Rgb(255, 190, 90),
        OutlineAlpha = 0.9f,
        BodyAlpha = 0.8f,
        Threshold = 0.2f,
        NoiseStrength = 0.45f,
        Drift = new Vector2(0.03f, -0.28f),
        Bands = 3f,
    };

    public static GasLook Spore => new()
    {
        Shadow = Rgb(40, 70, 30),
        Body = Rgb(110, 160, 70),
        Core = Rgb(190, 220, 140),
        Outline = Rgb(30, 45, 25),
        Threshold = 0.24f,
        Drift = new Vector2(0.04f, -0.06f),
    };

    public static GasLook Frost => new()
    {
        Shadow = Rgb(120, 150, 200),
        Body = Rgb(185, 210, 240),
        Core = Vector3.One,
        Outline = Vector3.One,
        OutlineAlpha = 0.7f,
        Threshold = 0.2f,
        NoiseStrength = 0.28f,
        Drift = new Vector2(0.05f, -0.11f),
    };

    internal void Apply(Effect effect)
    {
        EffectParameterCollection parameters = effect.Parameters;

        parameters["Threshold"]?.SetValue(Threshold);
        parameters["DepthRange"]?.SetValue(DepthRange);
        parameters["NoiseScale"]?.SetValue(NoiseScale);
        parameters["NoiseStrength"]?.SetValue(NoiseStrength);
        parameters["NoiseDrift"]?.SetValue(Drift);
        parameters["NoiseStretch"]?.SetValue(NoiseStretch);
        parameters["FlowStrength"]?.SetValue(FlowStrength);
        parameters["Bands"]?.SetValue(Bands);
        parameters["BodyAlpha"]?.SetValue(BodyAlpha);
        parameters["BodyAlphaFloor"]?.SetValue(BodyAlphaFloor);
        parameters["ShadowColor"]?.SetValue(Shadow);
        parameters["BodyColor"]?.SetValue(Body);
        parameters["CoreColor"]?.SetValue(Core);
        parameters["OutlineColor"]?.SetValue(Outline);
        parameters["OutlineAlpha"]?.SetValue(OutlineAlpha);
        parameters["HighlightColor"]?.SetValue(Highlight);
        parameters["HighlightAlpha"]?.SetValue(HighlightAlpha);
        parameters["TemperatureBlend"]?.SetValue(TemperatureBlend);
        parameters["HotShadowColor"]?.SetValue(HotShadow);
        parameters["HotBodyColor"]?.SetValue(HotBody);
        parameters["HotCoreColor"]?.SetValue(HotCore);

        if (WorldLight is { } light)
            parameters["WorldLight"]?.SetValue(light);

        // The FBM sampler cannot take a null texture, and the asset is null until it loads.
        if (Noise() is { Value: { } noise })
            parameters["uNoiseTex"]?.SetValue(noise);
    }
}
