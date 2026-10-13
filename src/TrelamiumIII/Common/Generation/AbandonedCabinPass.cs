using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Places the abandoned cabin on the snow biome's surface, its floor level with the ground. Tiers relax only the
/// allowed unevenness. Runs after Final Cleanup, so no vanilla pass grows trees into it.
/// </summary>
public class AbandonedCabinPass : GenPass
{
    private const string CabinPath = "Assets/Structures/AbandonedCabin";

    /// <summary>Structure row that replaces the surface row: the floor.</summary>
    private const int GroundRow = 12;

    /// <summary>Columns beyond each side of the cabin that count toward its biome.</summary>
    private const int BiomeMargin = 10;

    /// <summary>Share of the biome window that must be snow or ice.</summary>
    private const float MinSnowShare = 0.9f;

    private const int EvilMargin = 20;

    /// <summary>Columns kept between the cabin and the dungeon entrance.</summary>
    private const int DungeonClearance = 60;

    private const int StructurePadding = 4;

    private static readonly int[] MaxUnevenness = [4, 8, 12];

    public AbandonedCabinPass() : base("[T3] Abandoned Cabin", 1f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Snowing in an old cabin...";

        int width = Structures.Dimensions(CabinPath).X;
        var scan = new SurfaceScan();

        foreach (int unevenness in MaxUnevenness)
        {
            var sites = SurfaceSites.Rank(scan, width, unevenness, (left, right) => Accepts(scan, left, right));

            if (!SurfaceSites.TryBuild(CabinPath, GroundRow, sites, unevenness, StructurePadding, TileID.SnowBlock, out PlacedStructure placed))
                continue;

            TrelamiumIII.Instance.Logger.Info($"Placed the abandoned cabin at {placed.Origin} (unevenness {unevenness}).");
            progress.Set(1.0);
            return;
        }

        TrelamiumIII.Instance.Logger.Warn("Found no snow surface for the abandoned cabin.");
    }

    private static bool Accepts(SurfaceScan scan, int left, int right)
    {
        if (SurfaceSites.DistanceToSpan(Main.dungeonX, left, right) < DungeonClearance)
            return false;

        if (scan.Count(SurfaceGround.Evil, left - EvilMargin, right + EvilMargin) > 0)
            return false;

        int windowLeft = left - BiomeMargin;
        int windowRight = right + BiomeMargin;

        return scan.Count(SurfaceGround.Snow, windowLeft, windowRight) >= (windowRight - windowLeft + 1) * MinSnowShare;
    }
}
