using System.Collections.Generic;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>
/// Pools placed already settled: the empty tiles a liquid would fill below a surface row, closed in by solid tiles.
/// Subworld generation runs no liquid settle, so liquid placed any other way starts flowing once the world updates.
/// </summary>
public static class LiquidBasin
{
    private static readonly Point[] Neighbors = [new(-1, 0), new(1, 0), new(0, 1), new(0, -1)];

    /// <summary>
    /// Collects into <paramref name="region"/> the empty, dry tiles connected to <paramref name="seed"/> on or below row
    /// <paramref name="surfaceY"/>. False if the region touches a non-solid tile or liquid, leaves <paramref name="bounds"/>
    /// or grows past <paramref name="maxTiles"/>, since liquid filling it would flow out or merge.
    /// </summary>
    public static bool TryFindRegion(Point seed, int surfaceY, Rectangle bounds, int maxTiles, List<Point> region)
    {
        region.Clear();

        if (seed.Y < surfaceY || !bounds.Contains(seed) || !IsEmpty(Main.tile[seed.X, seed.Y]))
            return false;

        var visited = new HashSet<Point> { seed };
        var pending = new Stack<Point>();
        pending.Push(seed);

        while (pending.TryPop(out Point point))
        {
            region.Add(point);

            if (region.Count > maxTiles)
                return false;

            foreach (Point offset in Neighbors)
            {
                var next = new Point(point.X + offset.X, point.Y + offset.Y);

                if (next.Y < surfaceY || visited.Contains(next))
                    continue;

                if (!bounds.Contains(next))
                    return false;

                Tile tile = Main.tile[next.X, next.Y];

                if (IsSolid(tile))
                    continue;

                if (!IsEmpty(tile))
                    return false;

                visited.Add(next);
                pending.Push(next);
            }
        }

        return true;
    }

    public static void Fill(List<Point> region, int liquidType)
    {
        foreach (Point point in region)
        {
            Tile tile = Main.tile[point.X, point.Y];
            tile.LiquidType = liquidType;
            tile.LiquidAmount = byte.MaxValue;
        }
    }

    /// <summary> Sloped and half blocks count as solid; <see cref="Liquid"/> blocks flow on the tile type alone. </summary>
    private static bool IsSolid(Tile tile) => tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];

    private static bool IsEmpty(Tile tile) => !tile.HasTile && tile.LiquidAmount == 0;
}
