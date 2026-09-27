using TrelamiumIII.Common.ChemicalClass;
using static TrelamiumIII.Common.ChemicalClass.ChemicalPrefixTier;

namespace TrelamiumIII.Content.Prefixes;

/// <summary>
///     Every chemical prefix and its tier, registered as <see cref="ChemicalPrefix" /> instances. Universal ones roll on
///     any chemical weapon they change something for; an element's set rolls only on weapons of that element.
/// </summary>
public sealed class ChemicalPrefixID : ICustomAutoload
{
    private static readonly (ChemicalModifier Modifier, ChemicalPrefixTier Tier)[] Table =
    [
        // The six temperaments, the same modifiers flasks carry innately.
        (TemperamentModifier.Acidic, Good),
        (TemperamentModifier.Alkaline, Good),
        (TemperamentModifier.Volatile, Good),
        (TemperamentModifier.Kinetic, Good),
        (TemperamentModifier.Catalytic, Good),
        (TemperamentModifier.Inert, Good),

        (ConcentrationModifier.Diluted, Poor),
        (ConcentrationModifier.Concentrated, Good),
        (ConcentrationModifier.Saturated, Best),

        (PrincipleModifiers.Vital, Good),
        (PrincipleModifiers.Specular, Good),
        (PrincipleModifiers.Martial, Good),
        (PrincipleModifiers.Brisk, Good),
        (PrincipleModifiers.Fortunate, Good),
        (PrincipleModifiers.Harmonious, Good),
        (PrincipleModifiers.Baneful, Good),

        (UniversalModifiers.Valiant, Best),
        (UniversalModifiers.Gallant, Best),
        (UniversalModifiers.Hazy, Good),
        (UniversalModifiers.Lofty, Good),
        (UniversalModifiers.Valorous, Good),
        (UniversalModifiers.Waning, Poor),
        (UniversalModifiers.Suppressed, Poor),
        (UniversalModifiers.Recreant, Poor),
        (UniversalModifiers.Craven, Poor),

        (ThermalModifiers.Dauntless, Best),
        (ThermalModifiers.Comburant, Best),
        (ThermalModifiers.Inflammatory, Good),
        (ThermalModifiers.Provocative, Good),
        (ThermalModifiers.Igniting, Good),
        (ThermalModifiers.Combustible, Good),
        (ThermalModifiers.Dwindling, Poor),
        (ThermalModifiers.Warm, Poor),
        (ThermalModifiers.Meek, Poor),
        (ThermalModifiers.Amicable, Poor),

        (CryonicModifiers.Wintry, Best),
        (CryonicModifiers.Hyperborean, Best),
        (CryonicModifiers.Glacial, Good),
        (CryonicModifiers.Brumal, Good),
        (CryonicModifiers.Vaporous, Good),
        (CryonicModifiers.Temperate, Poor),
        (CryonicModifiers.Heated, Poor),
        (CryonicModifiers.Lukewarm, Poor),
        (CryonicModifiers.Nippy, Poor),

        (NecroticModifiers.Vitiating, Best),
        (NecroticModifiers.Nullifying, Best),
        (NecroticModifiers.Abolishing, Good),
        (NecroticModifiers.Umbral, Good),
        (NecroticModifiers.Deterring, Poor),
        (NecroticModifiers.Halting, Poor),
        (NecroticModifiers.Exhorting, Poor),
        (NecroticModifiers.Permissive, Poor),

        (CausticModifiers.Virulent, Best),
        (CausticModifiers.Acerbic, Best),
        (CausticModifiers.Pungent, Good),
        (CausticModifiers.Erosive, Good),
        (CausticModifiers.Corrosive, Good),
        (CausticModifiers.Tart, Poor),
        (CausticModifiers.Tickling, Poor),
        (CausticModifiers.Balanced, Poor),
        (CausticModifiers.Flat, Poor),

        (KineticModifiers.Stalwart, Best),
        (KineticModifiers.Tenacious, Best),
        (KineticModifiers.Electrifying, Good),
        (KineticModifiers.Terminal, Good),
        (KineticModifiers.Vitalized, Good),
        (KineticModifiers.Apathetic, Poor),
        (KineticModifiers.Inactive, Poor),
        (KineticModifiers.Unmotivated, Poor),
        (KineticModifiers.Wimpy, Poor),
    ];

    public static void Autoload(Mod mod)
    {
        foreach ((ChemicalModifier modifier, ChemicalPrefixTier tier) in Table)
            mod.AddContent(new ChemicalPrefix(modifier, tier));
    }

    /// <summary>The prefix type carrying <paramref name="modifier" />, or 0 when none does.</summary>
    public static int Of(ChemicalModifier modifier) => ChemicalPrefix.For(modifier)?.Type ?? 0;
}
