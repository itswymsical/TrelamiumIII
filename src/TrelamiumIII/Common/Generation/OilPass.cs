using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Liquids;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Oil lakes and pockets, placed from a world scan: no other liquid within the clearance, no protected objects or
/// walls, and mostly solid ground. Must run after Final Cleanup, which turns liquid behind underground-desert walls
/// into lava, and after Tile Cleanup, which adds water drips above liquids that aren't lava or honey.
/// </summary>
public class OilPass : GenPass
{
    private const int ScanCellSize = 8;
    private const int CandidateStride = 6;
    private const int PlacementAttempts = 200;
    private const int StructurePadding = 10;

    private const int LakeLiquidClearance = 30;
    private const int PocketLiquidClearance = 15;

    private const float LakeMinSolid = 0.6f;
    private const float PocketMinSolid = 0.7f;
    private const float DesertMinSolid = 0.45f;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] LakeMax = [2, 4, 6];
    private static readonly int[] PocketMin = [3, 5, 6];
    private static readonly int[] PocketMax = [6, 8, 10];

    public OilPass() : base("[T3] Oil Deposits", 150f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Seeping oil...";

        int oilType = LiquidLoader.LiquidType<CrudeOilLiquid>();
        int size = WorldGen.GetWorldSize();
        int lakeCount = WorldGen.genRand.Next(1, LakeMax[size] + 1);
        int pocketCount = WorldGen.genRand.Next(PocketMin[size], PocketMax[size] + 1);

        var scan = new PlacementScan(progress);
        int total = lakeCount + pocketCount;

        for (int n = 0; n < total; n++)
        {
            bool lake = n < lakeCount;
            OilDepositShape shape = lake ? OilDepositShape.RollLake() : OilDepositShape.RollPocket();

            // The first lake goes in the underground desert when one fits there.
            bool placedInDesert = n == 0 && !GenVars.UndergroundDesertLocation.IsEmpty
                && TryPlace(scan, shape, lake, DesertSearchArea(shape), oilType);

            if (!placedInDesert)
                TryPlace(scan, shape, lake, LayerSearchArea(shape, lake), oilType);

            progress.Set(0.5 + 0.5 * (n + 1) / total);
        }
    }

    private static bool TryPlace(PlacementScan scan, OilDepositShape shape, bool lake, Rectangle searchArea, int oilType)
    {
        int clearance = lake ? LakeLiquidClearance : PocketLiquidClearance;
        var candidates = new List<Point>();

        for (int y = searchArea.Top; y < searchArea.Bottom; y += CandidateStride)
        {
            for (int x = searchArea.Left; x < searchArea.Right; x += CandidateStride)
            {
                var center = new Point(x, y);
                Rectangle bounds = shape.Bounds(center);
                float minSolid = InDesert(center) ? DesertMinSolid : lake ? LakeMinSolid : PocketMinSolid;

                if (scan.Liquid.Count(Inflated(bounds, clearance)) == 0
                    && scan.Protected.Count(bounds) == 0
                    && scan.Solid.Density(bounds) >= minSolid)
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

            OilDepositBuilder.Build(shape, center, InDesert(center), oilType);
            GenVars.structures.AddProtectedStructure(bounds);
            return true;
        }

        return false;
    }

    private static Rectangle LayerSearchArea(OilDepositShape shape, bool lake)
    {
        int top = (int)Main.rockLayer + shape.DomeHeight + (lake ? 30 : 10);
        int bottom = Main.UnderworldLayer - shape.BasinDepth - (lake ? 60 : 30);
        int left = 150 + shape.HalfWidth;

        return new Rectangle(left, top, Main.maxTilesX - left * 2, bottom - top);
    }

    private static Rectangle DesertSearchArea(OilDepositShape shape)
    {
        Rectangle desert = GenVars.UndergroundDesertLocation;
        int top = Math.Max(desert.Top, (int)Main.worldSurface + 40) + shape.DomeHeight;
        int bottom = desert.Bottom - shape.BasinDepth;

        return new Rectangle(desert.Left + shape.HalfWidth, top, desert.Width - shape.HalfWidth * 2, bottom - top);
    }

    private static bool InDesert(Point tile) => GenVars.UndergroundDesertLocation.Contains(tile);

    private static Rectangle Inflated(Rectangle area, int amount)
    {
        area.Inflate(amount, amount);
        return area;
    }

    /// <summary> Per-cell counts of liquid, protected and solid tiles, from above the surface so desert clearance checks see oases. </summary>
    private sealed class PlacementScan
    {
        public readonly CellCountGrid Liquid = new(ScanCellSize);
        public readonly CellCountGrid Protected = new(ScanCellSize);
        public readonly CellCountGrid Solid = new(ScanCellSize);

        public PlacementScan(GenerationProgress progress)
        {
            int top = Math.Max((int)Main.worldSurface - 60, 0);

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                progress.Set(0.5 * x / Main.maxTilesX);

                for (int y = top; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.LiquidAmount > 0)
                        Liquid.Add(x, y);

                    if (IsProtected(tile))
                        Protected.Add(x, y);
                    else if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                        Solid.Add(x, y);
                }
            }

            Liquid.Build();
            Protected.Build();
            Solid.Build();
        }

        /// <summary> Multi-tile objects that aren't breakable decor, tree trunks, and dungeon, temple or hive walls. </summary>
        private static bool IsProtected(Tile tile)
        {
            if (Main.wallDungeon[tile.WallType] || tile.WallType == WallID.LihzahrdBrickUnsafe || tile.WallType == WallID.HiveUnsafe)
                return true;

            if (!tile.HasTile)
                return false;

            int type = tile.TileType;
            return TileID.Sets.IsATreeTrunk[type] || (Main.tileFrameImportant[type] && !IsDecor(type));
        }

        private static bool IsDecor(int type) => type switch
        {
            TileID.Larva or TileID.LifeFruit or TileID.BeeHive => false,
            TileID.ExposedGems or TileID.WaterDrip or TileID.LavaDrip or TileID.HoneyDrip or TileID.SandDrip => true,
            _ => Main.tileCut[type] || TileID.Sets.BreakableWhenPlacing[type],
        };
    }
}
