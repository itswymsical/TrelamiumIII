using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Core.Books.Authoring;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>
///     The structure editor's tool strip, tool options, palette and status bar. Rebuilt every draw like the authoring desk:
///     controls register hotspots, and clicks go to the topmost one. The rest of the screen is the canvas.
/// </summary>
internal sealed class StructureEditorUI : UIState
{
    private const float TextScale = 0.8f;
    private const int Margin = 12;
    private const int ToolSize = 36;
    private const int ToolGap = 3;
    private const int GroupGap = 8;
    private const int Columns = 6;
    private const int SlotSize = 42;
    private const int Padding = 10;
    private const int PanelWidth = Columns * SlotSize + Padding * 2;
    private const int StatusHeight = 28;
    private const int HeaderHeight = 20;
    private const int ChipHeight = 22;
    private const int FieldWidth = 52;
    private const int AnchorCell = 10;

    /// <summary>Side of a generated icon, in icon pixels; each is drawn as a square of screen pixels.</summary>
    private const int IconGrid = 16;

    private const string IconPath = "TrelamiumIII/Content/DevTools/StructureLab/Editor/Icons/";

    private static readonly Color SecondaryRing = new(255, 190, 60);
    private static readonly Color Muted = new(170, 175, 200);
    private static readonly Color IconInk = new(28, 28, 38);
    private static readonly Color IconLight = new(235, 235, 245);
    private static readonly Color IconAccent = new(235, 135, 45);

    private readonly List<(Rectangle Area, Action Primary, Action? Secondary)> hotspots = [];
    private readonly Dictionary<string, Asset<Texture2D>> icons = [];
    private readonly Dictionary<(ShapeSettings, bool), bool[,]> shapeIcons = [];
    private readonly List<GridRow> rows = [];

    private Rectangle toolbar;
    private Rectangle options;
    private Rectangle panel;
    private Rectangle status;
    private Rectangle grid;
    private Rectangle searchBox;
    private string? tooltip;
    private string search = string.Empty;

    private TextField focus;
    private string widthText = string.Empty;
    private string heightText = string.Empty;

    /// <summary>Which part of the canvas a typed size keeps in place, 0 to 2 per axis.</summary>
    private Point anchor = new(1, 1);

    /// <summary>The category shown, or null for all of them.</summary>
    private string? category;

    private int firstRow;
    private int maxFirstRow;
    private (EditorChannel Channel, string? Category, string Search, int Height) rowsBuiltFor;

    private enum TextField
    {
        None,
        Search,
        Width,
        Height
    }

    /// <summary>A section title, or one row of slots.</summary>
    private readonly record struct GridRow(string? Header, PaletteEntry[]? Entries)
    {
        public int Height => Header is null ? SlotSize : HeaderHeight;
    }

    public bool TextFocused => focus != TextField.None;

    public bool PanelsHidden { get; set; }

    public bool IsMouseOverPanel
    {
        get
        {
            Point cursor = Cursor;
            return status.Contains(cursor) || (!PanelsHidden && (toolbar.Contains(cursor) || options.Contains(cursor) || panel.Contains(cursor)));
        }
    }

    /// <summary>The mouse in UI coordinates; the same in update and draw, unlike <see cref="Main.MouseScreen" />.</summary>
    private static Point Cursor => new((int)(PlayerInput.MouseX / Main.UIScale), (int)(PlayerInput.MouseY / Main.UIScale));

    #region Input
    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        // A field's own hotspot focuses it again.
        focus = TextField.None;

        if (Hotspot(Cursor) is { } hit)
        {
            hit.Primary();
            return;
        }

        widthText = heightText = string.Empty;
    }

    public override void RightMouseDown(UIMouseEvent evt)
    {
        base.RightMouseDown(evt);
        Hotspot(Cursor)?.Secondary?.Invoke();
    }

    public override void ScrollWheel(UIScrollWheelEvent evt)
    {
        base.ScrollWheel(evt);

        int steps = Math.Sign(evt.ScrollWheelValue);

        if (!PanelsHidden && grid.Contains(Cursor))
            firstRow = Math.Clamp(firstRow - steps, 0, maxFirstRow);
        else if (!IsMouseOverPanel)
            StructureEditorMode.Zoom(steps);
    }

    /// <summary>
    ///     Typing into the focused field. Escape leaves it; Enter leaves it and applies a typed canvas size; Tab moves between
    ///     the size fields. Called by the mode instead of its shortcuts.
    /// </summary>
    public void UpdateText()
    {
        bool sizeField = focus is TextField.Width or TextField.Height;

        if (Pressed(Keys.Escape))
        {
            if (sizeField)
                widthText = heightText = string.Empty;

            focus = TextField.None;
            return;
        }

        if (Pressed(Keys.Enter))
        {
            focus = TextField.None;

            if (sizeField)
                ApplyCanvasSize();

            return;
        }

        if (sizeField && Pressed(Keys.Tab))
        {
            focus = focus == TextField.Width ? TextField.Height : TextField.Width;
            return;
        }

        PlayerInput.WritingText = true;
        Main.instance.HandleIME();

        string current = focus switch
        {
            TextField.Search => search,
            TextField.Width => widthText,
            _ => heightText
        };

        string text = Main.GetInputText(current);

        if (sizeField)
            text = string.Concat(text.Where(char.IsAsciiDigit).Take(4));

        if (text == current)
            return;

        switch (focus)
        {
            case TextField.Search:
                search = text;
                firstRow = 0;
                break;

            case TextField.Width:
                widthText = text;
                break;

            default:
                heightText = text;
                break;
        }
    }

    /// <summary>Resizes the canvas to the typed size, a blank field keeping that side, around <see cref="anchor" />.</summary>
    private void ApplyCanvasSize()
    {
        Rectangle canvas = StructureEditor.Canvas;
        int width = int.TryParse(widthText, out int typedWidth) && typedWidth > 0 ? typedWidth : canvas.Width;
        int height = int.TryParse(heightText, out int typedHeight) && typedHeight > 0 ? typedHeight : canvas.Height;

        widthText = heightText = string.Empty;
        focus = TextField.None;

        if (width > StructureEditor.MaxWidth || height > StructureEditor.MaxHeight)
        {
            StructureEditorMode.Notify($"The lab holds canvases up to {StructureEditor.MaxWidth} x {StructureEditor.MaxHeight}.");
            return;
        }

        StructureEditorMode.ResizeCanvas(StructureEditor.Anchored(width, height, anchor));
    }

    private (Rectangle Area, Action Primary, Action? Secondary)? Hotspot(Point point)
    {
        // Walked backwards, so the topmost drawn control gets the click.
        for (int i = hotspots.Count - 1; i >= 0; i--)
        {
            if (hotspots[i].Area.Contains(point))
                return hotspots[i];
        }

        return null;
    }

    private static bool Pressed(Keys key) => Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
    #endregion

    #region Frame
    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        hotspots.Clear();
        tooltip = null;

        CalculatedStyle dimensions = GetDimensions();
        var screen = new Rectangle((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);

        status = new Rectangle(screen.X + Margin, screen.Bottom - Margin - StatusHeight, screen.Width - Margin * 2, StatusHeight);

        if (PanelsHidden)
        {
            toolbar = options = panel = grid = searchBox = Rectangle.Empty;
        }
        else
        {
            DrawToolbar(spriteBatch, screen);
            DrawOptions(spriteBatch);
            DrawPalette(spriteBatch, screen);
        }

        DrawStatus(spriteBatch);
        DrawTooltip(spriteBatch, screen);
    }
    #endregion

    #region Toolbar
    private void DrawToolbar(SpriteBatch spriteBatch, Rectangle screen)
    {
        EditorTool[] tools = Enum.GetValues<EditorTool>();
        int buttons = tools.Length + 7;
        int height = buttons * (ToolSize + ToolGap) - ToolGap + GroupGap * 2;
        int x = screen.X + Margin;
        int y = screen.Y + Margin;

        toolbar = new Rectangle(x - 5, y - 5, ToolSize + 10, height + 10);
        VFXLabUIAssets.DrawPanel(spriteBatch, toolbar, VFXLabUIAssets.Panel * 0.95f);

        foreach (EditorTool tool in tools)
        {
            EditorTool captured = tool;
            string tip = $"{Label(tool)} ({tool.Shortcut()})";

            if (tool == EditorTool.Shape)
            {
                ShapeButton(spriteBatch, new Rectangle(x, y, ToolSize, ToolSize), ShapeIcon(StructureEditorMode.CurrentShape, false), IconLight,
                    StructureEditorMode.Tool == tool, () => StructureEditorMode.Tool = captured, null, tip);

                y += ToolSize + ToolGap;
                continue;
            }

            IconButton(spriteBatch, ref y, x, Icon(tool.ToString()), StructureEditorMode.Tool == tool, () => StructureEditorMode.Tool = captured, tip);
        }

        y += GroupGap;

        IconButton(spriteBatch, ref y, x, Icon("Filled"), StructureEditorMode.FilledShapes,
            () => StructureEditorMode.FilledShapes = !StructureEditorMode.FilledShapes, "Filled shapes and closed curves (F)");

        TextButton(spriteBatch, new Rectangle(x, y, ToolSize, ToolSize), StructureEditorMode.BrushSize.ToString(), false,
            () => StructureEditorMode.ResizeBrush(1), () => StructureEditorMode.ResizeBrush(-1),
            "Brush size. Left click grows it, right click shrinks it ([ and ])");

        y += ToolSize + ToolGap + GroupGap;

        IconButton(spriteBatch, ref y, x, VFXLabUIAssets.BackArrow, false, StructureEditorMode.Undo,
            StructureEditorMode.CurveInProgress ? "Remove the curve's last node (Ctrl+Z)" : "Undo (Ctrl+Z)",
            StructureEditorMode.History.CanUndo || StructureEditorMode.CurveInProgress);

        IconButton(spriteBatch, ref y, x, VFXLabUIAssets.ForwardArrow, false, StructureEditorMode.Redo, "Redo (Ctrl+Y)",
            StructureEditorMode.History.CanRedo && !StructureEditorMode.CurveInProgress);

        IconButton(spriteBatch, ref y, x, TextureAssets.Tile[ModContent.TileType<StructureVoidTile>()].Value, false,
            StructureEditorMode.VoidOutside, "Void outside: mark every empty cell joined to the canvas edge as void", iconSource: new Rectangle(0, 0, 16, 16));

        IconButton(spriteBatch, ref y, x, VFXLabUIAssets.Save, false, StructureEditorMode.Save, "Save to Assets/Structures (Ctrl+S)");

        var build = new Rectangle(x, y, ToolSize, ToolSize);
        bool hover = build.Contains(Cursor);

        VFXLabUIAssets.DrawButton(spriteBatch, build, hover);
        DrawItem(spriteBatch, ItemID.CopperPickaxe, build);
        Register(build, hover, StructureEditorMode.RequestExit, null, "Build with vanilla tools (~ or Esc)");
    }

    private void IconButton(SpriteBatch spriteBatch, ref int y, int x, Texture2D icon, bool active, Action act, string tip,
        bool enabled = true, Rectangle? iconSource = null)
    {
        var area = new Rectangle(x, y, ToolSize, ToolSize);
        bool hover = enabled && area.Contains(Cursor);

        VFXLabUIAssets.DrawButton(spriteBatch, area, hover, active);

        Rectangle source = iconSource ?? icon.Bounds;
        int scale = Math.Max(1, (ToolSize - 4) / Math.Max(source.Width, source.Height));
        var target = new Rectangle(area.Center.X - source.Width * scale / 2, area.Center.Y - source.Height * scale / 2,
            source.Width * scale, source.Height * scale);

        spriteBatch.Draw(icon, target, source, Color.White * (enabled ? 1f : 0.35f));

        if (enabled)
            Register(area, hover, act, null, tip);
        else if (area.Contains(Cursor))
            tooltip = tip;

        y += ToolSize + ToolGap;
    }

    private static string Label(EditorTool tool) => tool switch
    {
        EditorTool.Pencil => "Pencil",
        EditorTool.Eraser => "Eraser: left clears, right voids",
        EditorTool.Line => "Line (Shift snaps to 45 degrees)",
        EditorTool.Curve => "Curve: click for corners, drag for curves. Click the first node to close it, Enter to finish",
        EditorTool.Shape => $"Shape: {StructureEditorMode.Shape}. Press again for the next shape, O for an ellipse",
        EditorTool.Fill => "Fill",
        EditorTool.Eyedropper => "Eyedropper (or hold Alt)",
        _ => "Canvas: drag the frame's edges and corners to resize it, or type a size"
    };
    #endregion

    #region Tool options
    /// <summary>The bar beside the tool strip with the current tool's settings; none for tools without any.</summary>
    private void DrawOptions(SpriteBatch spriteBatch)
    {
        options = Rectangle.Empty;

        int x = toolbar.Right + 6 + 5;
        int y = toolbar.Y + 5;

        switch (StructureEditorMode.Tool)
        {
            case EditorTool.Shape:
                DrawShapeOptions(spriteBatch, x, y);
                break;

            case EditorTool.Canvas:
                DrawCanvasOptions(spriteBatch, x, y);
                break;
        }
    }

    private void DrawShapeOptions(SpriteBatch spriteBatch, int x, int y)
    {
        ShapeKind[] kinds = Enum.GetValues<ShapeKind>();
        ShapeKind current = StructureEditorMode.Shape;
        ShapeAmount? amount = current.Amount();
        int amountWidth = amount is null ? 0 : (int)(FontAssets.MouseText.Value.MeasureString(AmountLabel(amount.Value)).X * TextScale) + 20;
        int width = kinds.Length * (ToolSize + ToolGap) - ToolGap + GroupGap + ToolSize + (amount is null ? 0 : ToolGap + amountWidth);

        OpenOptions(spriteBatch, x, y, width);

        foreach (ShapeKind kind in kinds)
        {
            ShapeKind captured = kind;
            var settings = new ShapeSettings(kind, StructureEditorMode.Facing, StructureEditorMode.AmountOf(kind));

            ShapeButton(spriteBatch, new Rectangle(x, y, ToolSize, ToolSize), ShapeIcon(settings, false), IconLight, current == kind,
                () => StructureEditorMode.Shape = captured, null, kind.ToString());

            x += ToolSize + ToolGap;
        }

        x += GroupGap - ToolGap;

        ShapeFacing facing = StructureEditorMode.Facing;

        ShapeButton(spriteBatch, new Rectangle(x, y, ToolSize, ToolSize), ShapeIcon(new ShapeSettings(ShapeKind.Triangle, facing, 0), true),
            IconAccent, false, () => StructureEditorMode.TurnShape(1), () => StructureEditorMode.TurnShape(-1),
            $"Points {facing.ToString().ToLowerInvariant()}. Left click turns it clockwise, right click the other way (R, Shift+R)",
            current.HasFacing());

        if (amount is not { } value)
            return;

        x += ToolSize + ToolGap;

        TextButton(spriteBatch, new Rectangle(x, y, amountWidth, ToolSize), AmountLabel(value), false,
            () => StructureEditorMode.ChangeShapeAmount(1), () => StructureEditorMode.ChangeShapeAmount(-1),
            $"{value.Label}. Left click raises it, right click lowers it (, and .)");
    }

    private static string AmountLabel(ShapeAmount amount)
        => $"{amount.Label} {StructureEditorMode.AmountOf(StructureEditorMode.Shape)}{(amount.Percent ? "%" : string.Empty)}";

    private void DrawCanvasOptions(SpriteBatch spriteBatch, int x, int y)
    {
        const int labelWidth = 18;
        const int applyWidth = 58;
        int anchorSize = AnchorCell * 3 + 4;
        int width = (labelWidth + FieldWidth) * 2 + 6 + GroupGap + anchorSize + GroupGap + applyWidth;

        OpenOptions(spriteBatch, x, y, width);

        Rectangle size = StructureEditorMode.CanvasPreview;
        int fieldY = y + (ToolSize - 26) / 2;

        Text(spriteBatch, "W", new Vector2(x + 2, fieldY + 5), Muted);
        x += labelWidth;
        SizeField(spriteBatch, new Rectangle(x, fieldY, FieldWidth, 26), TextField.Width, widthText, size.Width);
        x += FieldWidth + 6;

        Text(spriteBatch, "H", new Vector2(x + 2, fieldY + 5), Muted);
        x += labelWidth;
        SizeField(spriteBatch, new Rectangle(x, fieldY, FieldWidth, 26), TextField.Height, heightText, size.Height);
        x += FieldWidth + GroupGap;

        DrawAnchor(spriteBatch, x, y + (ToolSize - anchorSize) / 2);
        x += anchorSize + GroupGap;

        TextButton(spriteBatch, new Rectangle(x, fieldY, applyWidth, 26), "Apply", false, ApplyCanvasSize, null,
            "Resize the canvas to the typed size (Enter in a field)");
    }

    private void SizeField(SpriteBatch spriteBatch, Rectangle area, TextField field, string typed, int current)
    {
        bool focused = focus == field;
        bool hover = area.Contains(Cursor);

        VFXLabUIAssets.DrawPanel(spriteBatch, area, (focused ? VFXLabUIAssets.ButtonIdle : VFXLabUIAssets.Inset) * (hover ? 1.1f : 1f));

        bool caret = focused && Main.GameUpdateCount / 30 % 2 == 0;
        bool showTyped = focused || typed.Length > 0;
        string shown = showTyped ? typed + (caret ? "|" : string.Empty) : current.ToString();

        Text(spriteBatch, shown, new Vector2(area.X + 7, area.Y + 5), showTyped && typed.Length > 0 ? SecondaryRing : Color.White);

        Register(area, hover, () => focus = field, () =>
        {
            if (field == TextField.Width)
                widthText = string.Empty;
            else
                heightText = string.Empty;
        }, $"{field} in tiles. Type a size, Enter applies it, Tab moves to the other field. Right click clears it");
    }

    /// <summary>A 3x3 grid picking the part of the canvas that a typed size keeps in place.</summary>
    private void DrawAnchor(SpriteBatch spriteBatch, int x, int y)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        for (int column = 0; column < 3; column++)
        {
            for (int row = 0; row < 3; row++)
            {
                var cell = new Point(column, row);
                var area = new Rectangle(x + column * (AnchorCell + 2), y + row * (AnchorCell + 2), AnchorCell, AnchorCell);
                bool hover = area.Contains(Cursor);
                bool chosen = anchor == cell;

                spriteBatch.Draw(pixel, area, chosen ? VFXLabUIAssets.Selection : hover ? VFXLabUIAssets.ButtonHover : VFXLabUIAssets.ButtonIdle);
                Register(area, hover, () => anchor = cell, null, "Anchor: the part of the canvas a typed size keeps in place");
            }
        }
    }

    private void OpenOptions(SpriteBatch spriteBatch, int x, int y, int width)
    {
        options = new Rectangle(x - 5, y - 5, width + 10, ToolSize + 10);
        VFXLabUIAssets.DrawPanel(spriteBatch, options, VFXLabUIAssets.Panel * 0.95f);
    }
    #endregion

    #region Palette
    private void DrawPalette(SpriteBatch spriteBatch, Rectangle screen)
    {
        panel = new Rectangle(screen.Right - Margin - PanelWidth, screen.Y + Margin, PanelWidth, status.Y - Margin - screen.Y - Margin);
        VFXLabUIAssets.DrawPanel(spriteBatch, panel, VFXLabUIAssets.Panel * 0.95f);

        int x = panel.X + Padding;
        int width = panel.Width - Padding * 2;
        int y = panel.Y + Padding;

        y = DrawChannels(spriteBatch, x, y, width) + 8;
        y = DrawSwatches(spriteBatch, x, y, width) + 8;

        if (StructureEditorMode.Channel == EditorChannel.Paint)
            y = DrawPaintTarget(spriteBatch, x, y, width) + 8;

        y = DrawCategories(spriteBatch, x, y, width) + 6;
        y = DrawSearch(spriteBatch, x, y, width) + 8;

        grid = new Rectangle(x, y, width, panel.Bottom - Padding - y);
        DrawGrid(spriteBatch);
    }

    private int DrawChannels(SpriteBatch spriteBatch, int x, int y, int width)
    {
        EditorChannel[] channels = Enum.GetValues<EditorChannel>();
        int tabWidth = (width - (channels.Length - 1) * 4) / channels.Length;

        for (int i = 0; i < channels.Length; i++)
        {
            EditorChannel channel = channels[i];
            var area = new Rectangle(x + i * (tabWidth + 4), y, tabWidth, 26);

            TextButton(spriteBatch, area, channel == EditorChannel.Blocks ? "Blocks" : channel.ToString(), StructureEditorMode.Channel == channel,
                () => SelectChannel(channel), null, $"{channel} ({i + 1})");
        }

        return y + 26;
    }

    private void SelectChannel(EditorChannel channel)
    {
        StructureEditorMode.Channel = channel;
        category = null;
        firstRow = 0;
    }

    private int DrawSwatches(SpriteBatch spriteBatch, int x, int y, int width)
    {
        EditorChannel channel = StructureEditorMode.Channel;
        PaletteEntry primary = EditorPalette.Find(channel, StructureEditorMode.Primary);
        PaletteEntry secondary = EditorPalette.Find(channel, StructureEditorMode.Secondary);

        var main = new Rectangle(x, y, 44, 44);
        var back = new Rectangle(x + 30, y + 22, 30, 30);

        VFXLabUIAssets.DrawButton(spriteBatch, back, false);
        DrawSwatch(spriteBatch, secondary, back);
        Ring(spriteBatch, back, SecondaryRing);

        VFXLabUIAssets.DrawButton(spriteBatch, main, false);
        DrawSwatch(spriteBatch, primary, main);
        Ring(spriteBatch, main, VFXLabUIAssets.Selection);

        Text(spriteBatch, $"L  {Fit(primary.Name, width - 110)}", new Vector2(x + 68, y + 4), Color.White);
        Text(spriteBatch, $"R  {Fit(secondary.Name, width - 110)}", new Vector2(x + 68, y + 28), SecondaryRing);

        var swap = new Rectangle(x + width - 26, y + 12, 26, 26);
        TextButton(spriteBatch, swap, "X", false, StructureEditorMode.SwapSwatches, null, "Swap left and right (X)");

        return y + 52;
    }

    private int DrawPaintTarget(SpriteBatch spriteBatch, int x, int y, int width)
    {
        PaintTarget[] targets = Enum.GetValues<PaintTarget>();
        int buttonWidth = (width - (targets.Length - 1) * 4) / targets.Length;

        for (int i = 0; i < targets.Length; i++)
        {
            PaintTarget target = targets[i];

            TextButton(spriteBatch, new Rectangle(x + i * (buttonWidth + 4), y, buttonWidth, 24), target.ToString(),
                StructureEditorMode.PaintTarget == target, () => StructureEditorMode.PaintTarget = target, null, "What the paint channel colours");
        }

        return y + 24;
    }

    /// <summary>"All" and each category that has entries, wrapped into rows.</summary>
    private int DrawCategories(SpriteBatch spriteBatch, int x, int y, int width)
    {
        EditorChannel channel = StructureEditorMode.Channel;
        List<string?> chips = [null, .. EditorPalette.Categories(channel).Where(name => name != EditorPalette.Basics)];

        if (chips.Count <= 2)
            return y - 6;

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        int cursorX = x;

        foreach (string? name in chips)
        {
            string label = name ?? "All";
            int chipWidth = (int)(font.MeasureString(label).X * 0.7f) + 14;

            if (cursorX + chipWidth > x + width)
            {
                cursorX = x;
                y += ChipHeight + 4;
            }

            var area = new Rectangle(cursorX, y, chipWidth, ChipHeight);
            string? captured = name;

            TextButton(spriteBatch, area, label, category == name, () =>
            {
                category = captured;
                firstRow = 0;
            }, null, name is null ? "Every category, in sections" : $"Only {name}", 0.7f);

            cursorX += chipWidth + 4;
        }

        return y + ChipHeight;
    }

    private int DrawSearch(SpriteBatch spriteBatch, int x, int y, int width)
    {
        searchBox = new Rectangle(x, y, width, 26);
        bool hover = searchBox.Contains(Cursor);
        bool focused = focus == TextField.Search;

        VFXLabUIAssets.DrawPanel(spriteBatch, searchBox, (focused ? VFXLabUIAssets.ButtonIdle : VFXLabUIAssets.Inset) * (hover ? 1.1f : 1f));

        bool caret = focused && Main.GameUpdateCount / 30 % 2 == 0;
        string shown = search.Length > 0 || focused ? search + (caret ? "|" : string.Empty) : "Search...";

        Text(spriteBatch, Fit(shown, width - 12), new Vector2(x + 7, y + 5), search.Length > 0 || focused ? Color.White : Muted);

        Register(searchBox, hover, () => focus = TextField.Search, () =>
        {
            search = string.Empty;
            firstRow = 0;
        }, "Search by name. Right click clears it");

        return y + 26;
    }

    private void DrawGrid(SpriteBatch spriteBatch)
    {
        EnsureRows();

        int y = grid.Y;
        firstRow = Math.Clamp(firstRow, 0, maxFirstRow);

        for (int r = firstRow; r < rows.Count && y + rows[r].Height <= grid.Bottom; r++)
        {
            GridRow row = rows[r];

            if (row.Header is { } header)
                Text(spriteBatch, header, new Vector2(grid.X + 2, y + 3), Muted, 0.75f);
            else
                DrawSlots(spriteBatch, row.Entries!, y);

            y += row.Height;
        }

        if (maxFirstRow > 0)
        {
            // Position along the list, as a thin bar at the panel's edge.
            float shown = (float)firstRow / maxFirstRow;
            int barHeight = Math.Max(24, grid.Height / 6);
            var bar = new Rectangle(panel.Right - 5, grid.Y + (int)((grid.Height - barHeight) * shown), 3, barHeight);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bar, Muted * 0.7f);
        }
    }

    private void DrawSlots(SpriteBatch spriteBatch, PaletteEntry[] entries, int y)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            PaletteEntry entry = entries[i];
            var slot = new Rectangle(grid.X + i * SlotSize, y, SlotSize - 2, SlotSize - 2);
            bool hover = slot.Contains(Cursor);

            VFXLabUIAssets.DrawButton(spriteBatch, slot, hover);
            DrawSwatch(spriteBatch, entry, slot);

            if (entry.Swatch == StructureEditorMode.Primary)
                Ring(spriteBatch, slot, VFXLabUIAssets.Selection);
            else if (entry.Swatch == StructureEditorMode.Secondary)
                Ring(spriteBatch, slot, SecondaryRing);

            Register(slot, hover, () => StructureEditorMode.SetSwatch(false, entry.Swatch), () => StructureEditorMode.SetSwatch(true, entry.Swatch),
                $"{entry.Name}\nLeft click for the left button, right click for the right");
        }
    }

    /// <summary>Rebuilds the grid rows when the channel, category, search or panel height changes.</summary>
    private void EnsureRows()
    {
        (EditorChannel, string?, string, int) key = (StructureEditorMode.Channel, category, search, grid.Height);

        if (rows.Count > 0 && rowsBuiltFor == key)
            return;

        rowsBuiltFor = key;
        rows.Clear();

        bool sections = category is null && search.Length == 0;
        string? current = null;
        List<PaletteEntry> pending = [];

        void Flush()
        {
            for (int i = 0; i < pending.Count; i += Columns)
                rows.Add(new GridRow(null, [.. pending.Skip(i).Take(Columns)]));

            pending.Clear();
        }

        foreach (PaletteEntry entry in EditorPalette.Query(StructureEditorMode.Channel, category, search))
        {
            if (sections && entry.Category != current)
            {
                Flush();
                current = entry.Category;

                if (current != EditorPalette.Basics)
                    rows.Add(new GridRow(current, null));
            }

            pending.Add(entry);
        }

        Flush();

        // The furthest the list can scroll: the first row from which the rest fits.
        int fitted = 0;
        maxFirstRow = rows.Count;

        while (maxFirstRow > 0 && fitted + rows[maxFirstRow - 1].Height <= Math.Max(grid.Height, SlotSize))
            fitted += rows[--maxFirstRow].Height;
    }
    #endregion

    #region Status
    private void DrawStatus(SpriteBatch spriteBatch)
    {
        VFXLabUIAssets.DrawPanel(spriteBatch, status, VFXLabUIAssets.Inset * 0.95f);

        Rectangle canvas = StructureEditor.Canvas;
        Point cell = StructureEditorMode.HoverCell;
        EditorChannel channel = StructureEditorMode.Channel;

        string where = StructureEditorMode.HoverOnCanvas ? $"{cell.X - canvas.X}, {cell.Y - canvas.Y}" : "off canvas";
        Rectangle box = StructureEditorMode.ShapeBox;
        string size = box.IsEmpty ? string.Empty : $"  ({box.Width} x {box.Height})";

        string left = $"{ToolName()} | {channel} | L {EditorPalette.Find(channel, StructureEditorMode.Primary).Name}"
                      + $" / R {EditorPalette.Find(channel, StructureEditorMode.Secondary).Name} | brush {StructureEditorMode.BrushSize}"
                      + $"{(StructureEditorMode.FilledShapes ? ", filled" : string.Empty)} | {where}{size} | zoom {Main.GameZoomTarget:0.0}x";

        Text(spriteBatch, left, new Vector2(status.X + 10, status.Y + 6), Color.White);

        string right = StructureEditorMode.Notice ?? Hint();
        float rightWidth = FontAssets.MouseText.Value.MeasureString(right).X * TextScale;

        Text(spriteBatch, right, new Vector2(status.Right - 10 - rightWidth, status.Y + 6), StructureEditorMode.Notice is null ? Muted : SecondaryRing);
    }

    private static string ToolName()
    {
        ShapeKind shape = StructureEditorMode.Shape;

        return StructureEditorMode.Tool switch
        {
            EditorTool.Shape when shape.HasFacing() => $"{shape} ({StructureEditorMode.Facing.ToString().ToLowerInvariant()})",
            EditorTool.Shape => shape.ToString(),
            EditorTool.Curve when StructureEditorMode.CurveInProgress => $"Curve ({StructureEditorMode.CurveNodes} nodes)",
            EditorTool tool => tool.ToString()
        };
    }

    private static string Hint() => StructureEditorMode.Tool switch
    {
        EditorTool.Curve when StructureEditorMode.CurveInProgress => "Enter: finish   Backspace: remove node   Esc: drop",
        EditorTool.Curve => "Click: corner   Drag: curve   First node: close",
        EditorTool.Shape => "Shift: own proportions   Alt: from centre   R: turn",
        EditorTool.Canvas => "Drag the frame's edges or corners   Esc: cancel",
        _ => "WASD / middle drag: pan   Wheel: zoom   Tab: panels   ~: build mode"
    };

    /// <summary>The hovered control's tooltip, drawn last so it sits over everything.</summary>
    private void DrawTooltip(SpriteBatch spriteBatch, Rectangle screen)
    {
        if (tooltip is null)
            return;

        Vector2 size = FontAssets.MouseText.Value.MeasureString(tooltip) * TextScale;
        int width = (int)size.X + 18;
        int height = (int)size.Y + 10;
        Point cursor = Cursor;

        int x = Math.Min(cursor.X + 18, screen.Right - width - 4);
        int y = Math.Min(cursor.Y + 20, screen.Bottom - height - 4);

        VFXLabUIAssets.DrawPanel(spriteBatch, new Rectangle(x, y, width, height), VFXLabUIAssets.Inset);
        Text(spriteBatch, tooltip, new Vector2(x + 9, y + 5), Color.White);
    }
    #endregion

    #region Drawing helpers
    private void TextButton(SpriteBatch spriteBatch, Rectangle area, string label, bool active, Action act, Action? secondary, string tip,
        float scale = TextScale)
    {
        bool hover = area.Contains(Cursor);

        VFXLabUIAssets.DrawButton(spriteBatch, area, hover, active);

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        Vector2 size = font.MeasureString(label) * scale;

        spriteBatch.DrawString(font, label, new Vector2(area.X + (area.Width - size.X) / 2f, area.Y + (area.Height - size.Y) / 2f + 2f),
            Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        Register(area, hover, act, secondary, tip);
    }

    private void ShapeButton(SpriteBatch spriteBatch, Rectangle area, bool[,] mask, Color fill, bool active, Action act, Action? secondary,
        string tip, bool enabled = true)
    {
        bool hover = enabled && area.Contains(Cursor);

        VFXLabUIAssets.DrawButton(spriteBatch, area, hover, active);
        DrawMask(spriteBatch, mask, area, fill, enabled ? 1f : 0.35f);

        if (enabled)
            Register(area, hover, act, secondary, tip);
        else if (area.Contains(Cursor))
            tooltip = tip;
    }

    private void Register(Rectangle area, bool hover, Action act, Action? secondary, string tip)
    {
        hotspots.Add((area, act, secondary));

        if (hover)
            tooltip = tip;
    }

    /// <summary>The cells the shape tool would draw for <paramref name="settings" />, fitted to an icon at the kind's own proportions.</summary>
    private bool[,] ShapeIcon(ShapeSettings settings, bool filled)
    {
        if (shapeIcons.TryGetValue((settings, filled), out bool[,]? mask))
            return mask;

        const int side = 13;
        float aspect = EditorShapes.Aspect(settings);
        int width = aspect >= 1f ? side : Math.Max(3, (int)MathF.Round(side * aspect));
        int height = aspect >= 1f ? Math.Max(3, (int)MathF.Round(side / aspect)) : side;
        var box = new Rectangle((IconGrid - width) / 2, (IconGrid - height) / 2, width, height);

        mask = new bool[IconGrid, IconGrid];

        foreach (Point point in Raster.Shape(settings, box, filled, 1))
            mask[point.X, point.Y] = true;

        return shapeIcons[(settings, filled)] = mask;
    }

    /// <summary>Draws an icon mask in <paramref name="fill" />, outlined in ink wherever it borders empty pixels.</summary>
    private static void DrawMask(SpriteBatch spriteBatch, bool[,] mask, Rectangle area, Color fill, float opacity)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        int size = Math.Max(1, (area.Width - 4) / IconGrid);
        int left = area.Center.X - IconGrid * size / 2;
        int top = area.Center.Y - IconGrid * size / 2;

        for (int x = 0; x < IconGrid; x++)
        {
            for (int y = 0; y < IconGrid; y++)
            {
                Color color;

                if (mask[x, y])
                    color = fill;
                else if (Borders(mask, x, y))
                    color = IconInk;
                else
                    continue;

                spriteBatch.Draw(pixel, new Rectangle(left + x * size, top + y * size, size, size), color * opacity);
            }
        }
    }

    private static bool Borders(bool[,] mask, int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && ny >= 0 && nx < IconGrid && ny < IconGrid && mask[nx, ny])
                    return true;
            }
        }

        return false;
    }

    /// <summary>A swatch's icon: its item, its tile or wall texture when no item places it, or a mark for air and void.</summary>
    private static void DrawSwatch(SpriteBatch spriteBatch, PaletteEntry entry, Rectangle area)
    {
        Swatch swatch = entry.Swatch;

        if (entry.Item > 0)
        {
            DrawItem(spriteBatch, entry.Item, area);
            return;
        }

        int inset = area.Width / 5;
        var inner = new Rectangle(area.X + inset, area.Y + inset, area.Width - inset * 2, area.Height - inset * 2);

        switch (swatch.Kind)
        {
            case SwatchKind.Empty:
                // A slash across the slot.
                Vector2 centre = area.Center.ToVector2();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, centre, new Rectangle(0, 0, 1, 1), new Color(230, 80, 80), MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(inner.Width * 1.2f, 3f), SpriteEffects.None, 0f);
                break;

            case SwatchKind.Void:
                bool wall = StructureEditorMode.Channel == EditorChannel.Walls;
                Texture2D voidTexture = wall
                    ? TextureAssets.Wall[ModContent.WallType<StructureVoidWall>()].Value
                    : TextureAssets.Tile[ModContent.TileType<StructureVoidTile>()].Value;

                spriteBatch.Draw(voidTexture, inner, new Rectangle(0, 0, 16, 16), Color.White);
                break;

            case SwatchKind.Tile:
                if (swatch.Type < TileID.Count)
                    Main.instance.LoadTiles(swatch.Type);

                Texture2D tile = TextureAssets.Tile[swatch.Type].Value;

                // The lone-block frame of a standard sheet; smaller sheets use their first frame.
                Rectangle frame = tile.Width >= 178 && tile.Height >= 70 ? new Rectangle(162, 54, 16, 16) : new Rectangle(0, 0, 16, 16);
                spriteBatch.Draw(tile, inner, frame, Color.White);
                break;

            case SwatchKind.Wall:
                if (swatch.Type < WallID.Count)
                    Main.instance.LoadWall(swatch.Type);

                spriteBatch.Draw(TextureAssets.Wall[swatch.Type].Value, inner, new Rectangle(8, 8, 16, 16), Color.White);
                break;

            default:
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, inner, Color.SkyBlue);
                break;
        }
    }

    private static void DrawItem(SpriteBatch spriteBatch, int type, Rectangle area)
    {
        if (ContentSamples.ItemsByType.TryGetValue(type, out Item? item))
            ItemSlot.DrawItemIcon(item, ItemSlot.Context.InventoryItem, spriteBatch, area.Center.ToVector2(), 1f, area.Width - 10, Color.White);
    }

    private static void Ring(SpriteBatch spriteBatch, Rectangle area, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, 2), color);
        spriteBatch.Draw(pixel, new Rectangle(area.X, area.Bottom - 2, area.Width, 2), color);
        spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, 2, area.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(area.Right - 2, area.Y, 2, area.Height), color);
    }

    private static void Text(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = TextScale)
        => Utils.DrawBorderString(spriteBatch, text, position, color, scale);

    /// <summary>Cuts <paramref name="text" /> to <paramref name="width" /> pixels at the default scale, with an ellipsis.</summary>
    private static string Fit(string text, int width)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;

        if (font.MeasureString(text).X * TextScale <= width)
            return text;

        while (text.Length > 1 && font.MeasureString(text + "...").X * TextScale > width)
            text = text[..^1];

        return text + "...";
    }

    private Texture2D Icon(string name)
    {
        if (!icons.TryGetValue(name, out Asset<Texture2D>? asset))
            icons[name] = asset = ModContent.Request<Texture2D>(IconPath + name, AssetRequestMode.ImmediateLoad);

        return asset.Value;
    }
    #endregion
}
