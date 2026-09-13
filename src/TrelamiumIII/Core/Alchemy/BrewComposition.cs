using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Category the alchemy station displays for its slotted ingredients. Derived, never selected.</summary>
public enum BrewCategory
{
    None,

    /// <summary>The resolved principle is not None.</summary>
    Synthesis,

    /// <summary>The solvent slot holds a <see cref="ChemicalRole.Solution" />.</summary>
    Augmentation,

    /// <summary>The resolved principle is None: neither the solvent nor any solute carries one.</summary>
    Refinement,
}

/// <summary>
///     Principle and qualities resolved from a solvent and its solutes. <see cref="Principle" /> comes from
///     <see cref="PrimarySolute" />, or from the solvent when no solute has one.
/// </summary>
public readonly record struct BrewComposition(
    int Solvent,
    int PrimarySolute,
    PrincipleType Principle,
    PrincipleType SecondaryPrinciples,
    ChemicalQuality Qualities,
    ChemicalQuality Modifiers,
    BrewCategory Category);

/// <summary>Builds <see cref="BrewComposition" /> values from almanac entries and roles. Does not read recipes.</summary>
public static class BrewCompositions
{
    /// <summary>Acidic/Alkaline, Volatile/Kinetic and Catalytic/Inert: three opposed pairs.</summary>
    public const ChemicalQuality BaseQualities = ChemicalQuality.Acidic | ChemicalQuality.Alkaline | ChemicalQuality.Volatile
        | ChemicalQuality.Kinetic | ChemicalQuality.Catalytic | ChemicalQuality.Inert;

    public const ChemicalQuality ModifierQualities = ChemicalQuality.Oxidizer | ChemicalQuality.Flammable
        | ChemicalQuality.Inhibitor | ChemicalQuality.Cryogen;

    private const int QUALITY_BITS = 10;

    public static PrincipleType PrincipleOf(int itemType) =>
        AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry) ? entry.Principle : PrincipleType.None;

    /// <summary>Air entries in <paramref name="solutes" /> are skipped.</summary>
    public static BrewComposition Of(Item? solvent, Item[] solutes)
    {
        Span<int> types = stackalloc int[solutes.Length];
        int count = 0;

        foreach (Item? solute in solutes)
        {
            if (solute is { IsAir: false })
                types[count++] = solute.type;
        }

        return Of(solvent is { IsAir: false } ? solvent.type : 0, types[..count]);
    }

    /// <summary>Group matchers contribute their first member.</summary>
    public static BrewComposition Of(AlchemyRecipe recipe)
    {
        Span<int> types = stackalloc int[recipe.Components.Length];

        for (int i = 0; i < types.Length; i++)
            types[i] = FirstMember(recipe.Components[i]);

        return Of(recipe.SolventType, types);
    }

    /// <summary>
    ///     Primary solute: highest potency with a principle, ties preferring the solvent's principle. A Solution-role solvent
    ///     keeps its principle and demotes solute principles to secondary. Inhibitor clears secondary principles.
    /// </summary>
    public static BrewComposition Of(int solventType, ReadOnlySpan<int> soluteTypes)
    {
        bool augmenting = ChemicalRoles.Of(solventType) == ChemicalRole.Solution;
        PrincipleType solventPrinciple = PrincipleOf(solventType);

        Span<int> counts = stackalloc int[QUALITY_BITS];
        Tally(counts, QualityOf(solventType));

        int primary = 0;
        int primaryPotency = -1;
        PrincipleType primaryPrinciple = PrincipleType.None;
        PrincipleType allPrinciples = PrincipleType.None;

        foreach (int type in soluteTypes)
        {
            Tally(counts, QualityOf(type));

            PrincipleType principle = PrincipleOf(type);

            if (principle == PrincipleType.None)
                continue;

            allPrinciples |= principle;

            if (augmenting)
                continue;

            int potency = PotencyOf(type);
            bool winsTie = (principle & solventPrinciple) != 0 && (primaryPrinciple & solventPrinciple) == 0;

            if (potency > primaryPotency || (potency == primaryPotency && winsTie))
            {
                primary = type;
                primaryPotency = potency;
                primaryPrinciple = principle;
            }
        }

        PrincipleType resolved = augmenting || primaryPrinciple == PrincipleType.None ? solventPrinciple : primaryPrinciple;
        ChemicalQuality modifiers = NetModifiers(counts);

        PrincipleType secondary = (modifiers & ChemicalQuality.Inhibitor) != 0
            ? PrincipleType.None
            : (allPrinciples | solventPrinciple) & ~resolved;

        BrewCategory category = solventType <= 0 || soluteTypes.Length == 0 ? BrewCategory.None
            : augmenting ? BrewCategory.Augmentation
            : resolved == PrincipleType.None ? BrewCategory.Refinement
            : BrewCategory.Synthesis;

        return new BrewComposition(solventType, primary, resolved, secondary, NetQualities(counts), modifiers, category);
    }

    /// <summary>Opposite pairs cancel by count; any Inert removes Catalytic.</summary>
    private static ChemicalQuality NetQualities(ReadOnlySpan<int> counts)
    {
        ChemicalQuality catalysis = counts[Bit(ChemicalQuality.Inert)] > 0 ? ChemicalQuality.Inert
            : counts[Bit(ChemicalQuality.Catalytic)] > 0 ? ChemicalQuality.Catalytic
            : ChemicalQuality.None;

        return Net(counts, ChemicalQuality.Acidic, ChemicalQuality.Alkaline)
            | Net(counts, ChemicalQuality.Volatile, ChemicalQuality.Kinetic)
            | catalysis;
    }

    /// <summary>Flammable and Cryogen cancel by count; Oxidizer and Inhibitor are additive.</summary>
    private static ChemicalQuality NetModifiers(ReadOnlySpan<int> counts)
    {
        ChemicalQuality modifiers = Net(counts, ChemicalQuality.Flammable, ChemicalQuality.Cryogen);

        if (counts[Bit(ChemicalQuality.Oxidizer)] > 0)
            modifiers |= ChemicalQuality.Oxidizer;

        if (counts[Bit(ChemicalQuality.Inhibitor)] > 0)
            modifiers |= ChemicalQuality.Inhibitor;

        return modifiers;
    }

    private static ChemicalQuality Net(ReadOnlySpan<int> counts, ChemicalQuality a, ChemicalQuality b)
    {
        int difference = counts[Bit(a)] - counts[Bit(b)];

        return difference > 0 ? a : difference < 0 ? b : ChemicalQuality.None;
    }

    private static void Tally(Span<int> counts, ChemicalQuality quality)
    {
        for (int bit = 0; bit < counts.Length; bit++)
        {
            if (((int)quality & (1 << bit)) != 0)
                counts[bit]++;
        }
    }

    private static int Bit(ChemicalQuality flag) => System.Numerics.BitOperations.TrailingZeroCount((int)flag);

    private static ChemicalQuality QualityOf(int itemType) =>
        AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry) ? entry.Chemical : ChemicalQuality.None;

    private static int PotencyOf(int itemType) =>
        AlmanacEntryRegistry.TryGet(itemType, out AlmanacEntry? entry) ? entry.Potency : 0;

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
    public static readonly BrewCategory[] All = [BrewCategory.Synthesis, BrewCategory.Augmentation, BrewCategory.Refinement];

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
        _ => Color.White,
    };
}
