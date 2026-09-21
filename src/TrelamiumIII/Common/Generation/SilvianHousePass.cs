using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.NPCs.Silvian;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Places Silvian's house on the surface and reserves it as his home, so the housing system spawns him inside it
/// once worldgen finishes.
/// </summary>
/// <remarks>
///     <para>
///         The site is searched in widening tiers, each taking the flattest ground it accepts: a mild biome in the
///         outer thirds, then a mild biome anywhere clear of spawn, then any surface clear of spawn that is not
///         corrupt or crimson. Mild means not jungle, snow or desert.
///     </para>
///     <para>
///         Runs after Final Cleanup, so no vanilla pass plants trees in the house or grows anything into it.
///     </para>
/// </remarks>
public class SilvianHousePass : GenPass
{
    private const string HousePath = "Assets/Structures/SilvianHouse";

    /// <summary>Open floor between the bookcase and the table, one row above the floorboards.</summary>
    private static readonly StructureAnchor StandTile = StructureAnchor.Tile(26, 16);

    /// <summary>Fraction of the world's width kept clear of the house on either side of spawn.</summary>
    private const float SpawnClearance = 0.1f;

    private const int BeachClearance = 50;

    /// <summary>Columns beyond each side of the house that count toward its biome.</summary>
    private const int BiomeMargin = 20;

    /// <summary>Share of the biome window that may be jungle, snow or sand, for the patches a forest carries.</summary>
    private const float HarshTolerance = 0.1f;

    /// <summary>Columns beyond each side that must be free of corruption and crimson, which spread.</summary>
    private const int EvilMargin = 50;

    /// <summary>Columns beyond each side held to the footprint's unevenness, so the house isn't cut into a cliff.</summary>
    private const int EdgeMargin = 3;

    private const int StructurePadding = 6;

    /// <summary>
    /// How far above the highest terrain the surface scan starts. Floating islands are anchored at least 50 tiles
    /// above it and hang about 20 below their anchor, so the scan starts under them and above all natural terrain.
    /// </summary>
    private const int SkyClearance = 20;

    /// <summary>Tiles under the surface sampled for corruption, which can sit beneath a skin of plain dirt.</summary>
    private const int EvilSampleDepth = 8;

    /// <summary>Rows above the house swept for tree tops that would be left hanging over the roof.</summary>
    private const int TreeHeadroom = 40;

    private readonly record struct Tier(string Name, bool OuterThirdsOnly, bool MildBiomeOnly, int MaxUnevenness);

    private static readonly Tier[] Tiers =
    [
        new("mild biome, outer thirds", OuterThirdsOnly: true, MildBiomeOnly: true, MaxUnevenness: 6),
        new("mild biome, clear of spawn", OuterThirdsOnly: false, MildBiomeOnly: true, MaxUnevenness: 6),
        new("uncorrupted surface, clear of spawn", OuterThirdsOnly: false, MildBiomeOnly: false, MaxUnevenness: 10)
    ];

    /// <param name="FloorY">Row the house's bottom row lands on: the median surface height under it.</param>
    /// <param name="Cost">Total tiles of height difference between the ground and that row.</param>
    private readonly record struct Site(int Left, int FloorY, int Cost, int Tiebreak);

    public SilvianHousePass() : base("[T3] Silvian's House", 5f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Raising a spruce cabin...";

        Point16 size = Structures.Dimensions(HousePath);
        var scan = new SurfaceScan();

        foreach (Tier tier in Tiers)
        {
            foreach (Site site in RankSites(scan, tier, size.X))
            {
                var area = new Rectangle(site.Left, site.FloorY - size.Y + 1, size.X, size.Y);

                // Checked down through the backfill as well, so filling a dip can never bury or break a chest.
                if (!GenVars.structures.CanPlace(area with { Height = size.Y + tier.MaxUnevenness }, StructurePadding))
                    continue;

                Build(area, tier);
                progress.Set(1.0);
                return;
            }
        }

        TrelamiumIII.Instance.Logger.Warn("Found no site for Silvian's house; he will only spawn naturally in this world.");
    }

    private static void Build(Rectangle area, Tier tier)
    {
        FellTrees(area, tier.MaxUnevenness);

        // The house is saved standing on a slab of its own grass, so its bottom row replaces the surface row
        // instead of resting on top of it.
        PlacedStructure placed = Structures.Place(HousePath, new Point(area.Left, area.Bottom - 1), StructureAnchor.BottomLeft);

        Backfill(placed.Area, tier.MaxUnevenness);
        FrameBorder(placed.Area, tier.MaxUnevenness);

        GenVars.structures.AddProtectedStructure(placed.Area, StructurePadding);
        placed.ReserveFor<Silvian>(StandTile);

        TrelamiumIII.Instance.Logger.Info($"Placed Silvian's house at {placed.Origin} ({tier.Name}).");
    }

    #region Site search
    /// <summary>Every footprint the tier accepts, flattest first. Ties are shuffled so a flat world doesn't always pick the west end.</summary>
    private static List<Site> RankSites(SurfaceScan scan, Tier tier, int width)
    {
        var sites = new List<Site>();
        var heights = new int[width];

        (int landLeft, int landRight) = LandBounds();
        int spawnGap = (int)(Main.maxTilesX * SpawnClearance);
        int third = Main.maxTilesX / 3;

        for (int left = landLeft; left + width - 1 <= landRight; left++)
        {
            int right = left + width - 1;

            if (DistanceToSpan(Main.spawnTileX, left, right) < spawnGap)
                continue;

            if (tier.OuterThirdsOnly && right >= third && left < Main.maxTilesX - third)
                continue;

            if (TryScore(scan, tier, left, heights, out int floorY, out int cost))
                sites.Add(new Site(left, floorY, cost, WorldGen.genRand.Next()));
        }

        sites.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.Tiebreak.CompareTo(b.Tiebreak));
        return sites;
    }

    /// <param name="heights">Scratch buffer the width of the house, reused across candidates.</param>
    private static bool TryScore(SurfaceScan scan, Tier tier, int left, int[] heights, out int floorY, out int cost)
    {
        floorY = cost = 0;

        int width = heights.Length;
        int right = left + width - 1;

        if (scan.CountBlocked(left - EdgeMargin, right + EdgeMargin) > 0)
            return false;

        if (scan.CountEvil(left - EvilMargin, right + EvilMargin) > 0)
            return false;

        if (tier.MildBiomeOnly
            && scan.CountHarsh(left - BiomeMargin, right + BiomeMargin) > (width + BiomeMargin * 2) * HarshTolerance)
            return false;

        Array.Copy(scan.Height, left, heights, 0, width);
        Array.Sort(heights);
        floorY = heights[width / 2];

        for (int x = left - EdgeMargin; x <= right + EdgeMargin; x++)
        {
            int offset = Math.Abs(scan.Height[x] - floorY);

            if (offset > tier.MaxUnevenness)
                return false;

            cost += offset;
        }

        return true;
    }

    /// <summary>The columns between the two beaches, pulled in by <see cref="BeachClearance"/>.</summary>
    private static (int Left, int Right) LandBounds()
    {
        // Only a replaced terrain pass leaves these unset.
        int fallback = (int)(Main.maxTilesX * 0.08f);
        int left = GenVars.leftBeachEnd > 0 ? GenVars.leftBeachEnd : fallback;
        int right = GenVars.rightBeachStart > 0 ? GenVars.rightBeachStart : Main.maxTilesX - fallback;

        return (left + BeachClearance, right - BeachClearance);
    }

    private static int DistanceToSpan(int x, int left, int right) => x < left ? left - x : x > right ? x - right : 0;
    #endregion

    #region Groundwork
    /// <summary>
    /// Removes trees and cacti in the way. The house writes its air over the whole footprint, which would cut their
    /// trunks and leave the tops hanging above the roof.
    /// </summary>
    private static void FellTrees(Rectangle area, int unevenness)
    {
        int top = Math.Max(10, area.Top - TreeHeadroom);
        int bottom = area.Bottom - 1 + unevenness;

        for (int x = area.Left - 1; x <= area.Right; x++)
        {
            for (int y = top; y <= bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && IsTreeLike(tile.TileType))
                    WorldGen.KillTile(x, y, noItem: true);
            }
        }

        // The tree check runs on framing: branches and roots left beside a felled trunk drop here.
        WorldGen.RangeFrame(area.Left - 3, top, area.Right + 2, bottom);
    }

    private static bool IsTreeLike(int type) => TileID.Sets.IsATreeTrunk[type] || type is TileID.PalmTree or TileID.Cactus;

    /// <summary>Fills any dip under the house down to the ground, so its foundation never overhangs air.</summary>
    private static void Backfill(Rectangle area, int maxDepth)
    {
        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Bottom; y < area.Bottom + maxDepth && !IsGround(Main.tile[x, y]); y++)
            {
                if (Main.tile[x, y].HasTile)
                    WorldGen.KillTile(x, y, noItem: true);

                WorldGen.PlaceTile(x, y, TileID.Dirt, mute: true, forced: true);
            }
        }
    }

    /// <summary>
    /// Re-frames the ring around the house, so anything its edge cut through breaks cleanly and the backfill joins
    /// the ground beside it.
    /// </summary>
    private static void FrameBorder(Rectangle area, int fillDepth)
    {
        // RangeFrame pads a tile on every side, which reaches only the house's outermost air and ground row.
        WorldGen.RangeFrame(area.Left - 2, area.Top - 2, area.Left - 1, area.Bottom + fillDepth);
        WorldGen.RangeFrame(area.Right, area.Top - 2, area.Right + 1, area.Bottom + fillDepth);
        WorldGen.RangeFrame(area.Left, area.Top - 2, area.Right - 1, area.Top - 1);
        WorldGen.RangeFrame(area.Left, area.Bottom, area.Right - 1, area.Bottom + fillDepth);
    }

    /// <summary>A block that can be stood on, slopes and half blocks included, which <see cref="WorldGen.SolidTile(int, int, bool)"/> turns away.</summary>
    private static bool IsGround(Tile tile) => tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
    #endregion

    #region Surface scan
    [Flags]
    private enum Ground : byte
    {
        Mild = 0,

        /// <summary>Jungle, snow or desert.</summary>
        Harsh = 1,

        Evil = 2,

        /// <summary>Liquid standing on the ground: a lake or a pond.</summary>
        Wet = 4,

        /// <summary>No natural ground in the surface layer: a chasm, a living tree, a cloud, a structure.</summary>
        Unusable = 8
    }

    /// <summary>
    /// The surface of every column in the world, read once: its height, what it is made of, and running counts so
    /// any range of columns can be asked about in constant time.
    /// </summary>
    private sealed class SurfaceScan
    {
        public readonly int[] Height = new int[Main.maxTilesX];

        private readonly int[] blocked, evil, harsh;

        public SurfaceScan()
        {
            var kinds = new Ground[Main.maxTilesX];

            int top = Math.Max(10, (int)GenVars.worldSurfaceLow - SkyClearance);
            int bottom = (int)Main.worldSurface;

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                int y = top;

                while (y < bottom && !IsGround(Main.tile[x, y]))
                    y++;

                // Ground on the scan's first row means terrain rising past it, a dune or a canopy; no ground at all
                // means a chasm or open water.
                bool found = y > top && y < bottom;

                Height[x] = y;
                kinds[x] = found ? Classify(x, y) : Ground.Unusable;
            }

            blocked = RunningCount(kinds, Ground.Unusable | Ground.Wet);
            evil = RunningCount(kinds, Ground.Evil);
            harsh = RunningCount(kinds, Ground.Harsh);
        }

        public int CountBlocked(int from, int to) => Count(blocked, from, to);

        public int CountEvil(int from, int to) => Count(evil, from, to);

        public int CountHarsh(int from, int to) => Count(harsh, from, to);

        private static Ground Classify(int x, int y)
        {
            var kind = Ground.Mild;

            for (int depth = 0; depth <= EvilSampleDepth; depth++)
            {
                Tile tile = Main.tile[x, y + depth];

                if (tile.HasTile && (TileID.Sets.Corrupt[tile.TileType] || TileID.Sets.Crimson[tile.TileType]))
                {
                    kind |= Ground.Evil;
                    break;
                }
            }

            int type = Main.tile[x, y].TileType;

            if (type is TileID.JungleGrass or TileID.Mud or TileID.SnowBlock or TileID.IceBlock || TileID.Sets.isDesertBiomeSand[type])
                kind |= Ground.Harsh;
            else if (!TileID.Sets.GeneralPlacementTiles[type] || IsLivingTreeOrCloud(type))
                kind |= Ground.Unusable;

            if (Main.tile[x, y - 1].LiquidAmount > 0)
                kind |= Ground.Wet;

            return kind;
        }

        private static bool IsLivingTreeOrCloud(int type) => type is TileID.LivingWood or TileID.LeafBlock
            or TileID.LivingMahogany or TileID.LivingMahoganyLeaves or TileID.Cloud or TileID.RainCloud or TileID.SnowCloud;

        /// <summary><c>sums[i]</c> is how many of the first <c>i</c> columns match <paramref name="mask"/>.</summary>
        private static int[] RunningCount(Ground[] kinds, Ground mask)
        {
            var sums = new int[kinds.Length + 1];

            for (int x = 0; x < kinds.Length; x++)
                sums[x + 1] = sums[x] + ((kinds[x] & mask) != 0 ? 1 : 0);

            return sums;
        }

        /// <summary>Matching columns in <c>[from, to]</c>, clamped to the world.</summary>
        private static int Count(int[] sums, int from, int to)
        {
            from = Math.Max(from, 0);
            to = Math.Min(to, sums.Length - 2);

            return to < from ? 0 : sums[to + 1] - sums[from];
        }
    }
    #endregion
}
