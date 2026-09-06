using ReLogic.Graphics;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>
///     Paints the item name one glyph at a time through a colour ramp, optionally rippling.
/// </summary>
/// <remarks>
///     <para>
///         Glyphs are placed by accumulating each character's own advance rather than by measuring
///         prefixes of the string. <see cref="DynamicSpriteFont" /> lays text out per character with
///         no pair kerning, so the two agree, and the accumulating form stays linear instead of
///         re-measuring a growing prefix for every character.
///     </para>
///     <para>
///         The ramp ping-pongs rather than wrapping. A sawtooth ramp that scrolls has a seam where it
///         resets, and on a six-letter word the seam is a third of the name flicking colour once a
///         second.
///     </para>
/// </remarks>
public sealed class GradientNameFlair : RarityFlair
{
    public Color From { get; init; } = Color.White;

    public Color To { get; init; } = Color.White;

    /// <summary>Ramps across the name's width. 1 is one full there-and-back over the whole name.</summary>
    public float Cycles { get; init; } = 1f;

    /// <summary>Ramp scroll speed, in cycles per second. 0 holds it still.</summary>
    public float Speed { get; init; } = 0.35f;

    /// <summary>Vertical ripple height in pixels. 0 keeps the baseline flat.</summary>
    public float WaveAmplitude { get; init; }

    public float WaveSpeed { get; init; } = 4f;

    /// <summary>Radians of ripple phase per character. Larger values make a tighter wave.</summary>
    public float WavePhasePerGlyph { get; init; } = 0.4f;

    /// <summary>Whether to keep vanilla's drop shadow. Off reads as light rather than ink.</summary>
    public bool Shadow { get; init; } = true;

    public override bool PaintsName => true;

    public override BlendState Blend => BlendState.AlphaBlend;

    public override void PaintName(in RarityNameContext ctx)
    {
        var line = ctx.Line;
        var font = line.Font;
        string text = line.Text;

        if (string.IsNullOrEmpty(text))
            return;

        float alpha = ctx.Color.A / 255f;
        float advance = 0f;

        for (int i = 0; i < text.Length; i++)
        {
            string glyph = text[i].ToString();

            float phase = text.Length == 1 ? 0f : i / (float)(text.Length - 1);
            float ramp = PingPong(phase * Cycles - ctx.Time * Speed);

            Color color = Color.Lerp(From, To, ramp) * alpha;

            float lift = WaveAmplitude == 0f
                ? 0f
                : MathF.Sin(ctx.Time * WaveSpeed + i * WavePhasePerGlyph) * WaveAmplitude;

            Vector2 position = ctx.Position + new Vector2(advance, lift);

            if (Shadow)
            {
                ChatManager.DrawColorCodedStringWithShadow(ctx.SpriteBatch, font, glyph, position,
                    color, line.Rotation, line.Origin, line.BaseScale);
            }
            else
            {
                ctx.SpriteBatch.DrawString(font, glyph, position, color, line.Rotation, line.Origin,
                    line.BaseScale, SpriteEffects.None, 0f);
            }

            advance += font.MeasureString(glyph).X * line.BaseScale.X;
        }
    }

    /// <summary>Maps any real onto 0..1 and back again, with no discontinuity at the turns.</summary>
    private static float PingPong(float t)
    {
        float wrapped = t - MathF.Floor(t);

        return 1f - MathF.Abs(1f - 2f * wrapped);
    }
}

/// <summary>
///     An additive halo of offset copies of the name, so the text reads as lit from behind.
/// </summary>
/// <remarks>
///     Drawn under the glyphs rather than over them: over the top it washes the letterforms out into
///     a smear at anything past a token intensity, which is the failure mode that makes this sort of
///     effect look cheap.
/// </remarks>
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
        if (Copies <= 0 || string.IsNullOrEmpty(ctx.Line.Text))
            return;

        var line = ctx.Line;
        Color tint = Color ?? ctx.Style.GlowColor;

        float breath = 1f + MathF.Sin(ctx.Time * PulseSpeed) * Pulse;
        Color halo = tint * (Intensity * breath * (ctx.Color.A / 255f));

        for (int i = 0; i < Copies; i++)
        {
            float angle = i / (float)Copies * MathHelper.TwoPi + ctx.Time * Spin;
            Vector2 offset = angle.ToRotationVector2() * Radius;

            ctx.SpriteBatch.DrawString(line.Font, line.Text, ctx.Position + offset, halo,
                line.Rotation, line.Origin, line.BaseScale, SpriteEffects.None, 0f);
        }
    }
}
