using System.Collections.Generic;
using ReLogic.Graphics;
using ReLogic.Text;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     The authoring desk: a composed page on a desk, formatting above it, the running order beside.
/// </summary>
/// <remarks>
///     <para>
///         The page is always the real render. There is no draft mode to switch out of, because a
///         writing tool that shows you something other than the thing you are making is only half a
///         tool. Every block on the page is drawn by the same <see cref="PageComposer" /> the reader's
///         copy goes through.
///     </para>
///     <para>
///         The block being edited is the one exception, and for a specific reason: composed prose is
///         wrapped by <see cref="BookText" /> over parsed snippets, while the caret is positioned by
///         <see cref="EditorLayout" /> over raw characters. Those two wrappers cannot be made to agree
///         once tags, justification or a drop cap are in play, so the caret would drift away from the
///         glyphs. The selected block is therefore drawn by the editor instead -- same face, same
///         scale, same ink, so it reads identically -- which makes the caret exact by construction
///         rather than by coincidence.
///     </para>
///     <para>
///         Input arrives through <see cref="UIElement" />'s events, never <c>Main.mouseLeft</c>: a
///         click is delivered once, to the hovered element, already in this element's coordinate
///         space.
///     </para>
/// </remarks>
public sealed class AuthoringUIState : UIState
{
    private const float CHROME = 0.8f;
    private const int PAGE_WIDTH = 470;
    private const int RAIL_WIDTH = 196;
    private const int TOOLBAR_HEIGHT = 82;

    /// <summary>Tall enough that the slot plate's bevel reads as a raised button rather than a line.</summary>
    private const int BUTTON_HEIGHT = 28;
    private const float FADE_FRAMES = 12f;
    private const float ANIM_SPEED = 0.18f;

    private readonly AuthoringSession session = new();
    private readonly EditorInput input = new();
    private readonly ColorWheelWidget colorWheel = new();
    private readonly List<(Rectangle Area, Action Act)> hotspots = [];

    private EditorLayout? layout;
    private ComposedBook? composed;

    /// <summary>Where on the page the selected block landed, in desk coordinates.</summary>
    private Rectangle editorRect;

    private Rectangle pageRect;
    private Rectangle colorRect;

    /// <summary>Sheet height the cached composition was flowed for.</summary>
    private int composedFor = -1;

    /// <summary>Column width the cached wrap was built for.</summary>
    private float layoutFor = -1f;
    private bool dragging;
    private int lastClickFrame;
    private int previewPage;

    private float fade;
    private bool closing;
    private float dropdown;
    private bool dropdownOpen;
    private float legend;
    private bool legendOpen;

    /// <summary>Field the modal prompt is editing, or null when no prompt is up.</summary>
    private EditorField? prompt;

    private bool colorOpen;

    /// <summary>Explanation for whatever the cursor is over this frame. Cleared every draw.</summary>
    private string? tooltip;

    public void Attach()
    {
        input.Attach();
        fade = 0f;
        closing = false;
    }

    public void Detach()
    {
        input.Detach();
        session.Commit();
    }

    public void BeginClose() => closing = true;

    /// <summary>Escape unwinds one layer at a time. True when something was dismissed.</summary>
    public bool DismissTopmost()
    {
        if (colorOpen) { colorOpen = false; return true; }
        if (prompt is not null) { ClosePrompt(); return true; }
        if (dropdownOpen) { dropdownOpen = false; return true; }

        if (session.Focus.IsShort()) { LeaveShortField(); return true; }

        return false;
    }

    private float Eased
    {
        get
        {
            float t = Math.Clamp(fade, 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }

    private static Vector2 Cursor => UserInterface.ActiveInstance?.MousePosition ?? Vector2.Zero;

    private static Point CursorPoint => new((int)Cursor.X, (int)Cursor.Y);

    #region Input

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        var point = new Point((int)evt.MousePosition.X, (int)evt.MousePosition.Y);

        if (colorOpen && colorWheel.Press(point, colorRect)) return;

        // Walked backwards, so the last thing drawn is the first thing hit. Painter's order puts
        // overlays on top; matching front-to-back would let a toolbar button underneath an open
        // dropdown steal the click that was meant for the list.
        for (int i = hotspots.Count - 1; i >= 0; i--)
        {
            if (!hotspots[i].Area.Contains(point)) continue;

            hotspots[i].Act();

            return;
        }

        // A click outside an open overlay dismisses it rather than falling through to the page.
        if (colorOpen || prompt is not null || dropdownOpen)
        {
            colorOpen = false;
            dropdownOpen = false;

            if (prompt is not null) ClosePrompt();

            return;
        }

        if (!editorRect.Contains(point)) return;

        // Clicking into the page leaves any toolbar field that still held the buffer. That discards
        // the wrap, and the caret placement below needs one, so it is rebuilt before being asked --
        // for the prose this time, not for the field just left.
        LeaveShortField();
        EnsureLayout();

        bool doubleClick = Main.GameUpdateCount - lastClickFrame < 20;

        lastClickFrame = (int)Main.GameUpdateCount;
        session.Buffer.MoveTo(IndexAt(evt.MousePosition), extend: false);

        if (doubleClick) session.Buffer.SelectWordAt(session.Buffer.Caret);
        else dragging = true;
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        dragging = false;
        colorWheel.Release();
    }

    public override void ScrollWheel(UIScrollWheelEvent evt)
    {
        base.ScrollWheel(evt);

        if (composed is null || composed.Pages.Count <= 1) return;

        previewPage = Math.Clamp(previewPage - Math.Sign(evt.ScrollWheelValue), 0, composed.Pages.Count - 1);
    }

    private int IndexAt(Vector2 cursor) => layout!.IndexAt(cursor.X - editorRect.X, cursor.Y - editorRect.Y);

    #endregion

    #region Frame

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        fade = Math.Clamp(fade + (closing ? -1f : 1f) / FADE_FRAMES, 0f, 1f);

        if (closing)
        {
            if (fade <= 0f) AuthoringSystem.Finish();

            return;
        }

        dropdown = Approach(dropdown, dropdownOpen ? 1f : 0f);
        legend = Approach(legend, legendOpen ? 1f : 0f);

        Main.LocalPlayer.mouseInterface = true;

        EnsureComposed();
        EnsureLayout();

        input.SingleLine = session.Focus.IsShort();
        input.Update(session.Buffer, layout!);

        if (input.Dirty)
        {
            session.Commit();
            composed = null;
            layout = null;
            EnsureComposed();
            EnsureLayout();
        }

        // Return on a one-line field hands focus back to the prose, so the writer is typing into
        // the page again rather than silently still editing a toolbar box.
        if (input.Submitted) LeaveShortField();

        if (dragging) session.Buffer.MoveTo(IndexAt(Cursor), extend: true);

        if (colorWheel.Dragging)
        {
            colorWheel.Drag(CursorPoint, colorRect);

            Color picked = colorWheel.Value;
            int packed = (picked.R << 16) | (picked.G << 8) | picked.B;

            // Same scope rule as emphasis: a selection is tinted, otherwise the whole block is.
            if (session.Buffer.HasSelection && !session.Focus.IsShort())
            {
                session.Buffer.ApplyFormat(f => f with { Ink = packed });
                session.Commit();
            }
            else if (session.Selected is { } tinted)
            {
                tinted.Ink = packed;
            }

            composed = null;
            layout = null;
        }
    }

    private static float Approach(float current, float target) =>
        Math.Abs(current - target) < 0.01f ? target : MathHelper.Lerp(current, target, ANIM_SPEED);

    private void EnsureComposed()
    {
        int height = Math.Max(200, pageRect.Height - 44);

        // The sheet's height is only known once the desk has laid itself out, and it decides how
        // much fits on a page. Composing against a stale height silently paginates the whole book
        // wrong, so the height is part of what makes the cache valid.
        if (composed is not null && composedFor == height) return;

        composedFor = height;

        var metrics = new PageMetrics(PAGE_WIDTH, height, 26f, 24f, 24f);

        composed = PageComposer.Compose(AuthoredPreview.ToBlocks(session.Book), [], metrics);

        // The page follows the writer. Selecting a block that flowed onto page four should show
        // page four, not leave the caret somewhere off-screen.
        int index = session.SelectedIndex;

        if (index >= 0 && index < composed.BlockPages.Length && composed.BlockPages[index] >= 0)
            previewPage = composed.BlockPages[index];

        previewPage = Math.Clamp(previewPage, 0, Math.Max(0, composed.Pages.Count - 1));
    }

    private DynamicSpriteFont PageFont()
    {
        if (session.Selected is not { } block) return FontAssets.MouseText.Value;

        BookFont role = block.Kind switch
        {
            AuthoredBlockKind.Heading or AuthoredBlockKind.Chapter => BookFont.Display,
            _                                                      => BookFont.Body,
        };

        BookTypeface set = Face() ?? BookTypeface.Vanilla;

        return set.Variant(role, block.Bold, block.Italic);
    }

    /// <summary>The typeface in force for the selected block, element choice over the book's.</summary>
    private BookTypeface? Face() => session.Selected is { } block
        ? AuthoredPreview.Resolve(block.Typeface) ?? AuthoredPreview.Resolve(session.Book.Typeface)
        : AuthoredPreview.Resolve(session.Book.Typeface);

    /// <summary>Size the selected block renders at. One definition, owned by the session.</summary>
    private float PageScale() => session.EffectiveScale;

    /// <summary>
    ///     Rebuilds the wrap when the text, the column or the face changed under it.
    /// </summary>
    /// <remarks>
    ///     The prompt gets its own metrics rather than borrowing the page's. It is a single line of
    ///     interface text in the chrome font, and measuring it with the book face at book scale would
    ///     put its caret somewhere else entirely.
    /// </remarks>
    private void EnsureLayout()
    {
        // Any one-line field is measured in the chrome font and never wraps, whether it lives in a
        // modal or inline in the toolbar. Measuring one with the book face at book scale would put
        // its caret somewhere else entirely.
        bool onShortField = session.Focus.IsShort();

        DynamicSpriteFont font = onShortField ? FontAssets.MouseText.Value : PageFont();
        float scale = onShortField ? CHROME : PageScale();
        float column = onShortField
            ? 4000f
            : Math.Max(60f, editorRect.Width > 0 ? editorRect.Width : PAGE_WIDTH - 52);

        if (layout is not null && Math.Abs(layoutFor - column) < 0.5f) return;

        layoutFor = column;

        string content = session.Buffer.Text;

        // EditorLayout reports drawn pixels, so the column it takes and the positions it returns are
        // both already scaled. Nothing downstream multiplies its output again.
        TextBuffer buffer = session.Buffer;

        layout = new EditorLayout(content, index =>
        {
            // A short field is uniform chrome text; the page resolves each character's own format.
            (DynamicSpriteFont at, float size) = onShortField ? (font, scale) : FaceAt(index);

            char glyph = index < content.Length ? content[index] : 'H';
            GlyphMetrics m = at.GetCharacterMetrics(glyph);
            float ascent = BaselineOf(at) * size;
            float height = at.LineSpacing * size;

            // A run boundary is measured the way the book measures it. BookText lays each snippet
            // out with its own MeasureString, which clamps the leading bearing of a run's first
            // glyph and charges no spacing before it. Charging them here instead would make a block
            // shift sideways the instant it stopped being the edited one and went back to being
            // composed -- correct typography, visibly wrong behaviour.
            bool runStart = onShortField
                ? index == 0
                : index == 0 || index >= content.Length || buffer.FormatAt(index) != buffer.FormatAt(index - 1);

            return new EditorLayout.CharMetrics(
                runStart ? Math.Max(m.LeftPadding * size, 0f) : m.LeftPadding * size,
                m.CharacterWidth * size,
                m.RightPadding * size,
                runStart ? 0f : at.CharacterSpacing * size,
                ascent,
                height - ascent);
        }, column);
    }

    /// <summary>
    ///     The face and size one character is set in: the block's settings, with its own on top.
    /// </summary>
    /// <remarks>
    ///     Character formatting adds to the block's rather than replacing it, so bolding a word inside
    ///     an already-italic block gives bold italic. A character size overrides the block's outright,
    ///     since two sizes cannot compose.
    /// </remarks>
    private (DynamicSpriteFont Font, float Scale) FaceAt(int index)
    {
        TextBuffer buffer = session.Buffer;

        // Past the end there is no character, so the answer is whatever the next keystroke would be.
        CharFormat format = index < buffer.Length ? buffer.FormatAt(index) : buffer.ActiveFormat;

        AuthoredBlock? block = session.Selected;
        BookFont role = block?.Kind switch
        {
            AuthoredBlockKind.Heading or AuthoredBlockKind.Chapter => BookFont.Display,
            _                                                      => BookFont.Body,
        };

        BookTypeface set = Face() ?? BookTypeface.Vanilla;

        return (
            set.Variant(role, format.Bold || (block?.Bold ?? false), format.Italic || (block?.Italic ?? false)),
            format.HasScale ? format.Scale : PageScale());
    }

    /// <summary>
    ///     Distance from a face's line-box top down to its baseline.
    /// </summary>
    /// <remarks>
    ///     Read off a capital that neither ascends above the cap line nor descends below the baseline:
    ///     its top bearing plus its ink height lands exactly on the baseline. There is no ascent metric
    ///     on <see cref="DynamicSpriteFont" /> to ask for directly, and guessing a fraction of the line
    ///     height would misalign faces against each other. Cached, since it is a property of the face.
    /// </remarks>
    private static float BaselineOf(DynamicSpriteFont font)
    {
        if (baselines.TryGetValue(font, out float cached)) return cached;

        float baseline = font.LineSpacing * 0.75f;

        if (font.SpriteCharacters.TryGetValue('H', out DynamicSpriteFont.SpriteCharacterData data)
            && data.Glyph.Height > 0)
        {
            baseline = data.Padding.Y + data.Glyph.Height;
        }

        baselines[font] = baseline;

        return baseline;
    }

    private static readonly Dictionary<DynamicSpriteFont, float> baselines = [];

    #endregion

    #region Drawing

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        hotspots.Clear();
        tooltip = null;

        float alpha = Eased;

        if (alpha <= 0.001f) return;

        CalculatedStyle dims = GetDimensions();
        var screen = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

        Fill(spriteBatch, screen, new Color(8, 8, 14) * (alpha * 0.8f));

        int documentWidth = PAGE_WIDTH + 20;
        int centre = screen.X + (screen.Width + RAIL_WIDTH) / 2;

        var toolbar = new Rectangle(centre - documentWidth / 2, screen.Y + 10, documentWidth, TOOLBAR_HEIGHT);
        var rail = new Rectangle(toolbar.X - RAIL_WIDTH - 10, toolbar.Y, RAIL_WIDTH, screen.Height - 52);

        pageRect = new Rectangle(toolbar.X, toolbar.Bottom + 8, documentWidth, screen.Height - toolbar.Bottom - 40);

        DrawPage(spriteBatch, alpha);
        DrawRail(spriteBatch, rail, alpha);
        DrawToolbar(spriteBatch, toolbar, alpha);
        DrawLegend(spriteBatch, screen, alpha);

        Text(spriteBatch, session.Status, new Vector2(rail.X, screen.Bottom - 26), Color.Gray * alpha, CHROME);

        // Overlays last: they sit above the desk and swallow clicks that reach them.
        if (dropdown > 0.01f) DrawDropdown(spriteBatch, toolbar, alpha);
        if (prompt is not null) DrawPrompt(spriteBatch, screen, alpha);
        if (colorOpen) DrawColorPicker(spriteBatch, screen, alpha);

        DrawTooltip(spriteBatch, screen, alpha);
    }

    /// <summary>The composed book, with the block under the caret drawn by the editor instead.</summary>
    private void DrawPage(SpriteBatch spriteBatch, float alpha)
    {
        AuthoringSkin.DrawPanel(spriteBatch, pageRect, AuthoringSkin.Inset * alpha);

        var sheet = new Rectangle(pageRect.X + 10, pageRect.Y + 10, PAGE_WIDTH, pageRect.Height - 44);

        Fill(spriteBatch, sheet, AuthoringSkin.Paper * alpha);

        EnsureComposed();

        editorRect = Rectangle.Empty;

        // Clamped here as well as in EnsureComposed: the page arrows step it during the click, and
        // a cached composition skips the clamp on the way back round.
        previewPage = Math.Clamp(previewPage, 0, Math.Max(0, composed!.Pages.Count - 1));

        if (composed.Pages.Count > 0)
        {
            var context = new BookPageContext { PageWidth = sheet.Width, PageHeight = sheet.Height };

            foreach (PlacedBlock placed in composed.Pages[previewPage].Blocks)
            {
                BlockRect rect = placed.Rect with
                {
                    X = placed.Rect.X + sheet.X,
                    Y = placed.Rect.Y + sheet.Y,
                };

                // While a short field owns the buffer the page has no caret to show, so the block is
                // drawn composed like every other one rather than through a layout built for a
                // different font entirely.
                if (!session.Focus.IsShort()
                    && placed.SourceIndex == session.SelectedIndex
                    && session.Selected is { HasText: true })
                {
                    editorRect = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

                    continue;
                }

                placed.Block.Draw(spriteBatch, rect, context);
            }
        }

        if (editorRect.Width > 0)
        {
            EnsureLayout();
            DrawEditedBlock(spriteBatch, alpha);
        }

        int y = pageRect.Bottom - 32;

        Arrow(spriteBatch, AuthoringSkin.BackArrow, pageRect.X + 12, y, alpha, () => previewPage--, "Previous page");
        Arrow(spriteBatch, AuthoringSkin.ForwardArrow, pageRect.X + 44, y, alpha, () => previewPage++, "Next page");

        Text(spriteBatch, $"{previewPage + 1} / {Math.Max(1, composed.Pages.Count)}",
            new Vector2(pageRect.X + 80, y + 6), Color.White * alpha, CHROME);
    }

    /// <summary>
    ///     Draws the block under the caret, run by run.
    /// </summary>
    /// <remarks>
    ///     A line can no longer be drawn as one string: neighbouring characters may be set in
    ///     different faces at different sizes. It is emitted as runs of identical formatting instead,
    ///     each positioned from the layout's own prefix widths so the glyphs land exactly where the
    ///     caret says they will.
    /// </remarks>
    private void DrawEditedBlock(SpriteBatch spriteBatch, float alpha)
    {
        TextBuffer buffer = session.Buffer;
        Color blockInk = session.Selected?.Ink is { } packed
            ? new Color((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF)
            : AuthoringSkin.Ink;

        var origin = new Vector2(editorRect.X, editorRect.Y);
        string content = buffer.Text;

        foreach (EditorLayout.Line line in layout!.Lines)
        {
            float y = origin.Y + line.Top;

            if (buffer.HasSelection)
            {
                int from = Math.Max(buffer.SelectionStart, line.Start);
                int to = Math.Min(buffer.SelectionEnd, line.End);

                if (to > from)
                {
                    // Measured against this line. XOf resolves a soft wrap's end onto the line
                    // below, which would draw the band backwards.
                    float left = layout.WidthOfRange(line.Start, from);
                    float right = layout.WidthOfRange(line.Start, to);

                    Fill(spriteBatch, new Rectangle((int)(origin.X + left), (int)y,
                        (int)Math.Max(2f, right - left), (int)line.Height),
                        new Color(150, 178, 216) * alpha);
                }
            }

            int i = line.Start;

            while (i < line.End)
            {
                CharFormat format = buffer.FormatAt(i);
                int start = i;

                while (i < line.End && buffer.FormatAt(i) == format) i++;

                (DynamicSpriteFont font, float scale) = FaceAt(start);
                Color ink = format.Ink is { } tint
                    ? new Color((tint >> 16) & 0xFF, (tint >> 8) & 0xFF, tint & 0xFF)
                    : blockInk;

                spriteBatch.DrawString(font, content[start..i],
                    new Vector2(origin.X + layout.WidthOfRange(line.Start, start), y + layout.BaselineOffset(start)),
                    ink * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        if (Main.GameUpdateCount % 40 >= 22) return;

        (float caretX, float caretY) = layout.CaretAt(buffer.Caret);

        // Armed formatting shows on the caret, so the writer can see bold is pending before typing.
        Color caretInk = buffer.Pending is { Ink: { } armed }
            ? new Color((armed >> 16) & 0xFF, (armed >> 8) & 0xFF, armed & 0xFF)
            : blockInk;

        Fill(spriteBatch, new Rectangle((int)(origin.X + caretX), (int)(origin.Y + caretY),
            buffer.Pending is null ? 2 : 3, (int)layout.CaretHeight(buffer.Caret)), caretInk * alpha);
    }

    /// <summary>Formatting, arranged the way a word processor arranges it.</summary>
    private void DrawToolbar(SpriteBatch spriteBatch, Rectangle area, float alpha)
    {
        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Panel * alpha);

        AuthoredBlock? block = session.Selected;
        BookTypeface? face = Face();

        int x = area.X + 8;
        int y = area.Y + 7;

        // Face, then size, then emphasis, then colour -- Word's Font group order.
        x += Button(spriteBatch, $"{TypefaceName()}  v", x, y, 122, alpha,
            () => dropdownOpen = !dropdownOpen, dropdownOpen, "Typeface for this block") + 4;

        x += SizeField(spriteBatch, x, y, 56, alpha) + 2;
        x += Button(spriteBatch, "A+", x, y, 32, alpha, () => Resize(0.04f), false, "Grow text") + 2;
        x += Button(spriteBatch, "A-", x, y, 32, alpha, () => Resize(-0.04f), false, "Shrink text") + 8;

        bool canBold = face?.Supports(true, false) ?? false;
        bool canItalic = face?.Supports(false, true) ?? false;

        bool selecting = session.Buffer.HasSelection && !session.Focus.IsShort();

        x += Toggle(spriteBatch, "B", x, y, 30, alpha, EmphasisOn(f => f.Bold, b => b.Bold), canBold,
            () => Emphasise(f => f with { Bold = !f.Bold }, b => b.Bold = !b.Bold),
            selecting ? "Bold the selection" : "Bold this block") + 2;

        x += Toggle(spriteBatch, "I", x, y, 30, alpha, EmphasisOn(f => f.Italic, b => b.Italic), canItalic,
            () => Emphasise(f => f with { Italic = !f.Italic }, b => b.Italic = !b.Italic),
            selecting ? "Italicise the selection" : "Italicise this block") + 8;

        x += Swatch(spriteBatch, x, y, alpha) + 8;

        x += IconButton(spriteBatch, AuthoringSkin.Copy, x, y, 32, alpha,
            () => Clip(cut: false), "Copy selection") + 2;

        IconButton(spriteBatch, AuthoringSkin.Paste, x, y, 32, alpha, Paste, "Paste");

        y += BUTTON_HEIGHT + 4;
        x = area.X + 8;

        // Change case, then paragraph settings.
        x += Button(spriteBatch, "aa", x, y, 32, alpha, () => Case(TextCase.Lower), false, "lower case") + 2;
        x += Button(spriteBatch, "AA", x, y, 32, alpha, () => Case(TextCase.Upper), false, "UPPER CASE") + 2;
        x += Button(spriteBatch, "Aa", x, y, 32, alpha, () => Case(TextCase.Title), false, "Title Case") + 2;
        x += Button(spriteBatch, "A.", x, y, 32, alpha, () => Case(TextCase.Sentence), false,
            "Sentence case  (Shift+F3 cycles)") + 8;

        if (block is not null)
        {
            x += Button(spriteBatch, block.Align.ToString(), x, y, 72, alpha,
                () => { block.Align = Next(block.Align); composed = null; }, false, "Line alignment") + 2;

            x += Toggle(spriteBatch, "cap", x, y, 42, alpha, block.DropCap, true,
                () => { block.DropCap = !block.DropCap; composed = null; }, "Drop cap on the first letter") + 8;
        }

        x += Button(spriteBatch, "key", x, y, 42, alpha, () => OpenPrompt(EditorField.BookKey), false,
            "Registry key and localization scope") + 2;

        x += Button(spriteBatch, "title", x, y, 46, alpha, () => OpenPrompt(EditorField.BookTitle), false,
            "Book title") + 8;

        IconButton(spriteBatch, AuthoringSkin.Save, x, y, 40, alpha, () => session.Export(),
            "Export .hjson and .cs to the configured source directory");
    }

    /// <summary>
    ///     Applies emphasis to the selection, or to the whole block when there is none.
    /// </summary>
    /// <remarks>
    ///     Selecting nothing and pressing bold does what it always did -- the paragraph turns bold --
    ///     so the control the writer already knows keeps working. Selecting a word narrows the same
    ///     control to that word. Character formatting stacks on top of the block's, so bolding a word
    ///     inside an italic paragraph gives bold italic rather than replacing the slant.
    /// </remarks>
    private void Emphasise(Func<CharFormat, CharFormat> character, Action<AuthoredBlock> whole)
    {
        if (session.Buffer.HasSelection && !session.Focus.IsShort())
        {
            session.Buffer.ApplyFormat(character);
            session.Commit();
        }
        else if (session.Selected is { } block)
        {
            whole(block);
        }

        composed = null;
        layout = null;
    }

    /// <summary>Whether the toggle should read as lit, for whichever scope it would act on.</summary>
    private bool EmphasisOn(Func<CharFormat, bool> character, Func<AuthoredBlock, bool> whole)
    {
        if (session.Buffer.HasSelection && !session.Focus.IsShort())
            return session.Buffer.SelectionAll(character);

        return session.Selected is { } block && whole(block);
    }

    private string TypefaceName()
    {
        AuthoredTypeface choice = session.Selected?.Typeface ?? AuthoredTypeface.Inherit;

        return choice == AuthoredTypeface.Inherit ? $"({session.Book.Typeface})" : choice.ToString();
    }

    private void Resize(float delta)
    {
        // A selection resizes just those characters; otherwise the block's own size moves.
        if (session.Buffer.HasSelection && !session.Focus.IsShort())
        {
            float from = session.Buffer.ActiveFormat is { HasScale: true, Scale: var current }
                ? current
                : session.EffectiveScale;

            session.Buffer.ApplyFormat(f => f with { Scale = Math.Clamp(from + delta, 0.2f, 3f) });
            session.Commit();
        }
        else
        {
            session.StepScale(delta);
        }

        composed = null;
        layout = null;
    }

    /// <summary>
    ///     The size box: a readout until clicked, an editable field after.
    /// </summary>
    /// <remarks>
    ///     Typed rather than only stepped, because a writer who wants 1.35 should not have to press
    ///     a button nine times to get there. It rides the same buffer as every other field, so
    ///     selection, clipboard and undo behave the same inside it as they do in the prose.
    /// </remarks>
    private int SizeField(SpriteBatch spriteBatch, int x, int y, int width, float alpha)
    {
        var area = new Rectangle(x, y, width, BUTTON_HEIGHT);
        bool focused = session.Focus == EditorField.BlockScale;
        bool hover = area.Contains(CursorPoint);

        AuthoringSkin.DrawButton(spriteBatch, area, hover, focused);

        if (hover) tooltip = "Text size -- click to type one, or use A+ / A-";

        if (focused) DrawInlineBuffer(spriteBatch, area, alpha);
        else Centre(spriteBatch, session.ScaleText, area, Color.White * alpha);

        hotspots.Add((area, () =>
        {
            if (session.Selected is null) return;

            session.FocusField(EditorField.BlockScale);
            session.Buffer.SelectAll();
            layout = null;
        }));

        return width;
    }

    /// <summary>The shared buffer drawn inside a one-line control, with caret and selection.</summary>
    private void DrawInlineBuffer(SpriteBatch spriteBatch, Rectangle area, float alpha)
    {
        EnsureLayout();

        TextBuffer buffer = session.Buffer;
        var origin = new Vector2(area.X + 7, area.Y + (area.Height - layout!.CaretHeight(0)) / 2f);

        if (buffer.HasSelection)
        {
            float left = layout.WidthOfRange(0, buffer.SelectionStart);
            float right = layout.WidthOfRange(0, buffer.SelectionEnd);

            Fill(spriteBatch, new Rectangle((int)(origin.X + left), (int)origin.Y,
                (int)Math.Max(2f, right - left), (int)layout.CaretHeight(0)), new Color(70, 120, 200) * alpha);
        }

        spriteBatch.DrawString(FontAssets.MouseText.Value, buffer.Text, origin, Color.White * alpha,
            0f, Vector2.Zero, CHROME, SpriteEffects.None, 0f);

        if (Main.GameUpdateCount % 40 >= 22) return;

        Fill(spriteBatch, new Rectangle((int)(origin.X + layout.WidthOfRange(0, buffer.Caret)),
            (int)origin.Y, 2, (int)layout.CaretHeight(0)), Color.White * alpha);
    }

    private void Case(TextCase mode)
    {
        session.Buffer.ChangeCase(mode);
        session.Commit();
        composed = null;
        layout = null;
    }

    /// <summary>The face list, sliding open under its button.</summary>
    private void DrawDropdown(SpriteBatch spriteBatch, Rectangle toolbar, float alpha)
    {
        AuthoredTypeface[] all = Enum.GetValues<AuthoredTypeface>();
        int rowHeight = 22;
        int full = all.Length * rowHeight + 8;
        int height = (int)(full * Ease(dropdown));

        if (height < 4) return;

        var area = new Rectangle(toolbar.X + 8, toolbar.Y + 31, 116, height);

        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Inset * alpha);

        for (int i = 0; i < all.Length; i++)
        {
            int y = area.Y + 4 + i * rowHeight;

            if (y + rowHeight > area.Bottom) break;

            AuthoredTypeface choice = all[i];
            var row = new Rectangle(area.X + 4, y, area.Width - 8, rowHeight - 2);
            bool hover = row.Contains(CursorPoint);
            bool active = session.Selected?.Typeface == choice;

            if (hover || active) Fill(spriteBatch, row, (active ? AuthoringSkin.Selection : AuthoringSkin.ButtonHover) * alpha);

            Text(spriteBatch, choice.ToString(), new Vector2(row.X + 6, row.Y + 3), Color.White * alpha, CHROME);

            hotspots.Add((row, () =>
            {
                if (session.Selected is { } block) block.Typeface = choice;

                dropdownOpen = false;
                composed = null;
                layout = null;
            }));
        }
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private void DrawRail(SpriteBatch spriteBatch, Rectangle area, float alpha)
    {
        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Panel * alpha);

        int x = area.X + 8;
        int y = area.Y + 8;
        int cell = (area.Width - 22) / 4;
        int column = 0;

        foreach (AuthoredBlockKind kind in Enum.GetValues<AuthoredBlockKind>())
        {
            AuthoredBlockKind captured = kind;

            Button(spriteBatch, Short(kind), x + column * (cell + 2), y, cell, alpha,
                () => { session.AddBlock(captured); composed = null; layout = null; });

            if (++column < 4) continue;

            column = 0;
            y += 26;
        }

        if (column != 0) y += 26;

        int bx = x;

        bx += Button(spriteBatch, "del", bx, y, 44, alpha,
            () => { session.DeleteBlock(); composed = null; layout = null; }) + 3;

        bx += Button(spriteBatch, "up", bx, y, 40, alpha,
            () => { session.MoveBlock(-1); composed = null; }) + 3;

        Button(spriteBatch, "dn", bx, y, 40, alpha, () => { session.MoveBlock(1); composed = null; });

        y += 32;

        for (int i = 0; i < session.Book.Blocks.Count && y < area.Bottom - 22; i++)
        {
            AuthoredBlock block = session.Book.Blocks[i];
            var row = new Rectangle(x, y, area.Width - 16, 20);
            bool selected = i == session.SelectedIndex;

            if (selected) Fill(spriteBatch, row, AuthoringSkin.Selection * (alpha * 0.8f));

            int captured = i;

            hotspots.Add((row, () => { session.SelectBlock(captured); layout = null; composed = null; }));

            Text(spriteBatch, block.HasText ? $"{Short(block.Kind)}  {block.Key}" : Short(block.Kind),
                new Vector2(row.X + 6, row.Y + 2), (selected ? Color.White : Color.LightGray) * alpha, CHROME);

            y += 22;
        }
    }

    private static string Short(AuthoredBlockKind kind) => kind switch
    {
        AuthoredBlockKind.PageBreak => "Brk",
        AuthoredBlockKind.Chapter   => "Chap",
        AuthoredBlockKind.Heading   => "Head",
        AuthoredBlockKind.Caption   => "Capt",
        _                           => kind.ToString(),
    };

    #endregion

    #region Overlays

    private void OpenPrompt(EditorField field)
    {
        prompt = field;
        session.FocusField(field);
        session.Buffer.SelectAll();
        layout = null;
    }

    private void ClosePrompt()
    {
        prompt = null;
        LeaveShortField();
    }

    /// <summary>Commits a one-line field and puts the buffer back on the prose.</summary>
    private void LeaveShortField()
    {
        if (!session.Focus.IsShort()) return;

        // FocusField commits the outgoing field on the way out, so the value is not lost.
        session.FocusField(EditorField.BlockText);
        composed = null;
        layout = null;
    }

    /// <summary>
    ///     A modal for the short fields, so the book's identity is not sitting in the toolbar where
    ///     it can be typed into by accident.
    /// </summary>
    private void DrawPrompt(SpriteBatch spriteBatch, Rectangle screen, float alpha)
    {
        var area = new Rectangle(screen.X + screen.Width / 2 - 190, screen.Y + screen.Height / 2 - 52, 380, 104);

        Fill(spriteBatch, screen, Color.Black * (alpha * 0.4f));
        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Panel * alpha);

        string label = prompt == EditorField.BookKey
            ? "Book key -- the registry key and localization scope"
            : "Book title";

        Text(spriteBatch, label, new Vector2(area.X + 14, area.Y + 12), Color.LightGray * alpha, CHROME);

        var box = new Rectangle(area.X + 14, area.Y + 36, area.Width - 28, 28);

        AuthoringSkin.DrawPanel(spriteBatch, box, AuthoringSkin.Inset * alpha);
        DrawField(spriteBatch, box, alpha);

        Button(spriteBatch, "done", area.Right - 82, area.Bottom - 32, 68, alpha, ClosePrompt);
    }

    /// <summary>The shared buffer rendered as one line, with its caret and selection.</summary>
    private void DrawField(SpriteBatch spriteBatch, Rectangle box, float alpha)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        TextBuffer buffer = session.Buffer;
        var origin = new Vector2(box.X + 8, box.Y + 5);

        EnsureLayout();

        if (buffer.HasSelection)
        {
            float left = layout!.WidthOfRange(0, buffer.SelectionStart);
            float right = layout.WidthOfRange(0, buffer.SelectionEnd);

            Fill(spriteBatch, new Rectangle((int)(origin.X + left), (int)origin.Y,
                (int)Math.Max(2f, right - left), (int)layout.CaretHeight(0)), new Color(70, 120, 200) * alpha);
        }

        spriteBatch.DrawString(font, buffer.Text, origin, Color.White * alpha,
            0f, Vector2.Zero, CHROME, SpriteEffects.None, 0f);

        if (Main.GameUpdateCount % 40 >= 22) return;

        Fill(spriteBatch, new Rectangle((int)(origin.X + layout!.WidthOfRange(0, buffer.Caret)),
            (int)origin.Y, 2, (int)layout.CaretHeight(0)), Color.White * alpha);
    }

    /// <summary>Copy or cut the selection to the system clipboard.</summary>
    private void Clip(bool cut)
    {
        TextBuffer buffer = session.Buffer;

        if (!buffer.HasSelection) return;

        ReLogic.OS.Platform.Get<ReLogic.OS.IClipboard>().Value = cut ? buffer.CutSelection() : buffer.SelectedText;

        if (!cut) return;

        session.Commit();
        composed = null;
        layout = null;
    }

    private void Paste()
    {
        string clip = ReLogic.OS.Platform.Get<ReLogic.OS.IClipboard>().MultiLineValue ?? string.Empty;

        if (clip.Length == 0) return;

        session.Buffer.Insert(clip.Replace("\r\n", "\n").Replace('\r', '\n'));
        session.Commit();
        composed = null;
        layout = null;
    }

    private int Swatch(SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        var area = new Rectangle(x, y, 46, BUTTON_HEIGHT);
        bool hover = area.Contains(CursorPoint);

        AuthoringSkin.DrawButton(spriteBatch, area, hover);

        if (hover) tooltip = "Ink colour for this block";

        Color ink = session.Selected?.Ink is { } packed
            ? new Color((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF)
            : AuthoringSkin.Ink;

        Fill(spriteBatch, new Rectangle(area.X + 7, area.Y + 8, area.Width - 14, area.Height - 16), ink * alpha);

        hotspots.Add((area, () =>
        {
            colorOpen = !colorOpen;

            if (colorOpen && session.Selected?.Ink is { } current)
                colorWheel.SetColor(new Color((current >> 16) & 0xFF, (current >> 8) & 0xFF, current & 0xFF));
        }));

        return area.Width;
    }

    private void DrawColorPicker(SpriteBatch spriteBatch, Rectangle screen, float alpha)
    {
        var area = new Rectangle(screen.X + screen.Width / 2 - 150, screen.Y + screen.Height / 2 - 110, 300, 220);

        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Panel * alpha);

        Text(spriteBatch, "Ink", new Vector2(area.X + 14, area.Y + 10), Color.LightGray * alpha, CHROME);

        colorRect = new Rectangle(area.X + 14, area.Y + 34, area.Width - 28, 140);

        colorWheel.Draw(spriteBatch, colorRect, alpha);

        Button(spriteBatch, "reset", area.X + 14, area.Bottom - 34, 62, alpha, () =>
        {
            if (session.Selected is { } block) block.Ink = null;

            composed = null;
        });

        Button(spriteBatch, "done", area.Right - 76, area.Bottom - 34, 62, alpha, () => colorOpen = false);
    }

    /// <summary>Collapsible keybind reference, anchored bottom right.</summary>
    private void DrawLegend(SpriteBatch spriteBatch, Rectangle screen, float alpha)
    {
        (string Key, string What)[] entries =
        [
            ("Ctrl+C / X / V", "copy, cut, paste"),
            ("Ctrl+Z / Y", "undo, redo"),
            ("Ctrl+A", "select all"),
            ("Ctrl+Left / Right", "move by word"),
            ("Home / End", "line ends"),
            ("Ctrl+Home / End", "block ends"),
            ("Shift+F3", "cycle case"),
            ("Double click", "select word"),
            ("Wheel", "turn the page"),
            ("Esc", "back out, then close"),
        ];

        const int ROW = 26;
        const float SCALE = 0.9f;

        int width = 330;
        int full = entries.Length * ROW + 20;
        int height = (int)(full * Ease(legend));

        // The gear is the handle. It turns while the panel moves, which reads as the panel being
        // cranked open rather than simply appearing.
        var handle = new Rectangle(screen.Right - 56, screen.Bottom - 68, 44, 44);
        bool hoverHandle = handle.Contains(CursorPoint);

        if (hoverHandle) tooltip = legendOpen ? "Hide keys" : "Show keys";

        Texture2D gear = AuthoringSkin.Gear;

        spriteBatch.Draw(gear,
            new Vector2(handle.X + handle.Width / 2f, handle.Y + handle.Height / 2f),
            null,
            (hoverHandle ? Color.White : new Color(200, 208, 235)) * alpha,
            Ease(legend) * MathHelper.Pi,
            new Vector2(gear.Width, gear.Height) / 2f,
            handle.Width / (float)Math.Max(1, gear.Width),
            SpriteEffects.None,
            0f);

        hotspots.Add((handle, () => legendOpen = !legendOpen));

        if (height < 8) return;

        var area = new Rectangle(screen.Right - width - 12, handle.Y - height - 6, width, height);

        AuthoringSkin.DrawPanel(spriteBatch, area, AuthoringSkin.Inset * alpha);

        for (int i = 0; i < entries.Length; i++)
        {
            int y = area.Y + 10 + i * ROW;

            if (y + ROW > area.Bottom) break;

            (string key, string what) = entries[i];

            int x = area.X + 12;

            // Shift and Backspace get their on-screen-keyboard glyphs; the rest stay as text.
            if (key.StartsWith("Shift", StringComparison.Ordinal))
                x += Glyph(spriteBatch, AuthoringSkin.KeyShift, x, y, alpha);
            else if (key.Contains("Back", StringComparison.Ordinal))
                x += Glyph(spriteBatch, AuthoringSkin.KeyBackspace, x, y, alpha);

            Text(spriteBatch, key, new Vector2(x, y + 2), new Color(176, 188, 228) * alpha, SCALE);
            Text(spriteBatch, what, new Vector2(area.X + 172, y + 2), new Color(132, 140, 168) * alpha, SCALE);
        }
    }

    /// <summary>A key glyph inline in the legend, scaled to the row.</summary>
    private static int Glyph(SpriteBatch spriteBatch, Texture2D icon, int x, int y, float alpha)
    {
        const int size = 18;

        spriteBatch.Draw(icon, new Rectangle(x, y + 2, size, size), Color.White * alpha);

        return size + 4;
    }

    #endregion

    #region Widgets

    private static T Next<T>(T value) where T : struct, Enum
    {
        T[] all = Enum.GetValues<T>();

        return all[(Array.IndexOf(all, value) + 1) % all.Length];
    }

    private int Button(SpriteBatch spriteBatch, string label, int x, int y, int width, float alpha,
        Action? act, bool active = false, string? tip = null)
    {
        var area = new Rectangle(x, y, width, BUTTON_HEIGHT);
        bool hover = act is not null && area.Contains(CursorPoint);

        AuthoringSkin.DrawButton(spriteBatch, area, hover, active);
        Centre(spriteBatch, label, area, Color.White * alpha);

        if (hover && tip is not null) tooltip = tip;
        if (act is not null) hotspots.Add((area, act));

        return width;
    }

    /// <summary>A button that shows its state, and greys out when the face cannot express it.</summary>
    private int Toggle(SpriteBatch spriteBatch, string label, int x, int y, int width, float alpha,
        bool on, bool enabled, Action act, string? tip = null)
    {
        var area = new Rectangle(x, y, width, BUTTON_HEIGHT);
        bool hover = enabled && area.Contains(CursorPoint);

        AuthoringSkin.DrawButton(spriteBatch, area, hover, on && enabled);
        Centre(spriteBatch, label, area, (enabled ? Color.White : new Color(110, 110, 130)) * alpha);

        if (area.Contains(CursorPoint) && tip is not null)
            tooltip = enabled ? tip : $"{tip} -- this face has no such weight";

        if (enabled) hotspots.Add((area, act));

        return width;
    }

    /// <summary>An icon on a plate, for the controls that read better as a glyph than a word.</summary>
    private int IconButton(SpriteBatch spriteBatch, Texture2D icon, int x, int y, int width, float alpha,
        Action act, string tip, float iconScale = 1f)
    {
        var area = new Rectangle(x, y, width, BUTTON_HEIGHT);
        bool hover = area.Contains(CursorPoint);

        AuthoringSkin.DrawIconOnPlate(spriteBatch, icon, area, hover, iconScale);

        if (hover) tooltip = tip;

        hotspots.Add((area, act));

        return width;
    }

    private void Arrow(SpriteBatch spriteBatch, Texture2D icon, int x, int y, float alpha, Action act, string tip)
    {
        var area = new Rectangle(x, y, 26, 26);
        bool hover = area.Contains(CursorPoint);

        AuthoringSkin.DrawIconButton(spriteBatch, icon, area, hover);

        if (hover) tooltip = tip;

        hotspots.Add((area, act));
    }

    /// <summary>
    ///     The hovered control's explanation, drawn last so it sits over everything.
    /// </summary>
    /// <remarks>
    ///     Drawn here rather than through <c>Main.hoverItemName</c>: the desk is a fancy UI state, and
    ///     vanilla's mouse text is composited by a layer that does not run while one is up.
    /// </remarks>
    private void DrawTooltip(SpriteBatch spriteBatch, Rectangle screen, float alpha)
    {
        if (tooltip is null) return;

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        Vector2 size = font.MeasureString(tooltip) * CHROME;
        int width = (int)size.X + 18;
        int height = (int)size.Y + 10;

        // Nudged off the cursor, then pulled back inside the screen so it never clips off an edge.
        int x = Math.Min((int)Cursor.X + 18, screen.Right - width - 4);
        int y = Math.Min((int)Cursor.Y + 20, screen.Bottom - height - 4);

        AuthoringSkin.DrawPanel(spriteBatch, new Rectangle(x, y, width, height), AuthoringSkin.Inset * alpha);
        Text(spriteBatch, tooltip, new Vector2(x + 9, y + 5), Color.White * alpha, CHROME);
    }

    private static void Centre(SpriteBatch spriteBatch, string label, Rectangle area, Color color)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        Vector2 size = font.MeasureString(label) * CHROME;

        spriteBatch.DrawString(font, label,
            new Vector2(area.X + (area.Width - size.X) / 2f, area.Y + (area.Height - size.Y) / 2f + 1f),
            color, 0f, Vector2.Zero, CHROME, SpriteEffects.None, 0f);
    }

    private static void Fill(SpriteBatch spriteBatch, Rectangle area, Color color) =>
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, color);

    private static void Text(SpriteBatch spriteBatch, string value, Vector2 position, Color color, float scale) =>
        spriteBatch.DrawString(FontAssets.MouseText.Value, value, position, color,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    #endregion
}
