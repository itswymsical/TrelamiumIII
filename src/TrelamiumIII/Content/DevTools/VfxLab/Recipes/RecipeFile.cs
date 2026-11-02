using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace TrelamiumIII.Content.DevTools.VfxLab.Recipes;

internal sealed class RecipeFormatException(string message) : Exception(message);

/// <summary> Recipes as text, and a folder of them by name. </summary>
internal static class RecipeFile
{
    public const string Extension = ".vfx.json";

    /// <summary> A number per line makes a colour take five lines and every diff harder to read. </summary>
    private static readonly Regex NumberArray = new(@"\[\r?\n\s+(-?[\d.eE+-]+(?:,\r?\n\s+-?[\d.eE+-]+)*)\r?\n\s+\]", RegexOptions.Compiled);

    private static readonly Regex ArrayGap = new(@",\r?\n\s+", RegexOptions.Compiled);

    public static string Serialize(VfxRecipe recipe)
    {
        string json = JsonConvert.SerializeObject(recipe, Formatting.Indented).Replace("\r\n", "\n");

        return NumberArray.Replace(json, match => $"[{ArrayGap.Replace(match.Groups[1].Value, ", ")}]") + "\n";
    }

    /// <exception cref="RecipeFormatException"> The text isn't a recipe, or it's from a newer build. </exception>
    public static VfxRecipe Parse(string text)
    {
        VfxRecipe recipe;

        try
        {
            recipe = JsonConvert.DeserializeObject<VfxRecipe>(text) ?? throw new RecipeFormatException("The file is empty.");
        }
        catch (JsonException exception)
        {
            throw new RecipeFormatException(exception.Message);
        }

        if (recipe.Format > VfxRecipe.CurrentFormat)
            throw new RecipeFormatException($"This recipe is format {recipe.Format} and this build reads up to {VfxRecipe.CurrentFormat}.");

        if (!VfxRecipe.IsValidName(recipe.Name))
            throw new RecipeFormatException($"'{recipe.Name}' isn't a valid recipe name.");

        return recipe;
    }

    public static string PathOf(string folder, string name) => Path.Combine(folder, name + Extension);

    public static void Save(string folder, VfxRecipe recipe)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(PathOf(folder, recipe.Name), Serialize(recipe));
    }

    public static VfxRecipe Load(string folder, string name) => Parse(File.ReadAllText(PathOf(folder, name)));

    public static IEnumerable<string> Names(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "*" + Extension).Select(static path => Path.GetFileName(path)[..^Extension.Length]).Order(StringComparer.Ordinal)
        : [];

    /// <summary> The recipes <paramref name="recipe" /> names that aren't in <paramref name="folder" />. </summary>
    public static IEnumerable<string> MissingLayers(string folder, VfxRecipe recipe)
        => recipe.Layers.Where(name => !File.Exists(PathOf(folder, name)));
}
