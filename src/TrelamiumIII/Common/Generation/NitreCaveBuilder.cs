using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Nitre cave dimensions in tiles: <see cref="ChamberCount"/> warped elliptical chambers spread across a
/// <see cref="HalfWidth"/> by <see cref="HalfHeight"/> area, joined by tunnels. Roughly half to two thirds of a marble cave.
/// </summary>
public readonly record struct NitreCaveShape(int HalfWidth, int HalfHeight, int ChamberCount, int PoolCount)
{
    public const int ShellThickness = 4;
    public const float WarpAmplitude = 5f;

    public int Margin => (int)MathF.Ceiling(WarpAmplitude) + ShellThickness + 3;

    public Rectangle Bounds(Point center) => new(
        center.X - HalfWidth - Margin,
        center.Y - HalfHeight - Margin,
        (HalfWidth + Margin) * 2 + 1,
        (HalfHeight + Margin) * 2 + 1);

    public static NitreCaveShape Roll() => new(
        HalfWidth: WorldGen.genRand.Next(28, 41),
        HalfHeight: WorldGen.genRand.Next(16, 23),
        ChamberCount: WorldGen.genRand.Next(3, 6),
        PoolCount: WorldGen.genRand.Next(2, 5));
}

/// <summary>
/// Carves a nitre cave: ordinary rock, stalactites and rubble, with aqua fortis pools sunk into its floors. The rock
/// is left as found; only gaps that would let outside liquid in, and the pools' linings, are filled with stone.
/// </summary>
public static class NitreCaveBuilder
{
    private const float WarpFrequency = 0.04f;

    private const int PoolLining = 2;
    private const int MaxFloorStep = 2;

    private const int LargeRubbleChance = 14;
    private const int SmallRubbleChance = 5;
    private const int HangingStalactiteChance = 3;

    public static void Build(NitreCaveShape shape, Point center)
    {
        Rectangle bounds = shape.Bounds(center);

        bool[,] cavity = CarveMask(shape, center, bounds);
        int[,] distance = MaskDistance.Chebyshev(cavity, NitreCaveShape.ShellThickness + 2);

        ClearObjects(bounds, cavity, distance);
        WriteTerrain(bounds, cavity, distance);
        AddStalactites(bounds, cavity);

        int[] floor = FloorRows(cavity, bounds);
        List<(int Left, int Right, int Level)> pools = DigPools(shape, bounds, floor);

        SmoothCeilings(bounds, cavity);
        FillPools(pools);
        Decorate(bounds, cavity);
    }

    private static bool[,] CarveMask(NitreCaveShape shape, Point center, Rectangle bounds)
    {
        var chambers = new List<(Vector2 Center, Vector2 Radius)>();
        int count = shape.ChamberCount;

        for (int n = 0; n < count; n++)
        {
            var radius = new Vector2(WorldGen.genRand.Next(9, 16), WorldGen.genRand.Next(6, 11));

            // Spread evenly across the width, jittered, at random heights.
            float t = count == 1 ? 0.5f : n / (float)(count - 1);
            float x = MathHelper.Lerp(-shape.HalfWidth + radius.X, shape.HalfWidth - radius.X, t) + WorldGen.genRand.NextFloat(-4f, 4f);
            float yRange = Math.Max(shape.HalfHeight - radius.Y, 0);
            float y = WorldGen.genRand.NextFloat(-yRange, yRange);

            chambers.Add((center.ToVector2() + new Vector2(x, y), radius));
        }

        var tunnels = new List<(Vector2 From, Vector2 To, float Radius)>();
        for (int n = 1; n < chambers.Count; n++)
            tunnels.Add((chambers[n - 1].Center, chambers[n].Center, WorldGen.genRand.NextFloat(2.5f, 4f)));

        FastNoiseLite warp = CreateNoise(WarpFrequency);
        var cavity = new bool[bounds.Width, bounds.Height];
        int guard = NitreCaveShape.ShellThickness + 1;

        for (int i = guard; i < bounds.Width - guard; i++)
        {
            for (int j = guard; j < bounds.Height - guard; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;
                var point = new Vector2(
                    x + warp.GetNoise(x, y) * NitreCaveShape.WarpAmplitude,
                    y + warp.GetNoise(x + 500f, y + 500f) * NitreCaveShape.WarpAmplitude * 0.6f);

                cavity[i, j] = InsideAny(point, chambers, tunnels);
            }
        }

        return cavity;
    }

    private static bool InsideAny(Vector2 point, List<(Vector2 Center, Vector2 Radius)> chambers, List<(Vector2 From, Vector2 To, float Radius)> tunnels)
    {
        foreach ((Vector2 center, Vector2 radius) in chambers)
        {
            Vector2 d = (point - center) / radius;
            if (d.LengthSquared() < 1f)
                return true;
        }

        foreach ((Vector2 from, Vector2 to, float radius) in tunnels)
        {
            Vector2 segment = to - from;
            float t = MathHelper.Clamp(Vector2.Dot(point - from, segment) / segment.LengthSquared(), 0f, 1f);
            if (Vector2.DistanceSquared(point, from + segment * t) < radius * radius)
                return true;
        }

        return false;
    }

    private static FastNoiseLite CreateNoise(float frequency)
    {
        var noise = new FastNoiseLite(WorldGen.genRand.Next());
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    private static void ClearObjects(Rectangle bounds, bool[,] cavity, int[,] distance)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                if (!cavity[i, j] && distance[i, j] > NitreCaveShape.ShellThickness)
                    continue;

                Tile tile = Main.tile[bounds.X + i, bounds.Y + j];
                if (tile.HasTile && Main.tileFrameImportant[tile.TileType])
                    LiquidDepositBuilder.ClearObject(bounds.X + i, bounds.Y + j);
            }
        }
    }

    /// <summary>
    /// Empties the cavity. Dry gaps in the shell stay open, so the cave joins the caves around it; gaps holding or
    /// touching outside liquid are sealed with stone, since no settle pass runs after this.
    /// </summary>
    private static void WriteTerrain(Rectangle bounds, bool[,] cavity, int[,] distance)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;
                Tile tile = Main.tile[x, y];

                if (cavity[i, j])
                {
                    tile.ClearTile();
                    tile.LiquidAmount = 0;
                    continue;
                }

                if (distance[i, j] > NitreCaveShape.ShellThickness || LiquidDepositBuilder.BlocksLiquid(tile))
                    continue;

                if (tile.LiquidAmount > 0 || TouchesOutsideLiquid(bounds, cavity, x, y))
                    LiquidDepositBuilder.SetSolid(tile, TileID.Stone);
            }
        }
    }

    private static bool TouchesOutsideLiquid(Rectangle bounds, bool[,] cavity, int x, int y)
    {
        foreach (Point next in (ReadOnlySpan<Point>)[new(x - 1, y), new(x + 1, y), new(x, y - 1), new(x, y + 1)])
        {
            int i = next.X - bounds.X;
            int j = next.Y - bounds.Y;
            bool inCavity = i >= 0 && j >= 0 && i < bounds.Width && j < bounds.Height && cavity[i, j];

            if (!inCavity && Main.tile[next.X, next.Y].LiquidAmount > 0)
                return true;
        }

        return false;
    }

    /// <summary> Tapered spikes hanging from the ceilings, of the rock they hang from. Removes their tiles from <paramref name="cavity"/>. </summary>
    private static void AddStalactites(Rectangle bounds, bool[,] cavity)
    {
        for (int i = WorldGen.genRand.Next(2, 6); i < bounds.Width - 2; i += WorldGen.genRand.Next(4, 10))
        {
            int ceiling = -1;
            for (int j = 1; j < bounds.Height; j++)
            {
                if (cavity[i, j] && !cavity[i, j - 1])
                {
                    ceiling = j;
                    break;
                }
            }

            if (ceiling < 0)
                continue;

            Tile anchor = Main.tile[bounds.X + i, bounds.Y + ceiling - 1];
            if (!anchor.HasTile || !Main.tileSolid[anchor.TileType])
                continue;

            int open = 0;
            while (ceiling + open < bounds.Height && cavity[i, ceiling + open])
                open++;

            int length = Math.Min(WorldGen.genRand.Next(3, 10), open - 5);
            if (length < 3)
                continue;

            ushort type = SpikeMaterial(anchor.TileType);
            int baseHalfWidth = length >= 7 ? 2 : 1;
            int column = i;

            for (int k = 0; k < length; k++)
            {
                if (k > 1 && WorldGen.genRand.NextBool(4))
                    column += WorldGen.genRand.Next(-1, 2);

                int halfWidth = (int)MathF.Round(baseHalfWidth * (1f - k / (float)length));

                for (int ci = Math.Max(column - halfWidth, 0); ci <= Math.Min(column + halfWidth, bounds.Width - 1); ci++)
                {
                    // The base row also fills upward to the ceiling where it sits higher beside the column.
                    int stop = k == 0 ? Math.Max(ceiling - 4, 0) : ceiling + k;

                    for (int cj = ceiling + k; cj >= stop && cavity[ci, cj]; cj--)
                    {
                        cavity[ci, cj] = false;
                        LiquidDepositBuilder.SetSolid(Main.tile[bounds.X + ci, bounds.Y + cj], type);
                    }
                }
            }

            i += baseHalfWidth * 2;
        }
    }

    private static ushort SpikeMaterial(ushort anchorType) => anchorType switch
    {
        TileID.Stone or TileID.Mud or TileID.IceBlock or TileID.Granite or TileID.Marble
            or TileID.Ebonstone or TileID.Crimstone or TileID.Pearlstone or TileID.Sandstone => anchorType,
        _ => TileID.Stone,
    };

    /// <summary> Per column, the world row of the solid tile under the lowest cavity cell, or -1. </summary>
    private static int[] FloorRows(bool[,] cavity, Rectangle bounds)
    {
        var floor = new int[bounds.Width];

        for (int i = 0; i < bounds.Width; i++)
        {
            floor[i] = -1;

            for (int j = bounds.Height - 2; j >= 0; j--)
            {
                if (cavity[i, j])
                {
                    floor[i] = bounds.Y + j + 1;
                    break;
                }
            }
        }

        return floor;
    }

    /// <summary>
    /// Sinks half-ellipse basins into fairly level stretches of floor and lines everything at or below their surface
    /// with stone, so the acid sits sealed and level. Returns each pool's column span and surface row.
    /// </summary>
    private static List<(int Left, int Right, int Level)> DigPools(NitreCaveShape shape, Rectangle bounds, int[] floor)
    {
        var pools = new List<(int, int, int)>();
        var occupied = new bool[bounds.Width];

        for (int n = 0; n < shape.PoolCount; n++)
        {
            int halfWidth = WorldGen.genRand.Next(4, 9);
            int depth = WorldGen.genRand.Next(3, 6);

            if (!TryPickPool(bounds, floor, occupied, halfWidth, out int px))
                continue;

            int level = floor[px - bounds.X];
            int left = px - halfWidth;
            int right = px + halfWidth;
            var basin = new HashSet<Point>();

            for (int x = left; x <= right; x++)
            {
                for (int y = level; y <= level + depth; y++)
                {
                    float dx = (x - px) / (float)halfWidth;
                    float dy = (y - level + 0.5f) / depth;

                    if (dx * dx + dy * dy < 1f)
                        basin.Add(new Point(x, y));
                }
            }

            foreach (Point cell in basin)
                Main.tile[cell.X, cell.Y].ClearTile();

            foreach (Point cell in basin)
            {
                for (int ox = -PoolLining; ox <= PoolLining; ox++)
                {
                    for (int oy = -PoolLining; oy <= PoolLining; oy++)
                    {
                        var next = new Point(cell.X + ox, cell.Y + oy);
                        if (next.Y >= level && !basin.Contains(next) && !LiquidDepositBuilder.BlocksLiquid(Main.tile[next.X, next.Y]))
                            LiquidDepositBuilder.SetSolid(Main.tile[next.X, next.Y], TileID.Stone);
                    }
                }
            }

            pools.Add((left, right, level));

            for (int c = Math.Max(left - 3 - bounds.X, 0); c <= Math.Min(right + 3 - bounds.X, bounds.Width - 1); c++)
                occupied[c] = true;
        }

        return pools;
    }

    /// <summary> A pool centre whose span has floor within <see cref="MaxFloorStep"/> rows of its own and no other pool. </summary>
    private static bool TryPickPool(Rectangle bounds, int[] floor, bool[] occupied, int halfWidth, out int px)
    {
        px = 0;
        int margin = halfWidth + PoolLining + 1;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            int i = WorldGen.genRand.Next(margin, bounds.Width - margin);
            int level = floor[i];
            if (level < 0)
                continue;

            bool fits = true;
            for (int c = i - halfWidth; c <= i + halfWidth && fits; c++)
                fits = !occupied[c] && floor[c] >= 0 && Math.Abs(floor[c] - level) <= MaxFloorStep;

            if (fits)
            {
                px = bounds.X + i;
                return true;
            }
        }

        return false;
    }

    /// <summary> Slopes the exposed ceilings and walls; floors stay square so pools and rubble sit flat. </summary>
    private static void SmoothCeilings(Rectangle bounds, bool[,] cavity)
    {
        for (int i = 1; i < bounds.Width - 1; i++)
        {
            for (int j = 1; j < bounds.Height - 1; j++)
            {
                if (cavity[i, j] || !(cavity[i - 1, j] || cavity[i + 1, j] || cavity[i, j + 1]) || cavity[i, j - 1])
                    continue;

                int x = bounds.X + i;
                int y = bounds.Y + j;
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && Main.tile[x, y + 1].LiquidAmount == 0)
                    Tile.SmoothSlope(x, y, applyToNeighbors: false);
            }
        }
    }

    private static void FillPools(List<(int Left, int Right, int Level)> pools)
    {
        int acid = LiquidLoader.LiquidType<AcidicLiquids>();
        byte aquaFortis = AcidicLiquids.Instance.AquaFortis.Index;

        foreach ((int left, int right, int level) in pools)
        {
            for (int x = left; x <= right; x++)
            {
                for (int y = level; !WorldGen.SolidTile(x, y) && y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.LiquidType = acid;
                    tile.LiquidAmount = byte.MaxValue;
<<<<<<< HEAD
                    tile.InternalLiquid() = aquaFortis;
=======
                    tile.LiquidCondition() = aquaFortis;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
                }
            }
        }
    }

    /// <summary> Rubble on dry floors and small stalactites under ceilings, as in ordinary caves. </summary>
    private static void Decorate(Rectangle bounds, bool[,] cavity)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 1; j < bounds.Height - 1; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;
                Tile tile = Main.tile[x, y];

                if (!cavity[i, j] || tile.HasTile || tile.LiquidAmount > 0)
                    continue;

                if (WorldGen.SolidTile(x, y + 1))
                {
                    if (WorldGen.genRand.NextBool(LargeRubbleChance))
                        LiquidDepositBuilder.PlaceLargeRubble(x, y, desert: false);
                    else if (WorldGen.genRand.NextBool(SmallRubbleChance))
                        LiquidDepositBuilder.PlaceSmallRubble(x, y);
                }
                else if (WorldGen.SolidTile(x, y - 1) && Main.tile[x, y + 1].LiquidAmount == 0 && WorldGen.genRand.NextBool(HangingStalactiteChance))
                {
                    WorldGen.PlaceTight(x, y);
                }
            }
        }
    }
}
