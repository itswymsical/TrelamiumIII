using System.Collections.Generic;
using Terraria.UI.Chat;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout.Blocks;

/// <summary>A paragraph of rich text; the only block that splits across pages, respecting widow and orphan minimums.</summary>
public sealed class ProseBlock : BookBlock
{
    /// <summary>Fewest lines that may sit alone on either side of a page break.</summary>
    private const int MIN_LINES_PER_SIDE = 2;

    /// <summary>Height of a drop cap, in body lines.</summary>
    private const float DROP_CAP_LINES = 2f;

    /// <summary>Gap between a drop cap and the text set beside it.</summary>
    private const float DROP_CAP_GAP = 3f;

    private readonly string? source;

    /// <summary>False on a continuation, which opens flush; only the paragraph's real start may set a drop cap.</summary>
    private readonly bool opensParagraph;

    private List<BookTextLine>? lines;
    private float layoutWidth = float.NaN;

    public ProseBlock(string? text)
    {
        source = text;
        opensParagraph = true;
    }

    /// <summary>A <see cref="Split" /> piece with pre-wrapped lines; the source is kept only so the head can draw its drop cap.</summary>
    private ProseBlock(string? source, bool opensParagraph, List<BookTextLine> laidOut, float width, BlockStyle style)
    {
        this.source = source;
        this.opensParagraph = opensParagraph;

        lines = laidOut;
        layoutWidth = width;
        Style = style;
    }

    /// <summary>Continuations carry lines but no source, so emptiness checks the lines when present.</summary>
    public override bool IsEmpty =>
        lines is not null ? lines.Count == 0 : string.IsNullOrWhiteSpace(source);

    public override bool Splittable => true;

    /// <summary>The paragraph's opening letter, when it is being set as a drop cap.</summary>
    private char DropCapChar =>
        opensParagraph && Style.DropCap && !string.IsNullOrEmpty(source) && char.IsLetter(source![0])
            ? source[0]
            : '\0';

    private float LineAdvance => BookText.LineHeight(Style.Face, Style.Scale) + Style.LineSpacing;

    protected override float MeasureCore(float width)
    {
        var laid = LinesFor(width);

        if (laid.Count == 0)
            return 0f;

        return laid.Count * LineAdvance - Style.LineSpacing;
    }

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        var laid = LinesFor(inner.Width);

        if (laid.Count == 0)
            return;

        float capWidth = 0f;
        char cap = DropCapChar;

        if (cap != '\0')
        {
            capWidth = DrawDropCap(spriteBatch, cap, inner.TopLeft);
            capWidth += DROP_CAP_GAP;
        }

        int indentLines = LeadingIndentLines(cap);
        float advance = LineAdvance;

        for (int i = 0; i < laid.Count; i++)
        {
            BookTextLine line = laid[i];

            if (line.IsBlank)
                continue;

            float indent = i < indentLines ? capWidth + Style.FirstLineIndent : 0f;
            var position = new Vector2(inner.X + indent, inner.Y + i * advance);

            BookText.DrawLine(
                spriteBatch,
                line,
                Style.Face,
                position,
                Style.Color,
                Style.Scale,
                inner.Width - indent,
                Style.Align);

            TrackHover(line, position, context, inner.Width - indent);
        }
    }

    /// <summary>Reports a hovered snippet so the UI can show a tooltip or follow a cross-reference.</summary>
    private void TrackHover(BookTextLine line, Vector2 position, BookPageContext context, float columnWidth)
    {
        if (context.LocalMouse is not { } mouse)
            return;

        Vector2 local = mouse - position;
        int index = BookText.HitTest(line, Style.Face, local, Style.Scale, columnWidth, Style.Align);

        if (index < 0)
            return;

        TextSnippet snippet = line.Snippets[index];

        if (snippet is IBookInteractiveSnippet interactive)
        {
            context.HoverText ??= interactive.TooltipText;
            context.HoverLink ??= interactive.LinkTarget;
        }
    }



    public override BookBlock? Split(float width, float available, out BookBlock? head)
    {
        head = null;

        float contentWidth = ContentWidth(width);
        var laid = LinesFor(contentWidth);
        float advance = LineAdvance;

        // How many whole lines clear the remaining height. The trailing gap does not have to fit,
        // hence adding it back before dividing.
        int fits = (int)Math.Floor((available + Style.LineSpacing) / advance);

        // A blank paragraph line at the cut is a gap nobody sees; keeping it would push the break up
        // by a line for no reason.
        while (fits > 0 && fits <= laid.Count && laid[fits - 1].IsBlank)
            fits--;

        // Too few lines left would be a widow, so pull one down rather than refusing the split, which would strand the
        // heading above on its own.
        if (laid.Count - fits < MIN_LINES_PER_SIDE)
            fits = laid.Count - MIN_LINES_PER_SIDE;

        // Still no good: the paragraph is too short to divide at all, so it travels whole.
        if (fits < MIN_LINES_PER_SIDE)
            return this;

        // The head is still the paragraph's opening, so it keeps the source and the style and goes on
        // drawing the cap its first lines were wrapped around.
        head = new ProseBlock(source, opensParagraph, laid.GetRange(0, fits), contentWidth, Style);

        var rest = laid.GetRange(fits, laid.Count - fits);

        // A continuation opens flush: no source, no cap, no first-line indent.
        return new ProseBlock(null, false, rest, contentWidth, Style with { FirstLineIndent = 0f, DropCap = false });
    }

    private List<BookTextLine> LinesFor(float width)
    {
        if (lines is not null && Math.Abs(layoutWidth - width) < 0.5f)
            return lines;

        char cap = DropCapChar;

        float leading = Style.FirstLineIndent;
        int leadingLines = Style.FirstLineIndent != 0f ? 1 : 0;

        if (cap != '\0')
        {
            leading = MeasureDropCap(cap) + DROP_CAP_GAP;
            leadingLines = (int)DROP_CAP_LINES;
        }

        // A drop cap eats its own letter: it is drawn separately, so the flowed text starts after it.
        string? body = cap != '\0' ? source![1..].TrimStart() : source;

        lines = BookText.Layout(body, Style.Face, width, Style.Scale, Style.Color, leading, leadingLines);
        layoutWidth = width;

        return lines;
    }

    private int LeadingIndentLines(char cap) =>
        cap != '\0' ? (int)DROP_CAP_LINES : Style.FirstLineIndent != 0f ? 1 : 0;

    /// <summary>Share of the line box a capital fills, for fonts with no baked glyph to measure.</summary>
    private const float FALLBACK_CAP_RATIO = 0.68f;

    /// <summary>Scale that makes the cap's ink exactly <see cref="DROP_CAP_LINES" /> lines tall.</summary>
    private float DropCapScale(char cap)
    {
        var font = BookText.FontFor(Style.FaceFor(BookFont.Display));
        float target = BookText.LineHeight(Style.Face, Style.Scale) * DROP_CAP_LINES;

        float ink = BookText.TryGetGlyphInk(font, cap, out float height, out _)
            ? height
            : font.MeasureString(cap.ToString()).Y * FALLBACK_CAP_RATIO;

        return target / Math.Max(1f, ink);
    }

    private float MeasureDropCap(char cap) =>
        BookText.FontFor(Style.FaceFor(BookFont.Display)).MeasureString(cap.ToString()).X * DropCapScale(cap);

    private float DrawDropCap(SpriteBatch spriteBatch, char cap, Vector2 position)
    {
        var font = BookText.FontFor(Style.FaceFor(BookFont.Display));
        string text = cap.ToString();
        float scale = DropCapScale(cap);

        // Lift by the top bearing so the cap's ink aligns with the top of the neighbouring text.
        float lift = BookText.TryGetGlyphInk(font, cap, out _, out float bearing) ? bearing * scale : 0f;

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            font,
            text,
            position.X,
            position.Y - lift,
            Style.Color,
            Color.Transparent,
            Vector2.Zero,
            scale);

        return font.MeasureString(text).X * scale;
    }
}
