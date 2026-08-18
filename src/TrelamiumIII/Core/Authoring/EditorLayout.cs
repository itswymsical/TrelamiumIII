using System.Collections.Generic;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     Wraps the edit buffer's plain text to a column and answers the geometry questions a caret asks:
///     where an offset sits, and which offset a point lands on.
/// </summary>
/// <remarks>
///     <para>
///         Character advances are accumulated with the same recurrence
///         <see cref="ReLogic.Graphics.DynamicSpriteFont.MeasureString" /> uses -- leading bearing
///         clamped on the first glyph of a line, then spacing plus the previous glyph's trailing
///         bearing before each subsequent one. Anything approximate here shows up immediately as a
///         caret that drifts away from the glyphs as you move along a line.
///     </para>
///     <para>
///         Metrics arrive through a delegate and every coordinate is a plain float, so the wrap and
///         hit-testing can be exercised with synthetic glyph widths and no graphics device. Only the
///         caller needs to know about fonts.
///     </para>
/// </remarks>
public sealed class EditorLayout
{
    /// <summary>Left bearing, ink width and right bearing of one glyph, unscaled.</summary>
    public delegate (float Left, float Width, float Right) MetricsProvider(char character);

    /// <summary>A visual line: a half-open range of the source text.</summary>
    /// <param name="HardBreak">True when a newline ended the line rather than the column edge.</param>
    public readonly record struct Line(int Start, int Length, bool HardBreak)
    {
        public int End => Start + Length;
    }

    private readonly string text;
    private readonly float[] prefix;
    private readonly List<Line> lines = [];

    public EditorLayout(
        string text,
        MetricsProvider metrics,
        float characterSpacing,
        float scale,
        float lineHeight,
        float maxWidth)
    {
        this.text = text;
        LineHeight = lineHeight;

        prefix = BuildPrefixWidths(text, metrics, characterSpacing, scale);
        Wrap(maxWidth);
    }

    public IReadOnlyList<Line> Lines => lines;

    public float LineHeight { get; }

    public float Height => lines.Count * LineHeight;

    /// <summary>
    ///     Running width of the first <c>i</c> characters, for every <c>i</c> from 0 to length.
    /// </summary>
    /// <remarks>
    ///     Measured once for the whole string rather than per line. A line's own width is the
    ///     difference of two entries only because every line here restarts at the same pen origin;
    ///     the first-glyph bearing clamp is re-applied in <see cref="XOf" /> where it matters.
    /// </remarks>
    private static float[] BuildPrefixWidths(
        string text,
        MetricsProvider metrics,
        float characterSpacing,
        float scale)
    {
        float[] widths = new float[text.Length + 1];
        float pen = 0f;
        float trailing = 0f;
        bool first = true;

        for (int i = 0; i < text.Length; i++)
        {
            (float left, float width, float right) = metrics(text[i]);

            if (first)
                left = Math.Max(left, 0f);
            else
                pen += characterSpacing + trailing;

            pen += left + width;
            trailing = right;
            first = false;

            widths[i + 1] = (pen + Math.Max(trailing, 0f)) * scale;
        }

        return widths;
    }

    /// <summary>Greedy word wrap. Breaks mid-word only when a single word cannot fit the column.</summary>
    private void Wrap(float maxWidth)
    {
        lines.Clear();

        int lineStart = 0;
        int lastBreak = -1;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(new Line(lineStart, i - lineStart, true));
                lineStart = i + 1;
                lastBreak = -1;

                continue;
            }

            if (text[i] == ' ') lastBreak = i;

            if (Width(lineStart, i + 1) <= maxWidth || i == lineStart) continue;

            // Break after the last space so the space stays on the line it ended.
            int cut = lastBreak > lineStart ? lastBreak + 1 : i;

            lines.Add(new Line(lineStart, cut - lineStart, false));
            lineStart = cut;
            lastBreak = -1;
            i = cut - 1;
        }

        // Always emit a final line, so an empty buffer still has somewhere to put the caret.
        lines.Add(new Line(lineStart, text.Length - lineStart, true));
    }

    private float Width(int start, int end) => prefix[end] - prefix[start];

    /// <summary>Index of the visual line holding an offset.</summary>
    /// <remarks>
    ///     The two kinds of break resolve the end-of-line caret differently. A soft break shares its
    ///     offset with the line below -- the next line starts at exactly that index -- and the caret
    ///     belongs to the line it wrapped onto. A hard break does not: the newline occupies the
    ///     offset, the next line starts past it, and a caret sitting there is at the end of this line
    ///     with nowhere else to be. Treating both the same puts the caret a whole line away from the
    ///     click every time a paragraph ends.
    /// </remarks>
    public int LineOf(int index)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (index < lines[i].End) return i;

            if (index == lines[i].End && lines[i].HardBreak) return i;
        }

        return lines.Count - 1;
    }

    public Line LineAt(int line) => lines[Math.Clamp(line, 0, lines.Count - 1)];

    /// <summary>Horizontal offset of a caret sitting at <paramref name="index" />.</summary>
    public float XOf(int index)
    {
        Line line = lines[LineOf(index)];

        return Width(line.Start, Math.Clamp(index, line.Start, line.End));
    }

    /// <summary>Top-left of the caret for an offset, relative to the text origin.</summary>
    public (float X, float Y) CaretAt(int index)
    {
        int line = LineOf(index);

        return (XOf(index), line * LineHeight);
    }

    /// <summary>Nearest caret offset to a point, for click and drag placement.</summary>
    public int IndexAt(float x, float y)
    {
        int line = Math.Clamp((int)(y / LineHeight), 0, lines.Count - 1);

        return IndexOnLine(line, x);
    }

    /// <summary>Nearest caret offset to an x position on a given line.</summary>
    public int IndexOnLine(int line, float x)
    {
        Line bounds = LineAt(line);

        // Trailing space on a wrapped line lives past the visible edge; landing the caret after it
        // would put it on the next line, so a click past the end stops before it.
        int last = bounds.End;

        if (!bounds.HardBreak && last > bounds.Start && text[last - 1] == ' ') last--;

        int best = bounds.Start;
        float bestDistance = float.MaxValue;

        for (int i = bounds.Start; i <= last; i++)
        {
            float distance = Math.Abs(Width(bounds.Start, i) - x);

            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    /// <summary>
    ///     Caret one visual line up or down, holding <paramref name="desiredX" /> across the move.
    /// </summary>
    /// <remarks>
    ///     The x is passed in rather than read from the current offset so a run of up-presses through
    ///     a short line does not permanently pull the caret left -- the same reason every editor keeps
    ///     a sticky column.
    /// </remarks>
    public int MoveVertical(int index, int direction, float desiredX)
    {
        int line = LineOf(index) + Math.Sign(direction);

        if (line < 0) return 0;
        if (line >= lines.Count) return text.Length;

        return IndexOnLine(line, desiredX);
    }

    public int LineStartOf(int index) => LineAt(LineOf(index)).Start;

    /// <summary>End of the visual line, before any trailing space a soft wrap left behind.</summary>
    public int LineEndOf(int index)
    {
        Line line = LineAt(LineOf(index));
        int end = line.End;

        if (!line.HardBreak && end > line.Start && text[end - 1] == ' ') end--;

        return end;
    }
}
