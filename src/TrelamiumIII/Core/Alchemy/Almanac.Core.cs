using System.Collections.Generic;
using System.Linq;
using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Broad chemical tags for an almanac entry, as flags (e.g. <c>Toxic | Volatile</c>). Labels and colours live in <c>AlchemyAlmanacUI</c>.
/// </summary>
[Flags]
public enum ChemicalQuality
{
    None = 0,
    /// <summary>Breaks down other compounds.</summary>
    Acidic = 1 << 0,
    /// <summary>Binds compounds together or neutralizes acids.</summary>
    Alkaline = 1 << 1,
    /// <summary>Evaporates easily or is unstable.</summary>
    Volatile = 1 << 2,
    /// <summary>Reacts with other compounds to release energy (heat, light, etc.).</summary>
    Kinetic = 1 << 3,
    /// <summary>Enables or accelerates reactions.</summary>
    Catalytic = 1 << 4,
    /// <summary>Refuses to react; can stabilize mixtures or dilute reactions.</summary>
    Inert = 1 << 5, // these ones and above are the general types, below are distinct qualities

    Oxidizer = 1 << 6,

    Flammable = 1 << 7,
    /// <summary>Used for bane tonic and necrotic items.</summary>
    Inhibitor = 1 << 8,

    Cryogen = 1 << 9,

}

/// <summary>
/// Thematic classification for flavour and almanac sorting/filtering, not tied to mechanics. Mostly for the Alchemy mode.
/// </summary>
[Flags]
public enum PrincipleType
{
    None = 0,
    /// <summary>Rejuvenation, life, growth.</summary>
    Vital = 1 << 0,
    /// <summary>Lucid, revealing, insight.</summary>
    Specular = 1 << 1,
    /// <summary>Strength, power, force.</summary>
    Martial = 1 << 2,
    /// <summary>Speed, dexterity, haste.</summary>
    Brisk = 1 << 3,
    /// <summary>Luck, fortune, abundance.</summary>
    Fortune = 1 << 4,
    /// <summary>Balance, harmony, binding.</summary>
    Harmony = 1 << 5,
    /// <summary>Decay, withering, unbinding.</summary>
    Bane = 1 << 6
}
/// <summary>
/// Authored almanac metadata for one item (flavour text and ratings), keyed by <see cref="ItemType"/>. Lives on the product, since many recipes share one.
/// </summary>
public sealed class AlmanacEntry
{
    /// <summary>
    /// The registry key. <see cref="IAlmanacDefined"/> items leave it unset; <see cref="AlmanacDefinitions"/> stamps their type in.
    /// </summary>
    public int ItemType { get; internal set; }

    /// <summary>Star rating, 1–5. <c>0</c> means "unrated" and draws no stars.</summary>
    public int Potency { get; init; }

    /// <summary>Chemical tag(s). Drawn as one coloured badge per set flag.</summary>
    public ChemicalQuality Chemical { get; init; }

    public PrincipleType Principle { get; init; } = PrincipleType.None;
    /// <summary>Display rarity as an <c>ItemRarityID</c>, judged by this system; unset falls back to the item's vanilla rarity.</summary>
    public int Rarity { get; init; } = UseVanillaRarity;

    /// <summary>Localization sub-key under <c>Almanac.</c>; null derives it from the display name with spaces stripped.</summary>
    public string? DescriptionKey { get; init; }

    /// <summary>When true (default), shows as "?" in the Ingredients index until obtained. Set false for starter materials.</summary>
    public bool Hidden { get; init; } = true;

    /// <summary>Sentinel for <see cref="Rarity"/>: defer to the item's own vanilla rarity.</summary>
    public const int UseVanillaRarity = int.MinValue;

    /// <summary>The rarity to actually display: explicit value, else the item's vanilla rarity.</summary>
    public int ResolvedRarity =>
        Rarity != UseVanillaRarity ? Rarity
        : ContentSamples.ItemsByType.TryGetValue(ItemType, out var sample) ? sample.rare
        : ItemRarityID.White;

    /// <summary>The localization sub-key actually used (explicit, else name-derived).</summary>
    public string ResolvedDescriptionKey =>
        DescriptionKey ?? Lang.GetItemNameValue(ItemType).Replace(" ", string.Empty);

    /// <summary>Localized flavour text, or empty when no key is defined (never a raw key path).</summary>
    public string GetDescription()
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.FlavorText.{ResolvedDescriptionKey}");
        string value = Language.GetTextValue(key);
        return value == key ? string.Empty : value;   // GetTextValue echoes the key when missing
    }
}

/// <summary>Authored <see cref="AlmanacEntry"/> metadata by output type; un-authored outputs get defaults via <see cref="For"/>.</summary>
public static class AlmanacEntryRegistry
{
    private static readonly Dictionary<int, AlmanacEntry> _entries = new();

    public static void Register(AlmanacEntry entry)
    {
        if (entry is { ItemType: > 0 })
            _entries[entry.ItemType] = entry;
    }

    public static void Clear() => _entries.Clear();

    public static bool Has(int outputType) => _entries.ContainsKey(outputType);

    /// <summary>Every authored entry (e.g. for building chemical-type ingredient groups).</summary>
    public static IEnumerable<AlmanacEntry> All => _entries.Values;

    /// <summary>
    /// Potency for un-authored solvents and reagents, since 0 ("unrated") would read as missing data. Authored entries always win.
    /// </summary>
    public const int DefaultIngredientPotency = 1;

    /// <summary>
    /// The authored entry, or a default: rated <see cref="DefaultIngredientPotency"/> for solvents and reagents, otherwise unrated.
    /// </summary>
    public static AlmanacEntry For(int outputType)
    {
        if (_entries.TryGetValue(outputType, out var entry)) return entry;

        // Reads AlchemyIngredients, which caches off the recipe registry on first use: don't call
        // this during PostSetupContent before the recipes are registered or that cache sticks empty.
        bool isIngredient = AlchemyIngredients.IsSolventType(outputType)
                         || AlchemyIngredients.IsSubstanceType(outputType);

        return new AlmanacEntry
        {
            ItemType = outputType,
            Potency = isIngredient ? DefaultIngredientPotency : 0,
        };
    }
}

/// <summary>
/// Flavour text under <c>Almanac.FlavorText.&lt;key&gt;</c>, shared by recipe and ingredient pages; falls back to legacy roots.
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
/// Ingredient flavour text under <c>Almanac.FlavorText.&lt;key&gt;</c>, keyed by display name with spaces stripped.
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


/// <summary>Read-only views over <see cref="AlchemyRecipeRegistry"/> for browsing, filtering and searching in the Almanac.</summary>
public static class AlmanacIndex
{
    private static List<int> _allOutputs;
    private static Dictionary<int, AlchemyMode> _outputMode;

    /// <summary>Distinct output types, sorted by display name.</summary>
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

    /// <summary>Recipes whose solvent or any solute slot requires <paramref name="itemType"/> with the given infusion.</summary>
    public static IEnumerable<AlchemyRecipe> RecipesUsing(int itemType, ChemicalQuality infusion)
    {
        if (infusion == ChemicalQuality.None)
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

/// <summary>
/// Almanac metadata for curated vanilla items, plus collection of <see cref="IAlmanacDefined"/> entries off our own items.
/// Everything else falls back to <see cref="AlmanacEntryRegistry.For"/>. Rarity uses the system's own judgement.
/// </summary>
internal static class AlmanacDefinitions
{
    public static void Register()
    {
        RegisterSolvents();

        RegisterIngredients();

        // Last, so an item's own word about itself wins over anything listed above.
        RegisterItemDefined();
    }

    /// <summary>
    /// Collects entries (and stated station roles) off items that author their own. Runs at PostSetupContent so types are assigned;
    /// an authored <see cref="ChemicalRole"/> beats the one <see cref="ChemicalRoles.Of(int)"/> infers.
    /// </summary>
    private static void RegisterItemDefined()
    {
        foreach (ModItem item in ModContent.GetContent<ModItem>())
        {
            if (item is IAlmanacDefined defined)
            {
                AlmanacEntry entry = defined.AlmanacEntry;
                entry.ItemType = item.Type;

                AlmanacEntryRegistry.Register(entry);
            }

            if (item is IChemicalRoleDefined { ChemicalRole: not ChemicalRole.None } roled)
                ChemicalRoles.Register(item.Type, roled.ChemicalRole);
        }
    }

    private static void RegisterSolvents()
    {
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.BottledWater,
            Potency = 0,
            Hidden = false,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Bottle,
            Potency = 0,
            Hidden = false,
            Chemical = ChemicalQuality.None,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.BottledHoney,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Rarity = ItemRarityID.Yellow,
        });
    }

    private static void RegisterIngredients()
    {
        #region Plants / Herbs
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Daybloom,
            Potency = 1,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Blinkroot,
            Potency = 1,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Moonglow,
            Potency = 3,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Deathweed,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Rarity = ItemRarityID.LightPurple,
            Hidden = false,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Waterleaf,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Fireblossom,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile,
            Rarity = ItemRarityID.Orange,
            Hidden = false,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Shiverthorn,
            Potency = 3,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Sunflower,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Brisk,
        });
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.YellowMarigold,
            Potency = 2,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Vital
        });
        #endregion

        #region Mushrooms
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Mushroom,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.VileMushroom,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ViciousMushroom,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });
        #endregion

        #region Ores
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CopperOre,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TinOre,
            Potency = 2,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.IronOre,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LeadOre,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane | PrincipleType.Harmony, // context dependent behavior
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SilverOre,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TungstenOre,
            Potency = 4,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.GoldOre,
            Potency = 4,
            Chemical = ChemicalQuality.Kinetic,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Vital | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PlatinumOre,
            Potency = 5,
            Chemical = ChemicalQuality.Alkaline,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Fortune,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DemoniteOre,
            Potency = 4,
            Chemical = ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimtaneOre,
            Potency = 4,
            Chemical = ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        // Todo: hardmode & special ores
        #endregion

        #region Miscellaneous

        // Miscellaneous frfr

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.RottenChunk,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Inhibitor,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Vertebrae,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Martial,
        });

        // Fish

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SpecularFish,
            Potency = 3,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Damselfish,
            Potency = 4,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Bass,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinkfish,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FlarefinKoi,
            Potency = 5,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Obsidifish,
            Potency = 4,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ArmoredCavefish,
            Potency = 3,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.AtlanticCod,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DoubleCod,
            Potency = 2,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.NeonTetra,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Ebonkoi,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Hemopiranha,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimsonTigerfish,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FrostMinnow,
            Potency = 3,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Cryogen,
            Principle = PrincipleType.Brisk | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PrincessFish,
            Potency = 4,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Prismite,
            Potency = 5,
            Chemical = ChemicalQuality.Kinetic | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        // Materials

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Gel,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Flammable,
            Principle = PrincipleType.Harmony | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinger,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Amber,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SharkFin,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk | PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Lens,
            Potency = 1,
            Chemical = ChemicalQuality.None,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LifeCrystal,
            Potency = 3,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Harmony,
        });
        #endregion
    }
}

/// <summary>How a guide chapter renders in the Almanac's Notes section.</summary>
public enum NoteKind
{
    /// <summary>Localized body text, paginated across the spread.</summary>
    Prose,
    /// <summary>The "Seven Principles" glossary table (special renderer; body is a short intro).</summary>
    Principles,
    /// <summary>Chemical-type axes + solute/solvent interaction rules (special renderer; body is a short intro).</summary>
    Chemistry,
}

public sealed class AlmanacNote
{
    public string Id { get; init; }
    public NoteKind Kind { get; init; } = NoteKind.Prose;

    public string Title
    {
        get
        {
            string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.Notes.{Id}.Title");
            string value = Language.GetTextValue(key);
            return value == key ? Id : value;
        }
    }

    public string Body
    {
        get
        {
            string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.Notes.{Id}.Body");
            string value = Language.GetTextValue(key);
            return value == key ? string.Empty : value;
        }
    }
}

public static class AlmanacNotes
{
    private static readonly AlmanacNote[] _chapters =
    {
        new() { Id = "Foreword" },
        new() { Id = "HowToUse" },
        new() { Id = "Principles", Kind = NoteKind.Principles },
        new() { Id = "Chemistry", Kind = NoteKind.Chemistry },
        new() { Id = "OnPotency" },
    };

    public static int Count => _chapters.Length;

    public static AlmanacNote At(int index) =>
        index >= 0 && index < _chapters.Length ? _chapters[index] : null;
}
