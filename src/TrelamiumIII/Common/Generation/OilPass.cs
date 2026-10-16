using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Liquids;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Oil lakes and pockets in the underground desert, placed from a world scan: no other liquid within the clearance,
/// no protected objects or walls, and enough solid ground. Must run after Final Cleanup, which turns liquid behind
/// underground-desert walls into lava, and after Tile Cleanup, which adds water drips above liquids that aren't lava or honey.
/// </summary>
public class OilPass : GenPass
{
    private const int CandidateStride = 6;
    private const int PlacementAttempts = 200;
    private const int StructurePadding = 10;

    private const int LakeLiquidClearance = 30;
    private const int PocketLiquidClearance = 15;

    private const float MinSolid = 0.45f;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] LakeMin = [1, 1, 2];
    private static readonly int[] LakeMax = [1, 2, 3];
    private static readonly int[] PocketMin = [3, 5, 6];
    private static readonly int[] PocketMax = [6, 8, 10];

    public OilPass() : base("[T3] Oil Deposits", 150f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Seeping oil...";

        if (GenVars.UndergroundDesertLocation.IsEmpty)
            return;

        int oilType = LiquidLoader.LiquidType<DarkOilLiquids>();
        int size = WorldGen.GetWorldSize();
        int lakeCount = WorldGen.genRand.Next(LakeMin[size], LakeMax[size] + 1);
        int pocketCount = WorldGen.genRand.Next(PocketMin[size], PocketMax[size] + 1);

        var scan = new PlacementScan(progress);
        int total = lakeCount + pocketCount;

        for (int n = 0; n < total; n++)
        {
            bool lake = n < lakeCount;
            LiquidDepositShape shape = lake ? LiquidDepositShape.RollLake() : LiquidDepositShape.RollPocket();

            TryPlace(scan, shape, lake, DesertSearchArea(shape), oilType);
            progress.Set(0.5 + 0.5 * (n + 1) / total);
        }
    }

    private static bool TryPlace(PlacementScan scan, LiquidDepositShape shape, bool lake, Rectangle searchArea, int oilType)
    {
        int clearance = lake ? LakeLiquidClearance : PocketLiquidClearance;
        var candidates = new List<Point>();

        for (int y = searchArea.Top; y < searchArea.Bottom; y += CandidateStride)
        {
            for (int x = searchArea.Left; x < searchArea.Right; x += CandidateStride)
            {
                var center = new Point(x, y);
                Rectangle bounds = shape.Bounds(center);

                if (scan.Liquid.Count(Inflated(bounds, clearance)) == 0
                    && scan.Protected.Count(bounds) == 0
                    && scan.Solid.Density(bounds) >= MinSolid)
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

            LiquidDepositBuilder.Build(shape, center, DepositMaterial.Desert, oilType);
            GenVars.structures.AddProtectedStructure(bounds);
            return true;
        }

        return false;
    }

    private static Rectangle DesertSearchArea(LiquidDepositShape shape)
    {
        Rectangle desert = GenVars.UndergroundDesertLocation;
        int top = Math.Max(desert.Top, (int)Main.worldSurface + 40) + shape.DomeHeight;
        int bottom = desert.Bottom - shape.BasinDepth;

        return new Rectangle(desert.Left + shape.HalfWidth, top, desert.Width - shape.HalfWidth * 2, bottom - top);
    }

    private static Rectangle Inflated(Rectangle area, int amount)
    {
        area.Inflate(amount, amount);
        return area;
    }
}
