using System.Collections.Generic;
using ReLogic.Graphics;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     Text measurement for projected faces, in texels: one texel per font pixel at scale 1.
/// </summary>
/// <remarks>
///     Whole strings, measured and drawn as whole strings. The dialogue box lays its lines out a
///     glyph at a time because it reveals and animates them individually; a board draws its text
///     all at once, and measuring it the same way it is drawn is what keeps the wrap and the ink
///     agreeing about where a line ends.
/// </remarks>
internal static class ProjectedText
{
    /// <summary>
    ///     Vertical step between lines. Tighter than the font's own spacing, which carries room for
    ///     descenders and accents that body text on a panel does not need.
    /// </summary>
    public static int LineHeight(DynamicSpriteFont font, float scale = 1f)
        => (int)MathF.Ceiling(font.MeasureString("Ay").Y * 0.82f * scale);

    public static float Width(DynamicSpriteFont font, string text, float scale = 1f)
        => font.MeasureString(text).X * scale;

    /// <summary>
    ///     Greedy word wrap that honours the line breaks already in the text and hard-breaks a word
    ///     too long for a line on its own.
    /// </summary>
    public static List<string> Wrap(DynamicSpriteFont font, string text, float maxWidth, float scale = 1f)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(text))
            return lines;

        foreach (string paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            string line = string.Empty;

            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;

                if (Width(font, candidate, scale) <= maxWidth)
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                    lines.Add(line);

                line = Width(font, word, scale) <= maxWidth ? word : HardBreak(font, word, maxWidth, scale, lines);
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Splits an over-long word across lines, adding all but the last to the list.</summary>
    private static string HardBreak(DynamicSpriteFont font, string word, float maxWidth, float scale, List<string> lines)
    {
        string piece = string.Empty;

        foreach (char c in word)
        {
            if (Width(font, piece + c, scale) > maxWidth && piece.Length > 0)
            {
                lines.Add(piece);
                piece = string.Empty;
            }

            piece += c;
        }

        return piece;
    }

    /// <summary>
    ///     Cuts a block to at most <paramref name="maxLines" /> lines and marks the cut with an
    ///     ellipsis on the last one, clipped so it still fits.
    /// </summary>
    public static List<string> Clamp(DynamicSpriteFont font, List<string> lines, int maxLines, float maxWidth,
        float scale = 1f)
    {
        if (maxLines <= 0 || lines.Count <= maxLines)
            return lines;

        var cut = lines.GetRange(0, maxLines);
        cut[^1] = Ellipsise(font, cut[^1], maxWidth, scale);

        return cut;
    }

    /// <summary>Clips a line to a width with an ellipsis, so a long name cannot run into its neighbour.</summary>
    public static string Truncate(DynamicSpriteFont font, string text, float maxWidth, float scale = 1f)
        => Width(font, text, scale) <= maxWidth ? text : Ellipsise(font, text, maxWidth, scale);

    /// <summary>Appends an ellipsis, dropping characters from the end until the result fits.</summary>
    private static string Ellipsise(DynamicSpriteFont font, string text, float maxWidth, float scale)
    {
        string body = text.TrimEnd();

        for (int length = body.Length; length > 0; length--)
        {
            string candidate = body[..length].TrimEnd() + "...";

            if (Width(font, candidate, scale) <= maxWidth)
                return candidate;
        }

        return "...";
    }
}
