using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Slot whitelists for the Alchemy Station. Derived entirely from
/// <see cref="AlchemyRecipeRegistry"/>: any vial type referenced by a registered recipe
/// becomes a valid vial in its recipe's mode, and any item type referenced by a
/// component matcher (or member of a referenced <see cref="AlchemyComponentGroup"/>)
/// becomes a valid component.
/// </summary>
public static class AlchemyIngredients
{
    private static HashSet<int> _substances;
    private static Dictionary<int, AlchemyMode> _solventModes;
    private static List<int> _allIngredients;

    /// <summary>Discard derived caches. Called on mod unload via <see cref="ChemistryRecipeSystem"/>.</summary>
    public static void Clear()
    {
        _substances = null;
        _solventModes = null;
        _allIngredients = null;
    }

    /// <summary>
    /// Resolves which mode a vial activates. Returns <see cref="AlchemyMode.None"/>
    /// for empty items or items with no recipes consuming them as a vial.
    /// </summary>
    public static AlchemyMode GetModeForSolvent(Item vial)
    {
        if (vial == null || vial.IsAir) return AlchemyMode.None;

        EnsureBuilt();
        return _solventModes.TryGetValue(vial.type, out var mode) ? mode : AlchemyMode.None;
    }

    public static bool IsValidSolvent(Item item)
    {
        if (item == null || item.IsAir) return false;

        EnsureBuilt();
        return _solventModes.ContainsKey(item.type);
    }

    public static bool IsValidSubstance(Item item)
    {
        if (item == null || item.IsAir) return false;

        EnsureBuilt();
        return _substances.Contains(item.type);
    }

    public static bool IsSolventType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _solventModes.ContainsKey(itemType);
    }

    public static bool IsSubstanceType(int itemType)
    {
        if (itemType <= 0) return false;
        EnsureBuilt();
        return _substances.Contains(itemType);
    }

    /// <summary>
    /// All distinct item types referenced as a vial or component by any registered recipe,
    /// sorted by display name. Backing list for the Almanac's Ingredients index, undiscovered
    /// <see cref="AlmanacEntry.Hidden"/> entries stay in the list and render there as "?"
    /// (see <see cref="AlmanacDiscoverySystem.IsIngredientVisible"/>).
    /// </summary>
    public static IReadOnlyList<int> AllIngredientTypes
    {
        get { EnsureBuilt(); return _allIngredients; }
    }

    private static void EnsureBuilt()
    {
        if (_substances != null) return;

        var components = new HashSet<int>();
        var vialModes = new Dictionary<int, AlchemyMode>();

        foreach (var recipe in AlchemyRecipeRegistry.All)
        {
            // First mode wins.
            // Conflicting registrations would be a recipe-data bug, not something to silently merge.
            vialModes.TryAdd(recipe.SolventType, recipe.Mode);

            foreach (var matcher in recipe.Components)
            {
                if (matcher.Group != null)
                {
                    foreach (int t in matcher.Group.ItemTypes)
                        components.Add(t);
                }
                else if (matcher.ExactType > 0)
                {
                    components.Add(matcher.ExactType);
                }
            }
        }

        _substances = components;
        _solventModes = vialModes;

        var all = new HashSet<int>(components);
        foreach (int vial in vialModes.Keys) all.Add(vial);
        _allIngredients = all
            .Where(t => t > 0)
            .OrderBy(t => Lang.GetItemNameValue(t))
            .ToList();
    }
}
