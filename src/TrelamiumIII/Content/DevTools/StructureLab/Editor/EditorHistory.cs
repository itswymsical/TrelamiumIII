using System.Collections.Generic;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>
///     Undo and redo for the canvas. An edit captures the whole canvas before and after and keeps only the rectangle that
///     differs, so side effects such as a broken chair are restored too.
/// </summary>
internal sealed class EditorHistory
{
    /// <summary>Cells kept across both stacks before the oldest steps are dropped.</summary>
    private const int CellBudget = 2_000_000;

    private readonly LinkedList<Step> undo = new();
    private readonly Stack<Step> redo = new();

    private StructureData? before;
    private int storedCells;

    /// <param name="Offset">Top-left of the changed rectangle, relative to the canvas.</param>
    private sealed record Step(Point Offset, StructureData Before, StructureData After)
    {
        public int Cells => Before.Cells.Length + After.Cells.Length;
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

    /// <summary>Starts an edit; does nothing if one is already open.</summary>
    public void Begin(Rectangle canvas) => before ??= StructureWorld.Capture(canvas);

    /// <summary>Closes the open edit. False when it changed nothing.</summary>
    public bool End(Rectangle canvas)
    {
        if (before is not { } original)
            return false;

        before = null;

        StructureData after = StructureWorld.Capture(canvas);
        Rectangle changed = Difference(original, after);

        if (changed.IsEmpty)
            return false;

        Push(new Step(changed.Location, original.Crop(changed), after.Crop(changed)));
        return true;
    }

    public bool Undo(Rectangle canvas)
    {
        if (!CanUndo)
            return false;

        Step step = undo.Last!.Value;
        undo.RemoveLast();

        StructureWorld.Place(step.Before, new Point(canvas.X + step.Offset.X, canvas.Y + step.Offset.Y), editable: true);
        redo.Push(step);

        return true;
    }

    public bool Redo(Rectangle canvas)
    {
        if (!CanRedo)
            return false;

        Step step = redo.Pop();

        StructureWorld.Place(step.After, new Point(canvas.X + step.Offset.X, canvas.Y + step.Offset.Y), editable: true);
        undo.AddLast(step);

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
