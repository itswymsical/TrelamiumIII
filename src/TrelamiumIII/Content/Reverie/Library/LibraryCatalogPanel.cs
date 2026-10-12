using System.Collections.Generic;
using System.Linq;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.UI;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.UI.Projected;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Content.Reverie.Library;

/// <summary>
/// One category of Reverie's shelves as a plane standing over Edith, in the job board's white on black: covers in a
/// grid down the left, the highlighted book's title and blurb on the right, and checkout along the bottom.
/// </summary>
internal sealed class LibraryCatalogPanel : WorldPanel, IProjectedPanel
{
    #region Tuning

    /// <summary> Screen pixels per texel; the job board's, so the two read alike. </summary>
    private const float PIXEL = 1.15f;

    private const int SURFACE_W = 560;
    private const int SURFACE_H = 420;

    private const int CONTENT_W = 440;
    private const int CONTENT_H = 252;

    private const int COLUMNS = 5;
    private const int CELL_W = 42;
    private const int CELL_H = 48;
    private const int CELL_GAP = 2;
    private const int GRID_W = COLUMNS * (CELL_W + CELL_GAP) - CELL_GAP;
    private const int COLUMN_GAP = 14;
    private const int COVER_INSET = 5;

    /// <summary> Texels the hovered cover rises out of its cell. </summary>
    private const int HOVER_LIFT = 3;

    /// <summary> Seconds for the glint to cross a hovered cover, and its band width in texels. </summary>
    private const float GLINT_SECONDS = 0.9f;

    private const int GLINT_WIDTH = 3;

    private const int BOX_PAD_Y = 5;
    private const int HEADER_GAP = 6;
    private const int BUTTON_W = 96;
    private const int BUTTON_GAP = 8;
    private const int TITLE_LINES = 2;

    private const int OPEN_FRAMES = 16;
    private const int CLOSE_FRAMES = 10;
    private const float ANCHOR_GAP = 30f;

    private const float ZOOM_FACTOR = 1.4f;
    private const float MAX_ZOOM = 2.4f;
    private const int ZOOM_FRAMES = 45;

    /// <summary> The ghost the library casts: warm paper rather than a speaker's colour. </summary>
    private static readonly Color Ghost = new Color(255, 214, 140) * 0.55f;

    #endregion

    private enum Widget
    {
        None,
        Book,
        Primary,
        Secondary
    }

    #region State

    private readonly NPC npc;
    private readonly LibraryCategory category;
    private readonly List<LibraryBook> books;

    private readonly ProjectedSurface surface = new(SURFACE_W, SURFACE_H, ProjectedInk.Pixelated);
    private readonly BubblePlacer placer;
    private readonly CursorTilt tilt = new();

    private LibraryBook? selected;
    private List<string> titleLines = [];
    private List<string> blurbLines = [];

    private float presence;

    /// <summary>Placement before <see cref="tilt" />; the tilt samples the cursor against this.</summary>
    private QuadPlacement rest;

    private QuadPlacement placement;
    private bool dropped;
    private bool zoomed;

    private Widget hovered = Widget.None;
    private int hoveredIndex = -1;
    private Widget clicked = Widget.None;
    private int clickedIndex = -1;

    /// <summary> <see cref="Main.GlobalTimeWrappedHourly"/> when the cursor reached the hovered cover; drives the glint. </summary>
    private float hoverStart;

    private Rectangle content;
    private Rectangle primary;
    private Rectangle secondary;
    private int gridTop;
    private int detailX;
    private int blurbTop;
    private int maxBlurbLines;

    #endregion

    public LibraryCatalogPanel(NPC npc, LibraryCategory category)
    {
        this.npc = npc;
        this.category = category;
        books = [.. LibraryCatalog.In(category)];

        placer = new BubblePlacer(PIXEL, Main.rand?.Next(1, 9999) ?? 1, 4f);
        WorldAnchor = npc.Top;

        surface.Initialize();
        ProjectedPanelSystem.Register(this);

        Select(books.FirstOrDefault());
        Place(snap: true);
    }

    protected override Vector2 PanelSize => Vector2.Zero;

    protected override void BuildPanel(UIElement body) { }

    protected override void DrawChrome(SpriteBatch spriteBatch, Rectangle bounds, float opacity) { }

    private static DynamicSpriteFont Body => ProjectedFonts.Body;

    private static DynamicSpriteFont Title => ProjectedFonts.Title;

    private static int LineH => ProjectedText.LineHeight(ProjectedFonts.Body);

    private static int DetailWidth => CONTENT_W - GRID_W - COLUMN_GAP;

    private static LibraryPlayer Loans => Main.LocalPlayer.GetModPlayer<LibraryPlayer>();

    public bool ShouldBeRemoved => dropped || !IsActive;

    private static string Text(string key, params object[] args) => Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.Library.{key}", args);

    #region Lifecycle

    public override void OnOpened()
    {
        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset with { Volume = 0.7f });
        ZoomIn();
    }

    protected override void OnCloseRequested()
    {
        SoundEngine.PlaySound(Assets.SFX.BookClose.Asset with { Volume = 0.6f });
        ZoomOut();
    }

    public override void OnClosed()
    {
        dropped = true;
        ZoomOut();
    }

    private void ZoomIn()
    {
        if (DialogueManager.Instance.IsZoomedIn)
            return;

        ZoomHandler.SetZoomAnimation(MathF.Min(MathF.Max(Main.GameZoomTarget, 1f) * ZOOM_FACTOR, MAX_ZOOM), ZOOM_FRAMES);
        zoomed = true;
    }

    private void ZoomOut()
    {
        if (!zoomed)
            return;

        zoomed = false;

        if (!DialogueManager.Instance.IsZoomedIn)
            ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, ZOOM_FRAMES);
    }

    public void Dispose() => surface.Dispose();

    #endregion

    #region Selection

    private void Select(LibraryBook? book)
    {
        selected = book;

        if (book is null)
        {
            titleLines = [];
            blurbLines = [];
            return;
        }

        titleLines = ProjectedText.Clamp(Title, ProjectedText.Wrap(Title, book.Title, DetailWidth), TITLE_LINES, DetailWidth);
        blurbLines = ProjectedText.Wrap(Body, book.Blurb, DetailWidth);
    }

    private void CheckOut()
    {
        if (selected is null || !Loans.CheckOut(selected))
            return;

        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset);
    }

    #endregion

    #region Update

    public override void Update(GameTime gameTime)
    {
        presence = IsClosing
            ? MathF.Max(presence - 1f / CLOSE_FRAMES, 0f)
            : MathF.Min(presence + 1f / OPEN_FRAMES, 1f);

        if (npc is not { active: true } || DialogueManager.Instance.IsAnyActive())
            RequestClose();

        tilt.Update();
        Place();
        ConsumeIntent();

        base.Update(gameTime);
    }

    private void Place(bool snap = false)
    {
        int nameWidth = (int)MathF.Ceiling(ProjectedText.Width(Title, npc.GivenOrTypeName)) + PanelStyle.NAME_PAD_X * 2;
        int nameHeight = ProjectedText.LineHeight(Title) + 4;

        placer.Place(new Point(CONTENT_W, CONTENT_H), nameWidth, nameHeight, Target(), ANCHOR_GAP, snap);

        Layout();

        rest = placer.Placement(presence, IsClosing);
        placement = tilt.Apply(rest);
    }

    private Vector2 Target() =>
        Vector2.Transform(npc.Top + new Vector2(0f, -8f) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

    private void Layout()
    {
        Point origin = placer.Geometry.ContentOrigin;
        int line = LineH;

        content = new Rectangle(origin.X, origin.Y, CONTENT_W, CONTENT_H);
        gridTop = content.Y + line + HEADER_GAP;
        detailX = content.X + GRID_W + COLUMN_GAP;

        int buttonH = line + BOX_PAD_Y * 2;
        int buttonsY = content.Bottom - buttonH;

        secondary = new Rectangle(content.Right - BUTTON_W, buttonsY, BUTTON_W, buttonH);
        primary = new Rectangle(secondary.X - BUTTON_GAP - BUTTON_W, buttonsY, BUTTON_W, buttonH);

        // Title lines, then the status line, then the blurb down to the buttons.
        blurbTop = gridTop + Math.Max(titleLines.Count, 1) * ProjectedText.LineHeight(Title) + line + HEADER_GAP;
        maxBlurbLines = Math.Max(1, (buttonsY - HEADER_GAP - blurbTop) / line);
    }

    private Rectangle Cell(int index) =>
        new(content.X + index % COLUMNS * (CELL_W + CELL_GAP), gridTop + index / COLUMNS * (CELL_H + CELL_GAP), CELL_W, CELL_H);

    /// <summary> Rows that fit above the bottom of the content; the shelves never scroll yet. </summary>
    private int VisibleBooks => Math.Min(books.Count, (content.Bottom - gridTop + CELL_GAP) / (CELL_H + CELL_GAP) * COLUMNS);

    private void ConsumeIntent()
    {
        Widget widget = clicked;
        int index = clickedIndex;

        clicked = Widget.None;
        clickedIndex = -1;

        if (IsClosing)
            return;

        switch (widget)
        {
            case Widget.Book when index >= 0 && index < books.Count && books[index] != selected:
                SoundEngine.PlaySound(SoundID.MenuTick);
                Select(books[index]);
                break;

            case Widget.Primary when selected is not null && Loans.CanCheckOut(selected):
                CheckOut();
                break;

            case Widget.Secondary:
                SoundEngine.PlaySound(SoundID.MenuTick);
                RequestClose();
                break;
        }
    }

    protected override void TrackAnchor()
    {
        if (npc is { active: true })
            WorldAnchor = npc.Top;
    }

    protected override bool ClickedAway() => !ProjectedHitTest.TryLocate(placement, Main.MouseScreen, out _);

    #endregion

    #region Input

    private void HandleMouse()
    {
        Widget previous = hovered;
        int previousIndex = hoveredIndex;

        hovered = Widget.None;
        hoveredIndex = -1;

        tilt.Sample(rest, Main.MouseScreen, !IsClosing && !PlayerInput.IgnoreMouseInterface);

        if (IsClosing || PlayerInput.IgnoreMouseInterface || ProjectedHitTest.Texel(placement, surface.Region, Main.MouseScreen) is not { } p)
            return;

        Main.LocalPlayer.mouseInterface = true;

        for (int i = 0; i < VisibleBooks; i++)
        {
            if (!Cell(i).Contains(p))
                continue;

            hovered = Widget.Book;
            hoveredIndex = i;
            break;
        }

        if (hovered == Widget.None && primary.Contains(p))
            hovered = Widget.Primary;
        else if (hovered == Widget.None && secondary.Contains(p))
            hovered = Widget.Secondary;

        if (hovered != previous || hoveredIndex != previousIndex)
        {
            if (hovered != Widget.None)
                SoundEngine.PlaySound(SoundID.MenuTick);

            hoverStart = Main.GlobalTimeWrappedHourly;
        }

        if (hovered != Widget.None && Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            clicked = hovered;
            clickedIndex = hoveredIndex;
        }
    }

    #endregion

    #region Drawing

    public void BuildSurfaces()
    {
        if (presence > 0f)
            surface.Build(Paint);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!IsActive || presence <= 0f || !surface.Ready)
            return;

        HandleMouse();

        Color tint = Color.White * presence;
        QuadPlacement quad = placement;

        ProjectedPanelSystem.DrawQuads(spriteBatch, (mesh, device) => surface.Draw(mesh, device, quad, tint));
    }

    private Point Paint(ProjectedPainter painter)
    {
        BubbleGeometry geometry = placer.Geometry;

        PanelStyle.DrawBubble(painter.Canvas, geometry, Ghost);

        if (geometry.NameTab != Rectangle.Empty)
            painter.Text(Title, npc.GivenOrTypeName, new Vector2(geometry.NameTab.X + PanelStyle.NAME_PAD_X, geometry.NameTab.Y + 1), PanelStyle.Inverted);

        PaintHeader(painter);
        PaintShelf(painter);

        ProjectedChrome.Rule(painter.Canvas, content.X + GRID_W + COLUMN_GAP / 2, gridTop, content.Bottom - gridTop, true, ProjectedChrome.Dim);

        if (selected is null)
            PaintEmpty(painter);
        else
            PaintDetail(painter);

        PaintButtons(painter);

        return geometry.Used;
    }

    private void PaintHeader(ProjectedPainter painter)
    {
        painter.Text(Title, LibraryCatalog.CategoryName(category), new Vector2(content.X, content.Y), ProjectedChrome.Accent);

        string loans = Text("Loans", Loans.Loans.Count, LibraryPlayer.MaxLoans);
        painter.Text(Body, loans, new Vector2(content.Right - ProjectedText.Width(Body, loans), content.Y + 1), ProjectedChrome.Dim);
    }

    /// <summary>
    /// Frames first, then covers, then the glint over the hovered one: the painter changes batch per kind, so each kind
    /// is drawn in one run.
    /// </summary>
    private void PaintShelf(ProjectedPainter painter)
    {
        int count = VisibleBooks;
        PixelCanvas canvas = painter.Canvas;

        for (int i = 0; i < count; i++)
        {
            bool isSelected = books[i] == selected;
            bool isHovered = hovered == Widget.Book && hoveredIndex == i;

            if (isSelected || isHovered)
                ProjectedChrome.Slot(canvas, Cell(i), false, isHovered);
        }

        for (int i = 0; i < count; i++)
        {
            bool onLoan = Loans.HasLoan(books[i]);
            Rectangle box = CoverBox(i);
            Texture2D cover = books[i].Cover();

            painter.Sprite(cover, CoverPosition(cover, box, out float scale), null, onLoan ? Color.White * 0.35f : Color.White, scale);
        }

        if (hovered == Widget.Book && hoveredIndex >= 0 && hoveredIndex < count)
            PaintGlint(painter.Canvas, CoverBox(hoveredIndex));
    }

    private Rectangle CoverBox(int index)
    {
        Rectangle box = Cell(index);
        box.Inflate(-COVER_INSET, -COVER_INSET);

        if (hovered == Widget.Book && hoveredIndex == index)
            box.Y -= HOVER_LIFT;

        return box;
    }

    /// <summary> Integer scale only, centred in the box: pixel art scaled by a fraction stops being pixel art. </summary>
    private static Vector2 CoverPosition(Texture2D cover, Rectangle box, out float scale)
    {
        scale = MathF.Max(1f, MathF.Floor(MathF.Min(box.Width / (float)cover.Width, box.Height / (float)cover.Height)));

        return new Vector2(box.X + (box.Width - cover.Width * scale) * 0.5f, box.Y + (box.Height - cover.Height * scale) * 0.5f);
    }

    /// <summary> A diagonal band of light sweeping once across the box, restarting while the cursor stays. </summary>
    private void PaintGlint(PixelCanvas canvas, Rectangle box)
    {
        float t = (Main.GlobalTimeWrappedHourly - hoverStart) / GLINT_SECONDS % 1.6f;

        if (t > 1f)
            return;

        int sweep = (int)MathHelper.Lerp(-box.Height, box.Width + GLINT_WIDTH, t);
        Color light = Color.White * 0.55f;

        for (int row = 0; row < box.Height; row++)
        {
            int x = box.X + sweep + (box.Height - row) / 2;
            int left = Math.Max(x, box.X);
            int right = Math.Min(x + GLINT_WIDTH, box.Right);

            if (right > left)
                canvas.Rect(left, box.Y + row, right - left, 1, light);
        }
    }

    private void PaintDetail(ProjectedPainter painter)
    {
        LibraryBook book = selected!;
        int titleHeight = ProjectedText.LineHeight(Title);

        for (int i = 0; i < titleLines.Count; i++)
            painter.Text(Title, titleLines[i], new Vector2(detailX, gridTop + i * titleHeight), PanelStyle.Text);

        bool onLoan = Loans.HasLoan(book);
        string status = onLoan ? Text("CheckedOut") : Text("Available");
        int statusY = gridTop + Math.Max(titleLines.Count, 1) * titleHeight;

        painter.Text(Body, status, new Vector2(detailX, statusY), onLoan ? ProjectedChrome.Accent : ProjectedChrome.Dim);

        List<string> lines = ProjectedText.Clamp(Body, blurbLines, maxBlurbLines, DetailWidth);

        for (int i = 0; i < lines.Count; i++)
            painter.Text(Body, lines[i], new Vector2(detailX, blurbTop + i * LineH), PanelStyle.Text);
    }

    private void PaintEmpty(ProjectedPainter painter)
    {
        string empty = Text("Empty");

        painter.Text(Body, empty, new Vector2(detailX, gridTop), ProjectedChrome.Dim);
    }

    private void PaintButtons(ProjectedPainter painter)
    {
        LibraryPlayer loans = Loans;
        bool canCheckOut = selected is not null && loans.CanCheckOut(selected);

        string label = selected is not null && loans.HasLoan(selected)
            ? Text("CheckedOut")
            : loans.Loans.Count >= LibraryPlayer.MaxLoans ? Text("LoansFull") : Text("CheckOut");

        PixelCanvas canvas = painter.Canvas;

        ProjectedChrome.ButtonFrame(canvas, primary, hovered == Widget.Primary, canCheckOut);
        ProjectedChrome.ButtonFrame(canvas, secondary, hovered == Widget.Secondary, true);

        PaintLabel(painter, primary, label, ProjectedChrome.ButtonText(hovered == Widget.Primary, canCheckOut));
        PaintLabel(painter, secondary, Text("Close"), ProjectedChrome.ButtonText(hovered == Widget.Secondary, true));
    }

    private static void PaintLabel(ProjectedPainter painter, Rectangle rect, string label, Color color)
    {
        float width = ProjectedText.Width(Body, label);

        painter.Text(Body, label, new Vector2(rect.X + (rect.Width - width) * 0.5f, rect.Y + BOX_PAD_Y), color);
    }

    #endregion
}

/// <summary>
/// Opens a <see cref="LibraryCatalogPanel"/> once Edith's dialogue has closed: a panel opened while a conversation is
/// active closes itself straight away, the same rule the job board follows.
/// </summary>
public sealed class LibraryCatalogSystem : ModSystem
{
    private const int RequestTimeoutTicks = 300;
    private const float EdithRange = 30 * 16f;

    private static LibraryCategory? pending;
    private static uint requestedAt;

    public static void Request(LibraryCategory category)
    {
        pending = category;
        requestedAt = Main.GameUpdateCount;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (pending is not { } category || Main.dedServ)
            return;

        if (Main.GameUpdateCount - requestedAt > RequestTimeoutTicks)
        {
            pending = null;
            return;
        }

        if (DialogueManager.Instance.IsAnyActive())
            return;

        pending = null;

        NPC? edith = Main.npc.FirstOrDefault(npc => npc.active && npc.type == ModContent.NPCType<Edith>()
            && npc.WithinRange(Main.LocalPlayer.Center, EdithRange));

        if (edith is not null)
            WorldUISystem.OpenPanelState(new LibraryCatalogPanel(edith, category));
    }
}
