using System.Collections.Generic;

namespace TrelamiumIII.Core.Books.Authoring;

/// <summary>
///     Wraps the buffer and answers caret geometry. Metrics are per index (letters can differ in face and size) and pre-scaled; advances
///     follow <see cref="ReLogic.Graphics.DynamicSpriteFont.MeasureString" /> exactly, and each line has its own height.
/// </summary>
public sealed class EditorLayout
{
    /// <summary>One character's metrics, in drawn pixels.</summary>
    /// <param name="Spacing">Extra advance before this glyph when it isn't first on its line.</param>
    /// <param name="Ascent">Line-box top down to this glyph's baseline.</param>
    /// <param name="Descent">Baseline down to the bottom of its line box.</param>
    public readonly record struct CharMetrics(
        float Left,
        float Width,
        float Right,
        float Spacing,
        float Ascent,
        float Descent);

    /// <summary>
    ///     Metrics for offsets 0 through text length. The last has no glyph, so answer with the format newly typed text would get.
    /// </summary>
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

    /// <summary>Max ascent and max descent taken separately, so mixed sizes on one line fit both extremes.</summary>
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
    ///     Width between two offsets without resolving their line; <see cref="XOf" /> would put a soft-wrap end on the next line.
    /// </summary>
    public float WidthOfRange(int start, int end) =>
        Width(Math.Clamp(start, 0, text.Length), Math.Clamp(end, 0, text.Length));

    /// <summary>Index of the visual line holding an offset.</summary>
    /// <remarks>At a soft break the offset belongs to the line below; at a hard break it stays at the end of this line.</remarks>
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

    /// <summary>Vertical offset within the line, by ascent difference, so mixed sizes share a baseline.</summary>
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

    /// <summary>Caret one visual line up or down, holding <paramref name="desiredX" /> so short lines don't pull the caret left.</summary>
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
