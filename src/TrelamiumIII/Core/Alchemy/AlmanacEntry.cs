using System.Collections.Generic;
using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Broad "what kind of chemical is this" tagging for an almanac entry. Flags so a
/// single product can read as e.g. <c>Toxic | Volatile</c>. Display label and colour
/// are handled UI-side (see <c>AlchemyAlmanacUI</c>).
/// </summary>
[Flags]
public enum ChemicalQuality
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
    Inert = 1 << 5, // these ones and above are the general types, below are distinct qualities

    Oxidizer = 1 << 6,

    Flammable = 1 << 7,
    /// <summary>
    /// used for bane tonic and necrotic items
    /// </summary>
    Inhibitor = 1 << 8,

    Cryogen = 1 << 9,

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
    public ChemicalQuality Chemical { get; init; }

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
