using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.NitreCaves;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Nitre cave micro biomes through the caverns: small cave systems with pools of aqua fortis. One is placed per
/// horizontal slice of the world so they spread out. Places acid, so like the other modded-liquid passes it runs
/// after Final Cleanup.
/// </summary>
public class NitreCavePass : GenPass
{
    private const int CandidateStride = 8;
    private const int PlacementAttempts = 120;
    private const int StructurePadding = 25;
    private const int EdgeMargin = 200;
    private const float MinSolid = 0.55f;

    /// <summary> Rows between the rock layer and the top of a cave. </summary>
    private const int MinRockDepth = 40;

    /// <summary> Rows kept between a cave and the underworld, leaving the band below to the obsidian forests. </summary>
    private const int MinUnderworldGap = 220;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] Count = [6, 9, 14];

    public NitreCavePass() : base("[T3] Nitre Caves", 100f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Seeping aqua fortis...";

        var scan = new PlacementScan(progress);
        int count = Count[WorldGen.GetWorldSize()];
        int sliceWidth = (Main.maxTilesX - EdgeMargin * 2) / count;

        for (int n = 0; n < count; n++)
        {
            NitreCaveShape shape = NitreCaveShape.Roll();
            int from = EdgeMargin + n * sliceWidth;

            // Fall back to anywhere in the world when the slice has no room.
            if (!TryPlace(scan, shape, from, from + sliceWidth))
                TryPlace(scan, shape, EdgeMargin, Main.maxTilesX - EdgeMargin);

            progress.Set(0.5 + 0.5 * (n + 1) / count);
        }
    }

    private static bool TryPlace(PlacementScan scan, NitreCaveShape shape, int left, int right)
    {
        int top = (int)Main.rockLayer + MinRockDepth + shape.HalfHeight + shape.Margin;
        int bottom = Main.UnderworldLayer - MinUnderworldGap - shape.HalfHeight - shape.Margin;
        var candidates = new List<Point>();

        for (int y = top; y < bottom; y += CandidateStride)
        {
            for (int x = left; x < right; x += CandidateStride)
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

            NitreCaveBuilder.Build(shape, center);
            GenVars.structures.AddProtectedStructure(bounds);
            NitreCaveSystem.Add(bounds);
            return true;
        }

        return false;
    }
}
