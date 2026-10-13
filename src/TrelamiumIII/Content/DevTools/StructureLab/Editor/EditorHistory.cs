using System.Collections.Generic;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>
///     Undo and redo for the canvas. An edit captures its area before and after and keeps only the rectangle that differs,
///     so side effects such as a broken chair are restored too. Steps are in world tiles and carry the canvas bounds.
/// </summary>
internal sealed class EditorHistory
{
    /// <summary>Cells kept across both stacks before the oldest steps are dropped.</summary>
    private const int CellBudget = 2_000_000;

    private readonly LinkedList<Step> undo = new();
    private readonly Stack<Step> redo = new();

    private StructureData? before;
    private Rectangle area;
    private Rectangle canvasBefore;
    private int storedCells;

    /// <param name="Origin">World tile of the changed rectangle's top-left.</param>
    /// <param name="Before">The changed cells before the edit; null when only the canvas bounds changed.</param>
    private sealed record Step(Point Origin, StructureData? Before, StructureData? After, Rectangle CanvasBefore, Rectangle CanvasAfter)
    {
        public int Cells => (Before?.Cells.Length ?? 0) + (After?.Cells.Length ?? 0);
    }

    public bool Recording => before is not null;

    public bool CanUndo => undo.Count > 0 && !Recording;

    public bool CanRedo => redo.Count > 0 && !Recording;

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
        before = null;
        storedCells = 0;
    }

    /// <summary>Starts an edit of the canvas; does nothing if one is already open.</summary>
    public void Begin(Rectangle canvas) => Begin(canvas, canvas);

    /// <summary>Starts an edit that may change any cell of <paramref name="capture" />, which covers the canvas.</summary>
    public void Begin(Rectangle canvas, Rectangle capture)
    {
        if (before is not null)
            return;

        canvasBefore = canvas;
        area = capture;
        before = StructureWorld.Capture(capture);
    }

    /// <summary>Closes the open edit with the canvas at <paramref name="canvas" />. False when it changed nothing.</summary>
    public bool End(Rectangle canvas)
    {
        if (before is not { } original)
            return false;

        before = null;

        StructureData after = StructureWorld.Capture(area);
        Rectangle changed = Difference(original, after);

        if (changed.IsEmpty && canvas == canvasBefore)
            return false;

        var origin = new Point(area.X + changed.X, area.Y + changed.Y);

        Push(changed.IsEmpty
            ? new Step(origin, null, null, canvasBefore, canvas)
            : new Step(origin, original.Crop(changed), after.Crop(changed), canvasBefore, canvas));

        return true;
    }

    /// <summary>Reverts the last edit; <paramref name="canvas" /> is the canvas bounds from before it.</summary>
    public bool Undo(out Rectangle canvas)
    {
        canvas = Rectangle.Empty;

        if (!CanUndo)
            return false;

        Step step = undo.Last!.Value;
        undo.RemoveLast();

        if (step.Before is not null)
            StructureWorld.Place(step.Before, step.Origin, editable: true);

        redo.Push(step);
        canvas = step.CanvasBefore;

        return true;
    }

    /// <summary>Reapplies the last undone edit; <paramref name="canvas" /> is the canvas bounds from after it.</summary>
    public bool Redo(out Rectangle canvas)
    {
        canvas = Rectangle.Empty;

        if (!CanRedo)
            return false;

        Step step = redo.Pop();

        if (step.After is not null)
            StructureWorld.Place(step.After, step.Origin, editable: true);

        undo.AddLast(step);
        canvas = step.CanvasAfter;

        return true;
    }

    private void Push(Step step)
    {
        foreach (Step dropped in redo)
            storedCells -= dropped.Cells;

        redo.Clear();
        undo.AddLast(step);
        storedCells += step.Cells;

        while (storedCells > CellBudget && undo.Count > 1)
        {
            storedCells -= undo.First!.Value.Cells;
            undo.RemoveFirst();
        }
    }

    /// <summary>Bounding box of the cells that differ, in local coordinates; empty when none do.</summary>
    private static Rectangle Difference(StructureData a, StructureData b)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = -1;
        int bottom = -1;

        for (int x = 0; x < a.Width; x++)
        {
            for (int y = 0; y < a.Height; y++)
            {
                if (a[x, y] == b[x, y])
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }
}
