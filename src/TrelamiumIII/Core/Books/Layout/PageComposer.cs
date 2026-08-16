using System.Collections.Generic;
using TrelamiumIII.Core.Books.Layout.Blocks;

namespace TrelamiumIII.Core.Books.Layout;

/// <summary>Page size and margins the flow is measured against.</summary>
public readonly record struct PageMetrics(int Width, int Height, float MarginX, float MarginTop, float MarginBottom)
{
    public float ContentWidth => Width - MarginX * 2f;

    public float ContentTop => MarginTop;

    public float ContentBottom => Height - MarginBottom;

    public float ContentHeight => ContentBottom - ContentTop;
}

/// <summary>A block placed at a definite spot on a definite page.</summary>
/// <param name="SourceIndex">
///     Position in the book's block list. Kept so a block moved to another page after the fact can
///     have its anchor corrected.
/// </param>
public readonly record struct PlacedBlock(BookBlock Block, BlockRect Rect, int SourceIndex);

/// <summary>One composed page: what sits in the flow, and what lies over it.</summary>
public sealed class ComposedPage
{
    public List<PlacedBlock> Blocks { get; } = [];

    public List<PlacedDecoration> Decorations { get; } = [];
}

/// <summary>A whole book after flowing: its pages, and where each source block ended up.</summary>
public sealed class ComposedBook
{
    public required List<ComposedPage> Pages { get; init; }

    /// <summary>
    ///     Page each source block first landed on, indexed by its position in the block list, or -1
    ///     if it never placed. A block split across a break reports the page its head is on.
    /// </summary>
    public required int[] BlockPages { get; init; }
}

/// <summary>
///     Flows a book's blocks onto pages.
/// </summary>
/// <remarks>
///     <para>
///         Page count is an output, never an input. Nothing an author writes says which page anything
///         lands on, so a longer translation grows the book instead of overrunning it.
///     </para>
///     <para>
///         The pass is a single greedy walk: take the next block, ask whether it fits in what is left
///         of the page, and either place it, break it, or start a new page. Blocks are measured
///         against the column width and cache that measurement, so the repeated asks are cheap.
///     </para>
/// </remarks>
public static class PageComposer
{
    /// <summary>
    ///     Guard against a block that reports a height no page could hold. Placing it anyway and
    ///     letting it clip is the lesser evil: an infinite loop of empty pages is not recoverable, and
    ///     a clipped figure is visible enough to get reported.
    /// </summary>
    private const int MAX_PAGES = 512;

    public static ComposedBook Compose(
        IReadOnlyList<BookBlock> blocks,
        IReadOnlyList<BookDecoration> decorations,
        in PageMetrics metrics)
    {
        var pages = new List<ComposedPage> { new() };

        // Where each source block first landed, so decorations anchored to it can find it. A block
        // split across a break records its head.
        var anchors = new (int Page, BlockRect Rect)?[blocks.Count];

        float width = metrics.ContentWidth;
        float y = metrics.ContentTop;
        int page = 0;

        // Scratch for headings being moved onto the following page. Reused across the whole walk.
        var carried = new List<PlacedBlock>();

        for (int i = 0; i < blocks.Count; i++)
        {
            BookBlock block = blocks[i];

            if (block is PageBreakBlock)
            {
                if (pages[page].Blocks.Count > 0 && pages.Count < MAX_PAGES)
                {
                    pages.Add(new ComposedPage());
                    page++;
                    y = metrics.ContentTop;
                }

                continue;
            }

            // A block with no content is skipped outright rather than placed at zero height: it must
            // not contribute its spacing, and it must not draw a frame around nothing.
            if (block.IsEmpty)
                continue;

            bool atPageTop = pages[page].Blocks.Count == 0;

            // Space above collapses at the top of a page; a paragraph should not start lower just
            // because the break happened to fall in front of it.
            if (!atPageTop)
                y += block.Style.SpaceBefore;

            while (true)
            {
                float remaining = metrics.ContentBottom - y;
                float height = block.Measure(width);

                if (height <= remaining)
                {
                    Place(pages[page], anchors, blocks, block, i, page, new BlockRect(metrics.MarginX, y, width, height));
                    y += height + block.Style.SpaceAfter;
                    break;
                }

                if (block.Splittable && remaining > 0f)
                {
                    BookBlock? tail = block.Split(width, remaining, out BookBlock? head);

                    if (head is not null)
                    {
                        float headHeight = head.Measure(width);
                        Place(pages[page], anchors, blocks, head, i, page, new BlockRect(metrics.MarginX, y, width, headHeight));

                        if (tail is null)
                        {
                            y += headHeight + block.Style.SpaceAfter;
                            break;
                        }

                        block = tail;
                        if (!NextPage(pages, ref page, ref y, metrics))
                            break;

                        continue;
                    }
                }

                // Nothing fit. Move to a fresh page and try once more; if it does not fit an empty
                // page either, place it and let it clip rather than looping.
                if (pages[page].Blocks.Count == 0)
                {
                    Place(pages[page], anchors, blocks, block, i, page, new BlockRect(metrics.MarginX, y, width, height));
                    y += height + block.Style.SpaceAfter;

                    TrelamiumIII.Instance?.Logger?.Warn(
                        $"Book block {block.GetType().Name} measures {height:F0}px against a {metrics.ContentHeight:F0}px page and will be clipped.");

                    break;
                }

                // Any heading directly above has to travel with this block, or it is left introducing
                // a page that does not contain what it introduces.
                CarryKeepWithNext(pages[page].Blocks, carried);

                if (!NextPage(pages, ref page, ref y, metrics))
                    break;

                ReplaceCarried(pages[page], page, carried, anchors, metrics, width, ref y);
            }
        }

        PlaceDecorations(pages, decorations, anchors, metrics);

        var blockPages = new int[blocks.Count];

        for (int i = 0; i < blocks.Count; i++)
            blockPages[i] = anchors[i]?.Page ?? -1;

        return new ComposedBook { Pages = pages, BlockPages = blockPages };
    }

    private static void Place(
        ComposedPage page,
        (int Page, BlockRect Rect)?[] anchors,
        IReadOnlyList<BookBlock> blocks,
        BookBlock block,
        int sourceIndex,
        int pageIndex,
        in BlockRect rect)
    {
        page.Blocks.Add(new PlacedBlock(block, rect, sourceIndex));

        anchors[sourceIndex] ??= (pageIndex, rect);
    }

    /// <summary>
    ///     Lifts the run of keep-with-next blocks off the end of a page so they can follow the block
    ///     that is being pushed past them.
    /// </summary>
    /// <remarks>
    ///     Does nothing when the run is the whole page. Moving it then would only reproduce the same
    ///     arrangement one page later while leaving a blank page behind -- and, since the block being
    ///     pushed still would not fit, would do it again on every page after that.
    /// </remarks>
    private static void CarryKeepWithNext(List<PlacedBlock> placed, List<PlacedBlock> carried)
    {
        carried.Clear();

        int start = placed.Count;

        while (start > 0 && placed[start - 1].Block.KeepWithNext)
            start--;

        if (start == 0 || start == placed.Count)
            return;

        for (int i = start; i < placed.Count; i++)
            carried.Add(placed[i]);

        placed.RemoveRange(start, placed.Count - start);
    }

    /// <summary>Re-lays carried blocks at the top of the page they were moved to.</summary>
    private static void ReplaceCarried(
        ComposedPage page,
        int pageIndex,
        List<PlacedBlock> carried,
        (int Page, BlockRect Rect)?[] anchors,
        in PageMetrics metrics,
        float width,
        ref float y)
    {
        foreach (PlacedBlock moved in carried)
        {
            var rect = new BlockRect(metrics.MarginX, y, width, moved.Rect.Height);

            page.Blocks.Add(moved with { Rect = rect });

            // Assigned, not coalesced as in Place: this block's anchor still points at the page it
            // just left, and a decoration pinned to it has to come along too.
            anchors[moved.SourceIndex] = (pageIndex, rect);

            y += moved.Rect.Height + moved.Block.Style.SpaceAfter;
        }

        carried.Clear();
    }

    private static bool NextPage(List<ComposedPage> pages, ref int page, ref float y, in PageMetrics metrics)
    {
        if (pages.Count >= MAX_PAGES)
        {
            TrelamiumIII.Instance?.Logger?.Warn($"Book exceeded {MAX_PAGES} pages; the rest was dropped.");
            return false;
        }

        pages.Add(new ComposedPage());
        page++;
        y = metrics.ContentTop;

        return true;
    }

    private static void PlaceDecorations(
        List<ComposedPage> pages,
        IReadOnlyList<BookDecoration> decorations,
        (int Page, BlockRect Rect)?[] anchors,
        in PageMetrics metrics)
    {
        foreach (BookDecoration decoration in decorations)
        {
            // An unwritten sticky note is no note at all; drawing it would leave blank paper stuck to
            // the page.
            if (decoration.Content.IsEmpty)
                continue;

            int page;
            BlockRect anchor;

            if (decoration.AnchorBlockIndex >= 0 && decoration.AnchorBlockIndex < anchors.Length)
            {
                if (anchors[decoration.AnchorBlockIndex] is not { } placed)
                    continue;

                page = placed.Page;
                anchor = placed.Rect;
            }
            else
            {
                page = decoration.AnchorPageIndex;
                anchor = new BlockRect(metrics.MarginX, metrics.ContentTop, metrics.ContentWidth, metrics.ContentHeight);
            }

            if (page < 0 || page >= pages.Count)
                continue;

            float height = decoration.Content.Measure(decoration.Width);
            Vector2 origin = AnchorPoint(decoration, anchor, metrics, decoration.Width, height);

            pages[page].Decorations.Add(new PlacedDecoration(decoration, origin + decoration.Offset, height));
        }
    }

    private static Vector2 AnchorPoint(
        BookDecoration decoration,
        in BlockRect anchor,
        in PageMetrics metrics,
        float width,
        float height) => decoration.Anchor switch
        {
            DecorationAnchor.BlockTopLeft     => new Vector2(anchor.X, anchor.Y),
            DecorationAnchor.BlockTopRight    => new Vector2(anchor.Right - width, anchor.Y),
            DecorationAnchor.BlockBottomLeft  => new Vector2(anchor.X, anchor.Bottom - height),
            DecorationAnchor.BlockBottomRight => new Vector2(anchor.Right - width, anchor.Bottom - height),

            DecorationAnchor.PageTopLeft     => new Vector2(0f, 0f),
            DecorationAnchor.PageTopRight    => new Vector2(metrics.Width - width, 0f),
            DecorationAnchor.PageBottomLeft  => new Vector2(0f, metrics.Height - height),
            DecorationAnchor.PageBottomRight => new Vector2(metrics.Width - width, metrics.Height - height),
            DecorationAnchor.PageCenter      => new Vector2((metrics.Width - width) * 0.5f, (metrics.Height - height) * 0.5f),

            _ => new Vector2(anchor.X, anchor.Y),
        };
}
