namespace TrelamiumIII.Common.Generation.Utils;

public static class MaskDistance
{
    /// <summary> Chebyshev distance from each cell to the nearest true cell of <paramref name="mask"/>, capped at <paramref name="cap"/>. </summary>
    public static int[,] Chebyshev(bool[,] mask, int cap)
    {
        int width = mask.GetLength(0);
        int height = mask.GetLength(1);
        var distance = new int[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
                distance[i, j] = mask[i, j] ? 0 : cap;
        }

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                int d = distance[i, j];
                if (i > 0) d = Math.Min(d, distance[i - 1, j] + 1);
                if (j > 0) d = Math.Min(d, distance[i, j - 1] + 1);
                if (i > 0 && j > 0) d = Math.Min(d, distance[i - 1, j - 1] + 1);
                if (i < width - 1 && j > 0) d = Math.Min(d, distance[i + 1, j - 1] + 1);
                distance[i, j] = d;
            }
        }

        for (int j = height - 1; j >= 0; j--)
        {
            for (int i = width - 1; i >= 0; i--)
            {
                int d = distance[i, j];
                if (i < width - 1) d = Math.Min(d, distance[i + 1, j] + 1);
                if (j < height - 1) d = Math.Min(d, distance[i, j + 1] + 1);
                if (i < width - 1 && j < height - 1) d = Math.Min(d, distance[i + 1, j + 1] + 1);
                if (i > 0 && j < height - 1) d = Math.Min(d, distance[i - 1, j + 1] + 1);
                distance[i, j] = d;
            }
        }

        return distance;
    }
}
