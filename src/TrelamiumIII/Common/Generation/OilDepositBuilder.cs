using System.Collections.Generic;
using ModLiquidLib.ID;
using Terraria.DataStructures;
using Terraria.ObjectData;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Oil cavity dimensions in tiles. The dome is the half-ellipse above the centre and the basin the half-ellipse
/// below it; oil fills every cavity row from <see cref="LevelOffset"/> rows below the centre downward.
/// </summary>
public readonly record struct OilDepositShape(
    int HalfWidth,
    int DomeHeight,
    int BasinDepth,
    int LevelOffset,
    float WarpAmplitude,
    float WarpFrequency,
    float DetailAmplitude,
    float DetailFrequency,
    int ShellThickness,
    bool Stalactites)
{
    /// <summary> Space around the undistorted ellipse that holds the maximum warp displacement plus the shell. </summary>
    public int Margin => (int)MathF.Ceiling(WarpAmplitude + DetailAmplitude) + ShellThickness + 2;

    public Rectangle Bounds(Point center) => new(
        center.X - HalfWidth - Margin,
        center.Y - DomeHeight - Margin,
        (HalfWidth + Margin) * 2 + 1,
        DomeHeight + BasinDepth + Margin * 2 + 1);

    public static OilDepositShape RollLake() => new(
        HalfWidth: WorldGen.genRand.Next(42, 66),
        DomeHeight: WorldGen.genRand.Next(18, 26),
        BasinDepth: WorldGen.genRand.Next(10, 15),
        LevelOffset: WorldGen.genRand.Next(0, 3),
        WarpAmplitude: 9f,
        WarpFrequency: 0.02f,
        DetailAmplitude: 2.5f,
        DetailFrequency: 0.09f,
        ShellThickness: 4,
        Stalactites: true);

    public static OilDepositShape RollPocket() => new(
        HalfWidth: WorldGen.genRand.Next(7, 12),
        DomeHeight: WorldGen.genRand.Next(4, 7),
        BasinDepth: WorldGen.genRand.Next(4, 7),
        LevelOffset: 0,
        WarpAmplitude: 3f,
        WarpFrequency: 0.06f,
        DetailAmplitude: 1f,
        DetailFrequency: 0.15f,
        ShellThickness: 2,
        Stalactites: false);
}

public static class OilDepositBuilder
{
    private const int LargeRubbleChance = 14;
    private const int SmallRubbleChance = 5;
    private const int HangingStalactiteChance = 4;
    private const int SpikeBaseRise = 6;

    /// <summary>
    /// Carves the cavity, makes the shell below the oil level liquid-tight, adds stalactites and rubble, then fills
    /// the basin. <paramref name="desert"/> converts the whole shell to sandstone; otherwise only gaps and falling
    /// tiles in the shell are replaced, with stone.
    /// </summary>
    public static void Build(OilDepositShape shape, Point center, bool desert, int oilType)
    {
        Rectangle bounds = shape.Bounds(center);
        int level = center.Y + shape.LevelOffset;
        ushort material = desert ? TileID.Sandstone : TileID.Stone;

        bool[,] cavity = CarveMask(shape, center, bounds);
        List<(Point Tile, ushort Type)> spikes = shape.Stalactites ? AddStalactites(shape, center, bounds, level, cavity, desert) : [];
        int[,] distance = DistanceToCavity(cavity, shape.ShellThickness + 1);

        ClearObjects(bounds, cavity, distance, shape.ShellThickness);
        WriteTiles(bounds, cavity, distance, shape.ShellThickness, level, material, desert);

        foreach ((Point tile, ushort type) in spikes)
            SetSolid(Main.tile[tile.X, tile.Y], type);

        SmoothDryEdges(bounds, cavity, level);
        Decorate(bounds, cavity, level, desert);
        FillOil(bounds, cavity, level, oilType);
    }

    private static bool[,] CarveMask(OilDepositShape shape, Point center, Rectangle bounds)
    {
        FastNoiseLite warp = CreateNoise(shape.WarpFrequency);
        FastNoiseLite detail = CreateNoise(shape.DetailFrequency);

        var cavity = new bool[bounds.Width, bounds.Height];
        int guard = shape.ShellThickness + 1;

        for (int i = guard; i < bounds.Width - guard; i++)
        {
            for (int j = guard; j < bounds.Height - guard; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                float warpedX = x + warp.GetNoise(x, y) * shape.WarpAmplitude + detail.GetNoise(x, y) * shape.DetailAmplitude;
                float warpedY = y + warp.GetNoise(x + 500f, y + 500f) * shape.WarpAmplitude + detail.GetNoise(x + 500f, y + 500f) * shape.DetailAmplitude;

                float dx = (warpedX - center.X) / shape.HalfWidth;
                float dy = (warpedY - center.Y) / (warpedY < center.Y ? shape.DomeHeight : shape.BasinDepth);

                cavity[i, j] = dx * dx + dy * dy < 1f;
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

    /// <summary>
    /// Tapered spikes hanging from the dome, longer toward the centre and ending at least 4 rows above the oil.
    /// Removes spike tiles from <paramref name="cavity"/> and returns them with their tile type.
    /// </summary>
    private static List<(Point Tile, ushort Type)> AddStalactites(OilDepositShape shape, Point center, Rectangle bounds, int level, bool[,] cavity, bool desert)
    {
        var spikes = new List<(Point, ushort)>();
        int width = bounds.Width;

        for (int i = shape.Margin + WorldGen.genRand.Next(2, 6); i < width - shape.Margin; i += WorldGen.genRand.Next(4, 10))
        {
            int ceiling = FindCeiling(cavity, i);
            if (ceiling <= 0)
                continue;

            int x = bounds.X + i;
            Tile anchor = Main.tile[x, bounds.Y + ceiling - 1];
            if (!anchor.HasTile || !Main.tileSolid[anchor.TileType])
                continue;

            int available = level - 4 - (bounds.Y + ceiling);
            float centrality = Math.Max(1f - Math.Abs(x - center.X) / (float)shape.HalfWidth, 0f);
            int length = (int)(available * WorldGen.genRand.NextFloat(0.3f, 0.65f) * (0.35f + 0.65f * centrality));
            if (length < 3)
                continue;

            int baseHalfWidth = Math.Clamp(length / 5, 1, 3);
            ushort type = desert ? TileID.Sandstone : SpikeMaterial(anchor.TileType);
            int column = i;

            for (int k = 0; k < length; k++)
            {
                if (k > 1 && WorldGen.genRand.NextBool(4))
                    column += WorldGen.genRand.Next(-1, 2);

                int halfWidth = (int)MathF.Round(baseHalfWidth * (1f - k / (float)length));
                int rise = k == 0 ? SpikeBaseRise : 0;

                for (int ci = Math.Max(column - halfWidth, 0); ci <= Math.Min(column + halfWidth, width - 1); ci++)
                {
                    // The base row also fills upward to the dome where the ceiling beside the column is higher.
                    for (int cj = ceiling + k; cj >= ceiling + k - rise && cj >= 0 && cavity[ci, cj]; cj--)
                    {
                        cavity[ci, cj] = false;
                        spikes.Add((new Point(bounds.X + ci, bounds.Y + cj), type));
                    }
                }
            }

            i += baseHalfWidth * 2;
        }

        return spikes;
    }

    private static int FindCeiling(bool[,] cavity, int column)
    {
        for (int j = 0; j < cavity.GetLength(1); j++)
        {
            if (cavity[column, j])
                return j;
        }

        return -1;
    }

    private static ushort SpikeMaterial(ushort anchorType) => anchorType switch
    {
        TileID.Stone or TileID.Mud or TileID.IceBlock or TileID.Granite or TileID.Marble
            or TileID.Ebonstone or TileID.Crimstone or TileID.Pearlstone or TileID.Sandstone => anchorType,
        _ => TileID.Stone,
    };

    /// <summary> Chebyshev distance from each tile to the nearest cavity tile, capped at <paramref name="cap"/>. </summary>
    private static int[,] DistanceToCavity(bool[,] cavity, int cap)
    {
        int width = cavity.GetLength(0);
        int height = cavity.GetLength(1);
        var distance = new int[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
                distance[i, j] = cavity[i, j] ? 0 : cap;
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

    /// <summary> Removes whole multi-tile objects that overlap the cavity or shell, so none are left half-cut. </summary>
    private static void ClearObjects(Rectangle bounds, bool[,] cavity, int[,] distance, int shellThickness)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                if (!cavity[i, j] && distance[i, j] > shellThickness)
                    continue;

                int x = bounds.X + i;
                int y = bounds.Y + j;
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && Main.tileFrameImportant[tile.TileType])
                    ClearObject(x, y);
            }
        }
    }

    private static void ClearObject(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        ushort type = tile.TileType;
        TileObjectData data = TileObjectData.GetTileData(tile);

        if (data is null)
        {
            tile.ClearTile();
            return;
        }

        Point16 topLeft = TileObjectData.TopLeft(x, y);

        for (int i = 0; i < data.Width; i++)
        {
            for (int j = 0; j < data.Height; j++)
            {
                Tile part = Main.tile[topLeft.X + i, topLeft.Y + j];
                if (part.HasTile && part.TileType == type)
                    part.ClearTile();
            }
        }
    }

    private static void WriteTiles(Rectangle bounds, bool[,] cavity, int[,] distance, int shellThickness, int level, ushort material, bool desert)
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

                if (distance[i, j] > shellThickness)
                    continue;

                bool replace = tile.HasTile && (desert
                    ? TileID.Sets.CanBeClearedDuringOreRunner[tile.TileType]
                    : TileID.Sets.Falling[tile.TileType]);

                // Openings above the oil stay; everything beside or below it must hold liquid.
                if (replace || (y >= level - 1 && !BlocksLiquid(tile)))
                    SetSolid(tile, material);
            }
        }
    }

    /// <summary> Solid to the liquid simulation, including while settle passes make IgnoresWater tiles non-solid. </summary>
    private static bool BlocksLiquid(Tile tile)
    {
        if (!tile.HasTile || tile.IsActuated)
            return false;

        int type = tile.TileType;

        return Main.tileSolid[type] && !Main.tileSolidTop[type]
            && !LiquidID_TLmod.Sets.IgnoresWater.Contains(type)
            && !LiquidID_TLmod.Sets.IgnoresWaterDuringWorldgen.Contains(type);
    }

    private static void SetSolid(Tile tile, ushort type)
    {
        tile.HasTile = true;
        tile.TileType = type;
        tile.Slope = SlopeType.Solid;
        tile.IsHalfBlock = false;
        tile.IsActuated = false;
        tile.TileFrameX = 0;
        tile.TileFrameY = 0;
        tile.LiquidAmount = 0;
    }

    private static void SmoothDryEdges(Rectangle bounds, bool[,] cavity, int level)
    {
        for (int i = 1; i < bounds.Width - 1; i++)
        {
            for (int j = 1; j < bounds.Height - 1; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                if (cavity[i, j] || y >= level)
                    continue;

                if (!cavity[i - 1, j] && !cavity[i + 1, j] && !cavity[i, j - 1] && !cavity[i, j + 1])
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                    Tile.SmoothSlope(x, y, applyToNeighbors: false);
            }
        }
    }

    private static void Decorate(Rectangle bounds, bool[,] cavity, int level, bool desert)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                int x = bounds.X + i;
                int y = bounds.Y + j;

                if (!cavity[i, j] || Main.tile[x, y].HasTile)
                    continue;

                if (WorldGen.SolidTile(x, y + 1))
                {
                    if (WorldGen.genRand.NextBool(LargeRubbleChance))
                        PlaceLargeRubble(x, y, desert);
                    else if (WorldGen.genRand.NextBool(SmallRubbleChance))
                        PlaceSmallRubble(x, y);
                }
                else if (y + 1 < level && WorldGen.SolidTile(x, y - 1) && WorldGen.genRand.NextBool(HangingStalactiteChance))
                {
                    WorldGen.PlaceTight(x, y);
                }
            }
        }
    }

    /// <summary> Styles follow the vanilla Piles pass: LargePiles2 29-34 on desert floors, LargePiles 8-15 and LargePiles2 23-28 on stone. </summary>
    private static void PlaceLargeRubble(int x, int y, bool desert)
    {
        if (desert)
            WorldGen.PlaceTile(x, y, TileID.LargePiles2, mute: true, style: WorldGen.genRand.Next(29, 35));
        else if (WorldGen.genRand.NextBool(2))
            WorldGen.PlaceTile(x, y, TileID.LargePiles, mute: true, style: WorldGen.genRand.Next(8, 16));
        else
            WorldGen.PlaceTile(x, y, TileID.LargePiles2, mute: true, style: WorldGen.genRand.Next(23, 29));
    }

    /// <summary> Plain rock styles: 0-11 for 1x1 piles, 0-5 for 2x1 piles. </summary>
    private static void PlaceSmallRubble(int x, int y)
    {
        if (WorldGen.genRand.NextBool(3))
            WorldGen.PlaceSmallPile(x, y, WorldGen.genRand.Next(6), 1);
        else
            WorldGen.PlaceSmallPile(x, y, WorldGen.genRand.Next(12), 0);
    }

    private static void FillOil(Rectangle bounds, bool[,] cavity, int level, int oilType)
    {
        for (int i = 0; i < bounds.Width; i++)
        {
            for (int j = 0; j < bounds.Height; j++)
            {
                int y = bounds.Y + j;
                if (!cavity[i, j] || y < level)
                    continue;

                Tile tile = Main.tile[bounds.X + i, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    continue;

                tile.LiquidType = oilType;
                tile.LiquidAmount = 255;
            }
        }
    }
}
