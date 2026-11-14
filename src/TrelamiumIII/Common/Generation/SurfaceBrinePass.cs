using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Common.Generation;

/// <summary>
/// Inland brine: two or three large salt lakes and a scattering of small ponds sunk into the surface, far likelier on
/// snow, and existing surface lakes salted, most of them in the snow. Deserts are left alone. Runs after
/// <see cref="BrinePass" />, which handles the coasts.
/// </summary>
public class SurfaceBrinePass : GenPass
{
    /// <summary> Size, depth and how uneven the ground under one kind of dug basin may be. </summary>
    private readonly record struct BasinKind(int MinHalfWidth, int MaxHalfWidth, int MinDepth, int MaxDepth, int MaxSurfaceStep);

    private static readonly BasinKind Pond = new(4, 9, 2, 5, MaxSurfaceStep: 1);

    private static readonly BasinKind Lake = new(16, 27, 6, 11, MaxSurfaceStep: 4);

    /// <summary> Columns kept clear of each beach, which <see cref="BrinePass" /> already salts. </summary>
    private const int CoastMargin = 80;

    /// <summary> Columns kept clear either side of the desert. </summary>
    private const int DesertMargin = 60;

    private const int CandidateStride = 4;
    private const int PlacementAttempts = 80;
    private const int StructurePadding = 8;

    /// <summary> A candidate on snow counts this many times over one elsewhere. </summary>
    private const int SnowWeight = 5;

    private const int BasinLining = 2;

    /// <summary> Rows above a basin kept free of trees and structures. </summary>
    private const int Headroom = 15;

    /// <summary> Lakes larger than this many tiles are left alone. </summary>
    private const int MaxLakeTiles = 6000;

    /// <summary> Share of a lake's shore that must be snow or ice for it to count as a snow lake, or sand for a desert one. </summary>
    private const float ShoreShare = 0.4f;

    private const float SnowLakeChance = 0.65f;
    private const float OtherLakeChance = 0.08f;

    // Indexed by WorldGen.GetWorldSize(): small, medium, large.
    private static readonly int[] PondCount = [5, 8, 12];

    public SurfaceBrinePass() : base("[T3] Surface Brine", 20f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Salting the snowmelt...";

        byte brine = WaterLiquids.Instance.Brine.Index;
        (int left, int right) = InlandSpan();

        SaltLakes(left, right, brine);
        progress.Set(0.3);

        List<int> candidates = BasinCandidates(left, right);
        int lakes = WorldGen.genRand.Next(2, 4);

        for (int n = 0; n < lakes; n++)
            TryPlaceBasin(candidates, Lake, brine);

        progress.Set(0.5);

        int ponds = PondCount[WorldGen.GetWorldSize()];
        for (int n = 0; n < ponds; n++)
        {
            TryPlaceBasin(candidates, Pond, brine);
            progress.Set(0.5 + 0.5 * (n + 1) / ponds);
        }
    }

    /// <summary> Whether column <paramref name="x" /> is within <see cref="DesertMargin" /> of the desert. </summary>
    private static bool InDesert(int x)
    {
        Rectangle desert = GenVars.UndergroundDesertLocation;
        return desert.Width > 0 && x >= desert.Left - DesertMargin && x <= desert.Right + DesertMargin;
    }

    private static bool IsSand(ushort type) => type is TileID.Sand or TileID.Pearlsand or TileID.Ebonsand or TileID.Crimsand
        or TileID.HardenedSand or TileID.Sandstone;

    /// <summary> The columns between both coasts, past the reach of <see cref="BrinePass" />. </summary>
    private static (int Left, int Right) InlandSpan()
    {
        int fallback = (int)(Main.maxTilesX * 0.08f);
        int left = (GenVars.leftBeachEnd > 0 ? GenVars.leftBeachEnd : fallback) + CoastMargin;
        int right = (GenVars.rightBeachStart > 0 ? GenVars.rightBeachStart : Main.maxTilesX - fallback) - CoastMargin;
        return (left, right);
    }

    private static bool IsSnow(ushort type) => type is TileID.SnowBlock or TileID.IceBlock or TileID.CorruptIce
        or TileID.FleshIce or TileID.HallowedIce or TileID.Slush;

    /// <summary> First solid row from the top of the world in column <paramref name="x" />, or -1 above the rock layer. </summary>
    private static int SurfaceRow(int x)
    {
        for (int y = 50; y < (int)Main.worldSurface; y++)
        {
            if (WorldGen.SolidTile(x, y))
                return y;
        }

        return -1;
    }

    #region Lakes

    /// <summary> Salts whole bodies of surface water, likelier when their shore is mostly snow or ice. Oases stay fresh. </summary>
    private static void SaltLakes(int left, int right, byte brine)
    {
        var visited = new HashSet<Point>();
        var body = new List<Point>();

        for (int x = left; x <= right; x++)
        {
            for (int y = 50; y < (int)Main.worldSurface; y++)
            {
                if (!IsWater(x, y) || visited.Contains(new Point(x, y)))
                    continue;

                if (!TryCollectLake(new Point(x, y), left, right, visited, body))
                    continue;

                if (InDesert(x) || Shore(body, IsSand) >= ShoreShare)
                    continue;

                float chance = Shore(body, IsSnow) >= ShoreShare ? SnowLakeChance : OtherLakeChance;
                if (WorldGen.genRand.NextFloat() >= chance)
                    continue;

                foreach (Point point in body)
                    Main.tile[point.X, point.Y].InternalLiquid() = brine;
            }
        }
    }

    private static bool IsWater(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
    }

    /// <summary>
    /// Collects the water connected to <paramref name="seed" /> above the rock layer. False for bodies that are too
    /// large or reach the coasts, which are left fresh; their tiles are still marked visited.
    /// </summary>
    private static bool TryCollectLake(Point seed, int left, int right, HashSet<Point> visited, List<Point> body)
    {
        body.Clear();
        var pending = new Stack<Point>();
        pending.Push(seed);
        visited.Add(seed);
        bool keep = true;

        while (pending.TryPop(out Point point))
        {
            body.Add(point);

            if (body.Count > MaxLakeTiles)
                keep = false;

            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y - 1), new(point.X, point.Y + 1)])
            {
                if (next.Y <= 1 || next.Y >= (int)Main.rockLayer || !IsWater(next.X, next.Y) || !visited.Add(next))
                    continue;

                if (next.X < left || next.X > right)
                    keep = false;

                pending.Push(next);
            }
        }

        return keep;
    }

    /// <summary> The share of solid tiles bordering <paramref name="body" /> that match <paramref name="kind" />. </summary>
    private static float Shore(List<Point> body, Func<ushort, bool> kind)
    {
        int shore = 0;
        int matching = 0;

        foreach (Point point in body)
        {
            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y + 1)])
            {
                Tile tile = Main.tile[next.X, next.Y];
                if (!WorldGen.SolidTile(next.X, next.Y))
                    continue;

                shore++;
                if (kind(tile.TileType))
                    matching++;
            }
        }

        return shore == 0 ? 0f : matching / (float)shore;
    }

    #endregion

    #region Basins

    /// <summary> Dry surface columns away from the desert, each listed once, or <see cref="SnowWeight" /> times on snow. </summary>
    private static List<int> BasinCandidates(int left, int right)
    {
        var candidates = new List<int>();

        for (int x = left; x <= right; x += CandidateStride)
        {
            int surface = SurfaceRow(x);
            if (surface < 0 || InDesert(x) || IsSand(Main.tile[x, surface].TileType) || Main.tile[x, surface - 1].LiquidAmount > 0)
                continue;

            int weight = IsSnow(Main.tile[x, surface].TileType) ? SnowWeight : 1;
            for (int w = 0; w < weight; w++)
                candidates.Add(x);
        }

        return candidates;
    }

    private static bool TryPlaceBasin(List<int> candidates, BasinKind kind, byte brine)
    {
        for (int attempt = 0; attempt < PlacementAttempts && candidates.Count > 0; attempt++)
        {
            int x = candidates[WorldGen.genRand.Next(candidates.Count)];
            int halfWidth = WorldGen.genRand.Next(kind.MinHalfWidth, kind.MaxHalfWidth + 1);
            int depth = WorldGen.genRand.Next(kind.MinDepth, kind.MaxDepth + 1);

            if (!Fits(x, halfWidth, depth, kind.MaxSurfaceStep, out int level, out int top))
                continue;

            var bounds = new Rectangle(x - halfWidth - BasinLining, top - Headroom, (halfWidth + BasinLining) * 2 + 1, level - top + Headroom + depth + BasinLining + 1);
            if (!GenVars.structures.CanPlace(bounds, StructurePadding))
                continue;

            Dig(x, level, halfWidth, depth, brine);
            BrinePass.CrustWithSalt(bounds, level, brine);
            GenVars.structures.AddProtectedStructure(bounds);

            // Keep basins apart.
            candidates.RemoveAll(c => Math.Abs(c - x) < halfWidth * 4);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ground across the span within <paramref name="maxStep" /> rows of level, clear of the desert, liquid, trees and
    /// protected objects. The basin's surface sits at <paramref name="level" />, the lowest ground in the span, so it
    /// cannot spill; <paramref name="top" /> is the highest.
    /// </summary>
    private static bool Fits(int x, int halfWidth, int depth, int maxStep, out int level, out int top)
    {
        level = -1;
        top = int.MaxValue;
        int left = x - halfWidth - BasinLining;
        int right = x + halfWidth + BasinLining;

        for (int c = left; c <= right; c++)
        {
            int surface = SurfaceRow(c);
            if (surface < 0 || InDesert(c) || IsSand(Main.tile[c, surface].TileType))
                return false;

            level = Math.Max(level, surface);
            top = Math.Min(top, surface);
        }

        if (level - top > maxStep)
            return false;

        for (int c = left; c <= right; c++)
        {
            for (int y = top - Headroom; y <= level + depth + BasinLining; y++)
            {
                Tile tile = Main.tile[c, y];
                if (tile.LiquidAmount > 0 || PlacementScan.IsProtected(tile))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Levels the ground over the basin down to <paramref name="level" />, sinks a half-ellipse basin below it, lines it
    /// with the ground's own material so it holds, and fills it with brine.
    /// </summary>
    private static void Dig(int x, int level, int halfWidth, int depth, byte brine)
    {
        ushort lining = LiningFor(Main.tile[x, SurfaceRow(x)].TileType);
        var basin = new HashSet<Point>();

        for (int c = x - halfWidth; c <= x + halfWidth; c++)
        {
            // Plants and rubble on the ground being dug away, then any ground above the water line.
            for (int y = level - Headroom; y < level; y++)
            {
                Tile tile = Main.tile[c, y];
                if (tile.HasTile && Main.tileFrameImportant[tile.TileType])
                    LiquidDepositBuilder.ClearObject(c, y);
            }

            for (int y = SurfaceRow(c); y >= 0 && y < level; y++)
                Main.tile[c, y].ClearTile();

            for (int y = level; y <= level + depth; y++)
            {
                float dx = (c - x) / (float)halfWidth;
                float dy = (y - level + 0.5f) / depth;

                if (dx * dx + dy * dy < 1f)
                    basin.Add(new Point(c, y));
            }
        }

        foreach (Point cell in basin)
            Main.tile[cell.X, cell.Y].ClearTile();

        foreach (Point cell in basin)
        {
            for (int ox = -BasinLining; ox <= BasinLining; ox++)
            {
                for (int oy = -BasinLining; oy <= BasinLining; oy++)
                {
                    var next = new Point(cell.X + ox, cell.Y + oy);
                    Tile tile = Main.tile[next.X, next.Y];

                    if (next.Y >= level && !basin.Contains(next) && (!LiquidDepositBuilder.BlocksLiquid(tile) || TileID.Sets.Falling[tile.TileType]))
                        LiquidDepositBuilder.SetSolid(tile, lining);
                }
            }
        }

        foreach (Point cell in basin)
        {
            Tile tile = Main.tile[cell.X, cell.Y];
            tile.LiquidType = LiquidID.Water;
            tile.LiquidAmount = byte.MaxValue;
            tile.InternalLiquid() = brine;
        }
    }

    /// <summary> Grass lines with its dirt, falling sand with its hardened form; other solid ground lines with itself. </summary>
    private static ushort LiningFor(ushort surface)
    {
        if (TileID.Sets.Grass[surface])
            return TileID.Dirt;

        return surface switch
        {
            TileID.Sand or TileID.Pearlsand or TileID.Ebonsand or TileID.Crimsand => TileID.HardenedSand,
            TileID.SnowBlock or TileID.IceBlock or TileID.Slush => TileID.SnowBlock,
            _ when Main.tileSolid[surface] && !TileID.Sets.Falling[surface] => surface,
            _ => TileID.Dirt,
        };
    }

    #endregion
}
