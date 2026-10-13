namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>The canvas tool: dragging an edge or corner of the canvas frame resizes the canvas on release.</summary>
public sealed partial class StructureEditorMode
{
    /// <summary>Screen pixels from a canvas edge that still grab it.</summary>
    private const float EdgeReach = 8f;

    private static readonly Color HandleIdle = new(255, 190, 60);
    private static readonly Color CutColor = new(255, 60, 60);

    [Flags]
    private enum CanvasEdges
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8
    }

    private static CanvasEdges hoverEdges;
    private static CanvasEdges dragEdges;
    private static Rectangle canvasPreview;

    public static bool ResizingCanvas => dragEdges != CanvasEdges.None;

    /// <summary>The canvas as the drag in progress would leave it, or the canvas itself.</summary>
    public static Rectangle CanvasPreview => ResizingCanvas ? canvasPreview : StructureEditor.Canvas;

    /// <summary>Moves the canvas to <paramref name="area" />, fitted to the lab, as one undo step.</summary>
    public static void ResizeCanvas(Rectangle area)
    {
        Rectangle canvas = StructureEditor.Canvas;
        area = StructureEditor.Fit(area);

        if (canvas.IsEmpty || area == canvas)
            return;

        History.Begin(canvas, Rectangle.Union(canvas, area));
        StructureEditor.Resize(area);
        History.End(area);

        Notify($"Canvas resized to {area.Width} x {area.Height}.");
    }

    /// <summary>The canvas edges within reach of world pixel <paramref name="world" />, at most one per axis.</summary>
    private static CanvasEdges EdgesAt(Vector2 world)
    {
        Rectangle canvas = StructureEditor.Canvas;
        float reach = EdgeReach / Main.GameViewMatrix.Zoom.X;
        var frame = new Vector4(canvas.Left, canvas.Top, canvas.Right, canvas.Bottom) * 16f;

        if (world.X < frame.X - reach || world.X > frame.Z + reach || world.Y < frame.Y - reach || world.Y > frame.W + reach)
            return CanvasEdges.None;

        CanvasEdges edges = CanvasEdges.None;
        float toLeft = MathF.Abs(world.X - frame.X);
        float toRight = MathF.Abs(world.X - frame.Z);
        float toTop = MathF.Abs(world.Y - frame.Y);
        float toBottom = MathF.Abs(world.Y - frame.W);

        if (Math.Min(toLeft, toRight) <= reach)
            edges |= toLeft <= toRight ? CanvasEdges.Left : CanvasEdges.Right;

        if (Math.Min(toTop, toBottom) <= reach)
            edges |= toTop <= toBottom ? CanvasEdges.Top : CanvasEdges.Bottom;

        return edges;
    }

    private static void BeginCanvasDrag()
    {
        if (hoverEdges == CanvasEdges.None)
            return;

        dragEdges = hoverEdges;
        canvasPreview = ShapeBox = StructureEditor.Canvas;
        stroking = true;
        strokeTool = EditorTool.Canvas;
    }

    /// <summary>Moves the dragged edges to the grid line nearest the cursor, keeping a tile of canvas and the lab's margins.</summary>
    private static void DragCanvas()
    {
        if (!ResizingCanvas)
            return;

        Vector2 mouse = MouseWorld() / 16f;
        int x = (int)MathF.Round(mouse.X);
        int y = (int)MathF.Round(mouse.Y);
        Rectangle canvas = StructureEditor.Canvas;
        Rectangle bounds = StructureEditor.Bounds;

        int left = canvas.Left;
        int right = canvas.Right;
        int top = canvas.Top;
        int bottom = canvas.Bottom;

        if (dragEdges.HasFlag(CanvasEdges.Left))
            left = Math.Clamp(x, bounds.Left, right - 1);

        if (dragEdges.HasFlag(CanvasEdges.Right))
            right = Math.Clamp(x, left + 1, bounds.Right);

        if (dragEdges.HasFlag(CanvasEdges.Top))
            top = Math.Clamp(y, bounds.Top, bottom - 1);

        if (dragEdges.HasFlag(CanvasEdges.Bottom))
            bottom = Math.Clamp(y, top + 1, bounds.Bottom);

        canvasPreview = ShapeBox = new Rectangle(left, top, right - left, bottom - top);
    }

    private static void EndCanvasDrag()
    {
        if (!ResizingCanvas)
            return;

        dragEdges = CanvasEdges.None;
        ShapeBox = Rectangle.Empty;
        ResizeCanvas(canvasPreview);
    }

    private static void CancelCanvasDrag()
    {
        if (!ResizingCanvas)
            return;

        dragEdges = CanvasEdges.None;
        ShapeBox = Rectangle.Empty;
        stroking = false;
    }

    /// <summary>Handles on the frame, and while dragging, the new frame with the cells it would clear tinted.</summary>
    private static void DrawCanvasTool(SpriteBatch batch)
    {
        Rectangle canvas = StructureEditor.Canvas;
        CanvasEdges edges = ResizingCanvas ? dragEdges : hoverEdges;
        Rectangle frame = canvas;

        if (ResizingCanvas)
        {
            frame = canvasPreview;
            Rectangle visible = Rectangle.Intersect(canvas, VisibleCells());

            for (int x = visible.Left; x < visible.Right; x++)
            {
                for (int y = visible.Top; y < visible.Bottom; y++)
                {
                    if (!frame.Contains(x, y))
                        FillRect(batch, new Rectangle(x * 16, y * 16, 16, 16), CutColor * 0.3f);
                }
            }

            var world = new Rectangle(frame.X * 16, frame.Y * 16, frame.Width * 16, frame.Height * 16);
            float width = 2f / Main.GameViewMatrix.Zoom.X;

            DrawSegment(batch, new Vector2(world.Left, world.Top), new Vector2(world.Right, world.Top), 2f, Color.White);
            DrawSegment(batch, new Vector2(world.Left, world.Bottom), new Vector2(world.Right, world.Bottom), 2f, Color.White);
            DrawSegment(batch, new Vector2(world.Left, world.Top - width / 2f), new Vector2(world.Left, world.Bottom + width / 2f), 2f, Color.White);
            DrawSegment(batch, new Vector2(world.Right, world.Top - width / 2f), new Vector2(world.Right, world.Bottom + width / 2f), 2f, Color.White);
        }

        float left = frame.Left * 16f;
        float right = frame.Right * 16f;
        float top = frame.Top * 16f;
        float bottom = frame.Bottom * 16f;
        float middleX = (left + right) / 2f;
        float middleY = (top + bottom) / 2f;

        DrawHandle(batch, new Vector2(left, top), edges == (CanvasEdges.Left | CanvasEdges.Top));
        DrawHandle(batch, new Vector2(middleX, top), edges == CanvasEdges.Top);
        DrawHandle(batch, new Vector2(right, top), edges == (CanvasEdges.Right | CanvasEdges.Top));
        DrawHandle(batch, new Vector2(right, middleY), edges == CanvasEdges.Right);
        DrawHandle(batch, new Vector2(right, bottom), edges == (CanvasEdges.Right | CanvasEdges.Bottom));
        DrawHandle(batch, new Vector2(middleX, bottom), edges == CanvasEdges.Bottom);
        DrawHandle(batch, new Vector2(left, bottom), edges == (CanvasEdges.Left | CanvasEdges.Bottom));
        DrawHandle(batch, new Vector2(left, middleY), edges == CanvasEdges.Left);
    }

    private static void DrawHandle(SpriteBatch batch, Vector2 world, bool hot)
    {
        DrawMarker(batch, world, hot ? 14f : 11f, Color.Black);
        DrawMarker(batch, world, hot ? 10f : 7f, hot ? Color.White : HandleIdle);
    }
}
