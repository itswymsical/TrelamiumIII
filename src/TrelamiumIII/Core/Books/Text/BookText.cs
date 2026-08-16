using System.Collections.Generic;
using System.Text;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.UI.Chat;

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
///     Implemented by snippets a book page can hover: glossary terms, cross-references, anything with
///     something to say when the cursor stops on it.
/// </summary>
/// <remarks>
///     Terraria's own hover mechanism resolves against screen coordinates, which are meaningless
///     inside a page's render target, so book pages hit-test themselves and read the result off this
///     interface instead of calling <see cref="TextSnippet.OnHover" />.
/// </remarks>
public interface IBookInteractiveSnippet
{
    /// <summary>Tooltip to raise, or null.</summary>
    string? TooltipText { get; }

    /// <summary>Book key to open on click, or null.</summary>
    string? LinkTarget { get; }
}

/// <summary>
///     Implemented by snippets that draw their own text but are still ordinary prose underneath, so
///     wrapping may break them at word boundaries.
/// </summary>
/// <remarks>
///     <para>
///         A <see cref="TextSnippet.UniqueDraw" /> snippet is atomic to the line breaker by default,
///         which is right for an icon or a glossary term but wrong for a run of text: a whole phrase
///         set in another face would be measured as one unbreakable word and hang off the page.
///         Implementing this puts the snippet back into the ordinary word-wrapping path.
///     </para>
///     <para>
///         An implementer must also override <see cref="TextSnippet.CopyMorph" /> to return its own
///         type, or the fragments it is split into revert to plain body text.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     Stretches inter-word gaps so both edges line up. Applied to every line except the last of
    ///     a paragraph, which would otherwise be spread across the full column from a few words.
    /// </summary>
    Justify,
}

/// <summary>
///     One laid-out line of book text: the snippets sitting on it, and the extent they occupy.
/// </summary>
public sealed class BookTextLine
{
    public required TextSnippet[] Snippets { get; init; }

    /// <summary>Measured extent at the scale the line was laid out for.</summary>
    public Vector2 Size { get; init; }

    /// <summary>
    ///     True when an explicit break ended this line rather than the column edge. Justification
    ///     skips these, since a hard-broken line is the end of its paragraph.
    /// </summary>
    public bool HardBreak { get; init; }

    /// <summary>A line carrying no snippets, produced by a paragraph token. Drawn as vertical space.</summary>
    public bool IsBlank => Snippets.Length == 0;
}

/// <summary>
///     Lays out rich text for the book renderer: parse to snippets, wrap to a column, then draw or
///     hit-test one line at a time.
/// </summary>
/// <remarks>
///     <para>
///         Text goes through Terraria's snippet parser, so every tag the game knows works inside a
///         book, as do this mod's own (see <see cref="Core.Text.InlineTagSystem" />). That is the
///         reason wrapping is written here rather than deferred to
///         <see cref="ChatManager.DrawColorCodedString(SpriteBatch, DynamicSpriteFont, string, Vector2, Color, float, Vector2, Vector2, float, bool)" />:
///         vanilla's word-wrap measures the raw string, so a line holding an inline icon is measured
///         as if the icon were not there and overruns the column. Walking the snippets and asking each
///         <see cref="TextSnippet.UniqueDraw" /> for its true size is the only way to wrap honestly.
///     </para>
///     <para>
///         Hit-testing is likewise ours. Vanilla resolves hover by comparing against
///         <see cref="Main.MouseScreen" />, but book pages are drawn into a render target in
///         page-local space, where screen coordinates mean nothing. Callers convert the cursor into
///         page space and pass it to <see cref="HitTest" />.
///     </para>
/// </remarks>
public static class BookText
{
    /// <summary>
    ///     Paragraph token. Authored text uses this rather than a bare newline because hjson block
    ///     strings swallow single line breaks, and because it survives translation intact -- a
    ///     translator who breaks their prose differently still gets the paragraph the author meant.
    /// </summary>
    public const string PARAGRAPH_TOKEN = "[p]";

    public static DynamicSpriteFont FontFor(BookFont font) => font switch
    {
        BookFont.Display => FontAssets.DeathText.Value,
        BookFont.Hand    => FontAssets.DeathText.Value,
        _                => FontAssets.MouseText.Value,
    };

    /// <summary>Height of one line of this font at this scale, before any extra leading.</summary>
    public static float LineHeight(BookFont font, float scale) =>
        FontFor(font).MeasureString("Ay").Y * scale;

    /// <summary>
    ///     Shortens text with an ellipsis until it fits, for the places that must stay one line --
    ///     a contents row, a tab label -- where wrapping would break the shape rather than the text.
    /// </summary>
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

    /// <summary>
    ///     The ink bounds of a single glyph: how tall the letter itself is, and how far below the pen
    ///     its top sits.
    /// </summary>
    /// <remarks>
    ///     <see cref="DynamicSpriteFont.MeasureString" /> reports the line box -- ascender, descender
    ///     and leading together -- which is the right measure for stacking lines and the wrong one for
    ///     sizing a letter. A drop cap fitted to the line box comes out around a third shorter than the
    ///     space it was given, sitting low in its own notch. The atlas source rectangle is the letter.
    /// </remarks>
    /// <returns>False when the font has no baked entry for the character; the caller falls back.</returns>
    public static bool TryGetGlyphInk(DynamicSpriteFont font, char character, out float height, out float topBearing)
    {
        if (font.SpriteCharacters.TryGetValue(character, out DynamicSpriteFont.SpriteCharacterData data)
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

    /// <summary>
    ///     Parses and wraps <paramref name="text" /> into lines no wider than <paramref name="maxWidth" />.
    /// </summary>
    /// <param name="leadingIndent">
    ///     Width withheld from the first <paramref name="leadingIndentLines" /> lines. One mechanism
    ///     covers both a first-line indent and the notch a drop cap has to be wrapped around; the
    ///     caller offsets those lines by the same amount when drawing.
    /// </param>
    /// <param name="leadingIndentLines">How many opening lines <paramref name="leadingIndent" /> applies to.</param>
    public static List<BookTextLine> Layout(
        string? text,
        BookFont bookFont,
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

        var builder = new LineBuilder(font, scale);

        // The column narrows for the opening lines when something is set into them, so the limit is
        // read per line rather than captured once.
        float Limit() => lines.Count < leadingIndentLines ? maxWidth - leadingIndent : maxWidth;

        foreach (TextSnippet snippet in snippets)
        {
            // An icon-bearing snippet reports its own size and can never be split. Ask at the
            // combined scale, since a snippet carries its own multiplier on top of the layout's.
            // Snippets that draw text rather than an icon opt out and go through the word path,
            // or a phrase in another face would be one unbreakable word.
            if (snippet is not IWrappableSnippet
                && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale * snippet.Scale))
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
    public static float MeasureHeight(IReadOnlyList<BookTextLine> lines, BookFont font, float scale, float lineSpacing)
    {
        if (lines.Count == 0)
            return 0f;

        return lines.Count * (LineHeight(font, scale) + lineSpacing) - lineSpacing;
    }

    /// <summary>
    ///     Draws one laid-out line with the tooltip drop shadow. <paramref name="position" /> is the
    ///     line's top-left in whatever space the sprite batch is currently in.
    /// </summary>
    public static void DrawLine(
        SpriteBatch spriteBatch,
        BookTextLine line,
        BookFont bookFont,
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
    ///     Puts a run of snippets on the page as ink: no drop shadow, and on a whole pixel.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Vanilla's shadowed draw is built for text floating over the world, and stamps four
    ///         black copies at a fixed two-pixel spread. Book text is set at well under full scale, so
    ///         its strokes are around a pixel wide and that shadow swallows them -- the page comes out
    ///         looking smudged rather than printed. Ink on paper has the page behind it for contrast
    ///         and needs no shadow at all.
    ///     </para>
    ///     <para>
    ///         Snapping matters for the same reason: the glyph atlas is a bitmap, and a line landing
    ///         on a fractional pixel gets resampled by the page target's linear filter into a blur.
    ///     </para>
    /// </remarks>
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

    /// <summary>
    ///     Reused across lines. Page drawing is single-threaded and a piece list never outlives the
    ///     call that built it, so one buffer serves the whole book instead of allocating per line.
    /// </summary>
    private static readonly List<TextPiece> pieceScratch = [];

    /// <summary>
    ///     Whether this line gets justified. The last line of a paragraph never does -- a few words
    ///     stretched across the full column is the classic way to make a page look broken.
    /// </summary>
    /// <remarks>
    ///     Deliberately not gated on the line being narrower than the column. Wrap decisions sum
    ///     per-word widths while <see cref="BookTextLine.Size" /> is one exact measurement of the
    ///     finished line, and the two disagree by the font's inter-character spacing at each word
    ///     boundary. Testing against that difference made full lines opt out and left only the loose
    ///     ones stretched, which read as erratic spacing. The gap clamp below handles it instead.
    /// </remarks>
    private static bool ShouldJustify(BookTextLine line, float columnWidth, TextAlign align) =>
        align == TextAlign.Justify && !line.HardBreak && columnWidth > 0f && line.Snippets.Length > 0;

    /// <summary>
    ///     Breaks a line into the runs justification moves independently, and spaces them to both
    ///     margins.
    /// </summary>
    /// <remarks>
    ///     Wrapping merges a line's words into one snippet per source run, so justifying by snippet
    ///     would find no gaps to widen on an ordinary line. The words have to be recovered here.
    /// </remarks>
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
                && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale * snippet.Scale))
            {
                pieceScratch.Add(new TextPiece(snippet, i, 0f, unique.X));
                natural += unique.X;
                continue;
            }

            foreach (string word in snippet.Text.Split(' '))
            {
                if (word.Length == 0)
                    continue;

                float width = wrappable?.MeasureText(word, scale) ?? font.MeasureString(word).X * scale * snippet.Scale;

                pieceScratch.Add(new TextPiece(snippet.CopyMorph(word), i, 0f, width));
                natural += width;
            }
        }

        int gaps = pieceScratch.Count - 1;

        if (gaps <= 0)
            return pieceScratch;

        float space = font.MeasureString(" ").X * scale;

        // Slack is measured against the words alone, so the gap already includes the space that was
        // taken out. Clamped at both ends: never tighter than a real space, which is what a line
        // measuring slightly over the column would otherwise ask for, and never so loose that a line
        // that wrapped early flies apart.
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

    /// <summary>
    ///     Index of the snippet under <paramref name="local" />, or -1. Coordinates are relative to
    ///     the line's top-left, in the same space it was drawn.
    /// </summary>
    public static int HitTest(
        BookTextLine line,
        BookFont bookFont,
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

        if (snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale * snippet.Scale))
            return unique.X;

        return font.MeasureString(snippet.Text).X * scale * snippet.Scale;
    }

    /// <summary>
    ///     Accumulates snippets onto the line currently being built.
    /// </summary>
    /// <remarks>
    ///     Text arriving from one source snippet can straddle a wrap, so the words waiting to be
    ///     written are held apart from the snippets already committed. Flushing turns whatever is
    ///     pending into a <see cref="TextSnippet.CopyMorph" /> of its source, which is what keeps a
    ///     wrapped run of coloured or tagged text carrying its own colour and scale onto the next line.
    /// </remarks>
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
                : font.MeasureString(word).X * scale * source.Scale;

        public float MeasureSpace(TextSnippet source) =>
            source is IWrappableSnippet wrappable
                ? wrappable.MeasureText(" ", scale)
                : font.MeasureString(" ").X * scale * source.Scale;

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

            // One exact measurement per finished line. Wrap decisions sum per-word widths, which is
            // linear but drops the font's inter-character spacing at word boundaries; re-measuring
            // here keeps centring and justification honest without making layout quadratic.
            float width = 0f;
            float height = 0f;

            foreach (TextSnippet snippet in snippets)
            {
                width += SnippetWidth(snippet, font, scale);

                float h = snippet is not IWrappableSnippet
                          && snippet.UniqueDraw(true, out Vector2 unique, null, default, Color.White, scale * snippet.Scale)
                    ? unique.Y
                    : font.MeasureString(snippet.Text).Y * scale * snippet.Scale;

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
