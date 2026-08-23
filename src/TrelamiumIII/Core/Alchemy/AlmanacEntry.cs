using System.Collections.Generic;
using Terraria.Localization;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Lodestone;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Broad "what kind of chemical is this" tagging for an almanac entry. Flags so a
/// single product can read as e.g. <c>Toxic | Volatile</c>. Display label and colour
/// are handled UI-side (see <c>AlchemyAlmanacUI</c>).
/// </summary>
[Flags]
public enum ChemicalType
{
    None      = 0,
    /// <summary>
    /// Breaks down other compounds.
    /// </summary>
    Acidic = 1 << 0,
    /// <summary>
    /// Binds compounds together or neutralizes acids.
    /// </summary>
    Alkaline = 1 << 1,
    /// <summary>
    /// evaporates easily or is unstable.
    /// </summary>
    Volatile = 1 << 2,
    /// <summary>
    /// reacts with other compounds to release energy (heat, light, etc.).
    /// </summary>
    Kinetic = 1 << 3,
    /// <summary>
    /// enables or accelerates reactions.
    /// </summary>
    Catalytic = 1 << 4,
    /// <summary>
    /// Refuses to react; can stabilize mixtures or dilute reactions.
    /// </summary>
    Inert = 1 << 5,
}

/// <summary>
/// this is basically for flavor text, but it also has some mechanical implications for the almanac's sorting and filtering. It is not a direct mapping to any in-game mechanics, but rather a thematic classification of the item based on its properties and effects.
/// <para>
/// Most applicable for the "alchemy" alchemy mode (making utilities and weapons)
/// </para>
/// </summary>
[Flags]
public enum PrincipleType
{
    None = 0,
    /// <summary>
    /// rejuvination, life, growth. 
    /// </summary>
    Vital = 1 << 0,
    /// <summary>
    /// lucid, revealing, insight.
    /// </summary>
    Specular = 1 << 1,
    /// <summary>
    /// strength, power, force.
    /// </summary>
    Martial = 1 << 2,
    /// <summary>
    /// speed, dexterity, haste.
    /// </summary>
    Brisk = 1 << 3,
    /// <summary>
    /// luck, fortune, abundance.
    /// </summary>
    Fortune = 1 << 4,
    /// <summary>
    /// balance, harmony, binding.
    /// </summary>
    Harmony = 1 << 5,
    /// <summary>
    /// decay, withering, unbinding.
    /// </summary>
    Bane = 1 << 6
}
/// <summary>
/// Authored metadata for one almanac output.
/// The "entry" shown in the Almanac's Detail view. Keyed by <see cref="ItemType"/>.
/// <para>
/// Recipes (<see cref="AlchemyRecipe"/>) describe how an item is <em>made</em>; an
/// entry describes what it <em>is</em> (flavour text + ratings) for the book. Many
/// recipes can share one output, so this metadata lives on the product, not the recipe.
/// </para>
/// </summary>
public sealed class AlmanacEntry
{
    /// <summary>Item type this entry describes. This is the registry key.</summary>
    public int ItemType { get; init; }

    /// <summary>Star rating, 1–5. <c>0</c> means "unrated" and draws no stars.</summary>
    public int Potency { get; init; }

    /// <summary>Chemical tag(s). Drawn as one coloured badge per set flag.</summary>
    public ChemicalType Chemical { get; init; }

    public PrincipleType Principle { get; init; } = PrincipleType.None;
    /// <summary>
    /// Rarity as a vanilla rarity value (an <c>ItemRarityID</c>), judged by this system's
    /// own rules rather than read from the item's <c>Item.rare</c>. Left unset, it falls
    /// back to the item's vanilla rarity via <see cref="ResolvedRarity"/>.
    /// </summary>
    public int Rarity { get; init; } = UseVanillaRarity;

    /// <summary>
    /// Localization sub-key under <c>Almanac.</c> for the flavour description.
    /// <c>null</c> derives it from the item's display name with spaces stripped, which
    /// preserves the original keying so existing descriptions keep working.
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// When true (the default), this item shows as a "?" slot in the Almanac's Ingredients
    /// index until the player has obtained it at least once
    /// (<see cref="AlmanacDiscoverySystem.IsIngredientVisible"/>). Set false for the starter materials
    /// </summary>
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

    /// <summary>
    /// Localized flavour text, or empty string when no description key is defined (so the
    /// Detail view draws nothing rather than a raw key path for un-authored entries).
    /// </summary>
    public string GetDescription()
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.FlavorText.{ResolvedDescriptionKey}");
        string value = Language.GetTextValue(key);
        return value == key ? string.Empty : value;   // GetTextValue echoes the key when missing
    }
}

/// <summary>
/// Registry of authored <see cref="AlmanacEntry"/> metadata, keyed by output item type.
/// Outputs without an authored entry resolve to a sensible default via <see cref="For"/>,
/// so the definition list can grow incrementally instead of needing every output covered.
/// </summary>
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
    /// Potency an un-authored solvent or reagent falls back to. Anything the recipe registry actually
    /// brews with is doing <em>something</em>, so 0 ("unrated", draws no stars) reads as missing data
    /// rather than as a rating. Authored entries always win, including ones that deliberately say 0.
    /// </summary>
    public const int DefaultIngredientPotency = 1;

    /// <summary>
    /// The authored entry for <paramref name="outputType"/>, or a default entry (no chemical tags,
    /// vanilla rarity) when none was registered. An un-authored solvent/reagent comes back rated
    /// <see cref="DefaultIngredientPotency"/>; anything else stays unrated.
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
/// Authored almanac entry metadata (potency / chemical type / rarity) for the outputs we
/// curate. Anything not listed here falls back to <see cref="AlmanacEntryRegistry.For"/>'s
/// defaults, so this list is meant to grow incrementally as products get fleshed out.
/// <para>Rarity uses vanilla <c>ItemRarityID</c> values, chosen by the system's own
/// judgement rather than the item's own <c>Item.rare</c>.</para>
/// </summary>
internal static class AlmanacEntryDefinitions
{
    public static void Register()
    {
        RegisterSolvents();

        RegisterIngredients();
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
            Chemical = ChemicalType.None,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.BottledHoney,
            Potency = 3,
            Chemical = ChemicalType.Acidic,
            Rarity = ItemRarityID.Yellow,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<InertSolutionItem>(),
            Potency = 1,
            Hidden = false,
            Chemical = ChemicalType.Inert,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<DeadSolutionItem>(),
            Potency = 4,
            Chemical = ChemicalType.Inert,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<ArcaneSolutionItem>(),
            Potency = 1,
            Chemical = ChemicalType.Catalytic,
            Rarity = ItemRarityID.Blue,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<MagnoliaSolutionItem>(),
            Potency = 2,
            Chemical = ChemicalType.Alkaline,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BloodtoothExtractItem>(),
            Potency = 3,
            Chemical = ChemicalType.Volatile,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Bane,
        });

    }

    private static void RegisterIngredients()
    {
        #region Plants / Herbs
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Daybloom,
            Potency = 1,
            Chemical = ChemicalType.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Blinkroot,
            Potency = 1,
            Chemical = ChemicalType.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Moonglow,
            Potency = 3,
            Chemical = ChemicalType.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Deathweed,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Rarity = ItemRarityID.LightPurple,
            Hidden = false,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Waterleaf,
            Potency = 2,
            Chemical = ChemicalType.Alkaline,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Fireblossom,
            Potency = 2,
            Chemical = ChemicalType.Volatile,
            Rarity = ItemRarityID.Orange,
            Hidden = false,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Shiverthorn,
            Potency = 3,
            Chemical = ChemicalType.Alkaline | ChemicalType.Catalytic,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<MagnoliaItem>(),
            Potency = 4,
            Chemical = ChemicalType.Alkaline | ChemicalType.Catalytic,
            Rarity = ItemRarityID.Green,
            Hidden = false,
            Principle = PrincipleType.Harmony | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Sunflower,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
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
            ItemType = ModContent.ItemType<FlyAgaricItem>(),
            Potency = 2,
            Chemical = ChemicalType.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Mushroom,
            Potency = 1,
            Chemical = ChemicalType.Inert,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.VileMushroom,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ViciousMushroom,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.Bane,
        });


        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BloodtoothFungusItem>(),
            Potency = 4,
            Chemical = ChemicalType.Volatile,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Martial,
        });
        #endregion

        #region Ores
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CopperOre,
            Potency = 1,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TinOre,
            Potency = 2,
            Chemical = ChemicalType.Catalytic,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.IronOre,
            Potency = 3,
            Chemical = ChemicalType.Kinetic,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LeadOre,
            Potency = 3,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.Bane | PrincipleType.Harmony, // context dependent behavior
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SilverOre,
            Potency = 3,
            Chemical = ChemicalType.Kinetic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TungstenOre,
            Potency = 4,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.GoldOre,
            Potency = 4,
            Chemical = ChemicalType.Kinetic,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Vital | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PlatinumOre,
            Potency = 5,
            Chemical = ChemicalType.Alkaline,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Fortune,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DemoniteOre,
            Potency = 4,
            Chemical = ChemicalType.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimtaneOre,
            Potency = 4,
            Chemical = ChemicalType.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<LodestoneItem>(),
            Potency = 3,
            Chemical = ChemicalType.Kinetic,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Martial,
        });

        // Todo: hardmode & special ores
        #endregion



        #region Miscellaneous

        // Miscellaneous frfr

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.RottenChunk,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Vertebrae,
            Potency = 2,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Martial,
        });

        // Fish

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SpecularFish,
            Potency = 3,
            Chemical = ChemicalType.Catalytic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Damselfish,
            Potency = 4,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Bass,
            Potency = 1,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinkfish,
            Potency = 3,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FlarefinKoi,
            Potency = 5,
            Chemical = ChemicalType.Volatile,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Obsidifish,
            Potency = 4,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ArmoredCavefish,
            Potency = 3,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.AtlanticCod,
            Potency = 1,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DoubleCod,
            Potency = 2,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.NeonTetra,
            Potency = 2,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Ebonkoi,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Hemopiranha,
            Potency = 2,
            Chemical = ChemicalType.Volatile,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimsonTigerfish,
            Potency = 1,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FrostMinnow,
            Potency = 3,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Brisk | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PrincessFish,
            Potency = 4,
            Chemical = ChemicalType.Kinetic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Prismite,
            Potency = 5,
            Chemical = ChemicalType.Kinetic | ChemicalType.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        // Materials

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Gel,
            Potency = 1,
            Chemical = ChemicalType.Alkaline,
            Principle = PrincipleType.Harmony | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinger,
            Potency = 2,
            Chemical = ChemicalType.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Amber,
            Potency = 3,
            Chemical = ChemicalType.Kinetic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SharkFin,
            Potency = 2,
            Chemical = ChemicalType.Volatile,
            Principle = PrincipleType.Brisk | PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Lens,
            Potency = 1,
            Chemical = ChemicalType.None,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LifeCrystal,
            Potency = 3,
            Chemical = ChemicalType.Inert,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<CharcoalItem>(),
            Potency = 1,
            Chemical = ChemicalType.Kinetic,
            Principle = PrincipleType.None,
        });
        #endregion
    } 
}
