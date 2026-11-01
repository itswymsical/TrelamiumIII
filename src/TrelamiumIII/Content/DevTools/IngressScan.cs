using System.Collections.Generic;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress.HighlandsBiome;

namespace TrelamiumIII.Content.DevTools;

/// <summary> Finds the Ingress realm's landmarks in the tile map, for the harnesses to report on, crop and visit. </summary>
internal static class IngressScan
{
    /// <summary> Columns either side of the centre the canopy tree and its grove can occupy, kept out of the peak search. </summary>
    public const int SiteHalfWidth = 220;

    /// <summary> First solid, non-platform row of every column. Leaves are solid, so a living tree's canopy reads as ground. </summary>
    public static int[] Ground()
    {
        int[] ground = new int[Main.maxTilesX];

        for (int x = 0; x < Main.maxTilesX; x++)
            ground[x] = GroundRow(x);

        return ground;
    }

    public static int GroundRow(int x)
    {
        for (int y = 10; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return y;
        }

        return Main.maxTilesY - 10;
    }

    public static bool SurfaceWater(int x, int groundRow)
    {
        Tile above = Main.tile[x, groundRow - 1];
        return groundRow < Main.worldSurface + 20 && !above.HasTile && above.LiquidAmount > 0 && above.LiquidType == LiquidID.Water;
    }

    public static int WaterLevel(int left, int right, int[] ground)
    {
        int x = (left + right) / 2;

        for (int y = ground[x] - 1; y > 10; y--)
        {
            if (Main.tile[x, y].LiquidAmount == 0)
                return y + 1;
        }

        return ground[x];
    }

    /// <summary> Runs of columns matching <paramref name="match"/>, merging runs fewer than <paramref name="gap"/> columns apart. </summary>
    public static List<(int Left, int Right)> Spans(Func<int, bool> match, int gap)
    {
        var spans = new List<(int Left, int Right)>();
        int start = -1;
        int last = -1;

        for (int x = 10; x < Main.maxTilesX - 10; x++)
        {
            if (!match(x))
                continue;

            if (start < 0 || x - last > gap)
            {
                if (start >= 0)
                    spans.Add((start, last));

                start = x;
            }

            last = x;
        }

        if (start >= 0)
            spans.Add((start, last));

        return spans;
    }

    public static List<(int Left, int Right)> LeafSpans()
    {
        int spruce = ModContent.TileType<SpruceLeafTile>();
        int birch = ModContent.TileType<BirchLeafTile>();

        return Spans(x =>
        {
            for (int y = 10; y < Main.worldSurface; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && (tile.TileType == spruce || tile.TileType == birch))
                    return true;
            }

            return false;
        }, 4);
    }

    /// <summary> Columns empty both just above and well below the bottom of the surface layer: only a canyon cuts through there. </summary>
    public static List<(int Left, int Right)> CanyonSpans()
    {
        int above = (int)Main.worldSurface - 5;
        int below = (int)Main.worldSurface + 15;

        return Spans(x => !Main.tile[x, above].HasTile && !Main.tile[x, below].HasTile, 4).FindAll(span => span.Right - span.Left >= 5);
    }

    /// <summary> Column of the highest ground outside the canopy tree's site. </summary>
    public static int Peak(int[] ground)
    {
        int peak = 10;

        for (int x = 10; x < Main.maxTilesX - 10; x++)
        {
            if (Math.Abs(x - Main.maxTilesX / 2) > SiteHalfWidth && ground[x] < ground[peak])
                peak = x;
        }

        return peak;
    }

    public static int Min(int[] values, int from, int to)
    {
        int min = int.MaxValue;

        for (int i = from; i <= to; i++)
            min = Math.Min(min, values[i]);

        return min;
    }

    /// <summary> Runs of columns whose top tile is the blight's grass, at least <paramref name="minWidth"/> wide. </summary>
    public static List<(int Left, int Right)> BlightSpans(int[] ground, int minWidth)
    {
        int corrupt = ModContent.TileType<CorruptHighlandsGrass>();
        int crimson = ModContent.TileType<CrimsonHighlandsGrass>();

        return Spans(x => Main.tile[x, ground[x]].TileType == corrupt || Main.tile[x, ground[x]].TileType == crimson, 20).FindAll(span => span.Right - span.Left >= minWidth);
    }

    /// <summary> Runs of columns whose top tile is podzol, at least <paramref name="minWidth"/> wide; wide gaps are bridged, since living trees' leaves read as ground. </summary>
    public static List<(int Left, int Right)> TaigaSpans(int[] ground, int minWidth)
    {
        int podzol = ModContent.TileType<PodzolGrass>();

        return Spans(x => Main.tile[x, ground[x]].TileType == podzol, 50).FindAll(span => span.Right - span.Left >= minWidth);
    }

    /// <summary> The first foreground bush's top-left tile well away from the world's edges, if there is one. </summary>
    public static Point? FirstForegroundBush()
    {
        int type = ModContent.TileType<ForegroundBushTile>();

        for (int x = 200; x < Main.maxTilesX - 200; x++)
        {
            for (int y = 10; y < Main.worldSurface; y++)
            {
                if (Main.tile[x, y] is { HasTile: true } tile && tile.TileType == type)
                    return new Point(x, y);
            }
        }

        return null;
    }

    /// <summary> Runs of columns whose surface holds water, at least <paramref name="minWidth"/> wide. </summary>
    public static List<(int Left, int Right)> WaterSpans(int[] ground, int minWidth)
        => Spans(x => SurfaceWater(x, ground[x]), 3).FindAll(span => span.Right - span.Left + 1 >= minWidth);
}
