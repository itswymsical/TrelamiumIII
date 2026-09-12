using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout.Blocks;

/// <summary>One line of a table of contents.</summary>
public sealed class ContentsEntry
{
    public required string Title { get; init; }

    /// <summary>Index of the heading block this entry points at, used to resolve <see cref="Page" />.</summary>
    public required int BlockIndex { get; init; }

    /// <summary>Nesting level. 0 is a chapter, 1 a section under it, and so on.</summary>
    public int Depth { get; init; }

    /// <summary>Page the heading landed on. Filled in after the book is composed; -1 until then.</summary>
    public int Page { get; set; } = -1;
}

/// <summary>
///     A book's chapter list, shared by reference: the block sizes itself from the entry count, and page numbers are filled in after composing.
/// </summary>
public sealed class BookContents
{
    public List<ContentsEntry> Entries { get; } = [];

    /// <summary>The chapter a page falls in: the last one that starts at or before it.</summary>
    public ContentsEntry? ChapterAt(int page)
    {
        ContentsEntry? found = null;

        foreach (ContentsEntry entry in Entries)
        {
            if (entry.Page >= 0 && entry.Page <= page && entry.Depth == 0)
                found = entry;
        }

        return found;
    }
}

/// <summary>A table of contents: titles, dot leaders and page numbers, each row a jump.</summary>
public sealed class ContentsBlock : BookBlock
{
    /// <summary>Breathing room between contents rows, on top of the line itself.</summary>
    private const float ROW_PADDING = 5f;

    private const float HEADER_GAP = 10f;
    private const float DEPTH_INDENT = 14f;

    /// <summary>Spacing of the leader dots between a title and its page number.</summary>
    private const float LEADER_STEP = 5f;

    private readonly BookContents contents;
    private readonly string? header;
    private readonly int firstRow;
    private readonly int rowLimit;

    public ContentsBlock(BookContents contents, string? header = null)
    {
        this.contents = contents;
        this.header = header;

        firstRow = 0;
        rowLimit = int.MaxValue;
    }

    private ContentsBlock(BookContents contents, string? header, int firstRow, int rowLimit, BlockStyle style)
    {
        this.contents = contents;
        this.header = header;
        this.firstRow = firstRow;
        this.rowLimit = rowLimit;

        Style = style;
    }

    /// <summary>Style the page numbers and leaders are drawn in.</summary>
    public BlockStyle NumberStyle { get; init; } = BlockStyle.Body with { Scale = 0.7f };

    /// <summary>A book with no chapters gets no contents, not a bare header over nothing.</summary>
    public override bool IsEmpty => RowCount == 0;

    public override bool Splittable => true;

    private int RowCount => Math.Min(rowLimit, Math.Max(0, contents.Entries.Count - firstRow));

    private float HeaderHeight =>
        string.IsNullOrEmpty(header) ? 0f : BookText.LineHeight(Style.FaceFor(BookFont.Display), Style.Scale) + HEADER_GAP;

    /// <summary>Derived from the style, so restyling can't make rows shorter than their text.</summary>
    private float RowHeight => BookText.LineHeight(Style.Face, Style.Scale) + ROW_PADDING;

    protected override float MeasureCore(float width) => HeaderHeight + RowCount * RowHeight;

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        float y = inner.Y;

        if (!string.IsNullOrEmpty(header))
        {
            Utils.DrawBorderStringFourWay(
                spriteBatch,
                BookText.FontFor(Style.FaceFor(BookFont.Display)),
                header,
                inner.X,
                y,
                Style.Color,
                Color.Transparent,
                Vector2.Zero,
                Style.Scale);

            y += HeaderHeight;
        }

        float rowHeight = RowHeight;

        for (int i = 0; i < RowCount; i++)
        {
            DrawRow(spriteBatch, contents.Entries[firstRow + i], inner, y + i * rowHeight, context);
        }
    }

    private void DrawRow(SpriteBatch spriteBatch, ContentsEntry entry, in BlockRect inner, float y, BookPageContext context)
    {
        float rowHeight = RowHeight;
        var row = new BlockRect(inner.X, y, inner.Width, rowHeight);
        bool hovered = context.IsHovering(row);

        if (hovered)
        {
            spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                new Rectangle((int)row.X - 3, (int)row.Y - 2, (int)row.Width + 6, (int)rowHeight - 1),
                new Color(150, 110, 60, 55));

            // An unresolved row would jump to nowhere; only offer the ones that landed.
            if (entry.Page >= 0)
                context.HoverPage ??= entry.Page;
        }

        var font = BookText.FontFor(Style.Face);
        Color color = hovered ? Style.Color : Style.Color * 0.88f;

        float indent = entry.Depth * DEPTH_INDENT;
        float titleX = row.X + indent;

        string number = entry.Page >= 0 ? (entry.Page + 1).ToString() : string.Empty;

        var numberFont = BookText.FontFor(NumberStyle.Face);
        float numberWidth = numberFont.MeasureString(number).X * NumberStyle.Scale;
        float numberX = row.Right - numberWidth;

        // Titles are elided rather than wrapped: a contents row that runs to two lines stops
        // scanning as a column of destinations.
        string title = BookText.Truncate(font, entry.Title, Style.Scale, numberX - titleX - LEADER_STEP * 3f);
        float titleWidth = font.MeasureString(title).X * Style.Scale;

        Utils.DrawBorderStringFourWay(spriteBatch, font, title, titleX, row.Y, color, Color.Transparent, Vector2.Zero, Style.Scale);

        if (number.Length > 0)
        {
            Utils.DrawBorderStringFourWay(
                spriteBatch, numberFont, number, numberX, row.Y, color, Color.Transparent, Vector2.Zero, NumberStyle.Scale);

            DrawLeader(
                spriteBatch,
                titleX + titleWidth + LEADER_STEP,
                numberX - LEADER_STEP,
                row.Y + BookText.LineHeight(Style.Face, Style.Scale) * 0.62f,
                color);
        }
    }

    private static void DrawLeader(SpriteBatch spriteBatch, float from, float to, float y, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        for (float x = from; x < to; x += LEADER_STEP)
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 1, 1), color * 0.6f);
    }

    public override BookBlock? Split(float width, float available, out BookBlock? head)
    {
        head = null;

        float forRows = available - HeaderHeight;
        int fits = (int)Math.Floor(forRows / RowHeight);

        // One row alone under a header is not worth a page; push the whole table instead.
        if (fits < 2 || fits >= RowCount)
            return this;

        head = new ContentsBlock(contents, header, firstRow, fits, Style) { NumberStyle = NumberStyle };

        return new ContentsBlock(contents, null, firstRow + fits, rowLimit == int.MaxValue ? int.MaxValue : rowLimit - fits, Style)
        {
            NumberStyle = NumberStyle,
        };
    }
}
