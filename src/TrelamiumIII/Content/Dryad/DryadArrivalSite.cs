namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     Picks where the Dryad arrives, always by the world spawn: the ground she walks to, a few steps from it, and
///     the ground a short walk beyond that her rift opens on. Her standing rift to Ingress opens by the same rules.
/// </summary>
internal static class DryadArrivalSite
{
    /// <summary>Columns from the spawn to the rift, tried in this order on each side.</summary>
    private static readonly int[] RiftDistances = [16, 14, 18, 12, 20, 10, 24, 28];

    /// <summary>Columns short of the spawn, on the rift's side, where she stops: beside a player waiting there, not on them.</summary>
    private const int STOP_GAP = 3;

    /// <summary>Open tiles a standing spot needs above it, and the rift's floor point needs above it.</summary>
    private const int HEADROOM = 3;

    private const int RIFT_HEADROOM = 9;

    /// <summary>Columns searched either side of a target for a standing spot.</summary>
    private const int SEARCH_RADIUS = 30;

    private const int MAX_SKIP = 2;

    /// <summary>
    ///     Finds the ground she stops on (<paramref name="destination" />), a few steps from the world spawn, and the
    ///     ground her rift opens on (<paramref name="rift" />) a short walk beyond it, both as bottom-centre points in
    ///     world pixels. The rift prefers the far side of the spawn from <paramref name="hint" />, so she walks toward
    ///     whoever is waiting there.
    /// </summary>
    /// <param name="hint">A player's position; also where to look when there is no open ground by the spawn.</param>
    public static bool TryFind(Vector2 hint, out Vector2 rift, out Vector2 destination)
    {
        rift = destination = Vector2.Zero;

        Point hintTile = hint.ToTileCoordinates();

        if (!TryStandingSpot(Main.spawnTileX, Main.spawnTileY, out Point spawn) && !TryStandingSpot(hintTile.X, hintTile.Y, out spawn))
            return false;

        int away = hintTile.X >= spawn.X ? -1 : 1;

        // The first pass wants a walkable path to where she stops; the second takes any open ground, and her walk
        // script puts her there if she gets stuck.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (int side in new[] { away, -away })
            {
                Point stop = StopSpot(spawn, side);

                foreach (int distance in RiftDistances)
                {
                    int x = spawn.X + side * distance;

                    if (!TryFloor(x, spawn.Y, 12, 14, out int floorY) || !IsOpenAbove(x, floorY))
                        continue;

                    if (pass == 0 && !IsWalkable(new Point(x, floorY), stop.X))
                        continue;

                    rift = Feet(new Point(x, floorY));
                    destination = Feet(stop);

                    return true;
                }
            }
        }

        // Nothing by the spawn is open enough for the rift: it opens where she stands.
        rift = destination = Feet(spawn);

        return true;
    }

    /// <summary>A standing spot <see cref="STOP_GAP" /> columns from the spawn toward <paramref name="side" />, or the spawn's own.</summary>
    private static Point StopSpot(Point spawn, int side)
    {
        int x = spawn.X + side * STOP_GAP;

        return TryFloor(x, spawn.Y, 3, 5, out int floorY) ? new Point(x, floorY) : spawn;
    }

    private static Vector2 Feet(Point floorTile) => new(floorTile.X * 16f + 8f, floorTile.Y * 16f);

    /// <summary>The nearest column to <paramref name="x" />, searched outward, that has a standing spot near <paramref name="y" />.</summary>
    private static bool TryStandingSpot(int x, int y, out Point floor)
    {
        for (int offset = 0; offset <= SEARCH_RADIUS; offset++)
        {
            for (int side = 0; side < (offset == 0 ? 1 : 2); side++)
            {
                int column = x + (side == 0 ? offset : -offset);

                if (!TryFloor(column, y, 16, 24, out int floorY))
                    continue;

                floor = new Point(column, floorY);

                return true;
            }
        }

        floor = default;

        return false;
    }

    /// <summary>The nearest standing spot in a column to <paramref name="aroundY" />, within <paramref name="up" /> and <paramref name="down" /> tiles of it.</summary>
    private static bool TryFloor(int x, int aroundY, int up, int down, out int floorY)
    {
        for (int d = 0; d <= Math.Max(up, down); d++)
        {
            if (d <= up && IsStandable(x, aroundY - d))
            {
                floorY = aroundY - d;

                return true;
            }

            if (d > 0 && d <= down && IsStandable(x, aroundY + d))
            {
                floorY = aroundY + d;

                return true;
            }
        }

        floorY = 0;

        return false;
    }

    /// <summary>
    ///     Ground with open, dry air above it that is not inside a house. The ground must be at least three tiles
    ///     deep and wider than one column, which rules out roofs, upper floors and wall tops.
    /// </summary>
    private static bool IsStandable(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 40))
            return false;

        Tile floor = Framing.GetTileSafely(x, y);

        if (!floor.HasUnactuatedTile || !Main.tileSolid[floor.TileType])
            return false;

        if (!IsSolid(x - 1, y + 1) || !IsSolid(x, y + 1) || !IsSolid(x + 1, y + 1) || !IsSolid(x, y + 2) || !IsSolid(x, y + 3))
            return false;

        for (int rise = 1; rise <= HEADROOM; rise++)
        {
            for (int side = -1; side <= 1; side++)
            {
                if (IsSolid(x + side, y - rise))
                    return false;
            }

            Tile air = Framing.GetTileSafely(x, y - rise);

            if (Main.wallHouse[air.WallType] || air.LiquidAmount > 0)
                return false;
        }

        return true;
    }

    private static bool IsOpenAbove(int x, int floorY)
    {
        for (int rise = 1; rise <= RIFT_HEADROOM; rise++)
        {
            for (int side = -2; side <= 2; side++)
            {
                if (IsSolid(x + side, floorY - rise))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether the ground between the two points is continuous to within a step or short drop, allowing up to
    ///     <see cref="MAX_SKIP" /> columns in a row with no standing spot: a crate, a sign or a small gap she jumps.
    /// </summary>
    private static bool IsWalkable(Point from, int toX)
    {
        int step = Math.Sign(toX - from.X);
        int x = from.X;
        int y = from.Y;

        while (x != toX)
        {
            bool advanced = false;

            for (int reach = 1; reach <= MAX_SKIP + 1 && !advanced; reach++)
            {
                int next = step > 0 ? Math.Min(x + reach, toX) : Math.Max(x - reach, toX);

                if (!TryFloor(next, y, 3, 5, out int floorY))
                    continue;

                x = next;
                y = floorY;
                advanced = true;
            }

            if (!advanced)
                return false;
        }

        return true;
    }

    private static bool IsSolid(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);

        return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
    }
}
