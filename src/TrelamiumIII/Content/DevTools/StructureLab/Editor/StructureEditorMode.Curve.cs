using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>
///     The curve tool: a click adds a corner node, a press dragged pulls out a smooth node's handles. Clicking the first node
///     closes the path, clicking the last one again or Enter ends it open; Backspace takes a node back, Escape drops the path.
/// </summary>
public sealed partial class StructureEditorMode
{
    /// <summary>Drag distance, in tiles, that turns a new node smooth.</summary>
    private const float HandleThreshold = 0.5f;

    private static readonly Color NodeColor = new(255, 255, 255);
    private static readonly Color HandleColor = new(120, 200, 255);

    private static CurvePath? curve;
    private static HashSet<Point>? curvePreview;
    private static (int Version, Point Hover, bool Filled, int Brush) curvePreviewKey;
    private static int curveVersion;

    public static bool CurveInProgress => curve is not null;

    public static int CurveNodes => curve?.Nodes.Count ?? 0;

    private static void BeginCurveNode(Point cell, bool secondary)
    {
        if (curve is { Nodes.Count: > 0 } path)
        {
            if (path.Nodes.Count >= 2 && cell == path.Nodes[0].Cell)
            {
                FinishCurve(closed: true);
                return;
            }

            if (cell == path.Nodes[^1].Cell)
            {
                FinishCurve(closed: false);
                return;
            }
        }

        curve ??= new CurvePath(secondary);
        curve.Nodes.Add(new CurveNode(cell.ToVector2()));
        curveVersion++;

        stroking = true;
        strokeTool = EditorTool.Curve;
        strokeSecondary = secondary;
    }

    /// <summary>Points the newest node's handles at the cursor and mirrors them; close to the node it stays a corner.</summary>
    private static void DragCurveHandle()
    {
        if (curve is not { Nodes.Count: > 0 } path)
            return;

        CurveNode node = path.Nodes[^1];
        Vector2 offset = MouseWorld() / 16f - new Vector2(0.5f) - node.Position;

        if (Down(Keys.LeftShift) || Down(Keys.RightShift))
        {
            float angle = MathF.Round(offset.ToRotation() / MathHelper.PiOver4) * MathHelper.PiOver4;
            offset = angle.ToRotationVector2() * offset.Length();
        }

        if (offset.Length() < HandleThreshold)
            offset = Vector2.Zero;

        if (node.Out == node.Position + offset)
            return;

        node.Out = node.Position + offset;
        node.In = node.Position - offset;
        path.Nodes[^1] = node;
        curveVersion++;
    }

    /// <summary>Writes the curve in progress, if it has two nodes or more, as one undo step.</summary>
    private static void FinishCurve(bool closed)
    {
        if (curve is not { } path)
            return;

        curve = null;
        curvePreview = null;

        Rectangle canvas = StructureEditor.Canvas;

        if (path.Nodes.Count < 2 || canvas.IsEmpty)
            return;

        History.Begin(canvas);
        Swatch swatch = StrokeSwatch(EditorTool.Curve, path.Secondary);

        foreach (Point point in path.Rasterize(closed, FilledShapes, BrushSize))
            Paint(point, swatch);

        FlushFrame();
        History.End(canvas);
    }

    private static void CancelCurve()
    {
        curve = null;
        curvePreview = null;
        Notify("Curve dropped.");
    }

    private static void RemoveCurveNode()
    {
        if (curve is null)
            return;

        curve.Nodes.RemoveAt(curve.Nodes.Count - 1);
        curveVersion++;

        if (curve.Nodes.Count == 0)
            curve = null;
    }

    /// <summary>Handles the keys of a curve in progress; true when one was used.</summary>
    private static bool HandleCurveKeys(bool control)
    {
        if (Pressed(Keys.Escape))
            CancelCurve();
        else if (Pressed(Keys.Enter))
            FinishCurve(closed: false);
        else if (Pressed(Keys.Back) || (control && Pressed(Keys.Z)))
            RemoveCurveNode();
        else
            return false;

        return true;
    }

    /// <summary>
    ///     The path with a segment on to the hovered cell, or closed when the first node is hovered. Rebuilt only when the path,
    ///     the hovered cell or the fill and brush settings change.
    /// </summary>
    private static void UpdateCurvePreview()
    {
        if (curve is not { Nodes.Count: > 0 } path)
        {
            curvePreview = null;
            return;
        }

        bool dragging = stroking && strokeTool == EditorTool.Curve;
        bool aside = dragging || ui!.IsMouseOverPanel;
        Point hover = aside ? new Point(int.MinValue, int.MinValue) : HoverCell;
        var key = (curveVersion, hover, FilledShapes, BrushSize);

        if (curvePreview is not null && key == curvePreviewKey)
            return;

        curvePreviewKey = key;

        bool closing = !aside && path.Nodes.Count >= 2 && hover == path.Nodes[0].Cell;
        Point? next = aside || closing || hover == path.Nodes[^1].Cell ? null : hover;

        curvePreview = path.Rasterize(closing, FilledShapes, BrushSize, next);
    }

    private static void DrawCurve(SpriteBatch batch, Color color)
    {
        if (curve is null)
            return;

        if (curvePreview is not null)
            DrawCells(batch, curvePreview, color * 0.4f);

        List<CurveNode> nodes = curve.Nodes;
        bool closing = !ui!.IsMouseOverPanel && nodes.Count >= 2 && HoverCell == nodes[0].Cell;

        for (int i = 0; i < nodes.Count; i++)
        {
            CurveNode node = nodes[i];
            Vector2 centre = WorldOf(node.Position);

            if (node.Out != node.Position)
            {
                DrawSegment(batch, WorldOf(node.In), WorldOf(node.Out), 1.5f, HandleColor);
                DrawMarker(batch, WorldOf(node.In), 5f, HandleColor);
                DrawMarker(batch, WorldOf(node.Out), 5f, HandleColor);
            }

            bool hot = i == 0 && closing;
            DrawMarker(batch, centre, hot ? 10f : 8f, Color.Black);
            DrawMarker(batch, centre, hot ? 8f : 6f, hot ? PreviewColor : NodeColor);
        }
    }

    /// <summary>World pixel of a point in cell coordinates.</summary>
    private static Vector2 WorldOf(Vector2 cell) => (cell + new Vector2(0.5f)) * 16f;
}
