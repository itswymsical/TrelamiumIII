using ReLogic.Content;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>Colours and noise for <see cref="FbmNameFlair" />, read by <c>TextFbm.fx</c>. Presets are starting points for <c>with</c>.</summary>
public sealed record FbmLook
{
    /// <summary>Ramp colour at low noise.</summary>
    public Color Shadow { get; init; } = new(70, 40, 120);

    /// <summary>Ramp colour at mid noise.</summary>
    public Color Body { get; init; } = new(150, 105, 225);

    /// <summary>Ramp colour at high noise.</summary>
    public Color Highlight { get; init; } = new(240, 225, 255);

    /// <summary>Noise UV per text pixel. Lower gives larger features.</summary>
    public float NoiseScale { get; init; } = 1f / 160f;

    /// <summary>Noise scroll, in UV per second.</summary>
    public Vector2 Drift { get; init; } = new(0.03f, -0.05f);

    /// <summary>Domain-warp strength, in UV. 0 disables.</summary>
    public float Warp { get; init; } = 0.2f;

    /// <summary>Stretch around the noise mean before the ramp. Four octaves cluster tightly, so 3 to 4 is typical.</summary>
    public float Contrast { get; init; } = 3.5f;

    /// <summary>Added to the noise before <see cref="Contrast" />. A texture whose mean is not 0.5 needs 0.5 minus its mean.</summary>
    public float Bias { get; init; }

    /// <summary>Posterisation steps. 0 keeps the ramp smooth.</summary>
    public float Bands { get; init; }

    /// <summary>The tiling noise the FBM is built from. The default is centred on 0.5.</summary>
    public Func<Asset<Texture2D>> Noise { get; init; } = static () => Assets.Textures.Noise.Perlin.Asset;

    public static FbmLook Arcane => new();

    public static FbmLook Molten => new()
    {
        Shadow = new Color(120, 25, 10),
        Body = new Color(235, 110, 30),
        Highlight = new Color(255, 235, 160),
        Drift = new Vector2(0.02f, -0.12f),
        Bands = 4f,
    };

    public static FbmLook Frost => new()
    {
        Shadow = new Color(70, 110, 170),
        Body = new Color(160, 200, 240),
        Highlight = Color.White,
        Drift = new Vector2(0.04f, 0.01f),
        Warp = 0.1f,
    };

    internal void Apply(Effect effect)
    {
        effect.Parameters["uNoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["uDrift"]?.SetValue(Drift);
        effect.Parameters["uWarp"]?.SetValue(Warp);
        effect.Parameters["uContrast"]?.SetValue(Contrast);
        effect.Parameters["uBias"]?.SetValue(Bias);
        effect.Parameters["uBands"]?.SetValue(Bands);
        effect.Parameters["uShadowColor"]?.SetValue(Shadow.ToVector3());
        effect.Parameters["uBodyColor"]?.SetValue(Body.ToVector3());
        effect.Parameters["uHighlightColor"]?.SetValue(Highlight.ToVector3());

        // The noise sampler cannot take a null texture, and the asset is null until it loads.
        if (Noise() is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
    }
}

/// <summary>
///     Paints the name through <c>TextFbm.fx</c>: drifting FBM noise through a three-colour ramp, continuous across glyphs.
///     Draws plain text while the shader loads.
/// </summary>
public sealed class FbmNameFlair : RarityFlair
{
    private Action<Effect, RarityNameContext>? applyLook;

    /// <summary>Resolved lazily, so a static asset reference cannot lose a load-order race.</summary>
    public Func<Asset<Effect>> Shader { get; init; } = static () => Assets.Shaders.TextFbm.Asset;

    public FbmLook Look { get; init; } = FbmLook.Arcane;

    /// <summary>Per-glyph wave, jitter and pulse. Null keeps the glyphs still.</summary>
    public GlyphMotion? Motion { get; init; }

    /// <summary>Outline and blends. Null uses the style's <see cref="RarityStyle.Text" />.</summary>
    public TextStyle? Text { get; init; }

    public override bool PaintsName => true;

    public override void PaintName(in RarityNameContext ctx)
        => ShaderNamePainter.Paint(ctx, Shader, null, Motion, Text, applyLook ??= (effect, _) => Look.Apply(effect));
}

/// <summary>
///     Paints the name through any pixel shader. Per glyph the painter sets uImageSize0, uSourceRect (glyph rectangle in font-page
///     pixels) and uGlyphPosition (glyph top-left in text pixels); per draw uTime and uTextSize. <c>TextFbm.fx</c> shows the mapping.
/// </summary>
public sealed class ShaderNameFlair : RarityFlair
{
    /// <summary>Resolved lazily, so a static asset reference cannot lose a load-order race.</summary>
    public required Func<Asset<Effect>> Shader { get; init; }

    /// <summary>Sets the effect's own parameters once per draw.</summary>
    public Action<Effect, RarityNameContext>? Apply { get; init; }

    /// <summary>Vertex colour of every glyph. Null is white, leaving colour to the shader.</summary>
    public Color? Tint { get; init; }

    /// <summary>Per-glyph wave, jitter and pulse. Null keeps the glyphs still.</summary>
    public GlyphMotion? Motion { get; init; }

    /// <summary>Outline and blends. Null uses the style's <see cref="RarityStyle.Text" />.</summary>
    public TextStyle? Text { get; init; }

    public override bool PaintsName => true;

    public override void PaintName(in RarityNameContext ctx) => ShaderNamePainter.Paint(ctx, Shader, Tint, Motion, Text, Apply);
}

internal static class ShaderNamePainter
{
    /// <summary>Paints the context's text through <paramref name="shader" />, or plainly in the text colour while it is not loaded.</summary>
    public static void Paint(in RarityNameContext ctx, Func<Asset<Effect>> shader, Color? tint, GlyphMotion? motion, TextStyle? text,
        Action<Effect, RarityNameContext>? apply)
    {
        Effect? effect = Main.dedServ ? null : shader().Value;
        GlyphLayout layout = ctx.Layout;

        Span<GlyphDraw> draws = TextPainter.Prepare(layout, effect is null ? ctx.Color : (tint ?? Color.White) * (ctx.Color.A / 255f));

        if (draws.IsEmpty)
            return;

        if (motion is not null)
        {
            for (int i = 0; i < draws.Length; i++)
                motion.Apply(ref draws[i], layout.Glyphs[i].Index, ctx.Time);
        }

        TextStyle style = text ?? ctx.Style.Text;

        if (effect is null)
        {
            TextPainter.Draw(ctx, draws, style);
            return;
        }

        effect.Parameters["uTime"]?.SetValue(ctx.Time);
        effect.Parameters["uTextSize"]?.SetValue(layout.Size);
        apply?.Invoke(effect, ctx);

        TextPainter.Draw(ctx, draws, style, effect);
    }
}
