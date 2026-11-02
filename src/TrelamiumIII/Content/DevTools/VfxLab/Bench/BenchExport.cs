using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TrelamiumIII.Content.DevTools.VfxLab.Bench;

/// <summary> One retuned value: numbers for a uniform, or a texture's name. </summary>
internal readonly record struct ExportValue(string Name, float[]? Numbers, string? Texture);

/// <summary> Turns retuned values into C# to paste back where they came from. Pure text, so it runs without the game. </summary>
internal static class BenchExport
{
    private const string NoiseTexture = "uNoiseTex";

    /// <summary> <c>GasLook.Apply</c> read backwards. Its properties mostly don't share names with the uniforms they set. </summary>
    private static readonly Dictionary<string, string> GasLookNames = new(StringComparer.Ordinal)
    {
        ["Threshold"] = "Threshold",
        ["DepthRange"] = "DepthRange",
        ["NoiseScale"] = "NoiseScale",
        ["NoiseStrength"] = "NoiseStrength",
        ["NoiseDrift"] = "Drift",
        ["NoiseStretch"] = "NoiseStretch",
        ["FlowStrength"] = "FlowStrength",
        ["Bands"] = "Bands",
        ["BodyAlpha"] = "BodyAlpha",
        ["BodyAlphaFloor"] = "BodyAlphaFloor",
        ["ShadowColor"] = "Shadow",
        ["BodyColor"] = "Body",
        ["CoreColor"] = "Core",
        ["OutlineColor"] = "Outline",
        ["OutlineAlpha"] = "OutlineAlpha",
        ["HighlightColor"] = "Highlight",
        ["HighlightAlpha"] = "HighlightAlpha",
        ["TemperatureBlend"] = "TemperatureBlend",
        ["HotShadowColor"] = "HotShadow",
        ["HotBodyColor"] = "HotBody",
        ["HotCoreColor"] = "HotCore",
        ["WorldLight"] = "WorldLight",
    };

    /// <summary> <c>TileFilmLook.Apply</c> read backwards. Its properties are named after the uniforms, all but the drift. </summary>
    private static readonly Dictionary<string, string> FilmLookNames = new(StringComparer.Ordinal)
    {
        ["Threshold"] = "Threshold",
        ["NoiseScale"] = "NoiseScale",
        ["NoiseStrength"] = "NoiseStrength",
        ["NoiseDrift"] = "NoiseDrift",
        ["DepthRange"] = "DepthRange",
        ["Bands"] = "Bands",
        ["ShadowColor"] = "ShadowColor",
        ["BodyColor"] = "BodyColor",
        ["CoreColor"] = "CoreColor",
        ["BodyAlpha"] = "BodyAlpha",
        ["BodyAlphaFloor"] = "BodyAlphaFloor",
        ["OutlineColor"] = "OutlineColor",
        ["OutlineAlpha"] = "OutlineAlpha",
        ["HighlightColor"] = "HighlightColor",
        ["HighlightAlpha"] = "HighlightAlpha",
        ["EmissiveEdge"] = "EmissiveEdge",
        ["AmbientLight"] = "AmbientLight",
        ["SurfaceLip"] = "SurfaceLip",
        ["TintStrength"] = "TintStrength",
        ["TintWash"] = "TintWash",
    };

    /// <summary> <c>Look with { ... }</c> for a <c>GasLook</c>. </summary>
    public static string GasLook(IEnumerable<ExportValue> values) => With(values, GasLookNames, static texture => $"Noise = static () => Assets.Textures.Noise.{texture}.Asset,");

    /// <summary> <c>Look with { ... }</c> for a <c>TileFilmLook</c>. A film's noise lives on the condition, not its look. </summary>
    public static string FilmLook(IEnumerable<ExportValue> values)
    {
        List<ExportValue> all = values.ToList();
        string with = With(all.Where(static value => value.Name != NoiseTexture), FilmLookNames, null);

        return all.FirstOrDefault(static value => value.Name == NoiseTexture) is { Texture: { } texture }
            ? $"{with}\n\nprotected override Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.{texture}.Asset;"
            : with;
    }

    /// <summary> Plain parameter sets, for a shader with no look record to go through. </summary>
    public static string Parameters(IEnumerable<ExportValue> values)
        => string.Join('\n', values.Select(Parameter));

    private static string With(IEnumerable<ExportValue> values, Dictionary<string, string> names, Func<string, string>? noiseLine)
    {
        var body = new StringBuilder();
        List<ExportValue> leftover = [];

        foreach (ExportValue value in values)
        {
            if (value.Name == NoiseTexture && noiseLine is not null && value.Texture is { } texture)
                body.Append("    ").Append(noiseLine(texture)).Append('\n');
            else if (names.TryGetValue(value.Name, out string? property) && value.Numbers is { } numbers)
                body.Append("    ").Append(property).Append(" = ").Append(Literal(numbers)).Append(",\n");
            else
                leftover.Add(value);
        }

        string with = body.Length == 0 ? string.Empty : $"Look with\n{{\n{body}}}";

        if (leftover.Count == 0)
            return with;

        string rest = Parameters(leftover);

        return with.Length == 0 ? rest : $"{with}\n\n// Not part of the look:\n{rest}";
    }

    private static string Parameter(ExportValue value)
    {
        string argument = value.Numbers is { } numbers
            ? Literal(numbers)
            : $"Assets.Textures.Noise.{value.Texture}.Asset.Value";

        return $"effect.Parameters[\"{value.Name}\"]?.SetValue({argument});";
    }

    public static string Literal(float[] numbers)
    {
        string list = string.Join(", ", numbers.Select(Number));

        return numbers.Length switch
        {
            1 => list,
            2 => $"new Vector2({list})",
            3 => $"new Vector3({list})",
            _ => $"new Vector4({list})",
        };
    }

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";
}
