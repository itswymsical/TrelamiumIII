namespace TrelamiumIII.Content.Dryad;

/// <summary>Picks the ground the Dryad walks to and the ground the rift opens on.</summary>
internal static class DryadArrivalSite
{
    /// <summary>Tiles from the town centre to the rift, widened to clear the outermost house up to <see cref="RIFT_DISTANCE_MAX" />.</summary>
    private const int RIFT_DISTANCE = 36;

    private const int RIFT_DISTANCE_MAX = 60;

    /// <summary>Open tiles a standing spot needs above it, and the rift's floor point needs above it.</summary>
    private const int HEADROOM = 3;

    private const int RIFT_HEADROOM = 9;

    /// <summary>Columns searched either side of a target for a standing spot.</summary>
    private const int SEARCH_RADIUS = 30;

    private const int MAX_SKIP = 2;

    private static readonly int[] RiftDistanceOffsets = [0, -4, 4, -8, 8, -12, 12];

    /// <summary>Mean home tile of the housed town NPCs; the Guide, then the world spawn, when none are housed.</summary>
    public static Point TownCenter()
    {
        long x = 0;
        long y = 0;
        int count = 0;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!IsResident(npc))
                continue;

            x += npc.homeTileX;
            y += npc.homeTileY;
            count++;
        }

        if (count > 0)
            return new Point((int)(x / count), (int)(y / count));

        int guide = NPC.FindFirstNPC(NPCID.Guide);

        return guide >= 0 ? Main.npc[guide].Center.ToTileCoordinates() : new Point(Main.spawnTileX, Main.spawnTileY);
    }

    /// <summary>
    ///     Finds the ground the Dryad ends up on (<paramref name="destination" />) and the ground the rift opens on
    ///     (<paramref name="rift" />), both as bottom-centre points in world pixels.
    /// </summary>
    /// <param name="hint">Where to look when the town has no open ground, and which side the rift prefers.</param>
    public static bool TryFind(Vector2 hint, out Vector2 rift, out Vector2 destination)
    {
        rift = destination = Vector2.Zero;

        Point town = TownCenter();
        Point hintTile = hint.ToTileCoordinates();

        if (!TryStandingSpot(town.X, town.Y, out Point standing) && !TryStandingSpot(hintTile.X, hintTile.Y, out standing))
            return false;

        destination = Feet(standing);
        rift = destination;

        int distance = Math.Min(Math.Max(RIFT_DISTANCE, TownRadius(town) + 12), RIFT_DISTANCE_MAX);
        int preferred = hint.X >= destination.X ? 1 : -1;

        // The first pass wants a walkable path back; the second takes any open ground, and her walk
        // script teleports her if she gets stuck.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (int extra in RiftDistanceOffsets)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int x = standing.X + (attempt == 0 ? preferred : -preferred) * (distance + extra);

                    if (!TryFloor(x, standing.Y, 12, 14, out int floorY) || !IsOpenAbove(x, floorY))
                        continue;

                    if (pass == 0 && !IsWalkable(new Point(x, floorY), standing.X))
                        continue;

                    rift = Feet(new Point(x, floorY));

                    return true;
                }
            }
        }

        return true;
    }

    private static bool IsResident(NPC npc) =>
        npc.townNPC
        && !npc.homeless
        && npc.homeTileX > 0
        && npc.type != NPCID.TravellingMerchant
        && !NPCID.Sets.IsTownPet[npc.type];

    private static int TownRadius(Point center)
    {
        int radius = 0;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (IsResident(npc))
                radius = Math.Max(radius, Math.Abs(npc.homeTileX - center.X));
        }

        return radius;
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
