using System.Collections.Generic;
using System.Text;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.UI.Chat;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Core.Books.Text;

/// <summary>Which font a run of book text is set in.</summary>
public enum BookFont
{
    /// <summary>Printed body text. The default for prose.</summary>
    Body,

    /// <summary>Larger display face, for chapter headings and title pages.</summary>
    Display,

    /// <summary>Stands in for handwriting: marginalia, sticky notes, a previous reader's corrections.</summary>
    Hand,
}

/// <summary>
///     A hoverable book snippet (glossary term, cross-reference). Pages hit-test themselves, since vanilla hover uses screen coordinates.
/// </summary>
public interface IBookInteractiveSnippet
{
    /// <summary>Tooltip to raise, or null.</summary>
    string? TooltipText { get; }

    /// <summary>Book key to open on click, or null.</summary>
    string? LinkTarget { get; }
}

/// <summary>
///     A self-drawing snippet the line breaker may split at words. Implementers must also override <see cref="TextSnippet.CopyMorph" />.
/// </summary>
public interface IWrappableSnippet
{
    /// <summary>Width of a run of this snippet's text at the layout scale.</summary>
    float MeasureText(string text, float scale);
}

/// <summary>Horizontal placement of a laid-out line within its column.</summary>
public enum TextAlign
{
    Left,
    Center,
    Right,

    /// <summary>Stretches word gaps to fill the column, except on a paragraph's last line.</summary>
    Justify,
}

/// <summary>One laid-out line: its snippets and their measured extent.</summary>
public sealed class BookTextLine
{
    public required TextSnippet[] Snippets { get; init; }

    /// <summary>Measured extent at the scale the line was laid out for.</summary>
    public Vector2 Size { get; init; }

    /// <summary>True when an explicit break ended the line; justification skips these.</summary>
    public bool HardBreak { get; init; }

    /// <summary>A line carrying no snippets, produced by a paragraph token. Drawn as vertical space.</summary>
    public bool IsBlank => Snippets.Length == 0;
}

/// <summary>
///     Lays out rich text for books: parse snippets, wrap to a column, draw or hit-test per line. Wraps itself because vanilla
///     measures raw strings (ignoring inline icons), and hit-tests itself because pages are drawn in page-local space.
/// </summary>
public static class BookText
{
    public const string PARAGRAPH_TOKEN = "[p]";

    /// <summary>Resolves a role against its typeface (vanilla fonts when none).</summary>
    public static DynamicSpriteFont FontFor(BookFace face) => face.Font;

    /// <summary>Height of one line of this font at this scale, before any extra leading.</summary>
    public static float LineHeight(BookFace face, float scale) =>
        FontFor(face).MeasureString("Ay").Y * scale;

    /// <summary>Shortens text with an ellipsis to fit one line, for contents rows and tab labels.</summary>
    public static string Truncate(DynamicSpriteFont font, string text, float scale, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f || font.MeasureString(text).X * scale <= maxWidth)
            return text;

        const string ellipsis = "...";
        float ellipsisWidth = font.MeasureString(ellipsis).X * scale;

        for (int length = text.Length - 1; length > 0; length--)
        {
            if (font.MeasureString(text[..length]).X * scale + ellipsisWidth <= maxWidth)
                return text[..length].TrimEnd() + ellipsis;
        }

        return ellipsis;
    }

    /// <summary>A glyph's ink height and top bearing from its atlas rect; MeasureString gives the line box, too tall for a drop cap.</summary>
    /// <returns>False when the font has no baked entry for the character.</returns>
    public static bool TryGetGlyphInk(DynamicSpriteFont font, char character, out float height, out float topBearing)
    {
        if (font.TryGetCharacterData(character, out var data)
            && data.Glyph.Height > 0)
        {
            height = data.Glyph.Height;
            topBearing = data.Padding.Y;

            return true;
        }

        height = 0f;
        topBearing = 0f;

        return false;
    }

    /// <summary>Parses and wraps <paramref name="text" /> into lines no wider than <paramref name="maxWidth" />.</summary>
    /// <param name="leadingIndent">Width withheld from the first lines, for a first-line indent or a drop cap's notch.</param>
    /// <param name="leadingIndentLines">How many opening lines <paramref name="leadingIndent" /> applies to.</param>
    public static List<BookTextLine> Layout(
        string? text,
        BookFace bookFont,
        float maxWidth,
        float scale,
        Color baseColor,
        float leadingIndent = 0f,
        int leadingIndentLines = 0)
    {
        var lines = new List<BookTextLine>();

        if (string.IsNullOrEmpty(text))
            return lines;

        var font = FontFor(bookFont);

        // The paragraph token has to go before parsing, not after. ChatManager only recognises
        // tags shaped [name:value], so a bare [p] would survive the parser and be drawn literally.
        string normalized = text.Replace(PARAGRAPH_TOKEN, "\n");

        List<TextSnippet> snippets = ChatManager.ParseMessage(normalized, baseColor);

        // ParseMessage is a static call with no idea which book it is parsing for, so the runs that
        // need to know the surrounding typeface are told here -- the one place that has it.
        Core.Text.FormattedSnippet.Bind(snippets, bookFont);

        var builder = new LineBuilder(font, scale);

        // The column narrows for the opening lines when something is set into them, so the limit is
        // read per line rather than captured once.
        float Limit() => lines.Count < leadingIndentLines ? maxWidth - leadingIndent : maxWidth;

        foreach (TextSnippet snippet in snippets)
        {
            // Icon snippets report their own size and never split (asked at the combined scale); snippets that draw text opt
            // into word wrap instead.
            if (snippet is not IWrappableSnippet
                && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale))
            {
                if (!builder.IsEmpty && builder.Width + unique.X > Limit())
                    lines.Add(builder.Flush(false));

                builder.AddAtomic(snippet, unique.X);
                continue;
            }

            string[] segments = snippet.Text.Split('\n');

            for (int s = 0; s < segments.Length; s++)
            {
                // Every newline closes the current line, including the ones that leave it empty:
                // a blank line is exactly how a paragraph gap renders.
                if (s > 0)
                    lines.Add(builder.Flush(true));

                AppendWords(builder, lines, snippet, segments[s], Limit);
            }
        }

        if (!builder.IsEmpty)
            lines.Add(builder.Flush(true));

        return lines;
    }

    private static void AppendWords(
        LineBuilder builder,
        List<BookTextLine> lines,
        TextSnippet source,
        string segment,
        Func<float> limit)
    {
        if (segment.Length == 0)
            return;

        foreach (string word in segment.Split(' '))
        {
            if (word.Length == 0)
                continue;

            float wordWidth = builder.MeasureWord(source, word);
            float spaceWidth = builder.IsEmpty ? 0f : builder.MeasureSpace(source);

            if (!builder.IsEmpty && builder.Width + spaceWidth + wordWidth > limit())
            {
                lines.Add(builder.Flush(false));
                spaceWidth = 0f;
            }

            builder.AddWord(source, word, spaceWidth, wordWidth);
        }
    }

    /// <summary>Total height of a laid-out block, counting blank paragraph lines and leading.</summary>
    public static float MeasureHeight(IReadOnlyList<BookTextLine> lines, BookFace font, float scale, float lineSpacing)
    {
        if (lines.Count == 0)
            return 0f;

        return lines.Count * (LineHeight(font, scale) + lineSpacing) - lineSpacing;
    }

    /// <summary>Draws one laid-out line; <paramref name="position" /> is its top-left in the batch's current space.</summary>
    public static void DrawLine(
        SpriteBatch spriteBatch,
        BookTextLine line,
        BookFace bookFont,
        Vector2 position,
        Color color,
        float scale,
        float columnWidth = -1f,
        TextAlign align = TextAlign.Left)
    {
        if (line.IsBlank)
            return;

        var font = FontFor(bookFont);

        // Justification has to place each word itself, so it cannot go through the batched draw below.
        if (ShouldJustify(line, columnWidth, align))
        {
            DrawJustified(spriteBatch, line, font, position, color, scale, columnWidth);
            return;
        }

        Vector2 origin = Snap(position + new Vector2(AlignOffset(line, columnWidth, align), 0f));

        DrawSnippets(spriteBatch, font, line.Snippets, origin, color, scale);
    }

    /// <summary>
    ///     Draws snippets as ink: no drop shadow (it swallows thin strokes at book scale), snapped to whole pixels to avoid blur.
    /// </summary>
    private static void DrawSnippets(
        SpriteBatch spriteBatch,
        DynamicSpriteFont font,
        TextSnippet[] snippets,
        Vector2 position,
        Color color,
        float scale) =>

        // maxWidth is -1 on purpose: the line is already wrapped, and letting vanilla wrap it a
        // second time against a different measurement would re-break it mid-line.
        ChatManager.DrawColorCodedString(
            spriteBatch,
            font,
            snippets,
            position,
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale),
            out _,
            -1f);

    private static Vector2 Snap(Vector2 position) =>
        new((float)Math.Round(position.X), (float)Math.Round(position.Y));

    /// <summary>A single positioned run: one word, or one atomic snippet.</summary>
    private readonly record struct TextPiece(TextSnippet Snippet, int SnippetIndex, float X, float Width);

    /// <summary>Reused scratch list; page drawing is single-threaded and pieces never outlive the call.</summary>
    private static readonly List<TextPiece> pieceScratch = [];

    /// <summary>
    ///     Justifies all but a paragraph's last line. Not gated on width, since wrap sums and line measurement differ slightly; the gap clamp handles it.
    /// </summary>
    private static bool ShouldJustify(BookTextLine line, float columnWidth, TextAlign align) =>
        align == TextAlign.Justify && !line.HardBreak && columnWidth > 0f && line.Snippets.Length > 0;

    /// <summary>Recovers a line's words (wrapping merges them per source run) and spaces them to both margins.</summary>
    private static List<TextPiece> BuildJustifiedPieces(
        BookTextLine line,
        DynamicSpriteFont font,
        float scale,
        float columnWidth)
    {
        pieceScratch.Clear();

        float natural = 0f;

        for (int i = 0; i < line.Snippets.Length; i++)
        {
            TextSnippet snippet = line.Snippets[i];
            var wrappable = snippet as IWrappableSnippet;

            if (wrappable is null
                && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale))
            {
                pieceScratch.Add(new TextPiece(snippet, i, 0f, unique.X));
                natural += unique.X;
                continue;
            }

            foreach (string word in snippet.Text.Split(' '))
            {
                if (word.Length == 0)
                    continue;

                float width = wrappable?.MeasureText(word, scale) ?? font.MeasureString(word).X * scale;

                pieceScratch.Add(new TextPiece(snippet.CopyMorph(word), i, 0f, width));
                natural += width;
            }
        }

        int gaps = pieceScratch.Count - 1;

        if (gaps <= 0)
            return pieceScratch;

        float space = font.MeasureString(" ").X * scale;

        // The gap is clamped between one space (for lines measuring slightly over) and 2.5 spaces (so early-wrapped lines
        // don't fly apart).
        float gap = MathHelper.Clamp((columnWidth - natural) / gaps, space, space * 2.5f);

        float x = 0f;

        for (int i = 0; i < pieceScratch.Count; i++)
        {
            TextPiece piece = pieceScratch[i];
            pieceScratch[i] = piece with { X = x };
            x += piece.Width + gap;
        }

        return pieceScratch;
    }

    private static void DrawJustified(
        SpriteBatch spriteBatch,
        BookTextLine line,
        DynamicSpriteFont font,
        Vector2 position,
        Color color,
        float scale,
        float columnWidth)
    {
        var single = new TextSnippet[1];

        foreach (TextPiece piece in BuildJustifiedPieces(line, font, scale, columnWidth))
        {
            single[0] = piece.Snippet;

            DrawSnippets(spriteBatch, font, single, Snap(position + new Vector2(piece.X, 0f)), color, scale);
        }
    }

    /// <summary>Index of the snippet under <paramref name="local" /> (relative to the line's top-left), or -1.</summary>
    public static int HitTest(
        BookTextLine line,
        BookFace bookFont,
        Vector2 local,
        float scale,
        float columnWidth = -1f,
        TextAlign align = TextAlign.Left)
    {
        if (line.IsBlank || local.Y < 0f || local.Y > line.Size.Y)
            return -1;

        var font = FontFor(bookFont);

        // Justified lines are placed word by word, so hit-testing has to walk the same placement or
        // a hovered term would be found at the position it would have had unjustified.
        if (ShouldJustify(line, columnWidth, align))
        {
            foreach (TextPiece piece in BuildJustifiedPieces(line, font, scale, columnWidth))
            {
                if (local.X >= piece.X && local.X <= piece.X + piece.Width)
                    return piece.SnippetIndex;
            }

            return -1;
        }

        float x = AlignOffset(line, columnWidth, align);

        for (int i = 0; i < line.Snippets.Length; i++)
        {
            float w = SnippetWidth(line.Snippets[i], font, scale);

            if (local.X >= x && local.X <= x + w)
                return i;

            x += w;
        }

        return -1;
    }

    private static float AlignOffset(BookTextLine line, float columnWidth, TextAlign align)
    {
        if (columnWidth <= 0f)
            return 0f;

        return align switch
        {
            TextAlign.Center => (columnWidth - line.Size.X) * 0.5f,
            TextAlign.Right  => columnWidth - line.Size.X,
            _                => 0f,
        };
    }

    internal static float SnippetWidth(TextSnippet snippet, DynamicSpriteFont font, float scale)
    {
        if (snippet is IWrappableSnippet wrappable)
            return wrappable.MeasureText(snippet.Text, scale);

        if (snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale))
            return unique.X;

        return font.MeasureString(snippet.Text).X * scale;
    }

    /// <summary>
    ///     Builds the current line. Pending words flush as a <see cref="TextSnippet.CopyMorph" /> of their source, keeping colour and scale across wraps.
    /// </summary>
    private sealed class LineBuilder(DynamicSpriteFont font, float scale)
    {
        private readonly List<TextSnippet> committed = [];
        private readonly StringBuilder pending = new();
        private TextSnippet? pendingSource;

        public float Width { get; private set; }

        public bool IsEmpty => committed.Count == 0 && pending.Length == 0;

        // A snippet that sets its own text measures its own words: the body font would give the
        // wrong width for a run in another face, and the line would break in the wrong place.
        public float MeasureWord(TextSnippet source, string word) =>
            source is IWrappableSnippet wrappable
                ? wrappable.MeasureText(word, scale)
                : font.MeasureString(word).X * scale;

        public float MeasureSpace(TextSnippet source) =>
            source is IWrappableSnippet wrappable
                ? wrappable.MeasureText(" ", scale)
                : font.MeasureString(" ").X * scale;

        public void AddAtomic(TextSnippet snippet, float width)
        {
            CommitPending();
            committed.Add(snippet);
            Width += width;
        }

        public void AddWord(TextSnippet source, string word, float spaceWidth, float wordWidth)
        {
            // A change of source means a change of colour, scale or tag, so the run so far has to
            // be sealed before the new word joins.
            if (pendingSource is not null && !ReferenceEquals(pendingSource, source))
                CommitPending();

            pendingSource = source;

            if (spaceWidth > 0f)
                pending.Append(' ');

            pending.Append(word);
            Width += spaceWidth + wordWidth;
        }

        public BookTextLine Flush(bool hardBreak)
        {
            CommitPending();

            var snippets = committed.ToArray();

            // One exact measurement per finished line: per-word sums drop inter-character spacing at word boundaries.
            float width = 0f;
            float height = 0f;

            foreach (TextSnippet snippet in snippets)
            {
                width += SnippetWidth(snippet, font, scale);

                float h = snippet is not IWrappableSnippet
                          && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale)
                    ? unique.Y
                    : font.MeasureString(snippet.Text).Y * scale;

                height = Math.Max(height, h);
            }

            if (snippets.Length == 0)
                height = font.MeasureString("Ay").Y * scale;

            var line = new BookTextLine
            {
                Snippets = snippets,
                Size = new Vector2(width, height),
                HardBreak = hardBreak,
            };

            committed.Clear();
            Width = 0f;

            return line;
        }

        private void CommitPending()
        {
            if (pendingSource is null || pending.Length == 0)
            {
                pending.Clear();
                pendingSource = null;
                return;
            }

            committed.Add(pendingSource.CopyMorph(pending.ToString()));
            pending.Clear();
            pendingSource = null;
        }
    }
}
