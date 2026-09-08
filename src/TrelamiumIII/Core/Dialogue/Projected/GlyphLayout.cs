using ReLogic.Graphics;

namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>Where one character of a line sits once the line has been broken to width.</summary>
internal readonly struct LaidGlyph(char character, int line, float x, float advance)
{
    public readonly char Character = character;

    /// <summary>Zero-based wrapped line this glyph belongs to.</summary>
    public readonly int Line = line;

    /// <summary>Offset from the left of its line, in texels.</summary>
    public readonly float X = x;

    /// <summary>How far the pen moves past <see cref="X" />, in texels.</summary>
    public readonly float Advance = advance;
}

/// <summary>
///     A line of dialogue broken to width once, up front, so nothing about it moves while it types.
/// </summary>
/// <remarks>
///     <para>
///         The whole line is laid out, not the revealed part of it, and this is the difference between
///         a box that grows and a box that thrashes. Wrapping what has arrived so far re-breaks the
///         text on almost every keystroke, so a word that fits at the end of one frame is thrown to
///         the next line on the following one and every glyph after it jumps. Laying out the finished
///         line and revealing into it means the only thing that changes as text arrives is how much of
///         a fixed arrangement is on screen.
///     </para>
///     <para>
///         Indices run one to one with the source string, spaces included, so the typewriter's
///         character count indexes straight into <see cref="Glyphs" /> and the affect system's spans
///         keep meaning what they meant.
///     </para>
/// </remarks>
internal sealed class GlyphLayout
{
    private GlyphLayout(LaidGlyph[] glyphs, float[] extents, int lineCount, float lineHeight)
    {
        Glyphs = glyphs;
        this.extents = extents;
        LineCount = lineCount;
        LineHeight = lineHeight;
    }

    /// <summary>Widest extent reached by the first n glyphs, for n in 0..Length.</summary>
    private readonly float[] extents;

    public LaidGlyph[] Glyphs { get; }

    public int LineCount { get; }

    public float LineHeight { get; }

    public static GlyphLayout Empty { get; } = new([], [0f], 0, 0f);

    /// <summary>How wide the box has to be to hold the first <paramref name="revealed" /> characters.</summary>
    public float ExtentAfter(int revealed) => extents[Math.Clamp(revealed, 0, extents.Length - 1)];

    /// <summary>How many lines the first <paramref name="revealed" /> characters have reached into.</summary>
    public int LinesAfter(int revealed)
    {
        int count = Math.Clamp(revealed, 0, Glyphs.Length);
        return count == 0 ? 0 : Glyphs[count - 1].Line + 1;
    }

    /// <param name="scale">Font scale, chosen so the drawn size lands on the texel grid.</param>
    /// <param name="maxWidth">Wrap width in texels.</param>
    public static GlyphLayout Build(string text, DynamicSpriteFont font, float scale, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return Empty;

        var glyphs = new LaidGlyph[text.Length];
        var extents = new float[text.Length + 1];

        float lineHeight = font.LineSpacing * scale;
        float pen = 0f;
        int line = 0;
        float widest = 0f;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (character == '\n')
            {
                // Consumed by the break: it still needs a glyph so indices stay aligned, but it
                // occupies no width and is never drawn.
                glyphs[i] = new LaidGlyph(character, line, pen, 0f);
                extents[i + 1] = widest;

                line++;
                pen = 0f;
                continue;
            }

            float advance = Advance(font, character, scale);

            if (character == ' ' && WordWidth(text, i + 1, font, scale) + pen + advance > maxWidth && pen > 0f)
            {
                glyphs[i] = new LaidGlyph(character, line, pen, 0f);
                extents[i + 1] = widest;

                line++;
                pen = 0f;
                continue;
            }

            glyphs[i] = new LaidGlyph(character, line, pen, advance);
            pen += advance;

            // A revealed trailing space would otherwise pad the box by its own width every time the
            // typewriter paused between words.
            if (character != ' ')
                widest = MathF.Max(widest, pen);

            extents[i + 1] = widest;
        }

        return new GlyphLayout(glyphs, extents, line + 1, lineHeight);
    }

    /// <summary>Width of the run starting at <paramref name="start" /> up to the next break.</summary>
    private static float WordWidth(string text, int start, DynamicSpriteFont font, float scale)
    {
        float width = 0f;

        for (int i = start; i < text.Length && text[i] != ' ' && text[i] != '\n'; i++)
            width += Advance(font, text[i], scale);

        return width;
    }

    /// <summary>
    ///     How far one character moves the pen.
    /// </summary>
    /// <remarks>
    ///     Measured per character and summed rather than measuring the whole string, because the
    ///     glyphs are drawn one at a time -- an affect can offset or recolour any of them -- and a
    ///     layout that disagreed with the drawing by even a fraction would drift across a long line.
    /// </remarks>
    private static float Advance(DynamicSpriteFont font, char character, float scale) =>
        font.MeasureString(character.ToString()).X * scale;
}
