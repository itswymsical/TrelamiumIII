using System.Collections.Generic;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Layout.Blocks;
using TrelamiumIII.Core.Books.Text;
using TrelamiumIII.Core.UI.AlchemyAlmanac;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>
///     A lore book on screen. Content is flowed once on open, in the current language; the cover faces page one, then two pages per spread.
/// </summary>
public sealed class BasicBookUI : BookUI
{
    public const int PAGE_WIDTH = 290;
    public const int PAGE_HEIGHT = 430;

    /// <summary>Magnifies a lore spread to the on-screen height of the Alchemist's Almanac.</summary>
    public const float READING_SCALE = AlchemyAlmanacUI.PAGE_HEIGHT * AlchemyAlmanacUI.UI_SCALE / PAGE_HEIGHT;

    private const float MARGIN_X = 24f;
    private const float MARGIN_TOP = 26f;
    private const float MARGIN_BOTTOM = 30f;

    /// <summary>Only one book is readable at a time; a second would fight for the cursor.</summary>
    public static BasicBookUI? Current { get; private set; }

    private readonly BookContent content;
    private readonly List<ComposedPage> pages;

    /// <summary>Page the table of contents sits on, or -1 when the book has none.</summary>
    private readonly int contentsPage;

    private readonly List<BookTab> tabs = [];
    private bool tabsDirty = true;

    public string Key { get; }

    public override int PageWidth => PAGE_WIDTH;

    public override int PageHeight => PAGE_HEIGHT;

    protected override float RenderScale => READING_SCALE;

    private readonly BookPalette palette;

    public override BookPalette BookPalette => palette;

    private BasicBookUI(BookDefinition definition)
    {
        Key = definition.Key;
        palette = definition.Palette;
        content = definition.Build();

        var metrics = new PageMetrics(PAGE_WIDTH, PAGE_HEIGHT, MARGIN_X, MARGIN_TOP, MARGIN_BOTTOM);
        ComposedBook composed = PageComposer.Compose(content.Blocks, content.Decorations, metrics);

        pages = composed.Pages;

        // The contents block measured itself from the chapter count and was flowed like any other
        // block. Only now, with everything placed, can the numbers it prints be filled in.
        foreach (ContentsEntry entry in content.Contents.Entries)
        {
            if (entry.BlockIndex >= 0 && entry.BlockIndex < composed.BlockPages.Length)
                entry.Page = composed.BlockPages[entry.BlockIndex];
        }

        int contentsBlock = content.Blocks.FindIndex(block => block is ContentsBlock);
        contentsPage = contentsBlock >= 0 ? composed.BlockPages[contentsBlock] : -1;
    }

    /// <summary>Opens a registered lore book. Does nothing when one is already open.</summary>
    public static void Open(string key)
    {
        if (Current is not null)
            return;

        BookDefinition? definition = LoreBookRegistry.Get(key);

        if (definition is null)
        {
            TrelamiumIII.Instance?.Logger?.Warn($"No lore book registered under '{key}'.");
            return;
        }

        var book = new BasicBookUI(definition);
        Current = book;
        book.BeginShowing();
    }

    /// <summary>Spread 0 is the cover plus page one; every later spread holds two pages.</summary>
    protected override int SpreadCount =>
        pages.Count <= 1 ? 1 : 1 + (pages.Count - 1 + 1) / 2;

    protected override bool SpreadShowsCover(int spread, BookSide side) =>
        spread == 0 && side == BookSide.Left;

    /// <summary>Page shown in a given slot, or -1 for a blank leaf.</summary>
    private int PageIndexFor(int spread, BookSide side)
    {
        if (spread == 0)
            return side == BookSide.Left ? -1 : 0;

        int index = side == BookSide.Left ? spread * 2 - 1 : spread * 2;

        return index < pages.Count ? index : -1;
    }

    /// <summary>The spread a page is shown on. Inverse of <see cref="PageIndexFor" />.</summary>
    private static int SpreadOf(int page) => page <= 0 ? 0 : (page + 1) / 2;

    /// <summary>The page a bookmark set right now would point at: the first one on this spread.</summary>
    private int CurrentPage
    {
        get
        {
            int left = PageIndexFor(CurrentSpread, BookSide.Left);

            return left >= 0 ? left : PageIndexFor(CurrentSpread, BookSide.Right);
        }
    }

    protected override void OnPageActivated(int page) => JumpTo(SpreadOf(page));

    #region Tabs

    /// <summary>Slot 0 is the contents, when the book has one; the rest are the reader's own marks.</summary>
    private int BookmarkSlotOffset => contentsPage >= 0 ? 1 : 0;

    private IReadOnlyList<int> Bookmarks =>
        LoreBookmarkPlayer.For(Main.LocalPlayer)?.Get(Key) ?? [];

    protected override IReadOnlyList<BookTab> Tabs
    {
        get
        {
            if (tabsDirty)
                RebuildTabs();

            return tabs;
        }
    }

    private void RebuildTabs()
    {
        tabs.Clear();
        tabsDirty = false;

        if (contentsPage >= 0)
        {
            tabs.Add(new BookTab(
                Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("LoreUI.ContentsTab")),
                true,
                palette.Tab,
                Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("LoreUI.ContentsTooltip"))));
        }

        IReadOnlyList<int> marks = Bookmarks;

        for (int slot = 0; slot < LoreBookmarkPlayer.MAX_BOOKMARKS; slot++)
        {
            if (slot < marks.Count)
            {
                int page = marks[slot];

                tabs.Add(new BookTab(
                    BookmarkLabel(page),
                    true,
                    RibbonColors[slot % RibbonColors.Length],
                    BookmarkTooltip(page)));
            }
            else
            {
                tabs.Add(new BookTab(
                    string.Empty,
                    false,
                    Color.Transparent,
                    Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("LoreUI.BookmarkEmpty"))));
            }
        }
    }

    private static readonly Color[] RibbonColors =
    [
        new(142, 52, 46),
        new(58, 92, 116),
        new(96, 106, 52),
    ];

    /// <summary>Names a bookmark by the chapter it falls in.</summary>
    private string BookmarkLabel(int page)
    {
        ContentsEntry? chapter = content.Contents.ChapterAt(page);

        return chapter is not null ? chapter.Title : PageLabel(page);
    }

    /// <summary>The full chapter name on hover, since a tab only fits a fragment.</summary>
    private string BookmarkTooltip(int page)
    {
        ContentsEntry? chapter = content.Contents.ChapterAt(page);

        return chapter is not null ? $"{chapter.Title}  ·  {PageLabel(page)}" : PageLabel(page);
    }

    private static string PageLabel(int page) =>
        Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey("LoreUI.BookmarkPage"), page + 1);

    protected override void OnTabActivated(int index, bool secondary)
    {
        if (contentsPage >= 0 && index == 0)
        {
            if (!secondary)
                JumpTo(SpreadOf(contentsPage));

            return;
        }

        int slot = index - BookmarkSlotOffset;
        LoreBookmarkPlayer? player = LoreBookmarkPlayer.For(Main.LocalPlayer);

        if (player is null || slot < 0)
            return;

        IReadOnlyList<int> marks = player.Get(Key);
        bool filled = slot < marks.Count;

        if (secondary)
        {
            if (filled && player.RemoveAt(Key, slot))
                tabsDirty = true;

            return;
        }

        if (filled)
        {
            JumpTo(SpreadOf(marks[slot]));
            return;
        }

        // Empty slots always take the current page, whichever slot was clicked: bookmarks are a
        // short ordered list, not three independently addressable registers.
        if (player.Add(Key, CurrentPage))
        {
            tabsDirty = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    #endregion

    protected override void DrawSpreadSide(SpriteBatch spriteBatch, int spread, BookSide side, BookPageContext context)
    {
        PagePaper.Draw(spriteBatch, PAGE_WIDTH, PAGE_HEIGHT, side, BookPalette);

        int pageIndex = PageIndexFor(spread, side);

        if (pageIndex < 0 || pageIndex >= pages.Count)
            return;

        ComposedPage page = pages[pageIndex];

        foreach (PlacedBlock placed in page.Blocks)
            placed.Block.Draw(spriteBatch, placed.Rect, context);

        // Decorations sit over the flow, so they are drawn after everything set into the page.
        foreach (PlacedDecoration placed in page.Decorations)
            DrawDecoration(spriteBatch, placed, context);

        DrawFolio(spriteBatch, pageIndex, side);
    }

    private static void DrawDecoration(SpriteBatch spriteBatch, in PlacedDecoration placed, BookPageContext context)
    {
        BookDecoration decoration = placed.Decoration;
        var rect = new BlockRect(placed.Position.X, placed.Position.Y, decoration.Width, placed.Height);

        if (decoration.Rotation == 0f)
        {
            decoration.Content.Draw(spriteBatch, rect, context);
            return;
        }

        // Rotation goes on the batch rather than into the blocks, so any block can be tilted without
        // knowing it is being tilted.
        Matrix transform =
            Matrix.CreateTranslation(-decoration.Width * 0.5f, -placed.Height * 0.5f, 0f)
            * Matrix.CreateRotationZ(decoration.Rotation)
            * Matrix.CreateTranslation(
                placed.Position.X + decoration.Width * 0.5f,
                placed.Position.Y + placed.Height * 0.5f,
                0f);

        spriteBatch.End();
        BookBatch.BeginPage(spriteBatch, SamplerState.LinearClamp, transform);

        // The cursor has to come along through the same transform, or hover on a tilted note would
        // be tested against where it would have been sitting straight.
        Vector2? savedMouse = context.LocalMouse;
        Matrix savedTransform = context.Transform;

        if (savedMouse is { } mouse)
            context.LocalMouse = Vector2.Transform(mouse, Matrix.Invert(transform));

        context.Transform = transform;

        decoration.Content.Draw(spriteBatch, new BlockRect(0f, 0f, decoration.Width, placed.Height), context);

        context.LocalMouse = savedMouse;
        context.Transform = savedTransform;

        spriteBatch.End();
        BookBatch.BeginPage(spriteBatch);
    }

    /// <summary>The page number, set in the outer bottom corner.</summary>
    private void DrawFolio(SpriteBatch spriteBatch, int pageIndex, BookSide side)
    {
        string folio = (pageIndex + 1).ToString();
        const float scale = 0.62f;

        Vector2 size = BookText.FontFor(BookFont.Body).MeasureString(folio) * scale;

        float x = side == BookSide.Left ? MARGIN_X : PAGE_WIDTH - MARGIN_X - size.X;
        float y = PAGE_HEIGHT - MARGIN_BOTTOM + 8f;

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            BookText.FontFor(BookFont.Body),
            folio,
            x,
            y,
            palette.FolioInk,
            Color.Transparent,
            Vector2.Zero,
            scale);
    }

    protected override void DrawCoverFace(SpriteBatch spriteBatch)
    {
        // The cover target is transparent where nothing is drawn, which lets the bound cover behind
        // the spread show through as the board itself.
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, PAGE_WIDTH, PAGE_HEIGHT), palette.Cover);

        Color gold = palette.CoverInk;
        var frame = new Rectangle(18, 22, PAGE_WIDTH - 36, PAGE_HEIGHT - 44);

        if (palette.CoverArt is { } art)
            spriteBatch.Draw(ModContent.Request<Texture2D>(art, AssetRequestMode.ImmediateLoad).Value, frame, Color.White);

        Layout.Blocks.ImageBlock.DrawBorder(spriteBatch, frame, gold * 0.8f);

        List<BookTextLine> lines = BookText.Layout(
            content.Title,
            BookFont.Display,
            frame.Width - 24f,
            0.62f,
            gold);

        float advance = BookText.LineHeight(BookFont.Display, 0.62f) + 4f;
        float y = PAGE_HEIGHT * 0.36f;

        foreach (BookTextLine line in lines)
        {
            BookText.DrawLine(spriteBatch, line, BookFont.Display, new Vector2(frame.X + 12f, y), gold, 0.62f, frame.Width - 24f, TextAlign.Center);
            y += advance;
        }
    }

    protected override void OnLinkActivated(string target)
    {
        if (!LoreBookRegistry.Has(target) || target == Key)
            return;

        // The book has to finish closing before the next one can claim the slot, so the jump is
        // queued and picked up in OnClosed.
        pendingOpen = target;
        Close();
    }

    /// <summary>Book queued by a cross-reference, opened once this one has finished closing.</summary>
    private static string? pendingOpen;

    protected override void OnClosed()
    {
        if (!ReferenceEquals(Current, this))
            return;

        Current = null;

        if (pendingOpen is not { } next)
            return;

        pendingOpen = null;
        Open(next);
    }
}
