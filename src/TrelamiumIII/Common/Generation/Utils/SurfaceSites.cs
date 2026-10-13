using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.WorldBuilding;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary>What a surface column is made of, as read by <see cref="SurfaceScan"/>.</summary>
[Flags]
public enum SurfaceGround : byte
{
    Mild = 0,
    Jungle = 1,
    Snow = 2,
    Desert = 4,

    /// <summary>Corruption or crimson anywhere within <see cref="SurfaceScan.EvilSampleDepth"/> of the surface.</summary>
    Evil = 8,

    /// <summary>Liquid standing on the ground: a lake or a pond.</summary>
    Wet = 16,

    /// <summary>No natural ground in the surface layer: a chasm, a living tree, a cloud, a structure.</summary>
    Unusable = 32,

    Harsh = Jungle | Snow | Desert,
    Blocked = Wet | Unusable
}

/// <summary>Surface height and ground of every column, with constant-time counts over any column range.</summary>
public sealed class SurfaceScan
{
    /// <summary>
    /// How far above the highest terrain the scan starts. Floating islands are anchored at least 50 tiles above it and
    /// hang about 20 below their anchor, so the scan starts under them and above all natural terrain.
    /// </summary>
    private const int SkyClearance = 20;

    /// <summary>Tiles under the surface sampled for corruption, which can sit beneath a skin of plain dirt.</summary>
    public const int EvilSampleDepth = 8;

    /// <summary>Row of the topmost ground tile in each column.</summary>
    public readonly int[] Height = new int[Main.maxTilesX];

    private readonly SurfaceGround[] kinds = new SurfaceGround[Main.maxTilesX];

    /// <summary><c>sums[i]</c> is how many of the first <c>i</c> columns match the key.</summary>
    private readonly Dictionary<SurfaceGround, int[]> sums = [];

    public SurfaceScan()
    {
        int top = Math.Max(10, (int)GenVars.worldSurfaceLow - SkyClearance);
        int bottom = (int)Main.worldSurface;

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            int y = top;

            while (y < bottom && !SurfaceSites.IsGround(Main.tile[x, y]))
                y++;

            // Ground on the scan's first row means terrain rising past it, a dune or a canopy; no ground at all means a
            // chasm or open water.
            bool found = y > top && y < bottom;

            Height[x] = y;
            kinds[x] = found ? Classify(x, y) : SurfaceGround.Unusable;
        }
    }

    /// <summary>Columns in <c>[from, to]</c> with any of <paramref name="ground"/>, clamped to the world.</summary>
    public int Count(SurfaceGround ground, int from, int to)
    {
        if (!sums.TryGetValue(ground, out int[]? running))
            sums[ground] = running = RunningCount(ground);

        from = Math.Max(from, 0);
        to = Math.Min(to, kinds.Length - 1);

        return to < from ? 0 : running[to + 1] - running[from];
    }

    private static SurfaceGround Classify(int x, int y)
    {
        var kind = SurfaceGround.Mild;

        for (int depth = 0; depth <= EvilSampleDepth; depth++)
        {
            Tile tile = Main.tile[x, y + depth];

            if (tile.HasTile && (TileID.Sets.Corrupt[tile.TileType] || TileID.Sets.Crimson[tile.TileType]))
            {
                kind |= SurfaceGround.Evil;
                break;
            }
        }

        int type = Main.tile[x, y].TileType;

        if (type is TileID.JungleGrass or TileID.Mud)
            kind |= SurfaceGround.Jungle;
        else if (type is TileID.SnowBlock or TileID.IceBlock)
            kind |= SurfaceGround.Snow;
        else if (TileID.Sets.isDesertBiomeSand[type])
            kind |= SurfaceGround.Desert;
        else if (!TileID.Sets.GeneralPlacementTiles[type] || IsLivingTreeOrCloud(type))
            kind |= SurfaceGround.Unusable;

        if (Main.tile[x, y - 1].LiquidAmount > 0)
            kind |= SurfaceGround.Wet;

        return kind;
    }

    private static bool IsLivingTreeOrCloud(int type) => type is TileID.LivingWood or TileID.LeafBlock
        or TileID.LivingMahogany or TileID.LivingMahoganyLeaves or TileID.Cloud or TileID.RainCloud or TileID.SnowCloud;

    private int[] RunningCount(SurfaceGround ground)
    {
        var running = new int[kinds.Length + 1];

        for (int x = 0; x < kinds.Length; x++)
            running[x + 1] = running[x] + ((kinds[x] & ground) != 0 ? 1 : 0);

        return running;
    }
}

/// <param name="FloorY">Row the structure's ground row lands on: the median surface height under the footprint.</param>
/// <param name="Cost">Total tiles of height difference between the ground and that row, edge margin included.</param>
public readonly record struct SurfaceSite(int Left, int FloorY, int Cost, int Tiebreak);

/// <summary>Finds level surface footprints for structures and places them with groundwork.</summary>
public static class SurfaceSites
{
    private const int BeachClearance = 50;

    /// <summary>Columns beyond each side held to the footprint's unevenness, so the structure isn't cut into a cliff.</summary>
    private const int EdgeMargin = 3;

    /// <summary>Rows above the structure swept for tree tops that would be left hanging over it.</summary>
    private const int TreeHeadroom = 40;

    /// <summary>Columns beyond each side cleared of trees, whose crowns and branches would overlap the structure.</summary>
    private const int TreeMargin = 3;

    /// <summary>
    /// Every footprint between the beaches that <paramref name="accept"/> takes (left, right), clear of blocked columns and
    /// within <paramref name="maxUnevenness"/> of its median height; flattest first, ties shuffled.
    /// </summary>
    public static List<SurfaceSite> Rank(SurfaceScan scan, int width, int maxUnevenness, Func<int, int, bool> accept)
    {
        var sites = new List<SurfaceSite>();
        var heights = new int[width];

        (int landLeft, int landRight) = LandBounds();

        for (int left = landLeft; left + width - 1 <= landRight; left++)
        {
            if (accept(left, left + width - 1) && TryScore(scan, left, heights, maxUnevenness, out int floorY, out int cost))
                sites.Add(new SurfaceSite(left, floorY, cost, WorldGen.genRand.Next()));
        }

        sites.Sort((a, b) => a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.Tiebreak.CompareTo(b.Tiebreak));
        return sites;
    }

    /// <summary>
    /// Places the structure at the first site <see cref="GenVars.structures"/> accepts, its <paramref name="groundRow"/>
    /// replacing the surface row. Fells trees, fills dips below with <paramref name="fillTile"/>, and protects the area.
    /// </summary>
    public static bool TryBuild(string path, int groundRow, IEnumerable<SurfaceSite> sites, int maxUnevenness, int padding,
        ushort fillTile, out PlacedStructure placed)
    {
        Point16 size = Structures.Dimensions(path);

        foreach (SurfaceSite site in sites)
        {
            var area = new Rectangle(site.Left, site.FloorY - groundRow, size.X, size.Y);
            int fillBottom = Math.Max(area.Bottom, site.FloorY + maxUnevenness + 1);

            // Checked down through the backfill as well, so filling a dip can never bury or break a chest.
            if (!StructureWorld.InWorld(area) || !GenVars.structures.CanPlace(area with { Height = fillBottom - area.Top }, padding))
                continue;

            FellTrees(area, maxUnevenness);

            placed = Structures.Place(path, new Point(site.Left, site.FloorY), StructureAnchor.Tile(0, groundRow));

            Backfill(placed, groundRow, maxUnevenness, fillTile);
            FrameBorder(placed.Area, fillBottom - placed.Area.Bottom);

            GenVars.structures.AddProtectedStructure(placed.Area, padding);
            return true;
        }

        placed = default;
        return false;
    }

    public static int DistanceToSpan(int x, int left, int right) => x < left ? left - x : x > right ? x - right : 0;

    /// <summary>A block that can be stood on, slopes and half blocks included, which <see cref="WorldGen.SolidTile(int, int, bool)"/> turns away.</summary>
    public static bool IsGround(Tile tile) => tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];

    /// <param name="heights">Scratch buffer the width of the footprint, reused across candidates.</param>
    private static bool TryScore(SurfaceScan scan, int left, int[] heights, int maxUnevenness, out int floorY, out int cost)
    {
        floorY = cost = 0;

        int width = heights.Length;
        int right = left + width - 1;

        if (scan.Count(SurfaceGround.Blocked, left - EdgeMargin, right + EdgeMargin) > 0)
            return false;

        Array.Copy(scan.Height, left, heights, 0, width);
        Array.Sort(heights);
        floorY = heights[width / 2];

        for (int x = left - EdgeMargin; x <= right + EdgeMargin; x++)
        {
            int offset = Math.Abs(scan.Height[x] - floorY);

            if (offset > maxUnevenness)
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

    #region Groundwork
    /// <summary>
    /// Removes trees and cacti in the way. The structure writes its air over the whole footprint, which would cut their
    /// trunks and leave the tops hanging above it.
    /// </summary>
    private static void FellTrees(Rectangle area, int depth)
    {
        int top = Math.Max(10, area.Top - TreeHeadroom);
        int bottom = area.Bottom - 1 + depth;

        for (int x = area.Left - TreeMargin; x < area.Right + TreeMargin; x++)
        {
            for (int y = top; y <= bottom; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && IsTreeLike(tile.TileType))
                    WorldGen.KillTile(x, y, noItem: true);
            }
        }

        // The tree check runs on framing: branches and roots left beside a felled trunk drop here.
        WorldGen.RangeFrame(area.Left - TreeMargin - 2, top, area.Right + TreeMargin + 1, bottom);
    }

    private static bool IsTreeLike(int type) => TileID.Sets.IsATreeTrunk[type] || type is TileID.PalmTree or TileID.Cactus;

    /// <summary>
    /// Fills air under the floor down to the ground, at most <paramref name="depth"/> rows below it, so the structure never
    /// overhangs a dip. Cells the structure writes are kept; a column whose floor cell is void is left to the world.
    /// </summary>
    private static void Backfill(PlacedStructure placed, int groundRow, int depth, ushort fillTile)
    {
        StructureData data = StructureLoader.Get(placed.Path);
        int floorY = placed.Origin.Y + groundRow;

        for (int localX = 0; localX < data.Width; localX++)
        {
            if (data[localX, groundRow].Has(CellFlags.TileVoid))
                continue;

            int x = placed.Origin.X + localX;

            for (int y = floorY + 1; y <= floorY + depth; y++)
            {
                int localY = y - placed.Origin.Y;

                if (localY < data.Height && !data[localX, localY].Has(CellFlags.TileVoid))
                    continue;

                Tile tile = Main.tile[x, y];

                if (IsGround(tile))
                    break;

                if (tile.HasTile)
                    WorldGen.KillTile(x, y, noItem: true);

                WorldGen.PlaceTile(x, y, fillTile, mute: true, forced: true);
            }
        }
    }

    /// <summary>
    /// Re-frames the ring around the structure, so anything its edge cut through breaks cleanly and the backfill joins
    /// the ground beside it.
    /// </summary>
    private static void FrameBorder(Rectangle area, int fillDepth)
    {
        // RangeFrame pads a tile on every side, which reaches only the structure's outermost row and column.
        WorldGen.RangeFrame(area.Left - 2, area.Top - 2, area.Left - 1, area.Bottom + fillDepth);
        WorldGen.RangeFrame(area.Right, area.Top - 2, area.Right + 1, area.Bottom + fillDepth);
        WorldGen.RangeFrame(area.Left, area.Top - 2, area.Right - 1, area.Top - 1);
        WorldGen.RangeFrame(area.Left, area.Bottom, area.Right - 1, area.Bottom + fillDepth);
    }
    #endregion
}
