using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Unified flavour-text lookup. An item that is both a recipe output and a reagent (Magnolia
/// Solution, for instance) needs only ONE string: author it under <c>Almanac.FlavorText.&lt;key&gt;</c>
/// and both its recipe page and its ingredient page show it. Falls back to the legacy
/// <c>RecipeDesc</c> / <c>IngredientDesc</c> roots so existing entries keep working.
/// </summary>
public static class AlmanacFlavor
{
    public static string Get(string key) =>
        Lookup($"Almanac.FlavorText.{key}")
        ?? Lookup($"Almanac.RecipeDesc.{key}")
        ?? Lookup($"Almanac.IngredientDesc.{key}")
        ?? string.Empty;

    private static string Lookup(string suffix)
    {
        string fullKey = TrelamiumIII.Instance.GetLocalizationKey(suffix);
        string value = Language.GetTextValue(fullKey);
        return value == fullKey ? null : value;   // null when missing so the ?? chain continues
    }
}

/// <summary>
/// Flavour-text lookup for ingredient items shown in the Almanac's Ingredients index.
/// Descriptions are localized under <c>Almanac.FlavorText.&lt;key&gt;</c>, where the key
/// is the item's display name with spaces stripped (matching the recipe-description scheme).
/// </summary>
public static class AlmanacIngredientInfo
{
    public static string DescriptionKey(int itemType) =>
        Lang.GetItemNameValue(itemType).Replace(" ", string.Empty);

    /// <summary>Localized ingredient flavour text, or empty string when none is authored.</summary>
    public static string GetDescription(int itemType)
    {
        if (itemType <= 0) return string.Empty;

        string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.FlavorText.{DescriptionKey(itemType)}");
        string value = Language.GetTextValue(key);
        return value == key ? string.Empty : value;   // GetTextValue echoes the key when missing
    }
}
