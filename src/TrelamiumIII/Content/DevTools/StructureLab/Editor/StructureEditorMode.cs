using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>
///     Editing mode for the structure lab: the canvas takes the screen, the camera is free and the mouse runs the tools.
///     Opening a canvas starts it; the Structure Editor key (default ~) switches to building with vanilla tools and back.
/// </summary>
public sealed partial class StructureEditorMode : ModSystem
{
    public const int MaxBrush = 16;

    private const float PanSpeed = 14f;
    private const float FastPanSpeed = 40f;
    private const int NoticeFrames = 240;
    private const string ScrollLock = "TrelamiumIII/StructureEditor";

    private static readonly Color PreviewColor = new(255, 190, 60);
    private static readonly Color EraseColor = new(255, 80, 80);
    private static readonly Color VoidColor = new(230, 60, 200);

    /// <summary>Vanilla interface layers kept while editing; every other vanilla layer is hidden.</summary>
    private static readonly HashSet<string> KeptLayers =
    [
        "Vanilla: Interface Logic 1", "Vanilla: Interface Logic 2", "Vanilla: Interface Logic 3", "Vanilla: Interface Logic 4",
        "Vanilla: Cursor"
    ];

    private static UserInterface? userInterface;
    private static StructureEditorUI? ui;
    private static bool enterRequested;
    private static bool exitRequested;
    private static uint enteredOnUpdate;
    private static float zoomOnEnter;

    private static readonly Dictionary<EditorChannel, Swatch> Primaries = new()
    {
        [EditorChannel.Blocks] = new Swatch(SwatchKind.Tile, TileID.Dirt),
        [EditorChannel.Walls] = new Swatch(SwatchKind.Wall, WallID.Wood),
        [EditorChannel.Liquid] = new Swatch(SwatchKind.Liquid, LiquidID.Water),
        [EditorChannel.Paint] = new Swatch(SwatchKind.Paint, PaintID.RedPaint)
    };

    private static readonly Dictionary<EditorChannel, Swatch> Secondaries = new()
    {
        [EditorChannel.Blocks] = Swatch.Empty,
        [EditorChannel.Walls] = Swatch.Empty,
        [EditorChannel.Liquid] = Swatch.Empty,
        [EditorChannel.Paint] = Swatch.Empty
    };

    /// <summary>Each kind's <see cref="ShapeSettings.Amount" />, once changed from its default.</summary>
    private static readonly Dictionary<ShapeKind, int> ShapeAmounts = [];

    private static EditorTool tool;

    /// <summary>A press in the world that has not been released yet.</summary>
    private static bool stroking;

    private static bool strokeSecondary;
    private static EditorTool strokeTool;
    private static Point lastCell;
    private static Point anchor;
    private static HashSet<Point>? preview;
    private static (Rectangle Box, Point LineEnd, ShapeSettings Settings, bool Filled, int Brush) previewKey;
    private static Rectangle pendingFrame;
    private static bool panning;
    private static Vector2 panFrom;

    private static string? notice;
    private static int noticeTimer;

    public static ModKeybind? ToggleKey { get; private set; }

    public static bool Active { get; private set; }

    /// <summary>Changing it writes a curve in progress.</summary>
    public static EditorTool Tool
    {
        get => tool;
        set
        {
            if (value == tool)
                return;

            FinishCurve(closed: false);
            tool = value;
        }
    }

    public static EditorChannel Channel { get; set; }

    public static PaintTarget PaintTarget { get; set; } = PaintTarget.Both;

    public static bool FilledShapes { get; set; }

    public static int BrushSize { get; private set; } = 1;

    public static ShapeKind Shape { get; set; }

    public static ShapeFacing Facing { get; set; }

    public static ShapeSettings CurrentShape => new(Shape, Facing, AmountOf(Shape));

    /// <summary>World position at the centre of the screen.</summary>
    public static Vector2 Camera { get; private set; }

    public static Point HoverCell { get; private set; }

    public static bool HoverOnCanvas { get; private set; }

    /// <summary>Box of the shape or canvas being dragged, for the status bar; empty otherwise.</summary>
    public static Rectangle ShapeBox { get; private set; }

    public static string? Notice => noticeTimer > 0 ? notice : null;

    internal static EditorHistory History { get; } = new();

    internal static StructureEditorUI? Interface => ui;

    public static Swatch Primary => Primaries[Channel];

    public static Swatch Secondary => Secondaries[Channel];

    #region Lifecycle
    public override void Load()
    {
        ToggleKey = KeybindLoader.RegisterKeybind(Mod, "StructureEditor", Keys.OemTilde);

        if (Main.dedServ)
            return;

        ui = new StructureEditorUI();
        ui.Activate();
        userInterface = new UserInterface();
    }

    public override void Unload()
    {
        ToggleKey = null;
        ui = null;
        userInterface = null;
        Active = false;
        curve = null;
        EditorPalette.Unload();
    }

    /// <summary>Called when a structure opens on the canvas: starts editing it with a clean history.</summary>
    internal static void OnDocumentOpened()
    {
        History.Clear();
        curve = null;
        enterRequested = true;
    }

    public static void Enter()
    {
        if (Active || ui is null || !StructureEditor.InLab || StructureEditor.Canvas.IsEmpty)
            return;

        Active = true;
        exitRequested = false;
        enteredOnUpdate = Main.GameUpdateCount;
        stroking = false;
        preview = null;
        zoomOnEnter = Main.GameZoomTarget;
        Camera = Main.LocalPlayer.Center;

        Main.playerInventory = false;
        userInterface!.SetState(ui);
    }

    public static void Exit()
    {
        if (!Active)
            return;

        CancelCanvasDrag();
        EndStroke();
        FinishCurve(closed: false);

        Active = false;
        panning = false;
        Main.GameZoomTarget = zoomOnEnter;
        userInterface?.SetState(null);

        PlayerInput.WritingText = false;
        Main.blockInput = false;

        // The builder carries on from where the camera was.
        if (StructureEditor.InLab)
            Main.LocalPlayer.Center = Camera;
    }

    /// <summary>Leaves at the end of the frame, for controls that would otherwise drop the UI state from inside its own click.</summary>
    public static void RequestExit() => exitRequested = true;

    public static void Notify(string text)
    {
        notice = text;
        noticeTimer = NoticeFrames;
    }

    public static void SetSwatch(bool secondary, Swatch swatch)
    {
        if (!EditorPainter.Accepts(Channel, swatch))
            return;

        if (secondary)
            Secondaries[Channel] = swatch;
        else
            Primaries[Channel] = swatch;
    }

    public static void SwapSwatches() => (Primaries[Channel], Secondaries[Channel]) = (Secondaries[Channel], Primaries[Channel]);

    public static void ResizeBrush(int delta) => BrushSize = Math.Clamp(BrushSize + delta, 1, MaxBrush);

    public static int AmountOf(ShapeKind kind) => ShapeAmounts.TryGetValue(kind, out int amount) ? amount : kind.Amount()?.Default ?? 0;

    /// <summary>Steps the current kind's amount by <paramref name="steps" /> of its step size.</summary>
    public static void ChangeShapeAmount(int steps)
    {
        if (Shape.Amount() is { } amount)
            ShapeAmounts[Shape] = Math.Clamp(AmountOf(Shape) + steps * amount.Step, amount.Min, amount.Max);
    }

    /// <summary>Turns the shapes clockwise by <paramref name="quarters" /> quarter turns.</summary>
    public static void TurnShape(int quarters) => Facing = Facing.Turned(quarters);
    #endregion

    #region Actions
    /// <summary>Takes back the last edit, or the last node of a curve in progress.</summary>
    public static void Undo()
    {
        if (stroking)
            return;

        if (curve is not null)
        {
            RemoveCurveNode();
            return;
        }

        if (!History.Undo(out Rectangle canvas))
        {
            Notify("Nothing to undo.");
            return;
        }

        StructureEditor.Restore(canvas);
        Notify("Undone.");
    }

    public static void Redo()
    {
        if (stroking || curve is not null)
            return;

        if (!History.Redo(out Rectangle canvas))
        {
            Notify("Nothing to redo.");
            return;
        }

        StructureEditor.Restore(canvas);
        Notify("Redone.");
    }

    public static void Save()
    {
        try
        {
            string path = StructureEditor.Save();
            Notify(StructureEditor.SavesToSource ? $"Saved {path}" : $"Saved {path} (outside the mod; set the Authoring source directory)");
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify($"Save failed: {exception.Message}");
        }
    }

    public static void VoidOutside()
    {
        History.Begin(StructureEditor.Canvas);
        int changed = StructureEditor.VoidOutside();
        History.End(StructureEditor.Canvas);

        Notify($"Voided {changed} cells around the structure.");
    }

    /// <summary>Zooms by <paramref name="steps" /> tenths, keeping the tile under the cursor where it is.</summary>
    public static void Zoom(int steps)
    {
        Vector2 cursor = MouseWorld();
        float before = Main.GameViewMatrix.Zoom.X;

        Main.GameZoomTarget = Math.Clamp(Main.GameZoomTarget + steps * 0.1f, 1f, 2f);

        // The view eases toward the target, so the anchor uses the target rather than the current zoom.
        float after = Main.GameZoomTarget;
        Camera = ClampCamera(cursor - (cursor - Camera) * before / after);
    }
    #endregion

    #region Update
    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || !StructureEditor.InLab || StructureEditor.Canvas.IsEmpty)
        {
            enterRequested = false;

            if (Active)
                Exit();

            return;
        }

        if (enterRequested)
        {
            enterRequested = false;
            Enter();

            // A fresh canvas is framed below the player; editing starts looking at the middle of it.
            Camera = StructureEditor.Canvas.Center.ToVector2() * 16f;
        }

        if (!Active)
            return;

        if (noticeTimer > 0)
            noticeTimer--;

        // Holds off Terraria's own bindings and item use while the tools have the mouse and keys.
        PlayerInput.WritingText = true;
        Main.blockInput = true;
        Main.playerInventory = false;
        Main.LocalPlayer.mouseInterface = true;
        PlayerInput.LockVanillaMouseScroll(ScrollLock);

        // Kept at the screen's size, so clicks anywhere reach the state after a resize.
        ui!.Recalculate();
        userInterface!.Update(gameTime);

        if (exitRequested)
        {
            Exit();
            return;
        }

        if (ui.TextFocused)
            ui.UpdateText();
        else if (HandleKeys())
            return;

        UpdateCamera();
        UpdateMouse();
        FlushFrame();
    }

    public override void ModifyScreenPosition()
    {
        if (!Active)
            return;

        Main.screenPosition = Camera - new Vector2(PlayerInput.RealScreenWidth, PlayerInput.RealScreenHeight) / 2f;
    }

    /// <summary>While editing, only the interface logic and the cursor stay from vanilla, so the whole screen is canvas.</summary>
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!Active || userInterface is null)
            return;

        foreach (GameInterfaceLayer layer in layers)
        {
            if (layer.Name.StartsWith("Vanilla:", StringComparison.Ordinal) && !KeptLayers.Contains(layer.Name))
                layer.Active = false;
        }

        int cursor = layers.FindIndex(layer => layer.Name == "Vanilla: Cursor");

        layers.Insert(cursor < 0 ? layers.Count : cursor, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Structure Editor",
            () =>
            {
                userInterface.Draw(Main.spriteBatch, new GameTime());
                return true;
            },
            InterfaceScaleType.UI));
    }

    /// <summary>True when the mode was left, which ends the frame's handling.</summary>
    private static bool HandleKeys()
    {
        bool control = Down(Keys.LeftControl) || Down(Keys.RightControl);
        bool shift = Down(Keys.LeftShift) || Down(Keys.RightShift);

        // The press that opened the mode may still read as fresh on this update.
        bool justEntered = Main.GameUpdateCount - enteredOnUpdate < 2;

        if (curve is not null && HandleCurveKeys(control))
            return false;

        if (ResizingCanvas && Pressed(Keys.Escape))
        {
            CancelCanvasDrag();
            return false;
        }

        if (Pressed(Keys.Escape) || (!justEntered && ToggleKeyPressed()))
        {
            Exit();
            return true;
        }

        if (control)
        {
            if (Pressed(Keys.Z))
            {
                if (shift)
                    Redo();
                else
                    Undo();
            }

            if (Pressed(Keys.Y))
                Redo();

            if (Pressed(Keys.S))
                Save();

            return false;
        }

        foreach (EditorTool each in Enum.GetValues<EditorTool>())
        {
            if (!Pressed(each.Shortcut()))
                continue;

            // The shape key again moves on to the next kind.
            if (each == EditorTool.Shape && Tool == EditorTool.Shape)
                Shape = (ShapeKind)(((int)Shape + 1) % Enum.GetValues<ShapeKind>().Length);

            Tool = each;
        }

        if (Pressed(Keys.O))
        {
            Tool = EditorTool.Shape;
            Shape = ShapeKind.Ellipse;
        }

        if (Pressed(Keys.R))
            TurnShape(shift ? -1 : 1);

        if (Pressed(Keys.OemComma))
            ChangeShapeAmount(-1);

        if (Pressed(Keys.OemPeriod))
            ChangeShapeAmount(1);

        if (Pressed(Keys.D1))
            Channel = EditorChannel.Blocks;

        if (Pressed(Keys.D2))
            Channel = EditorChannel.Walls;

        if (Pressed(Keys.D3))
            Channel = EditorChannel.Liquid;

        if (Pressed(Keys.D4))
            Channel = EditorChannel.Paint;

        if (Pressed(Keys.F))
            FilledShapes = !FilledShapes;

        if (Pressed(Keys.X))
            SwapSwatches();

        if (Pressed(Keys.OemOpenBrackets))
            ResizeBrush(-1);

        if (Pressed(Keys.OemCloseBrackets))
            ResizeBrush(1);

        if (Pressed(Keys.Tab))
            ui!.PanelsHidden = !ui.PanelsHidden;

        return false;
    }

    private static void UpdateCamera()
    {
        float zoom = Main.GameViewMatrix.Zoom.X;
        var pan = Vector2.Zero;

        if (!ui!.TextFocused)
        {
            if (Down(Keys.A) || Down(Keys.Left))
                pan.X--;

            if (Down(Keys.D) || Down(Keys.Right))
                pan.X++;

            if (Down(Keys.W) || Down(Keys.Up))
                pan.Y--;

            if (Down(Keys.S) || Down(Keys.Down))
                pan.Y++;
        }

        bool fast = Down(Keys.LeftShift) || Down(Keys.RightShift);

        if (pan != Vector2.Zero)
            Camera = ClampCamera(Camera + Vector2.Normalize(pan) * (fast ? FastPanSpeed : PanSpeed) / zoom);

        // Middle drag, or left drag while Space is held, grabs the canvas.
        MouseState mouse = PlayerInput.MouseInfo;
        bool grab = mouse.MiddleButton == ButtonState.Pressed || (!ui.TextFocused && Down(Keys.Space) && mouse.LeftButton == ButtonState.Pressed);
        var screenMouse = new Vector2(PlayerInput.MouseX, PlayerInput.MouseY);

        if (grab && (panning || !stroking))
        {
            if (panning)
                Camera = ClampCamera(Camera - (screenMouse - panFrom) / zoom);

            panning = true;
            panFrom = screenMouse;
        }
        else
        {
            panning = false;
        }
    }

    private static void UpdateMouse()
    {
        Point cell = MouseWorld().ToTileCoordinates();
        Rectangle canvas = StructureEditor.Canvas;

        HoverCell = cell;
        HoverOnCanvas = canvas.Contains(cell);
        hoverEdges = Tool == EditorTool.Canvas && !ui!.IsMouseOverPanel ? EdgesAt(MouseWorld()) : CanvasEdges.None;

        UpdateCurvePreview();

        if (panning)
            return;

        MouseState now = PlayerInput.MouseInfo;
        MouseState before = PlayerInput.MouseInfoOld;
        bool left = now.LeftButton == ButtonState.Pressed;
        bool right = now.RightButton == ButtonState.Pressed;

        if (!stroking)
        {
            bool leftPressed = left && before.LeftButton == ButtonState.Released;
            bool rightPressed = right && before.RightButton == ButtonState.Released;

            if ((leftPressed || rightPressed) && !ui!.IsMouseOverPanel)
                BeginStroke(cell, secondary: !leftPressed);

            return;
        }

        if (strokeSecondary ? right : left)
            ContinueStroke(cell);
        else
            EndStroke();
    }
    #endregion

    #region Tools
    private static void BeginStroke(Point cell, bool secondary)
    {
        EditorTool current = Down(Keys.LeftAlt) || Down(Keys.RightAlt) ? EditorTool.Eyedropper : Tool;
        Rectangle canvas = StructureEditor.Canvas;

        switch (current)
        {
            case EditorTool.Eyedropper:
                if (canvas.Contains(cell))
                    SetSwatch(secondary, EditorPainter.Pick(cell.X, cell.Y, Channel, PaintTarget));

                return;

            case EditorTool.Fill:
                if (canvas.Contains(cell))
                    Fill(cell, StrokeSwatch(current, secondary));

                return;

            case EditorTool.Curve:
                BeginCurveNode(cell, secondary);
                return;

            case EditorTool.Canvas:
                BeginCanvasDrag();
                return;
        }

        stroking = true;
        strokeSecondary = secondary;
        strokeTool = current;
        anchor = lastCell = cell;

        if (current.IsShape())
        {
            preview = null;
            UpdateShape(cell);
            return;
        }

        History.Begin(canvas);
        Stamp(cell);
    }

    private static void ContinueStroke(Point cell)
    {
        switch (strokeTool)
        {
            case EditorTool.Curve:
                DragCurveHandle();
                return;

            case EditorTool.Canvas:
                DragCanvas();
                return;
        }

        if (strokeTool.IsShape())
        {
            UpdateShape(cell);
            return;
        }

        if (cell == lastCell)
            return;

        foreach (Point point in Raster.Line(lastCell, cell))
            Stamp(point);

        lastCell = cell;
    }

    private static void EndStroke()
    {
        if (!stroking)
            return;

        stroking = false;

        switch (strokeTool)
        {
            case EditorTool.Curve:
                return;

            case EditorTool.Canvas:
                EndCanvasDrag();
                return;
        }

        Rectangle canvas = StructureEditor.Canvas;
        HashSet<Point>? cells = preview;

        preview = null;
        ShapeBox = Rectangle.Empty;

        // The lab is gone, so there is nothing to write or record.
        if (canvas.IsEmpty)
        {
            History.Clear();
            return;
        }

        if (cells is not null)
        {
            History.Begin(canvas);
            Swatch swatch = StrokeSwatch(strokeTool, strokeSecondary);

            foreach (Point point in cells)
                Paint(point, swatch);
        }

        FlushFrame();
        History.End(canvas);
    }

    /// <summary>Recomputes the dragged line or shape for the cursor at <paramref name="cell" />, with Shift and Alt as held now.</summary>
    private static void UpdateShape(Point cell)
    {
        bool shift = Down(Keys.LeftShift) || Down(Keys.RightShift);
        bool alt = Down(Keys.LeftAlt) || Down(Keys.RightAlt);
        ShapeSettings settings = CurrentShape;
        bool line = strokeTool == EditorTool.Line;
        Point end = line && shift ? Raster.Snap45(anchor, cell) : cell;
        Rectangle box = line ? Raster.Box(anchor, end) : Raster.DragBox(anchor, cell, shift ? EditorShapes.Aspect(settings) : null, alt);

        var key = (box, line ? end : Point.Zero, settings, FilledShapes, BrushSize);

        if (preview is not null && key == previewKey)
            return;

        previewKey = key;
        ShapeBox = box;
        preview = line ? Raster.Line(anchor, end, BrushSize) : Raster.Shape(settings, box, FilledShapes, BrushSize);
    }

    private static void Stamp(Point centre)
    {
        Swatch swatch = StrokeSwatch(strokeTool, strokeSecondary);

        foreach (Point point in Raster.Brush(centre, BrushSize))
            Paint(point, swatch);
    }

    private static void Fill(Point start, Swatch swatch)
    {
        Rectangle canvas = StructureEditor.Canvas;

        if (EditorPainter.Pick(start.X, start.Y, Channel, PaintTarget) == swatch)
            return;

        History.Begin(canvas);
        List<Point> region = EditorPainter.FloodRegion(start, canvas, Channel, PaintTarget);

        foreach (Point point in region)
            Paint(point, swatch);

        FlushFrame();
        History.End(canvas);
        Notify($"Filled {region.Count} cells.");
    }

    /// <summary>Writes one cell if it is on the canvas, widening the area reframed this frame.</summary>
    private static void Paint(Point point, Swatch swatch)
    {
        if (!StructureEditor.Canvas.Contains(point) || !EditorPainter.Apply(point.X, point.Y, Channel, swatch, PaintTarget))
            return;

        var cell = new Rectangle(point.X, point.Y, 1, 1);
        pendingFrame = pendingFrame.IsEmpty ? cell : Rectangle.Union(pendingFrame, cell);
    }

    private static void FlushFrame()
    {
        if (pendingFrame.IsEmpty)
            return;

        StructureWorld.Frame(pendingFrame);
        pendingFrame = Rectangle.Empty;
    }

    /// <summary>The eraser clears with the left button and voids with the right; the rest use the swatches.</summary>
    private static Swatch StrokeSwatch(EditorTool tool, bool secondary)
    {
        if (tool != EditorTool.Eraser)
            return secondary ? Secondary : Primary;

        return secondary && Channel is EditorChannel.Blocks or EditorChannel.Walls ? Swatch.Void : Swatch.Empty;
    }
    #endregion

    #region Drawing
    /// <summary>Draws the tool's preview under the cursor in the lab's world-space batch.</summary>
    internal static void DrawWorld(SpriteBatch batch)
    {
        if (!Active || ui is null || panning)
            return;

        if (Tool == EditorTool.Canvas || ResizingCanvas)
        {
            DrawCanvasTool(batch);
            return;
        }

        EditorTool current = stroking ? strokeTool : Tool;
        Color color = SwatchColor(StrokeSwatch(current, stroking ? strokeSecondary : curve?.Secondary ?? false));

        if (curve is not null)
        {
            DrawCurve(batch, color);
            return;
        }

        if (preview is not null)
        {
            DrawCells(batch, preview, color * 0.4f);
            return;
        }

        if (ui.IsMouseOverPanel)
            return;

        int size = current.UsesBrush() ? BrushSize : 1;
        int start = -(size / 2);
        var footprint = new Rectangle((HoverCell.X + start) * 16, (HoverCell.Y + start) * 16, size * 16, size * 16);

        FillRect(batch, footprint, color * 0.25f);
        OutlineRect(batch, footprint, color * 0.9f);
    }

    private static Color SwatchColor(Swatch swatch) => swatch.Kind switch
    {
        SwatchKind.Empty => EraseColor,
        SwatchKind.Void => VoidColor,
        _ => PreviewColor
    };

    /// <summary>Tints the cells of <paramref name="cells" /> on the canvas and on screen.</summary>
    private static void DrawCells(SpriteBatch batch, IEnumerable<Point> cells, Color color)
    {
        Rectangle visible = Rectangle.Intersect(StructureEditor.Canvas, VisibleCells());

        foreach (Point point in cells)
        {
            if (visible.Contains(point))
                FillRect(batch, new Rectangle(point.X * 16, point.Y * 16, 16, 16), color);
        }
    }

    private static void FillRect(SpriteBatch batch, Rectangle world, Color color)
    {
        world.Offset(-(int)Main.screenPosition.X, -(int)Main.screenPosition.Y);
        batch.Draw(TextureAssets.MagicPixel.Value, world, color);
    }

    private static void OutlineRect(SpriteBatch batch, Rectangle world, Color color)
    {
        FillRect(batch, new Rectangle(world.X, world.Y, world.Width, 1), color);
        FillRect(batch, new Rectangle(world.X, world.Bottom - 1, world.Width, 1), color);
        FillRect(batch, new Rectangle(world.X, world.Y, 1, world.Height), color);
        FillRect(batch, new Rectangle(world.Right - 1, world.Y, 1, world.Height), color);
    }

    /// <summary>A square of <paramref name="screenSize" /> screen pixels centred on a world position.</summary>
    private static void DrawMarker(SpriteBatch batch, Vector2 world, float screenSize, Color color)
    {
        float size = screenSize / Main.GameViewMatrix.Zoom.X;
        batch.Draw(TextureAssets.MagicPixel.Value, world - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), size,
            SpriteEffects.None, 0f);
    }

    /// <summary>A line <paramref name="screenWidth" /> screen pixels wide between two world positions.</summary>
    private static void DrawSegment(SpriteBatch batch, Vector2 from, Vector2 to, float screenWidth, Color color)
    {
        Vector2 delta = to - from;
        float width = screenWidth / Main.GameViewMatrix.Zoom.X;

        batch.Draw(TextureAssets.MagicPixel.Value, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, delta.ToRotation(),
            new Vector2(0f, 0.5f), new Vector2(delta.Length(), width), SpriteEffects.None, 0f);
    }

    /// <summary>The tiles on screen, with a tile of slack on each side.</summary>
    private static Rectangle VisibleCells()
    {
        Vector2 size = new Vector2(PlayerInput.RealScreenWidth, PlayerInput.RealScreenHeight) / Main.GameViewMatrix.Zoom.X;
        Vector2 topLeft = Camera - size / 2f;

        return new Rectangle((int)(topLeft.X / 16f) - 1, (int)(topLeft.Y / 16f) - 1, (int)(size.X / 16f) + 3, (int)(size.Y / 16f) + 3);
    }
    #endregion

    #region Input helpers
    /// <summary>The world pixel under the mouse, from the camera this frame will draw with.</summary>
    public static Vector2 MouseWorld()
    {
        var screen = new Vector2(PlayerInput.RealScreenWidth, PlayerInput.RealScreenHeight);
        var mouse = new Vector2(PlayerInput.MouseX, PlayerInput.MouseY);

        return Camera + (mouse - screen / 2f) / Main.GameViewMatrix.Zoom;
    }

    private static Vector2 ClampCamera(Vector2 camera)
    {
        const float edge = 42 * 16f;
        return Vector2.Clamp(camera, new Vector2(edge), new Vector2(Main.maxTilesX * 16f - edge, Main.maxTilesY * 16f - edge));
    }

    private static bool Down(Keys key) => Main.keyState.IsKeyDown(key);

    private static bool Pressed(Keys key) => Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);

    private static bool ToggleKeyPressed()
    {
        if (ToggleKey is null)
            return false;

        foreach (string name in ToggleKey.GetAssignedKeys())
        {
            if (Enum.TryParse(name, out Keys key) && Pressed(key))
                return true;
        }

        return false;
    }
    #endregion
}
