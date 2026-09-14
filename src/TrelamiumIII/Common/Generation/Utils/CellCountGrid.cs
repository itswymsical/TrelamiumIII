namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>
/// Tile counts binned into square cells over the whole world, with a summed-area table for constant-time rectangle
/// queries. Add tiles, call <see cref="Build"/> once, then query. Queries cover every cell the rectangle touches.
/// </summary>
public sealed class CellCountGrid
{
    private readonly int cellSize;
    private readonly int columns;
    private readonly int rows;
    private readonly int[] counts;
    private readonly int[] sums;

    public CellCountGrid(int cellSize)
    {
        this.cellSize = cellSize;
        columns = (Main.maxTilesX + cellSize - 1) / cellSize;
        rows = (Main.maxTilesY + cellSize - 1) / cellSize;
        counts = new int[columns * rows];
        sums = new int[(columns + 1) * (rows + 1)];
    }

    public void Add(int x, int y) => counts[y / cellSize * columns + x / cellSize]++;

    public void Build()
    {
        int stride = columns + 1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                sums[(r + 1) * stride + c + 1] = counts[r * columns + c]
                    + sums[r * stride + c + 1] + sums[(r + 1) * stride + c] - sums[r * stride + c];
            }
        }
    }

    public int Count(Rectangle area) =>
        TryGetCells(area, out int c0, out int r0, out int c1, out int r1) ? Sum(c0, r0, c1, r1) : 0;

    /// <summary> Counted tiles per tile over the touched cells, from 0 to 1. </summary>
    public float Density(Rectangle area)
    {
        if (!TryGetCells(area, out int c0, out int r0, out int c1, out int r1))
            return 0f;

        return Sum(c0, r0, c1, r1) / (float)((c1 - c0) * (r1 - r0) * cellSize * cellSize);
    }

    private int Sum(int c0, int r0, int c1, int r1)
    {
        int stride = columns + 1;
        return sums[r1 * stride + c1] - sums[r0 * stride + c1] - sums[r1 * stride + c0] + sums[r0 * stride + c0];
    }

    /// <summary> Cell range touched by <paramref name="area"/> clamped to the world; the upper bounds are exclusive. </summary>
    private bool TryGetCells(Rectangle area, out int c0, out int r0, out int c1, out int r1)
    {
        int left = Math.Max(area.Left, 0);
        int top = Math.Max(area.Top, 0);
        int right = Math.Min(area.Right, Main.maxTilesX);
        int bottom = Math.Min(area.Bottom, Main.maxTilesY);

        c0 = left / cellSize;
        r0 = top / cellSize;
        c1 = (right - 1) / cellSize + 1;
        r1 = (bottom - 1) / cellSize + 1;

        return right > left && bottom > top;
    }
}
