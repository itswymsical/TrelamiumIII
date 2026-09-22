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
public sealed class StructureEditorMode : ModSystem
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

    /// <summary>A press in the world that has not been released yet.</summary>
    private static bool stroking;

    private static bool strokeSecondary;
    private static EditorTool strokeTool;
    private static Point lastCell;
    private static Point anchor;
    private static HashSet<Point>? shape;
    private static Rectangle pendingFrame;
    private static bool panning;
    private static Vector2 panFrom;

    private static string? notice;
    private static int noticeTimer;

    public static ModKeybind? ToggleKey { get; private set; }

    public static bool Active { get; private set; }

    public static EditorTool Tool { get; set; }

    public static EditorChannel Channel { get; set; }

    public static PaintTarget PaintTarget { get; set; } = PaintTarget.Both;

    public static bool FilledShapes { get; set; }

    public static int BrushSize { get; private set; } = 1;

    /// <summary>World position at the centre of the screen.</summary>
    public static Vector2 Camera { get; private set; }

    public static Point HoverCell { get; private set; }

    public static bool HoverOnCanvas { get; private set; }

    /// <summary>Box of the shape being dragged, for the status bar; empty otherwise.</summary>
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
        EditorPalette.Unload();
    }

    /// <summary>Called when a structure opens on the canvas: starts editing it with a clean history.</summary>
    internal static void OnDocumentOpened()
    {
        History.Clear();
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
        shape = null;
        zoomOnEnter = Main.GameZoomTarget;
        Camera = Main.LocalPlayer.Center;

        Main.playerInventory = false;
        userInterface!.SetState(ui);
    }

    public static void Exit()
    {
        if (!Active)
            return;

        EndStroke();

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
    #endregion

    #region Actions
    public static void Undo()
    {
        if (stroking)
            return;

        Notify(History.Undo(StructureEditor.Canvas) ? "Undone." : "Nothing to undo.");
    }

    public static void Redo()
    {
        if (stroking)
            return;

        Notify(History.Redo(StructureEditor.Canvas) ? "Redone." : "Nothing to redo.");
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

        if (ui.SearchFocused)
            ui.UpdateSearch();
        else if (HandleKeys())
            return;

        UpdateCamera();
        UpdateMouse();

        if (!pendingFrame.IsEmpty)
        {
            StructureWorld.Frame(pendingFrame);
            pendingFrame = Rectangle.Empty;
        }
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

        foreach (EditorTool tool in Enum.GetValues<EditorTool>())
        {
            if (Pressed(tool.Shortcut()))
                Tool = tool;
        }

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

        if (!ui!.SearchFocused)
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
        bool grab = mouse.MiddleButton == ButtonState.Pressed || (!ui.SearchFocused && Down(Keys.Space) && mouse.LeftButton == ButtonState.Pressed);
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
        EditorTool tool = Down(Keys.LeftAlt) || Down(Keys.RightAlt) ? EditorTool.Eyedropper : Tool;
        Rectangle canvas = StructureEditor.Canvas;

        if (tool == EditorTool.Eyedropper)
        {
            if (canvas.Contains(cell))
                SetSwatch(secondary, EditorPainter.Pick(cell.X, cell.Y, Channel, PaintTarget));

            return;
        }

        if (tool == EditorTool.Fill)
        {
            if (canvas.Contains(cell))
                Fill(cell, StrokeSwatch(tool, secondary));

            return;
        }

        stroking = true;
        strokeSecondary = secondary;
        strokeTool = tool;
        anchor = lastCell = cell;

        if (tool.IsShape())
        {
            shape = Raster.Shape(tool, anchor, cell, FilledShapes, BrushSize);
            ShapeBox = Raster.Box(anchor, cell);
            return;
        }

        History.Begin(canvas);
        Stamp(cell);
    }

    private static void ContinueStroke(Point cell)
    {
        if (strokeTool.IsShape())
        {
            Point end = Down(Keys.LeftShift) || Down(Keys.RightShift) ? Raster.Constrain(strokeTool, anchor, cell) : cell;

            if (end == lastCell)
                return;

            lastCell = end;
            shape = Raster.Shape(strokeTool, anchor, end, FilledShapes, BrushSize);
            ShapeBox = Raster.Box(anchor, end);
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
        Rectangle canvas = StructureEditor.Canvas;

        if (shape is not null)
        {
            History.Begin(canvas);
            Swatch swatch = StrokeSwatch(strokeTool, strokeSecondary);

            foreach (Point point in shape)
                Paint(point, swatch);

            shape = null;
            ShapeBox = Rectangle.Empty;
        }

        if (!pendingFrame.IsEmpty)
        {
            StructureWorld.Frame(pendingFrame);
            pendingFrame = Rectangle.Empty;
        }

        History.End(canvas);
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

        if (!pendingFrame.IsEmpty)
        {
            StructureWorld.Frame(pendingFrame);
            pendingFrame = Rectangle.Empty;
        }

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

    /// <summary>The eraser clears with the left button and voids with the right; the rest use the swatches.</summary>
    private static Swatch StrokeSwatch(EditorTool tool, bool secondary)
    {
        if (tool != EditorTool.Eraser)
            return secondary ? Secondary : Primary;

        return secondary && Channel is EditorChannel.Blocks or EditorChannel.Walls ? Swatch.Void : Swatch.Empty;
    }
    #endregion

    #region Drawing
    /// <summary>Draws the shape being dragged, or the brush under the cursor, in the lab's world-space batch.</summary>
    internal static void DrawWorld(SpriteBatch batch)
    {
        if (!Active || ui is null || (ui.IsMouseOverPanel && !stroking) || panning)
            return;

        EditorTool tool = stroking ? strokeTool : Tool;
        Swatch swatch = StrokeSwatch(tool, stroking && strokeSecondary);

        Color color = swatch.Kind switch
        {
            SwatchKind.Empty => EraseColor,
            SwatchKind.Void => VoidColor,
            _ => PreviewColor
        };

        if (shape is not null)
        {
            foreach (Point point in shape)
            {
                if (StructureEditor.Canvas.Contains(point))
                    FillRect(batch, new Rectangle(point.X * 16, point.Y * 16, 16, 16), color * 0.4f);
            }

            return;
        }

        int size = tool.UsesBrush() ? BrushSize : 1;
        int start = -(size / 2);
        var footprint = new Rectangle((HoverCell.X + start) * 16, (HoverCell.Y + start) * 16, size * 16, size * 16);

        FillRect(batch, footprint, color * 0.25f);
        OutlineRect(batch, footprint, color * 0.9f);
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
