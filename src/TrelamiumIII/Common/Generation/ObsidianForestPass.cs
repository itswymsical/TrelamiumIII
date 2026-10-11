using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Obsidian Forest micro biomes low in the caverns, above the underworld. Places acid, so like the other modded-liquid
/// passes it runs after Final Cleanup.
/// </summary>
public class ObsidianForestPass : GenPass
{
    private const int CandidateStride = 8;
    private const int PlacementAttempts = 100;
    private const int StructurePadding = 20;
    private const int EdgeMargin = 250;
    private const float MinSolid = 0.5f;

    /// <summary> Rows between the forest's floor and the underworld layer. </summary>
    private const int MinUnderworldGap = 40;

    private const int MaxUnderworldGap = 200;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] Count = [2, 3, 4];

    public ObsidianForestPass() : base("[T3] Obsidian Forest", 100f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Growing obsidian forests...";

        var scan = new PlacementScan(progress);
        int count = Count[WorldGen.GetWorldSize()];

        for (int n = 0; n < count; n++)
        {
            TryPlace(scan, ObsidianForestShape.Roll());
            progress.Set(0.5 + 0.5 * (n + 1) / count);
        }
    }

    private static bool TryPlace(PlacementScan scan, ObsidianForestShape shape)
    {
        int top = Main.UnderworldLayer - MaxUnderworldGap;
        int bottom = Main.UnderworldLayer - MinUnderworldGap - shape.FloorDepth;
        var candidates = new List<Point>();

        for (int y = top; y < bottom; y += CandidateStride)
        {
            for (int x = EdgeMargin; x < Main.maxTilesX - EdgeMargin; x += CandidateStride)
            {
                var center = new Point(x, y);
                Rectangle bounds = shape.Bounds(center);

                if (!bounds.Intersects(GenVars.UndergroundDesertLocation)
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

            ObsidianForestBuilder.Build(shape, center);
            GenVars.structures.AddProtectedStructure(bounds);
            return true;
        }

        return false;
    }
}
