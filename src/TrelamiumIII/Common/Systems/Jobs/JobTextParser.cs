using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TrelamiumIII.Common.Systems.Jobs;


/// <summary>
/// Cool little class that parses Job description text with custom markup for effects like shaking, sine wave, and glowing.
/// </summary>
public static class JobTextParser
{
    public static (string plainText, Dictionary<int, JobTextEffect> effects) ParseDescription(string rawText)
    {
        var effects = new Dictionary<int, JobTextEffect>();
        var markupRemovals = new List<(int start, int length)>();

        // Updated regex to capture color values (RGB or hex)
        var pairedRegex = new Regex(@"<(\w+)(?::([0-9.,#A-Fa-f]+))?>(.*?)</\1>");
        foreach (Match match in pairedRegex.Matches(rawText))
        {
            var effectType = match.Groups[1].Value.ToLower();
            var hasValue = match.Groups[2].Success;
            var valueStr = match.Groups[2].Value;
            var content = match.Groups[3].Value;
            var cleanStartPos = CleanPosition(rawText, match.Index, markupRemovals);

            // Add effect for each character in the content
            for (int i = 0; i < content.Length; i++)
            {
                var effect = effectType switch
                {
                    "shake" => new JobTextEffect
                    {
                        Type = TextEffectType.Shake,
                        Intensity = hasValue ? float.Parse(valueStr) : 2f
                    },
                    "sine" => new JobTextEffect
                    {
                        Type = TextEffectType.Sine,
                        Intensity = hasValue ? float.Parse(valueStr) : 4f
                    },
                    "glow" => ParseGlowEffect(valueStr, hasValue),
                    _ => null
                };

                if (effect != null)
                    effects[cleanStartPos + i] = effect;
            }

            // Record the tags for removal
            var openingTagLength = match.Length - content.Length - $"</{effectType}>".Length;
            var closingTagLength = $"</{effectType}>".Length;
            markupRemovals.Add((match.Index, openingTagLength));
            markupRemovals.Add((match.Index + openingTagLength + content.Length, closingTagLength));
        }

        // Create clean text by removing all markup
        var cleanText = Regex.Replace(rawText, @"<[^>]*>", "");
        return (cleanText, effects);
    }

    private static JobTextEffect ParseGlowEffect(string valueStr, bool hasValue)
    {
        if (!hasValue)
        {
            return new JobTextEffect
            {
                Type = TextEffectType.Glow,
                Intensity = 1.5f
            };
        }

        // Try to parse as color
        var glowColor = TryParseColor(valueStr);

        if (glowColor.HasValue)
        {
            return new JobTextEffect
            {
                Type = TextEffectType.Glow,
                Intensity = 1.5f,
                GlowColor = glowColor.Value
            };
        }

        // Fallback: parse as intensity
        return new JobTextEffect
        {
            Type = TextEffectType.Glow,
            Intensity = float.Parse(valueStr)
        };
    }

    private static Color? TryParseColor(string colorStr)
    {
        try
        {
            // Hex format: #RRGGBB or #RGB
            if (colorStr.StartsWith("#"))
            {
                colorStr = colorStr.TrimStart('#');

                // Expand short form (#RGB to #RRGGBB)
                if (colorStr.Length == 3)
                {
                    colorStr = $"{colorStr[0]}{colorStr[0]}{colorStr[1]}{colorStr[1]}{colorStr[2]}{colorStr[2]}";
                }

                if (colorStr.Length == 6)
                {
                    var r = Convert.ToByte(colorStr.Substring(0, 2), 16);
                    var g = Convert.ToByte(colorStr.Substring(2, 2), 16);
                    var b = Convert.ToByte(colorStr.Substring(4, 2), 16);
                    return new Color(r, g, b);
                }
            }

            // RGB format: R,G,B
            if (colorStr.Contains(","))
            {
                var parts = colorStr.Split(',');
                if (parts.Length == 3)
                {
                    var r = byte.Parse(parts[0].Trim());
                    var g = byte.Parse(parts[1].Trim());
                    var b = byte.Parse(parts[2].Trim());
                    return new Color(r, g, b);
                }
            }
        }
        catch
        {
            // Failed to parse as color
        }

        return null;
    }

    private static int CleanPosition(string text, int markupIndex, List<(int start, int length)> previousRemovals)
    {
        int totalRemoved = 0;
        foreach (var (start, length) in previousRemovals)
        {
            if (start < markupIndex)
                totalRemoved += length;
        }
        return markupIndex - totalRemoved;
    }
}

public class JobTextEffect
{
    public TextEffectType Type { get; init; }
    public float Intensity { get; init; } = 1f;
    public Color? GlowColor { get; init; } = null; // Add this
}

public enum TextEffectType
{
    Shake,
    Sine,
    Glow
}