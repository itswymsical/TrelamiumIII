using System.Collections.Generic;
using ReLogic.Graphics;

namespace TrelamiumIII.Core.Rarities.Text;

/// <summary>One inked glyph of a <see cref="GlyphLayout" />, in unscaled font pixels.</summary>
public readonly struct TextGlyph(char character, int index, Texture2D texture, Rectangle source, Vector2 position)
{
    public readonly char Character = character;

    /// <summary>Index of the character in the laid-out string.</summary>
    public readonly int Index = index;

    /// <summary>The font page holding the glyph.</summary>
    public readonly Texture2D Texture = texture;

    /// <summary>The glyph's rectangle in <see cref="Texture" />.</summary>
    public readonly Rectangle Source = source;

    /// <summary>Top-left of the glyph quad, from the text's draw position.</summary>
    public readonly Vector2 Position = position;

    public Vector2 Size => new(Source.Width, Source.Height);

    public Vector2 Center => Position + Size * 0.5f;
}

/// <summary>
///     Where each glyph of a string lands, computed the way <see cref="DynamicSpriteFont" /> draws it (pen, kerning, padding),
///     plus the ink bounds and baseline that the line box from <c>MeasureString</c> does not give. Plain text only.
/// </summary>
public sealed class GlyphLayout
{
    private const int CACHE_LIMIT = 128;

    private static readonly Dictionary<(DynamicSpriteFont Font, string Text), GlyphLayout> cache = [];
    private static readonly Dictionary<DynamicSpriteFont, float> baselines = [];

    private GlyphLayout(DynamicSpriteFont font, string text)
    {
        Font = font;
        Text = text;
        Size = font.MeasureString(text);
        Baseline = BaselineOf(font);

        var glyphs = new List<TextGlyph>(text.Length);
        var inkMin = new Vector2(float.MaxValue);
        var inkMax = new Vector2(float.MinValue);

        Vector2 pen = Vector2.Zero;
        bool lineStart = true;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (character == '\n')
            {
                pen = new Vector2(0f, pen.Y + font.LineSpacing);
                lineStart = true;
                continue;
            }

            if (character == '\r')
                continue;

            DynamicSpriteFont.SpriteCharacterData data = font.SpriteCharacters.TryGetValue(character, out var found)
                ? found
                : font.DefaultCharacterData;

            Vector3 kerning = data.Kerning;

            // Mirrors DynamicSpriteFont.InternalDraw: character spacing between glyphs, no negative bearing at a line start.
            if (lineStart)
                kerning.X = MathF.Max(kerning.X, 0f);
            else
                pen.X += font.CharacterSpacing;

            pen.X += kerning.X;

            if (!char.IsWhiteSpace(character) && data.Texture is not null && data.Glyph.Width > 0 && data.Glyph.Height > 0)
            {
                var glyph = new TextGlyph(character, i, data.Texture, data.Glyph, pen + new Vector2(data.Padding.X, data.Padding.Y));

                glyphs.Add(glyph);
                inkMin = Vector2.Min(inkMin, glyph.Position);
                inkMax = Vector2.Max(inkMax, glyph.Position + glyph.Size);
            }

            pen.X += kerning.Y + kerning.Z;
            lineStart = false;
        }

        if (glyphs.Count == 0)
        {
            inkMin = new Vector2(0f, Baseline);
            inkMax = new Vector2(Size.X, Baseline);
        }

        Glyphs = [.. glyphs];
        InkMin = inkMin;
        InkMax = inkMax;
    }

    public DynamicSpriteFont Font { get; }

    public string Text { get; }

    /// <summary>Glyphs with ink, in string order. Whitespace and line breaks produce none.</summary>
    public TextGlyph[] Glyphs { get; }

    /// <summary>The line box, as <see cref="DynamicSpriteFont.MeasureString" /> reports it.</summary>
    public Vector2 Size { get; }

    /// <summary>Top-left of the tightest box around all glyph ink.</summary>
    public Vector2 InkMin { get; }

    /// <summary>Bottom-right of the tightest box around all glyph ink.</summary>
    public Vector2 InkMax { get; }

    /// <summary>Distance from the line-box top to the first line's baseline.</summary>
    public float Baseline { get; }

    /// <summary>The cached layout of <paramref name="text" /> in <paramref name="font" />.</summary>
    public static GlyphLayout For(DynamicSpriteFont font, string text)
    {
        if (cache.TryGetValue((font, text), out GlyphLayout? layout))
            return layout;

        if (cache.Count >= CACHE_LIMIT)
            cache.Clear();

        layout = new GlyphLayout(font, text);
        cache[(font, text)] = layout;

        return layout;
    }

    internal static void ClearCache()
    {
        cache.Clear();
        baselines.Clear();
    }

    /// <summary>Line-box top to baseline, from 'H' because fonts carry no ascent metric.</summary>
    private static float BaselineOf(DynamicSpriteFont font)
    {
        if (baselines.TryGetValue(font, out float cached))
            return cached;

        float baseline = font.LineSpacing * 0.75f;

        if (font.SpriteCharacters.TryGetValue('H', out DynamicSpriteFont.SpriteCharacterData data) && data.Glyph.Height > 0)
            baseline = data.Padding.Y + data.Glyph.Height;

        baselines[font] = baseline;

        return baseline;
    }
}
