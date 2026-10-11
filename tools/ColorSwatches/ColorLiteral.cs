using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ColorSwatches;

internal enum LiteralKind
{
    /// <summary><c>Rgb(r, g, b)</c>, 0–255 per channel.</summary>
    RgbBytes,

    /// <summary><c>Rgb(0xRRGGBB)</c>.</summary>
    RgbHex,

    /// <summary><c>new Color(...)</c> or <c>Color.FromNonPremultiplied(...)</c> with 0–255 ints.</summary>
    ColorInts,

    /// <summary><c>Color</c>/<c>Vector3</c>/<c>Vector4</c> floats; vector channels may exceed 1 (HDR).</summary>
    Floats,
}

/// <summary>A colour literal found on one line of C#. Offsets are relative to the line start.</summary>
internal sealed class ColorLiteral
{
    private const string Number = @"(?:0[xX][0-9A-Fa-f]+[uU]?|(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?[fFdDmM]?)";

    private static readonly Regex Head = new(
        @"(?<at>\b(?:\w+\.)*(?<rgb>Rgb))\s*\(" +
        @"|(?<at>\bColor\.(?<fnp>FromNonPremultiplied))\s*\(" +
        @"|(?<at>\bnew)\s+(?:\w+\.)*(?<type>Color|Vector3|Vector4)\s*\(" +
        @"|\b(?<type>Color|Vector3|Vector4)\s+@?\w+\s*=\s*(?<at>new)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Argument = new(
        $@"^(?<num>{Number})(?:\s*/\s*(?<div>{Number}))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A <c>Vector3</c>/<c>Vector4</c> only counts as a colour on a line matching this.</summary>
    private static readonly Regex VectorHint = new(
        "colou?r|tint|light|glow|rgb|hue|shade|palette|bloom|emiss",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public int SwatchAt { get; private set; }

    /// <summary>Span of the text between the parentheses.</summary>
    public int ArgsStart { get; private set; }

    public int ArgsLength { get; private set; }

    public LiteralKind Kind { get; private set; }

    /// <summary>0–1 channels, except HDR vectors which keep their raw values.</summary>
    public float R { get; private set; }

    public float G { get; private set; }

    public float B { get; private set; }

    public float A { get; private set; } = 1f;

    /// <summary>False when an argument is an expression such as <c>200f / 255f</c>.</summary>
    public bool Editable { get; private set; }

    /// <summary>The fourth argument verbatim, kept as-is on <see cref="Rewrite" />.</summary>
    private string? alphaText;

    private int decimals;

    public float Intensity => Math.Max(1f, Math.Max(R, Math.Max(G, B)));

    /// <summary>Display colour as bytes; HDR values are divided by <see cref="Intensity" /> to keep the hue.</summary>
    public void GetDisplay(out byte r, out byte g, out byte b, out byte a)
    {
        float scale = 1f / Intensity;
        r = ToByte(R * scale);
        g = ToByte(G * scale);
        b = ToByte(B * scale);
        a = ToByte(A);
    }

    /// <summary>Argument text for a newly picked colour, in the literal's own notation.</summary>
    public string Rewrite(byte r, byte g, byte b)
    {
        string alpha = alphaText is null ? "" : ", " + alphaText;

        switch (Kind)
        {
            case LiteralKind.RgbBytes:
                return $"{r}, {g}, {b}";
            case LiteralKind.RgbHex:
                return $"0x{r:X2}{g:X2}{b:X2}";
            case LiteralKind.ColorInts:
                return $"{r}, {g}, {b}{alpha}";
            default:
                float intensity = Intensity;
                string format = "F" + Math.Max(2, Math.Min(4, decimals));
                string F(byte channel) => (channel / 255f * intensity).ToString(format, CultureInfo.InvariantCulture) + "f";
                return $"{F(r)}, {F(g)}, {F(b)}{alpha}";
        }
    }

    public static IEnumerable<ColorLiteral> Find(string line)
    {
        foreach (Match head in Head.Matches(line))
        {
            ColorLiteral? literal = TryParse(line, head);
            if (literal is not null)
                yield return literal;
        }
    }

    private static ColorLiteral? TryParse(string line, Match head)
    {
        int open = head.Index + head.Length;
        int close = line.IndexOf(')', open);
        if (close < 0 || line.IndexOf('(', open, close - open) >= 0)
            return null;

        string[] parts = line.Substring(open, close - open).Split(',');
        var values = new double[parts.Length];
        bool anyFloat = false;
        bool anyDivision = false;
        bool anyHex = false;
        int decimals = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            Match arg = Argument.Match(parts[i].Trim());
            if (!arg.Success)
                return null;

            values[i] = ParseNumber(arg.Groups["num"].Value, ref anyFloat, ref anyHex, ref decimals);
            if (arg.Groups["div"].Success)
            {
                double divisor = ParseNumber(arg.Groups["div"].Value, ref anyFloat, ref anyHex, ref decimals);
                if (divisor == 0)
                    return null;

                values[i] /= divisor;
                anyFloat = true;
                anyDivision = true;
            }
        }

        var literal = new ColorLiteral
        {
            SwatchAt = head.Groups["at"].Index,
            ArgsStart = open,
            ArgsLength = close - open,
            Editable = !anyDivision,
            decimals = decimals,
        };

        if (head.Groups["rgb"].Success)
        {
            if (parts.Length == 1 && anyHex && !anyDivision)
            {
                uint hex = (uint)values[0];
                literal.Kind = LiteralKind.RgbHex;
                literal.SetBytes((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF);
                return literal;
            }

            if (parts.Length != 3 || anyFloat || values[0] > 255 || values[1] > 255 || values[2] > 255)
                return null;

            literal.Kind = LiteralKind.RgbBytes;
            literal.SetBytes(values[0], values[1], values[2]);
            return literal;
        }

        if (head.Groups["fnp"].Success)
        {
            if (parts.Length != 4 || anyFloat)
                return null;

            literal.Kind = LiteralKind.ColorInts;
            literal.SetBytes(values[0], values[1], values[2], values[3], parts[3].Trim());
            return literal;
        }

        string type = head.Groups["type"].Value;
        if (type == "Color")
        {
            if (parts.Length is not (3 or 4))
                return null;

            string? alpha = parts.Length == 4 ? parts[3].Trim() : null;

            // Any float argument selects XNA's float overload, which clamps to 0–1.
            if (anyFloat)
            {
                literal.Kind = LiteralKind.Floats;
                literal.SetFloats(Clamp01(values[0]), Clamp01(values[1]), Clamp01(values[2]), alpha is null ? 1 : Clamp01(values[3]), alpha);
                return literal;
            }

            literal.Kind = LiteralKind.ColorInts;
            literal.SetBytes(values[0], values[1], values[2], alpha is null ? 255 : values[3], alpha);
            return literal;
        }

        int channels = type == "Vector3" ? 3 : 4;
        if (parts.Length != channels || !(anyDivision || VectorHint.IsMatch(line)))
            return null;

        literal.Kind = LiteralKind.Floats;
        literal.SetFloats(values[0], values[1], values[2], channels == 4 ? Clamp01(values[3]) : 1, channels == 4 ? parts[3].Trim() : null);
        return literal;
    }

    private static double ParseNumber(string text, ref bool isFloat, ref bool isHex, ref int decimals)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            isHex = true;
            return ulong.Parse(text.Substring(2).TrimEnd('u', 'U'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        char last = text[text.Length - 1];
        if (char.IsLetter(last))
        {
            isFloat |= last is 'f' or 'F' or 'd' or 'D' or 'm' or 'M';
            text = text.Substring(0, text.Length - 1);
        }

        int dot = text.IndexOf('.');
        if (dot >= 0 || text.IndexOfAny(['e', 'E']) >= 0)
            isFloat = true;

        if (dot >= 0)
        {
            int exponent = text.IndexOfAny(['e', 'E'], dot);
            decimals = Math.Max(decimals, (exponent < 0 ? text.Length : exponent) - dot - 1);
        }

        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private void SetBytes(double r, double g, double b, double a = 255, string? alpha = null)
    {
        R = (float)(Math.Min(r, 255) / 255);
        G = (float)(Math.Min(g, 255) / 255);
        B = (float)(Math.Min(b, 255) / 255);
        A = (float)(Math.Min(a, 255) / 255);
        alphaText = alpha;
    }

    private void SetFloats(double r, double g, double b, double a, string? alpha)
    {
        R = (float)r;
        G = (float)g;
        B = (float)b;
        A = (float)a;
        alphaText = alpha;
    }

    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

    private static byte ToByte(float value) => (byte)Math.Round(Math.Max(0f, Math.Min(1f, value)) * 255f);
}
