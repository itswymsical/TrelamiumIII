using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Common.Systems.Alchemy;

/// <summary>
/// Read-only views over <see cref="AlchemyRecipeRegistry"/> tailored for the
/// Alchemist's Almanac UI. All recipes flow through here when the player browses,
/// filters, or searches.
/// </summary>
public static class AlmanacIndex
{
    private static List<int> _allOutputs;
    private static Dictionary<int, AlchemyMode> _outputMode;

    /// <summary>
    /// Distinct output types across the registry, sorted by display name. The fallback
    /// item is included implicitly only if a recipe explicitly produces it.
    /// </summary>
    public static IReadOnlyList<int> AllOutputTypes
    {
        get
        {
            EnsureBuilt();
            return _allOutputs;
        }
    }

    /// <summary>Outputs that match the given mode, sorted by display name.</summary>
    public static IEnumerable<int> OutputsByMode(AlchemyMode mode)
    {
        EnsureBuilt();
        return _allOutputs.Where(t => _outputMode.TryGetValue(t, out var m) && m == mode);
    }

    /// <summary>All recipes whose output equals <paramref name="outputType"/>.</summary>
    public static IEnumerable<AlchemyRecipe> RecipesProducing(int outputType)
    {
        if (outputType <= 0) yield break;

        foreach (var r in AlchemyRecipeRegistry.All)
        {
            if (ResolveType(r) == outputType)
                yield return r;
        }
    }

    /// <summary>
    /// Recipes whose solvent or any solute slot requires <paramref name="itemType"/>
    /// WITH the given instance infusion.
    /// </summary>
    public static IEnumerable<AlchemyRecipe> RecipesUsing(int itemType, ChemicalType infusion)
    {
        if (infusion == ChemicalType.None)
        {
            foreach (var r in RecipesUsing(itemType))
                yield return r;
            yield break;
        }

        foreach (var r in AlchemyRecipeRegistry.All)
        {
            if (r.SolventType == itemType && r.SolventInfusion == infusion)
            {
                yield return r;
                continue;
            }

            for (int i = 0; i < r.Components.Length; i++)
            {
                if (r.Components[i].Matches(itemType) && r.Components[i].Infusion == infusion)
                {
                    yield return r;
                    break;
                }
            }
        }
    }

    /// <summary>Recipes whose vial OR any component slot accepts <paramref name="itemType"/>.</summary>
    public static IEnumerable<AlchemyRecipe> RecipesUsing(int itemType)
    {
        if (itemType <= 0) yield break;

        foreach (var r in AlchemyRecipeRegistry.All)
        {
            if (r.SolventType == itemType)
            {
                yield return r;
                continue;
            }

            for (int i = 0; i < r.Components.Length; i++)
            {
                if (r.Components[i].Matches(itemType))
                {
                    yield return r;
                    break;
                }
            }
        }
    }

    /// <summary>True if at least one registered recipe produces this output.</summary>
    public static bool HasRecipeFor(int outputType)
    {
        if (outputType <= 0) return false;
        foreach (var r in AlchemyRecipeRegistry.All)
            if (ResolveType(r) == outputType) return true;
        return false;
    }

    /// <summary>Discard cached views. call after registry mutates (recipes added/removed).</summary>
    public static void Invalidate()
    {
        _allOutputs = null;
        _outputMode = null;
    }

    /// <summary>Public accessor for a recipe's resolved output item type.</summary>
    public static int ResolvedOutputType(AlchemyRecipe r) => ResolveType(r);

    private static int ResolveType(AlchemyRecipe r)
    {
        if (r.OutputItemType > 0) return r.OutputItemType;
        // Fallback for recipes that built CreateOutput inline without setting OutputItemType.
        var item = r.CreateOutput?.Invoke();
        return item is { IsAir: false } ? item.type : 0;
    }

    private static void EnsureBuilt()
    {
        if (_allOutputs != null) return;

        var byType = new Dictionary<int, AlchemyMode>();
        foreach (var r in AlchemyRecipeRegistry.All)
        {
            int t = ResolveType(r);
            if (t <= 0) continue;
            // First mode wins. An output is conceptually owned by its registering mode.
            byType.TryAdd(t, r.Mode);
        }

        _outputMode = byType;
        _allOutputs = byType.Keys
            .OrderBy(t => Lang.GetItemNameValue(t))
            .ToList();
    }
}
