using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>Character-level formatting that can change between letters; paragraph settings stay on the block.</summary>
public readonly record struct CharFormat(bool Bold, bool Italic, float Scale, int? Ink)
{
    /// <summary>Inherit everything from the block. Emits no tag.</summary>
    public static readonly CharFormat Inherit = new(false, false, 0f, null);

    public bool IsInherit => this == Inherit;

    /// <summary>Zero means "whatever the block is set at" rather than an actual size of zero.</summary>
    public bool HasScale => Scale > 0f;
}

/// <summary>
///     Converts between tagged localization strings and plain text with per-character formats. One combined <c>[fmt/options:text]</c>
///     tag per run, never nested, since Terraria's parser ends a body at the first unescaped <c>]</c>. Other tags stay literal.
/// </summary>
public static class FormatCodec
{
    /// <summary>Tag name for a formatted run. Letters only, ten at most, per the parser's pattern.</summary>
    public const string TAG = "fmt";

    /// <summary>Splits a tagged string into plain text and one format per character.</summary>
    public static (string Text, CharFormat[] Formats) Parse(string? tagged)
    {
        var text = new StringBuilder();
        var formats = new List<CharFormat>();

        if (string.IsNullOrEmpty(tagged)) return (string.Empty, []);

        for (int i = 0; i < tagged.Length; i++)
        {
            if (TryReadTag(tagged, i, out CharFormat format, out string body, out int consumed))
            {
                // The body is plain by construction: a run tag cannot contain another tag, so
                // whatever is inside is literal text carrying this one format.
                foreach (char c in Unescape(body))
                {
                    text.Append(c);
                    formats.Add(format);
                }

                i += consumed - 1;

                continue;
            }

            text.Append(tagged[i]);
            formats.Add(CharFormat.Inherit);
        }

        return (text.ToString(), [.. formats]);
    }

    /// <summary>Rebuilds the tagged string, grouping runs of equal formatting.</summary>
    public static string Serialize(string text, IReadOnlyList<CharFormat> formats)
    {
        var output = new StringBuilder();
        int i = 0;

        while (i < text.Length)
        {
            CharFormat format = At(formats, i);
            int start = i;

            while (i < text.Length && At(formats, i) == format) i++;

            string body = text[start..i];

            if (format.IsInherit)
            {
                // Unformatted text is emitted as-is, so a book that never used the toolbar comes out
                // byte-identical to how it went in.
                output.Append(body);

                continue;
            }

            output.Append('[').Append(TAG).Append('/').Append(Options(format)).Append(':')
                .Append(Escape(body)).Append(']');
        }

        return output.ToString();
    }

    private static CharFormat At(IReadOnlyList<CharFormat> formats, int index) =>
        index < formats.Count ? formats[index] : CharFormat.Inherit;

    /// <summary>The options field: comma-separated flags, no colons, order-independent when read back.</summary>
    private static string Options(CharFormat format)
    {
        var parts = new List<string>(4);

        if (format.Bold) parts.Add("b");
        if (format.Italic) parts.Add("i");
        if (format.HasScale) parts.Add("s" + format.Scale.ToString("0.###", CultureInfo.InvariantCulture));
        if (format.Ink is { } ink) parts.Add("c" + ink.ToString("X6", CultureInfo.InvariantCulture));

        return string.Join(',', parts);
    }

    /// <summary>Reads an options field back into a format. Public so the render layer shares it.</summary>
    public static CharFormat ReadOptions(string options)
    {
        bool bold = false;
        bool italic = false;
        float scale = 0f;
        int? ink = null;

        foreach (string part in options.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part[0])
            {
                case 'b': bold = true; break;
                case 'i': italic = true; break;
                case 's':
                    if (float.TryParse(part[1..], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        scale = parsed;

                    break;
                case 'c':
                    if (int.TryParse(part[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                        ink = hex;

                    break;
            }
        }

        return new CharFormat(bold, italic, scale, ink);
    }

    /// <summary>Reads a run tag at <paramref name="index" />, matched by hand to agree exactly with the game's escape rules.</summary>
    private static bool TryReadTag(string source, int index, out CharFormat format, out string body, out int consumed)
    {
        format = CharFormat.Inherit;
        body = string.Empty;
        consumed = 0;

        // An escaped bracket is a literal, exactly as the game's lookbehind treats it.
        if (source[index] != '[' || (index > 0 && source[index - 1] == '\\')) return false;

        int cursor = index + 1;
        string prefix = $"{TAG}/";

        if (cursor + prefix.Length > source.Length) return false;
        if (string.CompareOrdinal(source, cursor, prefix, 0, prefix.Length) != 0) return false;

        cursor += prefix.Length;

        int colon = source.IndexOf(':', cursor);

        if (colon < 0) return false;

        string options = source[cursor..colon];
        int close = colon + 1;

        while (close < source.Length && !(source[close] == ']' && source[close - 1] != '\\')) close++;

        if (close >= source.Length) return false;

        format = ReadOptions(options);
        body = source[(colon + 1)..close];
        consumed = close - index + 1;

        return true;
    }

    /// <summary>Brackets inside a run body are escaped so they cannot end the tag early.</summary>
    private static string Escape(string value) => value.Replace("[", "\\[").Replace("]", "\\]");

    private static string Unescape(string value) => value.Replace("\\[", "[").Replace("\\]", "]");
}
