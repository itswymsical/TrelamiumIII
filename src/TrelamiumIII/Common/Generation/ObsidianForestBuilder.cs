using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Content.ObsidianForest;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Obsidian Forest cavern dimensions in tiles. The dome is a half-ellipse above the centre row and the floor a flattened
/// bowl <see cref="FloorDepth"/> rows deep below it.
/// </summary>
public readonly record struct ObsidianForestShape(int HalfWidth, int DomeHeight, int FloorDepth, int PoolCount, int SpireCount)
{
    public const int ShellThickness = 5;
    public const float WarpAmplitude = 6f;
    public const float GrooveDepth = 3f;
    public const float StalacDepth = 4f;

    public int Margin => (int)MathF.Ceiling(WarpAmplitude + GrooveDepth + StalacDepth) + ShellThickness + 3;

    public Rectangle Bounds(Point center) => new(
        center.X - HalfWidth - Margin,
        center.Y - DomeHeight - Margin,
        (HalfWidth + Margin) * 2 + 1,
        DomeHeight + FloorDepth + Margin * 2 + 1);

    public static ObsidianForestShape Roll() => new(
        HalfWidth: WorldGen.genRand.Next(46, 62),
        DomeHeight: WorldGen.genRand.Next(22, 30),
        FloorDepth: WorldGen.genRand.Next(5, 8),
        PoolCount: WorldGen.genRand.Next(2, 4),
        SpireCount: WorldGen.genRand.Next(2, 5));
}

public static class ObsidianForestBuilder
{
    private const float WarpFrequency = 0.025f;
    private const float GrooveFrequency = 0.55f;
    private const float StalacFrequency = 0.33f;
    private const float VeinFrequency = 0.045f;

    /// <summary> Ridged-noise band width that turns shell tiles into obsidian. </summary>
    private const float VeinWidth = 0.09f;

    /// <summary> Columns kept clear of pools and spires at the cavern's sides, where the floor curves up. </summary>
    private const int FloorInset = 8;

    private const int PoolLining = 2;
    private const int SpireClearance = 6;

    /// <summary> Columns skipped after a tree, so gem tree canopies don't overlap. </summary>
    private const int MinTreeSpacing = 4;
    private const int FoliageChance = 2;

    public static void Build(ObsidianForestShape shape, Point center)
    {
        Rectangle bounds = shape.Bounds(center);
        ushort tuff = (ushort)ModContent.TileType<TuffTile>();

        bool[,] cavity = CarveMask(shape, center, bounds);
        int[,] distance = MaskDistance.Chebyshev(cavity, ObsidianForestShape.ShellThickness + 3);
        int[] floor = FloorRows(cavity, bounds, center);

        ClearObjects(bounds, cavity, distance);
        WriteTerrain(bounds, cavity, distance, center.Y, tuff);

        var occupied = new bool[bounds.Width];
        List<(int Left, int Right, int Level)> pools = DigPools(shape, center, bounds, floor, occupied);
        RaiseSpires(shape, center, bounds, cavity, floor, occupied, tuff);
        SmoothUpperWalls(bounds, cavity, center.Y);
        GrowTrees(bounds, floor, occupied, tuff);
        PlaceFoliage(bounds, cavity, tuff);
        FillPools(pools);
    }

    private static bool[,] CarveMask(ObsidianForestShape shape, Point center, Rectangle bounds)
    {
        FastNoiseLite warp = CreateNoise(WarpFrequency);
        FastNoiseLite grooveDepth = CreateNoise(0.08f);
        float groovePhase = WorldGen.genRand.NextFloat(MathHelper.TwoPi);
        float flutePhase = WorldGen.genRand.NextFloat(MathHelper.TwoPi);

        var cavity = new bool[bounds.Width, bounds.Height];
        int guard = ObsidianForestShape.ShellThickness + 1;

        for (int i = guard; i < bounds.Width - guard; i++)
        {
            for (int j = guard; j < bounds.Height - guard; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                float wx = x + warp.GetNoise(x, y) * ObsidianForestShape.WarpAmplitude;
                float wy = y + warp.GetNoise(x + 500f, y + 500f) * ObsidianForestShape.WarpAmplitude * 0.5f;

                // Horizontal grooves: narrow bands of rows where the side walls are cut deeper.
                float groove = MathF.Pow(MathF.Max(MathF.Sin(y * GrooveFrequency + groovePhase), 0f), 4f);
                float halfWidth = shape.HalfWidth + ObsidianForestShape.GrooveDepth * groove * (0.6f + 0.4f * grooveDepth.GetNoise(x, y));
                float dx = (wx - center.X) / halfWidth;

                if (wy < center.Y)
                {
                    // Flutes: columns where the ceiling reaches higher.
                    float flute = MathF.Pow(MathF.Max(MathF.Sin(x * StalacFrequency + flutePhase), 0f), 6f);
                    float dy = (wy - center.Y) / (shape.DomeHeight + ObsidianForestShape.StalacDepth * flute);
                    cavity[i, j] = dx * dx + dy * dy < 1f;
                }
                else
                {
                    // dx^4 flattens the bowl so most of the floor is level enough for trees.
                    float dy = (wy - center.Y) / shape.FloorDepth;
                    cavity[i, j] = dx * dx * dx * dx + dy * dy < 1f;
                }
            }
        }

        return cavity;
    }

    private static FastNoiseLite CreateNoise(float frequency)
    {
        var noise = new FastNoiseLite(WorldGen.genRand.Next());
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    /// <summary> Per column, the world row of the first solid tile under the cavity's centre row, or -1. </summary>
    private static int[] FloorRows(bool[,] cavity, Rectangle bounds, Point center)
    {
        var floor = new int[bounds.Width];
        int centerRow = center.Y - bounds.Y;

        for (int i = 0; i < bounds.Width; i++)
        {
            floor[i] = -1;

            if (!cavity[i, centerRow])
                continue;

            for (int j = centerRow; j < bounds.Height; j++)
            {
                if (!cavity[i, j])
                {
                    floor[i] = bounds.Y + j;
                    break;
                }
            }
        }

        return floor;
    }

    private static void ClearObjects(Rectangle bounds, bool[,] cavity, int[,] distance)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                if (!cavity[i, j] && distance[i, j] > ObsidianForestShape.ShellThickness)
                    continue;

                Tile tile = Main.tile[bounds.X + i, bounds.Y + j];
                if (tile.HasTile && Main.tileFrameImportant[tile.TileType])
                    LiquidDepositBuilder.ClearObject(bounds.X + i, bounds.Y + j);
            }
        }
    }

    /// <summary>
    /// Empties the cavity and turns the shell into tuff with obsidian veins. Dry gaps in the shell above
    /// <paramref name="centerY"/> stay open as ways in; the floor is sealed, and so is any gap holding or touching
    /// outside liquid, since no settle pass runs after this.
    /// </summary>
    private static void WriteTerrain(Rectangle bounds, bool[,] cavity, int[,] distance, int centerY, ushort tuff)
    {
        FastNoiseLite veins = CreateNoise(VeinFrequency);
        FastNoiseLite edge = CreateNoise(0.1f);
        ushort wall = (ushort)ModContent.WallType<TuffWall>();

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
                    tile.WallType = wall;
                    continue;
                }

                float thickness = ObsidianForestShape.ShellThickness + edge.GetNoise(x, y) * 2f;
                if (distance[i, j] > thickness)
                    continue;

                tile.WallType = wall;

                bool solid = tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
                if (!solid && y < centerY && tile.LiquidAmount == 0 && !TouchesOutsideLiquid(bounds, cavity, x, y))
                    continue;

                ushort type = MathF.Abs(veins.GetNoise(x, y)) < VeinWidth ? TileID.Obsidian : tuff;
                LiquidDepositBuilder.SetSolid(tile, type);
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

    /// <summary>
    /// Sinks half-ellipse basins into the floor and lines everything at or below their surface with obsidian, so the
    /// acid sits sealed and level. Returns each pool's column span and surface row.
    /// </summary>
    private static List<(int Left, int Right, int Level)> DigPools(ObsidianForestShape shape, Point center, Rectangle bounds, int[] floor, bool[] occupied)
    {
        var pools = new List<(int, int, int)>();
        int span = shape.HalfWidth - FloorInset;

        for (int n = 0; n < shape.PoolCount; n++)
        {
            int halfWidth = WorldGen.genRand.Next(5, 11);
            int depth = WorldGen.genRand.Next(3, 6);

            if (!TryPickColumn(center.X - span + halfWidth, center.X + span - halfWidth, halfWidth + 3, bounds, floor, occupied, out int px))
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
                        if (next.Y >= level && !basin.Contains(next))
                            LiquidDepositBuilder.SetSolid(Main.tile[next.X, next.Y], TileID.Obsidian);
                    }
                }
            }

            pools.Add((left, right, level));
            Occupy(occupied, bounds, left - 2, right + 2);
        }

        return pools;
    }

    /// <summary> Spires: a tuff column narrowing upward under a wider obsidian cap. </summary>
    private static void RaiseSpires(ObsidianForestShape shape, Point center, Rectangle bounds, bool[,] cavity, int[] floor, bool[] occupied, ushort tuff)
    {
        int span = shape.HalfWidth - FloorInset;

        for (int n = 0; n < shape.SpireCount; n++)
        {
            if (!TryPickColumn(center.X - span, center.X + span, 4, bounds, floor, occupied, out int sx))
                continue;

            int baseRow = floor[sx - bounds.X];
            int ceiling = CeilingRow(cavity, bounds, sx);
            int height = Math.Min(WorldGen.genRand.Next(8, 17), baseRow - ceiling - SpireClearance);
            if (height < 5)
                continue;

            int column = sx;
            int top = baseRow - height;

            for (int y = baseRow - 1; y > top; y--)
            {
                int k = baseRow - 1 - y;
                if (k > 2 && WorldGen.genRand.NextBool(5))
                    column += WorldGen.genRand.Next(-1, 2);

                int halfWidth = (int)MathF.Round(2f * MathF.Pow(1f - k / (float)height, 0.6f));
                for (int x = column - halfWidth; x <= column + halfWidth; x++)
                    LiquidDepositBuilder.SetSolid(Main.tile[x, y], tuff);
            }

            for (int x = column - 2; x <= column + 2; x++)
                LiquidDepositBuilder.SetSolid(Main.tile[x, top], TileID.Obsidian);

            for (int x = column - 1; x <= column + 1; x++)
                LiquidDepositBuilder.SetSolid(Main.tile[x, top - 1], TileID.Obsidian);

            Occupy(occupied, bounds, sx - 3, sx + 3);
        }
    }

    private static int CeilingRow(bool[,] cavity, Rectangle bounds, int x)
    {
        int i = x - bounds.X;

        for (int j = 0; j < bounds.Height; j++)
        {
            if (cavity[i, j])
                return bounds.Y + j;
        }

        return bounds.Y;
    }

    /// <summary> A random column in [<paramref name="from"/>, <paramref name="to"/>] with a floor and nothing within <paramref name="clearance"/>. </summary>
    private static bool TryPickColumn(int from, int to, int clearance, Rectangle bounds, int[] floor, bool[] occupied, out int x)
    {
        x = 0;
        if (to < from)
            return false;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            x = WorldGen.genRand.Next(from, to + 1);
            if (floor[x - bounds.X] < 0)
                continue;

            bool free = true;
            for (int c = Math.Max(x - clearance - bounds.X, 0); c <= Math.Min(x + clearance - bounds.X, bounds.Width - 1) && free; c++)
                free = !occupied[c];

            if (free)
                return true;
        }

        return false;
    }

    private static void Occupy(bool[] occupied, Rectangle bounds, int left, int right)
    {
        for (int c = Math.Max(left - bounds.X, 0); c <= Math.Min(right - bounds.X, occupied.Length - 1); c++)
            occupied[c] = true;
    }

    /// <summary> Slopes the dome's exposed edges; the floor stays square so trees, plants and pools sit flat. </summary>
    private static void SmoothUpperWalls(Rectangle bounds, bool[,] cavity, int centerY)
    {
        for (int i = 1; i < bounds.Width - 1; i++)
        {
            for (int j = 1; j < bounds.Height - 1; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                if (cavity[i, j] || y >= centerY || !(cavity[i - 1, j] || cavity[i + 1, j] || cavity[i, j + 1]))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                    Tile.SmoothSlope(x, y, applyToNeighbors: false);
            }
        }
    }

    /// <summary> Emerald gem trees as placeholders, on free tuff floor. </summary>
    private static void GrowTrees(Rectangle bounds, int[] floor, bool[] occupied, ushort tuff)
    {
        WorldGen.GrowTreeSettings settings = WorldGen.GrowTreeSettings.Profiles.GemTree_Emerald;
        settings.GroundTest = type => type == tuff;
        settings.WallTest = _ => true;

        for (int i = WorldGen.genRand.Next(0, 3); i < bounds.Width; i++)
        {
            if (floor[i] < 0 || occupied[i])
                continue;

            if (!WorldGen.GrowTreeWithSettings(bounds.X + i, floor[i], settings))
                continue;

            Occupy(occupied, bounds, bounds.X + i - 2, bounds.X + i + 2);
            i += WorldGen.genRand.Next(MinTreeSpacing, MinTreeSpacing + 4);
        }
    }

    private static void PlaceFoliage(Rectangle bounds, bool[,] cavity, ushort tuff)
    {
        int foliage = ModContent.TileType<TuffFoliageTile>();

        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 1; j < bounds.Height; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                if (!cavity[i, j - 1] || Main.tile[x, y - 1].HasTile || Main.tile[x, y].TileType != tuff || !WorldGen.SolidTile(x, y))
                    continue;

                if (WorldGen.genRand.NextBool(FoliageChance))
                    WorldGen.PlaceTile(x, y - 1, foliage, mute: true, style: WorldGen.genRand.Next(TuffFoliageTile.Styles));
            }
        }
    }

    private static void FillPools(List<(int Left, int Right, int Level)> pools)
    {
        int acid = LiquidLoader.LiquidType<AcidicLiquids>();

        foreach ((int left, int right, int level) in pools)
        {
            for (int x = left; x <= right; x++)
            {
                for (int y = level; !WorldGen.SolidTile(x, y) && y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.LiquidType = acid;
                    tile.LiquidAmount = byte.MaxValue;
                    tile.InternalLiquid() = 0;
                }
            }
        }
    }
}