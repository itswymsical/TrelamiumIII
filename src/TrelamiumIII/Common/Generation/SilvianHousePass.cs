using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.Tiles.Seals;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Places Silvian's house on the surface near spawn, protects it, and reserves it as his home: the housing system
/// spawns him inside once worldgen finishes, and no other NPC can move in.
/// </summary>
/// <remarks>
///     <para>
///         The site is searched in widening tiers, each taking the flattest ground it accepts: a mild biome within
///         250 tiles of spawn, then within 600, then anywhere, then any surface that is not corrupt or crimson. Mild
///         means not jungle, snow or desert. Every tier keeps <see cref="MinSpawnDistance"/> clear around spawn.
///     </para>
///     <para>
///         Runs after Final Cleanup, so no vanilla pass plants trees in the house or grows anything into it.
///     </para>
/// </remarks>
public class SilvianHousePass : GenPass
{
    private const string HousePath = "Assets/Structures/SilviansHouseV2";

    /// <summary>Structure row that replaces the surface row: the floor, with three rows of foundation below.</summary>
    private const int GroundRow = 18;

    /// <summary>Open floor between the potted plants and the garden door, one row above the floor.</summary>
    private static readonly StructureAnchor StandTile = StructureAnchor.Tile(35, 17);

    /// <summary>Columns between the spawn point and the nearest edge of the house.</summary>
    private const int MinSpawnDistance = 75;

    /// <summary>Columns beyond each side of the house that count toward its biome.</summary>
    private const int BiomeMargin = 20;

    /// <summary>Share of the biome window that may be jungle, snow or sand, for the patches a forest carries.</summary>
    private const float HarshTolerance = 0.1f;

    /// <summary>Columns beyond each side that must be free of corruption and crimson, which spread.</summary>
    private const int EvilMargin = 50;

    private const int StructurePadding = 6;

    private readonly record struct Tier(string Name, int MaxSpawnDistance, bool MildBiomeOnly, int MaxUnevenness);

    private static readonly Tier[] Tiers =
    [
        new("mild biome near spawn", MaxSpawnDistance: 250, MildBiomeOnly: true, MaxUnevenness: 6),
        new("mild biome", MaxSpawnDistance: 600, MildBiomeOnly: true, MaxUnevenness: 6),
        new("mild biome, any distance", MaxSpawnDistance: int.MaxValue, MildBiomeOnly: true, MaxUnevenness: 6),
        new("uncorrupted surface", MaxSpawnDistance: int.MaxValue, MildBiomeOnly: false, MaxUnevenness: 10)
    ];

    public SilvianHousePass() : base("[T3] Silvian's House", 5f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Raising a spruce cabin...";

        int width = Structures.Dimensions(HousePath).X;
        var scan = new SurfaceScan();

        foreach (Tier tier in Tiers)
        {
            var sites = SurfaceSites.Rank(scan, width, tier.MaxUnevenness, (left, right) => Accepts(scan, tier, left, right));

            if (!SurfaceSites.TryBuild(HousePath, GroundRow, sites, tier.MaxUnevenness, StructurePadding, TileID.Dirt, out PlacedStructure placed))
                continue;

            SealSystem.Seal(placed.Area, ModContent.GetInstance<WardSeal>());
            placed.ReserveFor<Silvian>(StandTile);

            int distance = SurfaceSites.DistanceToSpan(Main.spawnTileX, placed.Area.Left, placed.Area.Right - 1);
            TrelamiumIII.Instance.Logger.Info($"Placed Silvian's house at {placed.Origin}, {distance} tiles from spawn ({tier.Name}).");

            progress.Set(1.0);
            return;
        }

        TrelamiumIII.Instance.Logger.Warn("Found no site for Silvian's house; he will only spawn naturally in this world.");
    }

    private static bool Accepts(SurfaceScan scan, Tier tier, int left, int right)
    {
        int distance = SurfaceSites.DistanceToSpan(Main.spawnTileX, left, right);

        if (distance < MinSpawnDistance || distance > tier.MaxSpawnDistance)
            return false;

        if (scan.Count(SurfaceGround.Evil, left - EvilMargin, right + EvilMargin) > 0)
            return false;

        return !tier.MildBiomeOnly
            || scan.Count(SurfaceGround.Harsh, left - BiomeMargin, right + BiomeMargin) <= (right - left + 1 + BiomeMargin * 2) * HarshTolerance;
    }
}
