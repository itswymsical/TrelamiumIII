using System.Collections.Generic;
using TrelamiumIII.Core.Jobs.Events;

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
    public static bool IsCopperFurniture(GameEvent evt)
    {
        return evt.ItemType == ItemID.CopperPlating || evt.ItemType == ItemID.CopperPlatingWall ||
            evt.ItemType == ItemID.CopperBrick || evt.ItemType == ItemID.CopperBrickWall ||
            evt.ItemType == ItemID.CopperChandelier ||
            evt.ItemType == ItemID.AncientCopperBrick;
    }
    public static bool IsCopperTool(GameEvent evt)
    {
        return evt.ItemType == ItemID.CopperBow || evt.ItemType == ItemID.CopperBroadsword ||
            evt.ItemType == ItemID.CopperHammer || evt.ItemType == ItemID.CopperWatch ||
            evt.ItemType == ItemID.CopperAxe || evt.ItemType == ItemID.CopperPickaxe;
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
    #region Cross-mod

    /// <summary>
    /// Mods Substrate has hand-written alchemy support for. Their potions are treated like vanilla's:
    /// brew-branded on creation, and their crafting recipes handed to the alchemy station wherever we
    /// provide a replacement. Every mod not listed here is left completely alone.
    /// <para>Only add a mod once <c>SubstrateRecipeSystem</c> registers recipes for it.</para>
    /// </summary>
    public static readonly HashSet<string> SupportedMods = new(StringComparer.Ordinal)
    {
        "SpiritReforged",
    };

    /// <summary>
    /// True for items Terraria itself owns. Vanilla ids fill [1, <see cref="ItemID.Count"/>); every
    /// modded item is allocated an id past that mark and carries a <see cref="ModItem"/>. Equivalent
    /// to <c>item.ModItem is null</c>, minus air (type 0).
    /// </summary>
    public static bool IsVanilla(this Item item) => item.type > ItemID.None && item.type < ItemID.Count;

    /// <summary>Internal name of the mod that added this item; null for vanilla items.</summary>
    public static string? SourceMod(this Item item) => item.ModItem?.Mod.Name;

    /// <summary>True when the mod with this internal name added the item.</summary>
    public static bool IsFrom(this Item item, string modName) => item.SourceMod() == modName;

    /// <summary>True when <typeparamref name="TMod"/> is the mod that added this item.</summary>
    public static bool IsFrom<TMod>(this Item item) where TMod : Mod => item.ModItem?.Mod is TMod;

    /// <summary>True when this item is one of ours.</summary>
    public static bool IsT3(this Item item) => item.IsFrom<TrelamiumIII>();

    /// <summary>True when this item came from a mod in <see cref="SupportedMods"/>.</summary>
    public static bool IsFromSupportedMod(this Item item)
        => item.SourceMod() is { } source && SupportedMods.Contains(source);

    /// <summary>
    /// Vanilla, ours, or a mod we explicitly support. The scope Substrate's alchemy claims. Gate
    /// name-matching heuristics on this so an unsupported mod is never swept up by our string patterns.
    /// </summary>
    public static bool IsManagedSource(this Item item)
        => item.IsVanilla() || item.IsT3() || item.IsFromSupportedMod();

    /// <summary>Internal name of the mod that added this recipe; null for Terraria's own.</summary>
    public static string? SourceMod(this Recipe recipe) => recipe.Mod?.Name;

    /// <summary>
    /// A recipe Terraria itself registered. Mod-added recipes carry their owning <see cref="Mod" />,
    /// so this separates a potion's *default* recipe from some other mod's alternate route to it.
    /// </summary>
    public static bool IsVanillaRecipe(this Recipe recipe) => recipe.Mod is null;

    /// <summary>True when this recipe was added by a mod in <see cref="SupportedMods"/>.</summary>
    public static bool IsFromSupportedMod(this Recipe recipe)
        => recipe.SourceMod() is { } source && SupportedMods.Contains(source);

    #endregion

    #region Potions

    /// <summary>
    /// A potion by shape rather than by name: consumable, drunk, and it either grants a buff or
    /// restores something. Unlike the name check this is language-independent and needs no per-mod
    /// knowledge, which is what makes it usable on <see cref="SupportedMods"/> content.
    /// </summary>
    public static bool IsDrinkablePotion(this Item item)
        => item.consumable
        && item.useStyle == ItemUseStyleID.DrinkLiquid
        && (item.buffType > 0 || item.healLife > 0 || item.healMana > 0);

    /// <summary>
    /// Vanilla potions. The name match is only safe because <see cref="IsVanilla"/> fences it to
    /// Terraria's own id range.
    /// Unfenced it also swallows any enabled mod's "... Potion" items.
    /// </summary>
    public static bool IsVanillaPotion(this Item item)
    {
        if (!item.IsVanilla())
            return false;

        return (item.Name.EndsWith("Potion") || item.type == ItemID.PotionOfReturn)
            && item.type != ItemID.PlaceableManaPotion && item.type != ItemID.PlaceableHealingPotion;
    }

    /// <summary>A potion belonging to a mod in <see cref="SupportedMods"/>.</summary>
    public static bool IsSupportedModPotion(this Item item)
        => item.IsFromSupportedMod() && item.IsDrinkablePotion();

    /// <summary>
    /// Every potion Substrate's alchemy claims: vanilla's, plus those of a supported mod. This is the
    /// set that gets brew-branded. The narrower question of whose *recipes* we replace is decided in
    /// <c>SubstrateRecipeSystem.PostAddRecipes</c>.
    /// </summary>
    public static bool IsManagedPotion(this Item item)
        => item.IsVanillaPotion() || item.IsSupportedModPotion();

    public static bool CraftsVanillaPotion(this Recipe recipe)
    {
        return recipe.createItem.IsVanillaPotion();
    }

    /// <inheritdoc cref="IsManagedPotion"/>
    public static bool CraftsManagedPotion(this Recipe recipe) => recipe.createItem.IsManagedPotion();

    #endregion

    #region Recursive search
    /// <summary>
    /// Checks if an item is crafted with a specific ingredient, directly or indirectly.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="ingredient"></param>
    /// <returns></returns>
    public static bool CraftedWith(this Item item, int ingredient)
    {
        for (var i = 0; i < Recipe.maxRecipes; i++)
        {
            var currentRecipe = Main.recipe[i];

            if (currentRecipe == null || currentRecipe.createItem.type != item.type)
                continue;

            for (var j = 0; j < currentRecipe.requiredItem.Count; j++)
            {
                if (currentRecipe.requiredItem[j] != null && currentRecipe.requiredItem[j].type == ingredient)
                    return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Checks if an item is crafted with a specific ingredient, directly or indirectly.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="ingredient"></param>
    /// <returns></returns>
    public static bool CraftedWith(this Item item, Item ingredient)
    {
        for (var i = 0; i < Recipe.maxRecipes; i++)
        {
            var currentRecipe = Main.recipe[i];

            if (currentRecipe == null || currentRecipe.createItem.type != item.type)
                continue;

            for (var j = 0; j < currentRecipe.requiredItem.Count; j++)
            {
                if (currentRecipe.requiredItem[j] != null && currentRecipe.requiredItem[j].type == ingredient.type)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recursively checks if an item is crafted from a base ingredient, directly or indirectly.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="baseIngredient"></param>
    /// <param name="visitedItems"></param>
    /// <returns></returns>
    public static bool CraftedFromRecursive(this Item item, Item baseIngredient, HashSet<int> visitedItems = null)
    {
        // Initialize visited items set to prevent infinite recursion
        if (visitedItems == null)
            visitedItems = new HashSet<int>();

        // If we've already checked this item, skip to prevent cycles
        if (visitedItems.Contains(item.type))
            return false;

        // Add current item to visited set
        visitedItems.Add(item.type);

        // Check all recipes that create this item
        for (var i = 0; i < Recipe.maxRecipes; i++)
        {
            var currentRecipe = Main.recipe[i];

            // Skip null or invalid recipes
            if (currentRecipe == null || currentRecipe.createItem.type != item.type)
                continue;

            // Check each ingredient in this recipe
            for (var j = 0; j < currentRecipe.requiredItem.Count; j++)
            {
                var requiredItem = currentRecipe.requiredItem[j];
                if (requiredItem == null)
                    continue;

                // Direct match - this recipe directly uses the base ingredient
                if (requiredItem.type == baseIngredient.type)
                    return true;

                // Recursive check - does this ingredient's crafting chain contain the base ingredient?
                if (requiredItem.CraftedFromRecursive(baseIngredient, visitedItems))
                    return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Recursively checks if an item is crafted from a base ingredient, directly or indirectly.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="baseIngredient"></param>
    /// <param name="visitedItems"></param>
    /// <returns></returns>
    public static bool CraftedFromRecursive(this Item item, int baseIngredient, HashSet<int> visitedItems = null)
    {
        // Initialize visited items set to prevent infinite recursion
        if (visitedItems == null)
            visitedItems = new HashSet<int>();

        // If we've already checked this item, skip to prevent cycles
        if (visitedItems.Contains(item.type))
            return false;

        // Add current item to visited set
        visitedItems.Add(item.type);

        // Check all recipes that create this item
        for (var i = 0; i < Recipe.maxRecipes; i++)
        {
            var currentRecipe = Main.recipe[i];

            // Skip null or invalid recipes
            if (currentRecipe == null || currentRecipe.createItem.type != item.type)
                continue;

            // Check each ingredient in this recipe
            for (var j = 0; j < currentRecipe.requiredItem.Count; j++)
            {
                var requiredItem = currentRecipe.requiredItem[j];
                if (requiredItem == null)
                    continue;

                // Direct match - this recipe directly uses the base ingredient
                if (requiredItem.type == baseIngredient)
                    return true;

                // Recursive check - does this ingredient's crafting chain contain the base ingredient?
                if (requiredItem.CraftedFromRecursive(baseIngredient, visitedItems))
                    return true;
            }
        }

        return false;
    }

    #endregion
}
