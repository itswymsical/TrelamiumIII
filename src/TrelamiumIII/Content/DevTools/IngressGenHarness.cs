using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;
using TrelamiumIII.Content.Ingress.HighlandsBiome.SpruceFurniture;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Dev tool. Generates the Ingress realm headlessly on a dedicated server and renders it to PNGs with a stats file, to
///     check the terrain without playing. Runs only when <see cref="OutputVariable" /> names an output folder, after
///     content loads and before any world does, then exits the server. Mirrors SubworldLibrary's own generation: the
///     realm's size, the same world-clearing calls, and every pass reseeded with the one seed.
///     See tools/IngressHarness/Run-IngressHarness.ps1.
/// </summary>
public sealed class IngressGenHarness : ModSystem
{
    private const string OutputVariable = "TRELAMIUM_INGRESS_HARNESS";
    private const string SeedsVariable = "TRELAMIUM_INGRESS_SEEDS";

    private const int OverviewScale = 4;
    private const int SurfaceScale = 2;
    private const int CropZoom = 2;
    private const int SurfaceMarginAbove = 30;
    private const int SurfaceMarginBelow = 60;
    private const int CanyonCropHalfWidth = 130;
    private const int CanyonCropBelowRockLayer = 70;

    /// <summary> Columns either side of the centre the canopy tree and its grove can occupy, kept out of the peak search. </summary>
    private const int SiteHalfWidth = 220;
    private const int TreeCropHalfWidth = 45;
    private const int TreeCropAbove = 120;
    private const int TreeCropBelow = 30;
    private const int PeakCropHalfWidth = 160;
    private const int PeakCropHeight = 260;
    private const int TreesCropped = 3;

    private static readonly Color Sky = new(150, 196, 232);
    private static readonly Color Underground = new(16, 14, 18);
    private static readonly Color Water = new(44, 96, 210);
    private static readonly Color Lava = new(235, 96, 24);

    public override void PostSetupRecipes()
    {
        if (!Main.dedServ || Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } output)
            return;

        Directory.CreateDirectory(output);
        int failures = 0;

        foreach (int seed in Seeds())
        {
            try
            {
                Run(seed, output);
            }
            catch (Exception exception)
            {
                failures++;
                File.WriteAllText(Path.Combine(output, $"ingress_{seed}_error.txt"), exception.ToString());
                Mod.Logger.Error($"Ingress harness seed {seed} failed", exception);
            }
        }

        Mod.Logger.Info($"Ingress harness finished with {failures} failure(s); output in {output}");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static IEnumerable<int> Seeds()
    {
        string seeds = Environment.GetEnvironmentVariable(SeedsVariable) ?? "1,2,3";

        foreach (string seed in seeds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(seed, out int value))
                yield return value;
        }
    }

    private static void Run(int seed, string output)
    {
        var realm = ModContent.GetInstance<IngressRealm>();
        var report = new StringBuilder($"Ingress seed {seed}\n\n");

        Main.maxTilesX = realm.Width;
        Main.maxTilesY = realm.Height;
        Main.spawnTileX = Main.maxTilesX / 2;
        Main.spawnTileY = Main.maxTilesY / 2;
        WorldGen.setWorldSize();
        WorldGen.clearWorld();
        Main.worldSurface = Main.maxTilesY * 0.3;
        Main.rockLayer = Main.maxTilesY * 0.5;
        GenVars.waterLine = Main.maxTilesY;

        // The realm takes the main world's evil; alternate it by seed so both get exercised.
        WorldGen.crimson = seed % 2 == 0;
        report.AppendLine($"evil: {(WorldGen.crimson ? "crimson" : "corruption")}\n");

        List<GenPass> tasks = realm.Tasks;
        var progress = new GenerationProgress();

        foreach (GenPass pass in tasks)
            progress.TotalWeight += pass.Weight;

        WorldGen.gen = true;
        string? failed = null;

        try
        {
            foreach (GenPass pass in tasks)
            {
                WorldGen._genRand = new UnifiedRandom(seed);
                Main.rand = new UnifiedRandom(seed);

                var watch = Stopwatch.StartNew();

                try
                {
                    progress.Start(pass.Weight);
                    pass.Apply(progress, null);
                    progress.End();
                }
                catch (Exception exception)
                {
                    failed = pass.Name;
                    report.AppendLine($"FAILED {pass.Name} after {watch.ElapsedMilliseconds} ms:\n{exception}\n");
                    break;
                }

                report.AppendLine($"{pass.Name,-34} {watch.ElapsedMilliseconds,6} ms{SaplingNote()}");
            }
        }
        finally
        {
            WorldGen.gen = false;
        }

        int[] ground = new int[Main.maxTilesX];

        for (int x = 0; x < Main.maxTilesX; x++)
            ground[x] = GroundRow(x);

        Describe(report, ground);
        Render(output, seed, ground, report);

        File.WriteAllText(Path.Combine(output, $"ingress_{seed}_report.txt"), report.ToString());

        if (failed is not null)
            throw new InvalidOperationException($"Pass '{failed}' threw; see ingress_{seed}_report.txt");
    }

    private static void Describe(StringBuilder report, int[] ground)
    {
        int peak = Peak(ground);
        int baseY = IngressRealm.BaseSurfaceY;

        report.AppendLine();
        report.AppendLine($"worldSurface {Main.worldSurface:0}, rockLayer {Main.rockLayer:0}, base line {baseY}, sky layer below {Main.worldSurface * 0.35:0}");
        report.AppendLine($"highest ground away from the canopy tree: row {ground[peak]} (column {peak}, {baseY - ground[peak]} above base)");
        report.AppendLine($"canopy tree top row {Min(ground, Main.maxTilesX / 2 - SiteHalfWidth, Main.maxTilesX / 2 + SiteHalfWidth)}");
        report.AppendLine($"spawn ({Main.spawnTileX}, {Main.spawnTileY})");

        (string Name, int Type)[] watched =
        [
            ("HighlandsGrass", ModContent.TileType<HighlandsGrass>()),
            ("SnowHighlandsGrass", ModContent.TileType<SnowHighlandsGrass>()),
            ("IngressGrass", ModContent.TileType<IngressGrass>()),
            ("Peat", ModContent.TileType<PeatTile>()),
            ("Basalt", ModContent.TileType<BasaltTile>()),
            ("Slate", ModContent.TileType<SlateTile>()),
            ("Snow", TileID.SnowBlock),
            ("Clay", TileID.ClayBlock),
            ("Gravel", ModContent.TileType<GravelTile>()),
            ("SpruceWood", ModContent.TileType<SpruceWoodTile>()),
            ("BirchWood", ModContent.TileType<BirchWoodTile>()),
            ("SpruceLeaves", ModContent.TileType<SpruceLeafTile>()),
            ("BirchLeaves", ModContent.TileType<BirchLeafTile>()),
            ("SpruceTree", ModContent.TileType<SpruceTree>()),
            ("Vanilla tree tiles (birch)", TileID.Trees),
            ("CorruptHighlandsGrass", ModContent.TileType<CorruptHighlandsGrass>()),
            ("CrimsonHighlandsGrass", ModContent.TileType<CrimsonHighlandsGrass>()),
            ("Ebonstone", TileID.Ebonstone),
            ("Crimstone", TileID.Crimstone),
            ("Shadow orb / heart tiles", TileID.ShadowOrbs),
            ("Altar tiles", TileID.DemonAltar)
        ];

        var counts = new int[watched.Length];
        int surfaceWater = 0;

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];

                if (!tile.HasTile && tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water && y < Main.worldSurface)
                    surfaceWater++;

                if (!tile.HasTile)
                    continue;

                for (int i = 0; i < watched.Length; i++)
                {
                    if (tile.TileType == watched[i].Type)
                        counts[i]++;
                }
            }
        }

        report.AppendLine();
        report.AppendLine("Tile counts:");

        for (int i = 0; i < watched.Length; i++)
            report.AppendLine($"  {watched[i].Name,-26} {counts[i],9}");

        report.AppendLine($"  {"water above worldSurface",-26} {surfaceWater,9}");

        int chests = 0;

        foreach (Chest? chest in Main.chest)
        {
            if (chest is not null)
                chests++;
        }

        report.AppendLine($"  {"chests",-26} {chests,9}");

        // An altar missing a tile breaks when the world frames it, and smashes as if hit with a hammer.
        var altarTiles = new List<Point>();

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                if (Main.tile[x, y] is { HasTile: true, TileType: TileID.DemonAltar })
                    altarTiles.Add(new Point(x, y));
            }
        }

        int whole = 0;

        foreach (Point point in altarTiles)
        {
            Tile tile = Main.tile[point.X, point.Y];

            if (tile.TileFrameX % 54 != 0 || tile.TileFrameY != 0)
                continue;

            bool complete = true;

            for (int dx = 0; dx < 3; dx++)
            {
                for (int dy = 0; dy < 2; dy++)
                    complete &= Main.tile[point.X + dx, point.Y + dy] is { HasTile: true, TileType: TileID.DemonAltar };
            }

            if (complete)
                whole++;
            else
                report.AppendLine($"  broken altar at ({point.X}, {point.Y})");
        }

        report.AppendLine($"  {"whole altars",-26} {whole,9} ({altarTiles.Count - whole * 6} stray tiles)");

        report.AppendLine();
        report.AppendLine("Canyon spans (columns open through the bottom of the surface layer):");

        foreach ((int left, int right) in CanyonSpans())
            report.AppendLine($"  {left}-{right} ({right - left + 1} wide), rim row {Min(ground, Math.Max(left - 40, 10), Math.Min(right + 40, Main.maxTilesX - 11))}");

        report.AppendLine();
        report.AppendLine("Living tree spans (columns holding highland leaves):");

        foreach ((int left, int right) in LeafSpans())
            report.AppendLine($"  {left}-{right} ({right - left + 1} wide), {LeafSpecies(left, right)}");

        report.AppendLine();
        report.AppendLine("Surface water spans over 40 wide:");

        foreach ((int left, int right) in Spans(x => SurfaceWater(x, ground[x]), 3))
        {
            if (right - left + 1 > 40)
                report.AppendLine($"  {left}-{right} ({right - left + 1} wide) at row {WaterLevel(left, right, ground)}");
        }
    }

    private static void Render(string output, int seed, int[] ground, StringBuilder report)
    {
        string Name(string part) => Path.Combine(output, $"ingress_{seed}_{part}.png");

        WritePng(Name("overview"), Main.maxTilesX / OverviewScale, Main.maxTilesY / OverviewScale, (px, py) => TileColor(px * OverviewScale, py * OverviewScale));

        int stripTop = int.MaxValue;

        for (int x = 10; x < Main.maxTilesX - 10; x++)
            stripTop = Math.Min(stripTop, ground[x]);

        stripTop = Math.Max(stripTop - SurfaceMarginAbove, 0);
        int stripBottom = Math.Min((int)Main.worldSurface + SurfaceMarginBelow, Main.maxTilesY - 1);

        WritePng(Name("surface"), Main.maxTilesX / SurfaceScale, (stripBottom - stripTop) / SurfaceScale,
            (px, py) => TileColor(px * SurfaceScale, stripTop + py * SurfaceScale));

        var crops = new List<(string Label, Rectangle Area)>();
        int peak = Peak(ground);
        crops.Add(("peak", new Rectangle(peak - PeakCropHalfWidth, ground[peak] - 20, PeakCropHalfWidth * 2, PeakCropHeight)));

        // The canopy tree's crown reads as ground, so the site is framed off the base line instead.
        int site = Main.maxTilesX / 2;
        int baseY = IngressRealm.BaseSurfaceY;
        crops.Add(("site", new Rectangle(site - SiteHalfWidth, baseY - 330, SiteHalfWidth * 2, 380)));

        int canyon = 0;

        foreach ((int left, int right) in CanyonSpans())
        {
            int center = (left + right) / 2;
            int rim = Min(ground, Math.Max(left - 40, 10), Math.Min(right + 40, Main.maxTilesX - 11));
            int foot = (int)Main.rockLayer + CanyonCropBelowRockLayer;
            crops.Add(($"canyon{++canyon}", new Rectangle(center - CanyonCropHalfWidth, rim - 30, CanyonCropHalfWidth * 2, foot - rim + 30)));
        }

        int corrupt = ModContent.TileType<CorruptHighlandsGrass>();
        int crimsonGrass = ModContent.TileType<CrimsonHighlandsGrass>();

        foreach ((int left, int right) in Spans(x => Main.tile[x, ground[x]].TileType == corrupt || Main.tile[x, ground[x]].TileType == crimsonGrass, 20))
        {
            if (right - left < 60)
                continue;

            int rim = Min(ground, Math.Max(left - 40, 10), Math.Min(right + 40, Main.maxTilesX - 11));
            report.AppendLine($"blight {left}-{right} ({right - left + 1} wide)");
            crops.Add(("blight", new Rectangle(left - 40, rim - 40, right - left + 80, (int)Main.worldSurface + 130 - rim)));
            break;
        }

        int tree = 0;

        foreach ((int left, int right) in LeafSpans())
        {
            if (tree >= TreesCropped)
                break;

            // Leaves are solid, so the ground is read just outside the canopy.
            int center = (left + right) / 2;
            int treeGround = ground[Math.Max(left - 3, 0)];
            crops.Add(($"tree{++tree}", new Rectangle(center - TreeCropHalfWidth, treeGround - TreeCropAbove, TreeCropHalfWidth * 2, TreeCropAbove + TreeCropBelow)));
        }

        foreach ((int left, int right) in Spans(x => SurfaceWater(x, ground[x]), 3))
        {
            if (right - left + 1 <= 150)
                continue;

            int level = WaterLevel(left, right, ground);
            crops.Add(("river", new Rectangle(left - 60, level - 50, 240, 90)));
            break;
        }

        report.AppendLine();
        report.AppendLine("Crops (tile rectangles):");

        foreach ((string label, Rectangle area) in crops)
        {
            Rectangle clamped = Rectangle.Intersect(area, new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));

            if (clamped.Width <= 0 || clamped.Height <= 0)
                continue;

            report.AppendLine($"  {label,-8} x {clamped.X}-{clamped.Right}, y {clamped.Y}-{clamped.Bottom}");
            WritePng(Name(label), clamped.Width * CropZoom, clamped.Height * CropZoom, (px, py) => TileColor(clamped.X + px / CropZoom, clamped.Y + py / CropZoom));
        }
    }

    private static Color TileColor(int x, int y)
    {
        Tile tile = Main.tile[x, y];

        if (tile.HasTile)
            return TypeColor(tile.TileType);

        if (tile.LiquidAmount > 0)
            return tile.LiquidType == LiquidID.Lava ? Lava : Water;

        if (tile.WallType != WallID.None)
            return tile.WallType == WallID.LivingWoodUnsafe ? new Color(92, 66, 44) : new Color(44, 36, 30);

        return y < Main.worldSurface ? Sky : Underground;
    }

    private static Color TypeColor(int type)
    {
        if (type == ModContent.TileType<HighlandsGrass>())
            return new Color(58, 152, 70);
        if (type == ModContent.TileType<SnowHighlandsGrass>())
            return new Color(176, 214, 196);
        if (type == ModContent.TileType<IngressGrass>())
            return new Color(132, 186, 52);
        if (type == ModContent.TileType<PeatTile>())
            return new Color(98, 70, 50);
        if (type == ModContent.TileType<BasaltTile>())
            return new Color(52, 54, 64);
        if (type == ModContent.TileType<SlateTile>())
            return new Color(118, 122, 120);
        if (type == ModContent.TileType<GravelTile>())
            return new Color(206, 196, 170);
        if (type == ModContent.TileType<SpruceWoodTile>())
            return new Color(122, 84, 58);
        if (type == ModContent.TileType<BirchWoodTile>())
            return new Color(232, 222, 206);
        if (type == ModContent.TileType<SpruceLeafTile>())
            return new Color(24, 86, 66);
        if (type == ModContent.TileType<BirchLeafTile>())
            return new Color(168, 210, 80);
        if (type == ModContent.TileType<BramblerootTile>())
            return new Color(70, 48, 40);
        if (type == ModContent.TileType<TerracottaTile>())
            return new Color(178, 92, 60);
        if (type == ModContent.TileType<GrayTerracottaTile>())
            return new Color(140, 126, 118);

        switch (type)
        {
            case TileID.SnowBlock:
                return new Color(244, 248, 252);
            case TileID.ClayBlock:
                return new Color(200, 116, 84);
            case TileID.Stone:
                return new Color(128, 128, 128);
            case TileID.LivingWood:
                return new Color(150, 100, 60);
            case TileID.LeafBlock:
                return new Color(40, 140, 40);
        }

        if (Main.tileSolidTop[type])
            return new Color(190, 140, 80);

        if (!Main.tileSolid[type])
            return new Color(255, 120, 200);

        // Anything else gets a muted colour of its own, so unexpected tiles still stand out from their neighbours.
        uint h = (uint)type * 2654435761u;
        return new Color(90 + (int)(h & 0x7F), 90 + (int)((h >> 8) & 0x7F), 90 + (int)((h >> 16) & 0x7F));
    }

    private static int GroundRow(int x)
    {
        for (int y = 10; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return y;
        }

        return Main.maxTilesY - 10;
    }

    private static bool SurfaceWater(int x, int groundRow)
    {
        Tile above = Main.tile[x, groundRow - 1];
        return groundRow < Main.worldSurface + 20 && !above.HasTile && above.LiquidAmount > 0 && above.LiquidType == LiquidID.Water;
    }

    private static int WaterLevel(int left, int right, int[] ground)
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
    private static List<(int Left, int Right)> Spans(Func<int, bool> match, int gap)
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

    /// <summary> Sapling tiles standing anywhere right now, by type, so the report shows which pass left them. </summary>
    private static string SaplingNote()
    {
        var counts = new Dictionary<int, int>();

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && TileID.Sets.TreeSapling[tile.TileType])
                    counts[tile.TileType] = counts.GetValueOrDefault(tile.TileType) + 1;
            }
        }

        if (counts.Count == 0)
            return "";

        var note = new StringBuilder("   saplings:");

        foreach ((int type, int count) in counts)
            note.Append($" {(type < TileID.Count ? TileID.Search.GetName(type) : TileLoader.GetTile(type).Name)}={count}");

        return note.ToString();
    }

    private static List<(int Left, int Right)> LeafSpans()
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

    private static string LeafSpecies(int left, int right)
    {
        int spruce = 0;
        int birch = 0;

        for (int x = left; x <= right; x++)
        {
            for (int y = 10; y < Main.worldSurface; y++)
            {
                Tile tile = Main.tile[x, y];

                if (!tile.HasTile)
                    continue;

                if (tile.TileType == ModContent.TileType<SpruceLeafTile>())
                    spruce++;
                else if (tile.TileType == ModContent.TileType<BirchLeafTile>())
                    birch++;
            }
        }

        return spruce >= birch ? $"spruce ({spruce} leaves)" : $"birch ({birch} leaves)";
    }

    /// <summary> Columns empty both just above and well below the bottom of the surface layer: only a canyon cuts through there. </summary>
    private static List<(int Left, int Right)> CanyonSpans()
    {
        int above = (int)Main.worldSurface - 5;
        int below = (int)Main.worldSurface + 15;

        return Spans(x => !Main.tile[x, above].HasTile && !Main.tile[x, below].HasTile, 4).FindAll(span => span.Right - span.Left >= 5);
    }

    /// <summary> Column of the highest ground outside the canopy tree's site. </summary>
    private static int Peak(int[] ground)
    {
        int peak = 10;

        for (int x = 10; x < Main.maxTilesX - 10; x++)
        {
            if (Math.Abs(x - Main.maxTilesX / 2) > SiteHalfWidth && ground[x] < ground[peak])
                peak = x;
        }

        return peak;
    }

    private static int Min(int[] values, int from, int to)
    {
        int min = int.MaxValue;

        for (int i = from; i <= to; i++)
            min = Math.Min(min, values[i]);

        return min;
    }

    private static void WritePng(string path, int width, int height, Func<int, int, Color> pixel)
    {
        if (width <= 0 || height <= 0)
            return;

        int stride = width * 3 + 1;
        byte[] raw = new byte[stride * height];

        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                Color color = pixel(px, py);
                int i = py * stride + 1 + px * 3;
                raw[i] = color.R;
                raw[i + 1] = color.G;
                raw[i + 2] = color.B;
            }
        }

        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw);

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 2;

        using FileStream file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = Crc32(Crc32(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc ^= b;

            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }
}
