using System.Globalization;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TrelamiumIII.Tools.FontBaker;

/// <summary>
///     Rasterises a TrueType font into the atlas + metric pair that
///     <c>TrelamiumIII.Core.Text.BakedFont</c> feeds to ReLogic's DynamicSpriteFont.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Ranges baked when --ranges is omitted: Latin plus the punctuation, currency and
    ///     arrows that prose actually reaches for. Codepoints the font does not map are dropped
    ///     silently, so an over-broad default costs nothing but bake time.
    /// </summary>
    private const string DEFAULT_RANGES =
        "20-7E,A0-FF,100-17F,180-24F,2010-2027,2030-205E,20A0-20BF,2122,2190-2193,2212,25CF,2660-2667";

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");

            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(HelpText);

            return 0;
        }

        string? outDir = null;
        float size = 0f;
        int lineSpacingOverride = 0;
        float spacing = 0f;
        char defaultChar = '?';
        string ranges = DEFAULT_RANGES;
        int maxPage = 2048;
        List<string> fonts = [];

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":          outDir = args[++i]; break;
                case "--size":         size = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--line-spacing": lineSpacingOverride = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--spacing":      spacing = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--default-char": defaultChar = args[++i][0]; break;
                case "--ranges":       ranges = args[++i]; break;
                case "--max-page":     maxPage = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default:               fonts.Add(args[i]); break;
            }
        }

        if (outDir is null) throw new ArgumentException("--out is required");
        if (size <= 0f) throw new ArgumentException("--size is required and must be positive");
        if (fonts.Count == 0) throw new ArgumentException("at least one .ttf path is required");

        Directory.CreateDirectory(outDir);
        int[] codepoints = ParseRanges(ranges);

        foreach (string path in fonts)
        {
            Bake(path, outDir, size, lineSpacingOverride, spacing, defaultChar, codepoints, maxPage);
        }

        return 0;
    }

    private static void Bake(
        string fontPath,
        string outDir,
        float size,
        int lineSpacingOverride,
        float spacing,
        char defaultChar,
        int[] codepoints,
        int maxPage)
    {
        string name = Path.GetFileNameWithoutExtension(fontPath);

        FontCollection collection = new();
        FontFamily family = collection.Add(fontPath);
        Font font = family.CreateFont(size, FontStyle.Regular);

        // Dpi 72 makes em size and pixels 1:1. Pair kerning is measured out deliberately --
        // DynamicSpriteFont stores only per-glyph ABC metrics and has nowhere to put a pair table.
        TextOptions measure = new(font)
        {
            Dpi = 72f,
            KerningMode = KerningMode.None,
        };

        List<Glyph> glyphs = [];
        int skipped = 0;

        foreach (int codepoint in codepoints)
        {
            // DynamicSpriteFont keys its dictionary on char, so anything above the BMP is unreachable.
            if (codepoint > char.MaxValue) continue;

            char c = (char)codepoint;

            if (!HasGlyph(font, c))
            {
                skipped++;

                continue;
            }

            glyphs.Add(Render(font, c, size, measure));
        }

        if (glyphs.Count == 0)
        {
            throw new InvalidOperationException($"{name}: font mapped none of the requested codepoints");
        }

        if (!glyphs.Any(g => g.Character == defaultChar))
        {
            throw new InvalidOperationException(
                $"{name}: default character '{defaultChar}' was not baked; unbaked text would draw as nothing");
        }

        int lineSpacing = NormaliseLineBox(glyphs, lineSpacingOverride);
        int pageSize = Pack(glyphs, maxPage);
        string pagePath = Path.Combine(outDir, $"{name}_0.png");

        using (Image<Rgba32> atlas = new(pageSize, pageSize, new Rgba32(255, 255, 255, 0)))
        {
            foreach (Glyph g in glyphs)
            {
                if (g.Image is null) continue;

                Glyph glyph = g;
                atlas.Mutate(ctx => ctx.DrawImage(glyph.Image!, new Point(glyph.X, glyph.Y), 1f));
            }

            // Straight (non-premultiplied) alpha: ReLogic's PngReader calls PreMultiplyAlpha when
            // the mod loads this, so premultiplying here would darken every edge twice.
            atlas.SaveAsPng(pagePath);
        }

        foreach (Glyph g in glyphs) g.Image?.Dispose();

        WriteMetrics(Path.Combine(outDir, $"{name}.dsf"), name, size, lineSpacing, spacing, defaultChar, glyphs);

        Console.WriteLine(
            $"{name}: {glyphs.Count} glyphs ({skipped} unmapped), {pageSize}x{pageSize} atlas, line spacing {lineSpacing}px");
    }

    private static bool HasGlyph(Font font, char c)
    {
        if (!font.FontMetrics.TryGetGlyphMetrics(
                new CodePoint(c),
                TextAttributes.None,
                TextDecorations.None,
                LayoutMode.HorizontalTopBottom,
                ColorFontSupport.None,
                out IReadOnlyList<GlyphMetrics>? metrics))
        {
            return false;
        }

        // An unmapped codepoint resolves to .notdef (glyph 0), which draws as a hollow box.
        return metrics is { Count: > 0 } && metrics[0].GlyphId != 0;
    }

    /// <summary>
    ///     Draws one glyph into a generously oversized cell, then measures its ink from the
    ///     rendered pixels rather than trusting a layout-space bounds call.
    /// </summary>
    private static Glyph Render(Font font, char c, float size, TextOptions measure)
    {
        float advance = TextMeasurer.MeasureAdvance(c.ToString(), measure).Width;

        // Ink can overhang the advance on either side (accents, italics, swashes) and can sit above
        // or below the nominal line box, so pad wide on every edge and crop back to what was drawn.
        int pad = Math.Max(8, (int)MathF.Ceiling(size));
        int cellW = (int)MathF.Ceiling(advance) + pad * 2;
        int cellH = (int)MathF.Ceiling(size) + pad * 2;

        using Image<Rgba32> cell = new(cellW, cellH, new Rgba32(255, 255, 255, 0));

        RichTextOptions draw = new(font)
        {
            Dpi = 72f,
            KerningMode = KerningMode.None,
            Origin = new Vector2(pad, pad),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextAlignment = TextAlignment.Start,
        };

        // White ink: Terraria tints text at draw time, so the atlas carries coverage only.
        cell.Mutate(ctx => ctx.DrawText(draw, c.ToString(), Color.White));

        if (!TryFindInk(cell, out Rectangle ink))
        {
            // Whitespace. No atlas footprint, but the advance still has to survive.
            return new Glyph(c, 0, 0, 0, 0, 0, advance, null);
        }

        // Origin sat at (pad, pad), so subtracting it converts ink position into the pen-relative
        // offsets DynamicSpriteFont wants: X against the pen, Y against the top of the line box.
        return new Glyph(
            c,
            ink.Width,
            ink.Height,
            ink.X - pad,
            ink.Y - pad,
            0,
            advance,
            cell.Clone(ctx => ctx.Crop(ink)));
    }

    private static bool TryFindInk(Image<Rgba32> image, out Rectangle ink)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);

                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].A == 0) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        });

        if (maxX < 0)
        {
            ink = default;

            return false;
        }

        ink = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

        return true;
    }

    /// <summary>
    ///     Rebases every glyph against the highest ink in the bake and returns a line height that
    ///     clears the lowest.
    /// </summary>
    /// <remarks>
    ///     The font's own line metrics are not usable here. Andika's stacked diacritics reach eight
    ///     pixels above the nominal line top at size 22 and its descenders fall five below it, so a
    ///     box taken from the font tables clips the first and lets the second collide with the line
    ///     beneath. Measuring the baked ink instead makes the box exactly as tall as the glyphs that
    ///     were actually baked, so narrowing --ranges tightens the leading by itself.
    /// </remarks>
    private static int NormaliseLineBox(List<Glyph> glyphs, int overrideValue)
    {
        List<Glyph> inked = [.. glyphs.Where(g => g.Height > 0)];

        int top = inked.Min(g => g.YOffset);
        int bottom = inked.Max(g => g.YOffset + g.Height);
        char highest = inked.First(g => g.YOffset == top).Character;
        char deepest = inked.First(g => g.YOffset + g.Height == bottom).Character;

        // DynamicSpriteFont measures Padding.Y down from the top of the line box, so nothing may
        // start above zero. Shifting every glyph by the same amount preserves the baseline.
        foreach (Glyph g in inked) g.YOffset -= top;

        int measured = bottom - top;

        Console.WriteLine($"  ink box {measured}px: highest '{highest}' (U+{(int)highest:X4}), deepest '{deepest}' (U+{(int)deepest:X4})");

        if (overrideValue <= 0) return measured;

        if (overrideValue < measured)
        {
            Console.WriteLine($"  warning: --line-spacing {overrideValue} is shorter than the {measured}px ink box; lines will overlap");
        }

        return overrideValue;
    }

    /// <summary>
    ///     Shelf-packs the inked glyphs, doubling the page until they fit. Assigns each glyph its
    ///     atlas position in place and returns the chosen edge length.
    /// </summary>
    private static int Pack(List<Glyph> glyphs, int maxPage)
    {
        // Keeps bilinear sampling at non-integer scales from bleeding a neighbour into a glyph.
        const int GUTTER = 1;

        List<Glyph> inked = [.. glyphs.Where(g => g.Image is not null).OrderByDescending(g => g.Height)];

        for (int pageSize = 64; pageSize <= maxPage; pageSize *= 2)
        {
            int x = GUTTER, y = GUTTER, shelfHeight = 0;
            bool fits = true;

            foreach (Glyph g in inked)
            {
                if (x + g.Width + GUTTER > pageSize)
                {
                    x = GUTTER;
                    y += shelfHeight + GUTTER;
                    shelfHeight = 0;
                }

                if (y + g.Height + GUTTER > pageSize)
                {
                    fits = false;

                    break;
                }

                g.X = x;
                g.Y = y;
                x += g.Width + GUTTER;
                shelfHeight = Math.Max(shelfHeight, g.Height);
            }

            if (fits) return pageSize;
        }

        throw new InvalidOperationException(
            $"glyphs do not fit in a {maxPage}x{maxPage} page; lower --size or narrow --ranges");
    }

    private static void WriteMetrics(
        string path,
        string name,
        float size,
        int lineSpacing,
        float spacing,
        char defaultChar,
        List<Glyph> glyphs)
    {
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);

        writer.Write("TDSF"u8);       // magic
        writer.Write(1);              // format version
        writer.Write(name);
        writer.Write(size);
        writer.Write(lineSpacing);
        writer.Write(spacing);
        writer.Write((ushort)defaultChar);
        writer.Write(1);              // page count
        writer.Write(glyphs.Count);

        foreach (Glyph g in glyphs.OrderBy(g => g.Character))
        {
            writer.Write((ushort)g.Character);
            writer.Write((byte)g.Page);
            writer.Write((short)g.X);
            writer.Write((short)g.Y);
            writer.Write((short)g.Width);
            writer.Write((short)g.Height);
            writer.Write((short)g.XOffset);
            writer.Write((short)g.YOffset);
            writer.Write(g.Advance);
        }
    }

    private static int[] ParseRanges(string spec)
    {
        SortedSet<int> set = [];

        foreach (string part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] bounds = part.Split('-');
            int lo = int.Parse(bounds[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int hi = bounds.Length > 1
                ? int.Parse(bounds[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : lo;

            for (int i = lo; i <= hi; i++) set.Add(i);
        }

        return [.. set];
    }

    private static string HelpText =>
        "FontBaker -- bakes a TTF into a DynamicSpriteFont atlas.\n\n" +
        "  FontBaker --out <dir> --size <px> [options] <font.ttf> [font2.ttf ...]\n\n" +
        "  --out <dir>            Output directory (required).\n" +
        "  --size <px>            Em size in pixels (required).\n" +
        "  --line-spacing <px>    Override the font's natural line height.\n" +
        "  --spacing <px>         Extra advance between glyphs. Default 0.\n" +
        "  --default-char <c>     Glyph substituted for unbaked characters. Default '?'.\n" +
        "  --ranges <list>        Comma-separated hex codepoints/ranges, e.g. 20-7E,2010-2027.\n" +
        "  --max-page <px>        Atlas size ceiling, power of two. Default 2048.";

    private sealed class Glyph(
        char character,
        int width,
        int height,
        int xOffset,
        int yOffset,
        int page,
        float advance,
        Image<Rgba32>? image)
    {
        public char Character { get; } = character;

        public int Width { get; } = width;

        public int Height { get; } = height;

        public int XOffset { get; } = xOffset;

        public int YOffset { get; set; } = yOffset;

        public int Page { get; } = page;

        public float Advance { get; } = advance;

        public Image<Rgba32>? Image { get; } = image;

        public int X { get; set; }

        public int Y { get; set; }
    }
}
