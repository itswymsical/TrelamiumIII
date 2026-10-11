using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Turns one ocean, picked at random, to brine and sinks salt-crusted brine pools into the ground beside both oceans.
/// Brine is a <see cref="WaterConditions"/> condition, so the water stays water to vanilla and other mods.
/// </summary>
public class BrinePass : GenPass
{
    /// <summary> Columns past the beach that water connected to the ocean is still salted. </summary>
    private const int InlandReach = 40;

    /// <summary> Pools are placed between this many columns seaward and inland of the beach's end. </summary>
    private const int PoolSeaward = 60;

    private const int PoolInland = 140;

    /// <summary> Rows of ground kept between a pool's dome and the surface above it. </summary>
    private const int PoolMinCover = 8;

    private const int PoolMaxDepth = 60;
    private const int PoolLiquidClearance = 8;
    private const float PoolMinSolid = 0.6f;
    private const int CandidateStride = 4;
    private const int PlacementAttempts = 60;
    private const int StructurePadding = 6;

    /// <summary> 1 in this many shell tiles touching brine below its surface row turn to salt; the surface row always does. </summary>
    private const int SaltCrustChance = 3;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] PoolsPerCoast = [2, 3, 4];

    public BrinePass() : base("[T3] Brine", 60f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Salting the sea...";

        var scan = new PlacementScan(progress);
        byte brine = WaterConditions.Instance.Brine.Index;

        SaltOcean(WorldGen.genRand.NextBool(), brine);
        progress.Set(0.6);

        int pools = PoolsPerCoast[WorldGen.GetWorldSize()];
        foreach (bool left in (ReadOnlySpan<bool>)[true, false])
        {
            for (int n = 0; n < pools; n++)
                TryPlacePool(scan, left, brine);

            progress.Set(left ? 0.8 : 1.0);
        }
    }

    private static int BeachEnd(bool left)
    {
        int fallback = (int)(Main.maxTilesX * 0.08f);
        return left
            ? GenVars.leftBeachEnd > 0 ? GenVars.leftBeachEnd : fallback
            : GenVars.rightBeachStart > 0 ? GenVars.rightBeachStart : Main.maxTilesX - fallback;
    }

    /// <summary> Salts the water above the rock layer on one side of the beach, and everything it connects to within reach. </summary>
    private static void SaltOcean(bool left, byte brine)
    {
        int beach = BeachEnd(left);
        int minX = left ? 1 : beach - InlandReach;
        int maxX = left ? beach + InlandReach : Main.maxTilesX - 2;
        int maxY = (int)Main.rockLayer;

        var pending = new Stack<Point>();
        var visited = new HashSet<Point>();

        for (int x = left ? minX : beach; x <= (left ? beach : maxX); x++)
        {
            for (int y = 1; y < (int)Main.worldSurface; y++)
            {
                if (IsWater(x, y) && visited.Add(new Point(x, y)))
                    pending.Push(new Point(x, y));
            }
        }

        while (pending.TryPop(out Point point))
        {
            Main.tile[point.X, point.Y].LiquidCondition() = brine;

            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y - 1), new(point.X, point.Y + 1)])
            {
                if (next.X >= minX && next.X <= maxX && next.Y > 0 && next.Y < maxY && IsWater(next.X, next.Y) && visited.Add(next))
                    pending.Push(next);
            }
        }
    }

    private static bool IsWater(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
    }

    private static bool TryPlacePool(PlacementScan scan, bool left, byte brine)
    {
        LiquidDepositShape shape = LiquidDepositShape.RollPocket();
        int beach = BeachEnd(left);
        int from = left ? beach - PoolSeaward : beach - PoolInland;
        int to = left ? beach + PoolInland : beach + PoolSeaward;
        var candidates = new List<Point>();

        for (int x = Math.Max(from, 50); x <= Math.Min(to, Main.maxTilesX - 50); x += CandidateStride)
        {
            int surface = SurfaceRow(x);
            if (surface < 0)
                continue;

            for (int y = surface + PoolMinCover + shape.DomeHeight + shape.Margin; y <= surface + PoolMaxDepth; y += CandidateStride)
            {
                var center = new Point(x, y);
                Rectangle bounds = shape.Bounds(center);
                Rectangle clearance = bounds;
                clearance.Inflate(PoolLiquidClearance, PoolLiquidClearance);

                if (scan.Liquid.Count(clearance) == 0 && scan.Protected.Count(bounds) == 0 && scan.Solid.Density(bounds) >= PoolMinSolid)
                    candidates.Add(center);
            }
        }

        for (int attempt = 0; attempt < PlacementAttempts && candidates.Count > 0; attempt++)
        {
            int index = WorldGen.genRand.Next(candidates.Count);
            Point center = candidates[index];
            candidates[index] = candidates[^1];
            candidates.RemoveAt(candidates.Count - 1);

            Rectangle bounds = shape.Bounds(center);
            if (!GenVars.structures.CanPlace(bounds, StructurePadding))
                continue;

            int level = LiquidDepositBuilder.Build(shape, center, DepositMaterial.Coast, LiquidID.Water, brine);
            CrustWithSalt(bounds, level, brine);
            GenVars.structures.AddProtectedStructure(bounds);
            return true;
        }

        return false;
    }

    /// <summary> First solid row from the top of the world in column <paramref name="x"/>, or -1. </summary>
    private static int SurfaceRow(int x)
    {
        for (int y = 50; y < (int)Main.rockLayer; y++)
        {
            if (WorldGen.SolidTile(x, y))
                return y;
        }

        return -1;
    }

    /// <summary> Salt where the pool meets its shell: the rim at the surface row, and patches below it. </summary>
    private static void CrustWithSalt(Rectangle bounds, int level, byte brine)
    {
        ushort salt = (ushort)ModContent.TileType<PureSaltTile>();

        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            for (int y = Math.Max(bounds.Top, level - 1); y < bounds.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                if (!LiquidDepositBuilder.BlocksLiquid(tile) || !TouchesBrine(x, y, brine))
                    continue;

                if (y <= level || WorldGen.genRand.NextBool(SaltCrustChance))
                    tile.TileType = salt;
            }
        }
    }

    private static bool TouchesBrine(int x, int y, byte brine)
    {
        foreach (Point next in (ReadOnlySpan<Point>)[new(x - 1, y), new(x + 1, y), new(x, y - 1), new(x, y + 1)])
        {
            Tile tile = Main.tile[next.X, next.Y];
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water && tile.LiquidCondition() == brine)
                return true;
        }

        return false;
    }
}
