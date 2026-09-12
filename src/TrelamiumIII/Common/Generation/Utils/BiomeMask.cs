using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>
/// Which tiles fell inside the base blob's density field, snapshotted before carving. Later passes validate
/// against this instead of the blob's bounding box.
/// </summary>
public sealed class BiomeMask
{
    public readonly int Left;
    public readonly int Top;
    public readonly int Width;
    public readonly int Height;
    public readonly List<Point> ValidTiles = new();

    private readonly bool[,] grid;

    public BiomeMask(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        grid = new bool[width, height];
    }

    /// <summary>Called from BiomeBlob's rasterization loop; assumes (x, y) is already in bounds.</summary>
    public void Mark(int worldX, int worldY)
    {
        grid[worldX - Left, worldY - Top] = true;
        ValidTiles.Add(new Point(worldX, worldY));
    }

    public bool Contains(int worldX, int worldY)
    {
        var lx = worldX - Left;
        var ly = worldY - Top;
        if (lx < 0 || ly < 0 || lx >= Width || ly >= Height) return false;
        return grid[lx, ly];
    }

    public Point RandomPoint() => ValidTiles[WorldGen.genRand.Next(ValidTiles.Count)];
}
