using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;
using TrelamiumIII.Core.Books.Layout;
using static TrelamiumIII.Core.Books.BookCover;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     A book on screen: open, read, turn and close. Subclasses address content by spread. Hosted as an <see cref="IInGameNotification" />,
///     which updates after vanilla consumes mouse-release flags, so clicks come from tracked edges.
/// </summary>
public abstract class BookUI : IInGameNotification, IBookView, IOpenBook
{
    private const float OPEN_SPEED = 0.042f;
    private const float FLIP_SPEED = 0.05f;

    private enum BookState
    {
        Opening,
        Reading,
        Flipping,
        Closing,
    }

    private BookState state = BookState.Opening;
    private bool forward = true;

    private float openProgress;
    private float flipProgress;

    private bool wasLeftDown = true;
    private bool wasRightDown = true;

    private BookRenderer? renderer;

    /// <summary>Tooltip raised by whatever the cursor is over, collected while the pages draw.</summary>
    private string? hoverText;
    private string? hoverLink;
    private int? hoverPage;

    /// <summary>Tab under the cursor, resolved during input and reused when drawing the strip.</summary>
    private int hoveredTab = -1;

    protected int CurrentSpread { get; private set; }

    protected int NextSpread { get; private set; }

    public abstract int PageWidth { get; }

    public abstract int PageHeight { get; }

    public virtual int Gutter => 6;

    /// <summary>Total spreads in this book. Read every frame, so it may grow as content unlocks.</summary>
    protected abstract int SpreadCount { get; }

    protected virtual SoundStyle? FlipSound => SoundID.MenuTick;

    protected virtual SoundStyle? CloseSound => SoundID.MenuClose;

    /// <summary>Draws one side of a spread into page-local space.</summary>
    protected abstract void DrawSpreadSide(SpriteBatch spriteBatch, int spread, BookSide side, BookPageContext context);

    /// <summary>Draws the cover face. Called once; the result is cached in its own target.</summary>
    protected abstract void DrawCoverFace(SpriteBatch spriteBatch);

    /// <summary>True when this slot shows the cover instead of a leaf, e.g. the opening spread.</summary>
    protected virtual bool SpreadShowsCover(int spread, BookSide side) => false;

    /// <summary>Followed when a cross-reference on the page is clicked.</summary>
    protected virtual void OnLinkActivated(string target) { }

    /// <summary>Followed when a contents row is clicked. The page is an index, not a spread.</summary>
    protected virtual void OnPageActivated(int page) { }

    /// <summary>Tabs along the book's top edge. Empty leaves the strip off entirely.</summary>
    protected virtual IReadOnlyList<BookTab> Tabs => [];

    /// <summary><paramref name="secondary" /> is a right-click, which clears a tab rather than following it.</summary>
    protected virtual void OnTabActivated(int index, bool secondary) { }

    #region Lifecycle

    protected void BeginShowing()
    {
        renderer = new BookRenderer(PageWidth, PageHeight, Gutter);
        renderer.Initialize();

        BookRenderSystem.Register(this);
        InGameNotificationsTracker.AddNotification(this);
    }

    public void Close(bool immediate = false)
    {
        if (state == BookState.Closing)
            return;

        state = BookState.Closing;

        if (immediate)
            openProgress = 0f;
        else if (CloseSound is { } sound)
            SoundEngine.PlaySound(sound);
    }

    public bool ShouldBeRemoved => state == BookState.Closing && openProgress <= 0f;

    public void PushAnchor(ref Vector2 positionAnchorBottom) { }

    #endregion

    #region IBookView

    public float PageTurnProgress => state == BookState.Flipping ? flipProgress : 0f;

    /// <summary>Runs 1 to 0 as the book opens, uncurling the right leaf onto the spread.</summary>
    public float OpenProgress => state == BookState.Opening ? 1f - openProgress : 0f;

    /// <summary>Runs 0 to 1 as the book shuts, folding both leaves back to the spine.</summary>
    public float CloseProgress => state == BookState.Closing ? 1f - openProgress : 0f;

    public bool IsForwardFlip => forward;

    /// <summary>Pages are redrawn live so hover and animation stay current.</summary>
    public virtual bool PagesDirty => true;

    protected float Opacity => state is BookState.Opening or BookState.Closing ? openProgress : 1f;

    bool IBookView.IsCover(BookSide side, bool next) => SpreadShowsCover(next ? NextSpread : CurrentSpread, side);
    public virtual BookPalette BookPalette => BookPalette.Default();
    void IBookView.DrawCover(SpriteBatch spriteBatch) => DrawCoverFace(spriteBatch);

    void IBookView.DrawPage(SpriteBatch spriteBatch, BookSide side, bool next)
    {
        int spread = next ? NextSpread : CurrentSpread;

        var context = new BookPageContext
        {
            PageWidth = PageWidth,
            PageHeight = PageHeight,

            // Only the settled spread takes the cursor. Hit-testing a leaf that is mid-turn would
            // report hits at the position it is drawn flat, not where the reader sees it.
            LocalMouse = !next && state == BookState.Reading ? LocalMouse(side) : null,
        };

        DrawSpreadSide(spriteBatch, spread, side, context);

        hoverText ??= context.HoverText;
        hoverLink ??= context.HoverLink;
        hoverPage ??= context.HoverPage;
    }

    void IOpenBook.BuildFrame()
    {
        // Cleared before drawing, not after: notifications update before the draw, so hover set during this draw has to
        // survive to the next update.
        hoverText = null;
        hoverLink = null;
        hoverPage = null;

        renderer?.Build(this);
    }

    #endregion

    #region Update

    public void Update()
    {
        if (Main.gameMenu)
        {
            Close(immediate: true);
            return;
        }

        if (state != BookState.Closing)
            Main.LocalPlayer.mouseInterface = true;

        switch (state)
        {
            case BookState.Opening:
                openProgress = Math.Min(1f, openProgress + OPEN_SPEED);

                if (openProgress >= 1f)
                    state = BookState.Reading;

                break;

            case BookState.Reading:
                HandleInput();
                break;

            case BookState.Flipping:
                flipProgress = Math.Min(1f, flipProgress + FLIP_SPEED);

                if (flipProgress >= 1f)
                {
                    CurrentSpread = NextSpread;
                    flipProgress = 0f;
                    state = BookState.Reading;
                }

                break;

            case BookState.Closing:
                openProgress = Math.Max(0f, openProgress - OPEN_SPEED);
                break;
        }

        if (ShouldBeRemoved)
            Release();

        wasLeftDown = Main.mouseLeft;
        wasRightDown = Main.mouseRight;
    }

    void IOpenBook.ForceRelease()
    {
        state = BookState.Closing;
        openProgress = 0f;

        Release();
    }

    private void Release()
    {
        if (renderer is null)
            return;

        BookRenderSystem.Unregister(this);
        renderer.Dispose();
        renderer = null;

        OnClosed();
    }

    /// <summary>Called once the book has finished closing and its targets have been released.</summary>
    protected virtual void OnClosed() { }

    private void HandleInput()
    {
        bool leftPressed = Main.mouseLeft && !wasLeftDown;
        bool rightPressed = Main.mouseRight && !wasRightDown;

        Point mouse = Main.MouseScreen.ToPoint();

        // Tabs are resolved first and swallow the click either way, so a right-click meant to clear
        // one does not shut the book instead.
        hoveredTab = BookCover.HitTest(SpreadOrigin(), Tabs.Count, mouse);

        if (hoveredTab >= 0)
        {
            if (leftPressed || rightPressed)
                OnTabActivated(hoveredTab, rightPressed);

            return;
        }

        if (Main.keyState.IsKeyDown(Keys.Escape) || rightPressed)
        {
            Close();
            return;
        }

        // A click on a cross-reference or a contents row follows it rather than turning the page.
        if (leftPressed && hoverLink is { } link)
        {
            OnLinkActivated(link);
            return;
        }

        if (leftPressed && hoverPage is { } page)
        {
            OnPageActivated(page);
            return;
        }

        if (leftPressed && PageRect(BookSide.Right).Contains(mouse))
        {
            Turn(1);
            return;
        }

        if (leftPressed && PageRect(BookSide.Left).Contains(mouse))
        {
            Turn(-1);
            return;
        }

        if (Main.keyState.IsKeyDown(Keys.Right))
            Turn(1);
        else if (Main.keyState.IsKeyDown(Keys.Left))
            Turn(-1);
    }

    /// <summary>Starts a turn by <paramref name="delta"/> spreads. Turning past the end shuts the book.</summary>
    protected void Turn(int delta)
    {
        if (state != BookState.Reading || delta == 0)
            return;

        int target = CurrentSpread + delta;

        if (target < 0)
            return;

        if (target >= SpreadCount)
        {
            Close();
            return;
        }

        JumpTo(target);
    }

    /// <summary>Turns to an arbitrary spread, animating in whichever direction it lies.</summary>
    protected void JumpTo(int spread)
    {
        if (state != BookState.Reading || spread == CurrentSpread)
            return;

        if (spread < 0 || spread >= SpreadCount)
            return;

        forward = spread > CurrentSpread;
        NextSpread = spread;
        flipProgress = 0f;
        state = BookState.Flipping;

        if (FlipSound is { } sound)
            SoundEngine.PlaySound(sound);
    }

    #endregion

    #region Draw

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        if (Main.gameMenu || renderer?.Composite is null)
            return;

        Vector2 origin = SpreadOrigin();

        // Tabs first: the cover boards draw over their lower edge, which is what makes them read as
        // tucked behind the book rather than stuck on the front of it.
        IReadOnlyList<BookTab> tabs = Tabs;

        if (tabs.Count > 0)
            BookCover.DrawBookTabs(spriteBatch, origin, tabs, hoveredTab, Opacity, BookPalette);

        BookCover.Draw(spriteBatch, origin, PageWidth, PageHeight, Gutter, Opacity, BookPalette);

        spriteBatch.Draw(renderer.Composite, origin, Color.White * Opacity);

        DrawHoverTooltip(spriteBatch);
    }

    private void DrawHoverTooltip(SpriteBatch spriteBatch)
    {
        if (state != BookState.Reading)
            return;

        // A hovered tab speaks for itself and overrides whatever the page underneath was saying.
        string? text = hoveredTab >= 0 && hoveredTab < Tabs.Count ? Tabs[hoveredTab].Tooltip : hoverText;

        if (string.IsNullOrEmpty(text))
            return;

        Vector2 size = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One);
        Vector2 position = Main.MouseScreen + new Vector2(16f, 16f);

        position.X = MathHelper.Clamp(position.X, 0f, Main.screenWidth - size.X - 8f);
        position.Y = MathHelper.Clamp(position.Y, 0f, Main.screenHeight - size.Y - 8f);

        var box = new Rectangle((int)position.X - 6, (int)position.Y - 4, (int)size.X + 12, (int)size.Y + 8);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, new Color(24, 18, 12, 230));
        Layout.Blocks.ImageBlock.DrawBorder(spriteBatch, box, new Color(150, 110, 60));

        ChatManager.DrawColorCodedStringWithShadow(
            spriteBatch,
            FontAssets.MouseText.Value,
            ChatManager.ParseMessage(text, Color.White).ToArray(),
            position,
            0f,
            Color.White,
            Vector2.Zero,
            Vector2.One,
            out _);
    }

    #endregion

    #region Layout

    protected Vector2 SpreadOrigin()
    {
        int totalWidth = PageWidth * 2 + Gutter;

        return new Vector2(
            Main.screenWidth / 2f - totalWidth / 2f,
            Main.screenHeight / 2f - PageHeight / 2f);
    }

    protected Rectangle PageRect(BookSide side)
    {
        Vector2 origin = SpreadOrigin();

        if (side == BookSide.Right)
            origin.X += PageWidth + Gutter;

        return new Rectangle((int)origin.X, (int)origin.Y, PageWidth, PageHeight);
    }

    /// <summary>Cursor in page-local space, or null when it is not over that leaf.</summary>
    protected Vector2? LocalMouse(BookSide side)
    {
        Rectangle rect = PageRect(side);
        Point mouse = Main.MouseScreen.ToPoint();

        if (!rect.Contains(mouse))
            return null;

        return new Vector2(mouse.X - rect.X, mouse.Y - rect.Y);
    }

    #endregion
}
