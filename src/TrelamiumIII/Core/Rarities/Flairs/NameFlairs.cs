using ReLogic.Graphics;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>Paints the name glyph by glyph through a colour ramp. The ramp ping-pongs, so scrolling it leaves no seam where it wraps.</summary>
public sealed class GradientNameFlair : RarityFlair
{
    public Color From { get; init; } = Color.White;

    public Color To { get; init; } = Color.White;

    /// <summary>Ramps across the name's width. 1 is one full there-and-back over the whole name.</summary>
    public float Cycles { get; init; } = 1f;

    /// <summary>Ramp scroll speed, in cycles per second. 0 holds it still.</summary>
    public float Speed { get; init; } = 0.35f;

    /// <summary>Per-glyph wave, jitter and pulse. Null keeps the glyphs still.</summary>
    public GlyphMotion? Motion { get; init; }

    /// <summary>Outline and blends. Null uses the style's <see cref="RarityStyle.Text" />.</summary>
    public TextStyle? Text { get; init; }

    public override bool PaintsName => true;

    public override void PaintName(in RarityNameContext ctx)
    {
        GlyphLayout layout = ctx.Layout;
        Span<GlyphDraw> draws = TextPainter.Prepare(layout, Color.White);

        if (draws.IsEmpty)
            return;

        float alpha = ctx.Color.A / 255f;
        float last = Math.Max(ctx.Text.Length - 1, 1);

        for (int i = 0; i < draws.Length; i++)
        {
            ref GlyphDraw draw = ref draws[i];
            int index = layout.Glyphs[i].Index;

            draw.Color = Color.Lerp(From, To, PingPong(index / last * Cycles - ctx.Time * Speed)) * alpha;
            Motion?.Apply(ref draw, index, ctx.Time);
        }

        TextPainter.Draw(ctx, draws, Text ?? ctx.Style.Text);
    }

    /// <summary>Maps any real onto 0..1 and back again, with no discontinuity at the turns.</summary>
    private static float PingPong(float t)
    {
        float wrapped = t - MathF.Floor(t);

        return 1f - MathF.Abs(1f - 2f * wrapped);
    }
}

/// <summary>An additive ring of offset copies of the name, drawn under the glyphs so the text reads as lit from behind.</summary>
/// <remarks>Over the glyphs it washes the letterforms out at anything past a token intensity.</remarks>
public sealed class GhostNameFlair : RarityFlair
{
    /// <summary>Null takes the style's glow colour, which is usually what is wanted.</summary>
    public Color? Color { get; init; }

    /// <summary>Copies in the ring. Eight reads as a halo; fewer shows its corners.</summary>
    public int Copies { get; init; } = 8;

    /// <summary>Ring radius in pixels.</summary>
    public float Radius { get; init; } = 2f;

    /// <summary>Per-copy brightness. The ring sums, so this is well under 1 by design.</summary>
    public float Intensity { get; init; } = 0.3f;

    /// <summary>Radians per second the ring turns. 0 holds it still.</summary>
    public float Spin { get; init; } = 0.6f;

    /// <summary>Depth of the brightness pulse, 0..1. 0 holds it steady.</summary>
    public float Pulse { get; init; } = 0.35f;

    public float PulseSpeed { get; init; } = 2.3f;

    public override BlendState Blend => BlendState.Additive;

    public override void UnderName(in RarityNameContext ctx)
    {
        if (Copies <= 0 || ctx.Layout.Glyphs.Length == 0)
            return;

        var tint = Color ?? ctx.Style.GlowColor;
        float breath = 1f + MathF.Sin(ctx.Time * PulseSpeed) * Pulse;
        var halo = tint * (Intensity * breath * (ctx.Color.A / 255f));

        for (int i = 0; i < Copies; i++)
        {
            float angle = i / (float)Copies * MathHelper.TwoPi + ctx.Time * Spin;
            Vector2 offset = angle.ToRotationVector2() * Radius;

            ctx.SpriteBatch.DrawString(ctx.Font, ctx.Text, ctx.Position + offset, halo, ctx.Rotation, ctx.Origin, ctx.Scale,
                SpriteEffects.None, 0f);
        }
    }
}
