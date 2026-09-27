using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Category the alchemy station displays for its slotted ingredients. Derived, never selected.</summary>
public enum BrewCategory
{
    None,

    /// <summary>The resolved principle is not None.</summary>
    Synthesis,

    /// <summary>The input slot holds a <see cref="ChemicalRole.Solution" />.</summary>
    Augmentation,

    /// <summary>The resolved principle is None: neither the input nor any solute carries one.</summary>
    Refinement,

    /// <summary>The input slot holds a <see cref="ChemicalRole.Solute" />.</summary>
    Mixture,

    /// <summary>A solvent dissolves a finished <see cref="ChemicalRole.Solution" /> held in a component slot.</summary>
    Transmutation,
}

/// <summary>
///     Principle, potency, temperaments and traits resolved from an input and its solutes. <see cref="Principle" />
///     comes from <see cref="PrimarySolute" />, or from a non-solute input when no solute has one.
/// </summary>
public readonly record struct BrewComposition(
    int Input,
    int PrimarySolute,
    int Potency,
    PrincipleType Principle,
    PrincipleType SecondaryPrinciples,
    Temperament Temperaments,
    ChemicalTrait Traits,
    BrewCategory Category);

/// <summary>Builds <see cref="BrewComposition" /> values from almanac entries, roles and slot potencies. Does not read recipes.</summary>
public static class BrewCompositions
{
    private const int TEMPERAMENT_BITS = 6;

    private const int TRAIT_BITS = 4;

    public static PrincipleType PrincipleOf(int itemType) =>
        AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry) ? entry.Principle : PrincipleType.None;

    /// <summary>Solute potencies are read from stack sizes. Air entries in <paramref name="solutes" /> are skipped.</summary>
    public static BrewComposition Of(Item? input, Item[] solutes)
    {
        Span<int> levels = stackalloc int[solutes.Length];

        for (int i = 0; i < solutes.Length; i++)
            levels[i] = SolutePotency.LevelOf(solutes[i]);

        return Of(input, solutes, levels);
    }

    /// <summary><paramref name="soluteLevels" /> aligns to <paramref name="solutes" />. Air entries are skipped.</summary>
    public static BrewComposition Of(Item? input, Item[] solutes, ReadOnlySpan<int> soluteLevels)
    {
        Span<int> types = stackalloc int[solutes.Length];
        Span<int> levels = stackalloc int[solutes.Length];
        int count = 0;

        for (int i = 0; i < solutes.Length; i++)
        {
            if (solutes[i] is not { IsAir: false } solute)
                continue;

            types[count] = solute.type;
            levels[count] = i < soluteLevels.Length ? soluteLevels[i] : 0;
            count++;
        }

        int inputType = input is { IsAir: false } ? input.type : 0;

        return Of(inputType, SolutePotency.LevelOf(input), types[..count], levels[..count]);
    }

    /// <summary>Group matchers contribute their first member; each solute is read at its matcher's minimum potency.</summary>
    public static BrewComposition Of(AlchemyRecipe recipe)
    {
        Span<int> types = stackalloc int[recipe.Components.Length];
        Span<int> levels = stackalloc int[recipe.Components.Length];

        for (int i = 0; i < types.Length; i++)
        {
            types[i] = FirstMember(recipe.Components[i]);
            levels[i] = recipe.Components[i].MinPotency;
        }

        return Of(recipe.InputType, 1, types, levels);
    }

    /// <summary>
    ///     Primary solute: highest potency with a principle, ties preferring the input's principle. A Solution input keeps its
    ///     principle and demotes solute principles to secondary; a Solute input competes as a solute. Inhibitor clears
    ///     secondary principles. <paramref name="soluteLevels" /> must match <paramref name="soluteTypes" /> in length.
    /// </summary>
    public static BrewComposition Of(int inputType, int inputLevel, ReadOnlySpan<int> soluteTypes, ReadOnlySpan<int> soluteLevels)
    {
        ChemicalRole inputRole = ChemicalRoles.Of(inputType);
        bool augmenting = inputRole == ChemicalRole.Solution;
        bool mixing = inputRole == ChemicalRole.Solute;
        PrincipleType inputPrinciple = mixing ? PrincipleType.None : PrincipleOf(inputType);

        int candidates = soluteTypes.Length + (mixing ? 1 : 0);
        Span<int> types = stackalloc int[candidates];
        Span<int> levels = stackalloc int[candidates];

        soluteTypes.CopyTo(types);
        soluteLevels.CopyTo(levels);

        if (mixing)
        {
            types[^1] = inputType;
            levels[^1] = inputLevel;
        }

        Span<int> temperaments = stackalloc int[TEMPERAMENT_BITS];
        Span<int> traits = stackalloc int[TRAIT_BITS];

        if (!mixing)
            Tally(inputType, temperaments, traits);

        int primary = 0;
        int primaryLevel = -1;
        int highestLevel = 0;
        bool dissolving = false;
        PrincipleType primaryPrinciple = PrincipleType.None;
        PrincipleType allPrinciples = PrincipleType.None;

        for (int i = 0; i < types.Length; i++)
        {
            int type = types[i];
            Tally(type, temperaments, traits);
            highestLevel = Math.Max(highestLevel, levels[i]);
            dissolving |= ChemicalRoles.Of(type) == ChemicalRole.Solution;

            PrincipleType principle = PrincipleOf(type);

            if (principle == PrincipleType.None)
                continue;

            allPrinciples |= principle;

            if (augmenting)
                continue;

            bool winsTie = (principle & inputPrinciple) != 0 && (primaryPrinciple & inputPrinciple) == 0;

            if (levels[i] > primaryLevel || (levels[i] == primaryLevel && winsTie))
            {
                primary = type;
                primaryLevel = levels[i];
                primaryPrinciple = principle;
            }
        }

        PrincipleType resolved = augmenting || primaryPrinciple == PrincipleType.None ? inputPrinciple : primaryPrinciple;
        ChemicalTrait netTraits = NetTraits(traits);

        PrincipleType secondary = (netTraits & ChemicalTrait.Inhibitor) != 0
            ? PrincipleType.None
            : (allPrinciples | inputPrinciple) & ~resolved;

        int potency = primary > 0 ? primaryLevel : highestLevel;

        BrewCategory category = inputType <= 0 || soluteTypes.Length == 0 ? BrewCategory.None
            : augmenting ? BrewCategory.Augmentation
            : mixing ? BrewCategory.Mixture
            : dissolving ? BrewCategory.Transmutation
            : resolved == PrincipleType.None ? BrewCategory.Refinement
            : BrewCategory.Synthesis;

        return new BrewComposition(inputType, primary, potency, resolved, secondary, NetTemperaments(temperaments), netTraits, category);
    }

    /// <summary>Opposite pairs cancel by count; any Inert removes Catalytic.</summary>
    private static Temperament NetTemperaments(ReadOnlySpan<int> counts)
    {
        Temperament catalysis = counts[Bit((int)Temperament.Inert)] > 0 ? Temperament.Inert
            : counts[Bit((int)Temperament.Catalytic)] > 0 ? Temperament.Catalytic
            : Temperament.None;

        return (Temperament)(Net(counts, (int)Temperament.Acidic, (int)Temperament.Alkaline)
                | Net(counts, (int)Temperament.Volatile, (int)Temperament.Kinetic))
            | catalysis;
    }

    /// <summary>Flammable and Cryogen cancel by count; Oxidizer and Inhibitor are additive.</summary>
    private static ChemicalTrait NetTraits(ReadOnlySpan<int> counts)
    {
        var traits = (ChemicalTrait)Net(counts, (int)ChemicalTrait.Flammable, (int)ChemicalTrait.Cryogen);

        if (counts[Bit((int)ChemicalTrait.Oxidizer)] > 0)
            traits |= ChemicalTrait.Oxidizer;

        if (counts[Bit((int)ChemicalTrait.Inhibitor)] > 0)
            traits |= ChemicalTrait.Inhibitor;

        return traits;
    }

    /// <summary>Whichever of the single-bit flags <paramref name="a" /> and <paramref name="b" /> was counted more; 0 on a tie.</summary>
    private static int Net(ReadOnlySpan<int> counts, int a, int b)
    {
        int difference = counts[Bit(a)] - counts[Bit(b)];

        return difference > 0 ? a : difference < 0 ? b : 0;
    }

    private static void Tally(int itemType, Span<int> temperaments, Span<int> traits)
    {
        if (!AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry))
            return;

        Tally(temperaments, (int)entry.Temperaments);
        Tally(traits, (int)entry.Traits);
    }

    private static void Tally(Span<int> counts, int flags)
    {
        for (int bit = 0; bit < counts.Length; bit++)
        {
            if ((flags & (1 << bit)) != 0)
                counts[bit]++;
        }
    }

    private static int Bit(int flag) => System.Numerics.BitOperations.TrailingZeroCount(flag);

    internal static int FirstMember(in ComponentMatcher matcher)
    {
        if (matcher.Group == null)
            return matcher.ExactType;

        foreach (int type in matcher.Group.ItemTypes)
            return type;

        return 0;
    }
}

/// <summary>Localized names and colors for <see cref="BrewCategory" />.</summary>
public static class BrewCategories
{
    public static readonly BrewCategory[] All =
        [BrewCategory.Synthesis, BrewCategory.Augmentation, BrewCategory.Refinement, BrewCategory.Mixture, BrewCategory.Transmutation];

    /// <summary>Localized under <c>BrewCategories.{member}</c>; falls back to the member name.</summary>
    public static string DisplayName(BrewCategory category)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"BrewCategories.{category}");
        string value = Language.GetTextValue(key);

        return value == key ? category.ToString() : value;
    }

    public static Color ColorOf(BrewCategory category) => category switch
    {
        BrewCategory.Synthesis => new Color(140, 200, 255),
        BrewCategory.Augmentation => new Color(255, 200, 120),
        BrewCategory.Refinement => new Color(255, 130, 130),
        BrewCategory.Mixture => new Color(190, 160, 255),
        BrewCategory.Transmutation => new Color(150, 230, 180),
        _ => Color.White,
    };
}
