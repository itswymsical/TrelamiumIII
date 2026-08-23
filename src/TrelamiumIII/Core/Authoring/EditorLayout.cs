using System.Collections.Generic;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     Wraps the edit buffer to a column and answers the geometry questions a caret asks: where an
///     offset sits, and which offset a point lands on.
/// </summary>
/// <remarks>
///     <para>
///         Metrics arrive per <em>index</em>, not per character value, because two identical letters
///         in the same buffer may be set at different sizes or in different faces. Everything the
///         provider returns is already scaled, so this class never learns what a font is and every
///         coordinate it reports is in drawn pixels.
///     </para>
///     <para>
///         Character advances accumulate with the same recurrence
///         <see cref="ReLogic.Graphics.DynamicSpriteFont.MeasureString" /> uses -- leading bearing
///         clamped on the first glyph of a line, then spacing plus the previous glyph's trailing
///         bearing before each subsequent one. Anything approximate here shows up immediately as a
///         caret that drifts along a line.
///     </para>
///     <para>
///         Line height is no longer a constant. A line is as tall as the tallest thing on it, which
///         means a line's height depends on where it happened to wrap. Heights are therefore stored
///         per line and positions are accumulated rather than multiplied.
///     </para>
/// </remarks>
public sealed class EditorLayout
{
    /// <summary>
    ///     One character's contribution, already scaled to drawn pixels.
    /// </summary>
    /// <param name="Left">Leading bearing.</param>
    /// <param name="Width">Ink width.</param>
    /// <param name="Right">Trailing bearing.</param>
    /// <param name="Spacing">Extra advance before this glyph when it is not first on its line.</param>
    /// <param name="Ascent">Distance from this glyph's line-box top down to its baseline.</param>
    /// <param name="Descent">Distance from its baseline down to the bottom of its line box.</param>
    public readonly record struct CharMetrics(
        float Left,
        float Width,
        float Right,
        float Spacing,
        float Ascent,
        float Descent);

    /// <summary>
    ///     Supplies metrics for the character at an offset.
    /// </summary>
    /// <remarks>
    ///     Called for offsets from 0 up to and including the text length. The last of those is the
    ///     position after the final character, which has no glyph; a caller answers it with whatever
    ///     newly typed text would be set in, so an empty buffer and an empty trailing line still get
    ///     a line height and a caret of the right size.
    /// </remarks>
    public delegate CharMetrics MetricsProvider(int index);

    /// <summary>A visual line: a half-open range of the source text, and how tall it turned out.</summary>
    /// <param name="HardBreak">True when a newline ended the line rather than the column edge.</param>
    public readonly record struct Line(
        int Start,
        int Length,
        bool HardBreak,
        float Ascent,
        float Descent,
        float Top)
    {
        public int End => Start + Length;

        public float Height => Ascent + Descent;

        public float Bottom => Top + Height;
    }

    private readonly string text;
    private readonly CharMetrics[] metrics;
    private readonly float[] prefix;
    private readonly List<Line> lines = [];

    public EditorLayout(string text, MetricsProvider provider, float maxWidth)
    {
        this.text = text;

        // Sampled once, including the position past the end, so nothing below calls back into the
        // caller mid-wrap and every pass sees the same numbers.
        metrics = new CharMetrics[text.Length + 1];

        for (int i = 0; i <= text.Length; i++) metrics[i] = provider(i);

        prefix = BuildPrefixWidths();
        Wrap(maxWidth);
    }

    public IReadOnlyList<Line> Lines => lines;

    public float Height => lines.Count == 0 ? 0f : lines[^1].Bottom;

    /// <summary>Running width of the first <c>i</c> characters, for every <c>i</c>.</summary>
    private float[] BuildPrefixWidths()
    {
        float[] widths = new float[text.Length + 1];
        float pen = 0f;
        float trailing = 0f;
        bool first = true;

        for (int i = 0; i < text.Length; i++)
        {
            CharMetrics m = metrics[i];
            float left = m.Left;

            if (first)
                left = Math.Max(left, 0f);
            else
                pen += m.Spacing + trailing;

            pen += left + m.Width;
            trailing = m.Right;
            first = false;

            widths[i + 1] = pen + Math.Max(trailing, 0f);
        }

        return widths;
    }

    /// <summary>Greedy word wrap. Breaks mid-word only when a single word cannot fit the column.</summary>
    private void Wrap(float maxWidth)
    {
        lines.Clear();

        int lineStart = 0;
        int lastBreak = -1;
        float top = 0f;

        void Emit(int start, int length, bool hardBreak)
        {
            (float ascent, float descent) = Extent(start, length);

            lines.Add(new Line(start, length, hardBreak, ascent, descent, top));
            top += ascent + descent;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                Emit(lineStart, i - lineStart, true);
                lineStart = i + 1;
                lastBreak = -1;

                continue;
            }

            if (text[i] == ' ') lastBreak = i;

            if (Width(lineStart, i + 1) <= maxWidth || i == lineStart) continue;

            // Break after the last space so the space stays on the line it ended.
            int cut = lastBreak > lineStart ? lastBreak + 1 : i;

            Emit(lineStart, cut - lineStart, false);
            lineStart = cut;
            lastBreak = -1;
            i = cut - 1;
        }

        // Always emit a final line, so an empty buffer still has somewhere to put the caret.
        Emit(lineStart, text.Length - lineStart, true);
    }

    /// <summary>
    ///     How far a line reaches above and below its baseline.
    /// </summary>
    /// <remarks>
    ///     Taken as the maximum of each separately rather than of the total heights. A line holding
    ///     small text with deep descenders and large text with tall ascenders needs room for both
    ///     extremes at once, which the larger of the two totals would not necessarily give.
    /// </remarks>
    private (float Ascent, float Descent) Extent(int start, int length)
    {
        // An empty line still occupies its height, measured at the position the caret would sit.
        if (length == 0)
        {
            CharMetrics at = metrics[Math.Min(start, text.Length)];

            return (at.Ascent, at.Descent);
        }

        float ascent = 0f;
        float descent = 0f;

        for (int i = start; i < start + length; i++)
        {
            ascent = Math.Max(ascent, metrics[i].Ascent);
            descent = Math.Max(descent, metrics[i].Descent);
        }

        return (ascent, descent);
    }

    private float Width(int start, int end) => prefix[end] - prefix[start];

    /// <summary>
    ///     Width of the text between two offsets, without asking which line they belong to.
    /// </summary>
    /// <remarks>
    ///     <see cref="XOf" /> resolves its own line first, which is wrong for a selection highlight:
    ///     the offset at a soft wrap resolves to the line below, so measuring the end of a highlighted
    ///     line through it returns a position on the next line and the band draws backwards. A caller
    ///     that already knows the line measures against it directly.
    /// </remarks>
    public float WidthOfRange(int start, int end) =>
        Width(Math.Clamp(start, 0, text.Length), Math.Clamp(end, 0, text.Length));

    /// <summary>Index of the visual line holding an offset.</summary>
    /// <remarks>
    ///     The two kinds of break resolve the end-of-line caret differently. A soft break shares its
    ///     offset with the line below -- the next line starts at exactly that index -- and the caret
    ///     belongs to the line it wrapped onto. A hard break does not: the newline occupies the offset,
    ///     the next line starts past it, and a caret sitting there is at the end of this line with
    ///     nowhere else to be. Treating both the same puts the caret a whole line away from the click
    ///     every time a paragraph ends.
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
    public (float X, float Y) CaretAt(int index) => (XOf(index), LineAt(LineOf(index)).Top);

    /// <summary>Height of the caret at an offset: the height of the line it sits on.</summary>
    public float CaretHeight(int index) => LineAt(LineOf(index)).Height;

    /// <summary>
    ///     Where a character draws vertically within its line, so mixed sizes share a baseline.
    /// </summary>
    /// <remarks>
    ///     Top-aligning boxes of different heights would hang large text from the same ceiling as
    ///     small text and leave their baselines apart, which reads as one word floating above the
    ///     line. Offsetting by the difference in ascent puts every baseline in the same place.
    /// </remarks>
    public float BaselineOffset(int index)
    {
        Line line = LineAt(LineOf(index));

        return line.Ascent - metrics[Math.Clamp(index, 0, text.Length)].Ascent;
    }

    /// <summary>Nearest caret offset to a point, for click and drag placement.</summary>
    public int IndexAt(float x, float y)
    {
        // Walked rather than divided: lines are no longer a uniform height.
        for (int i = 0; i < lines.Count; i++)
        {
            if (y < lines[i].Bottom || i == lines.Count - 1) return IndexOnLine(i, x);
        }

        return IndexOnLine(lines.Count - 1, x);
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
