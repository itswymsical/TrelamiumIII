using System.Collections.Generic;

namespace TrelamiumIII.Utilities;

/// <summary>
/// Provides utility methods for items.
/// </summary>
public static class ItemUtils
{
    /// <summary>
    /// Names that mark an item as metal for magnetism purposes. Matched against the display name
    /// rather than a type list so modded ores and bars are picked up without registration.
    /// </summary>
    private static readonly HashSet<string> MetalKeywords =
    [
        "Ore", "Bar", "Iron", "Lead", "Silver", "Gold", "Platinum", "Tungsten", "Tin", "Copper",
        "Cobalt", "Palladium", "Mythril", "Orichalcum", "Adamantite", "Titanium"
    ];

    public static bool IsAMetalItem(Item item)
    {
        foreach (string keyword in MetalKeywords)
        {
            if (item.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsGem(this Item item)
    {
        return item.type == ItemID.Amethyst
            || item.type == ItemID.Topaz
            || item.type == ItemID.Sapphire
            || item.type == ItemID.Emerald
            || item.type == ItemID.Ruby
            || item.type == ItemID.Diamond
            || item.type == ItemID.Amber;
    }
}
