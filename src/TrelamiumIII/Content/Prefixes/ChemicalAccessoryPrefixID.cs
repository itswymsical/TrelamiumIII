using TrelamiumIII.Common.ChemicalClass;
using static TrelamiumIII.Common.ChemicalClass.ChemicalAccessoryStat;
using static TrelamiumIII.Common.ChemicalClass.ChemicalDamageType;
using static TrelamiumIII.Common.ChemicalClass.ChemicalPrefixTier;

namespace TrelamiumIII.Content.Prefixes;

/// <summary>
///     Every chemical accessory prefix, registered as <see cref="ChemicalAccessoryPrefix" /> instances: the weapon prefix
///     whose name it shows, the stat it raises (and the element, for element damage), by how much, and its tier. All
///     numbers provisional.
/// </summary>
public sealed class ChemicalAccessoryPrefixID : ICustomAutoload
{
    private static readonly (ChemicalModifier Name, ChemicalAccessoryStat Stat, ChemicalDamageType Element, float Amount, ChemicalPrefixTier Tier)[] Table =
    [
        (ThermalModifiers.Warm, ElementDamage, Thermal, 0.04f, Good),
        (ThermalModifiers.Igniting, ElementDamage, Thermal, 0.08f, Best),

        (CryonicModifiers.Nippy, ElementDamage, Cryonic, 0.04f, Good),
        (CryonicModifiers.Glacial, ElementDamage, Cryonic, 0.08f, Best),

        (CausticModifiers.Tart, ElementDamage, Caustic, 0.04f, Good),
        (CausticModifiers.Acerbic, ElementDamage, Caustic, 0.08f, Best),

        (NecroticModifiers.Umbral, ElementDamage, Necrotic, 0.04f, Good),
        (NecroticModifiers.Vitiating, ElementDamage, Necrotic, 0.08f, Best),

        (KineticModifiers.Vitalized, ElementDamage, Kinetic, 0.04f, Good),
        (KineticModifiers.Terminal, ElementDamage, Kinetic, 0.08f, Best),

        (UniversalModifiers.Valorous, ReactionDamage, None, 0.05f, Good),
        (UniversalModifiers.Valiant, ReactionDamage, None, 0.1f, Best),

        (PrincipleModifiers.Martial, ControlledDamage, None, 0.03f, Good),
        (PrincipleModifiers.Baneful, ControlledDamage, None, 0.06f, Best),
    ];

    public static void Autoload(Mod mod)
    {
        foreach ((ChemicalModifier name, ChemicalAccessoryStat stat, ChemicalDamageType element, float amount, ChemicalPrefixTier tier) in Table)
            mod.AddContent(new ChemicalAccessoryPrefix(name, stat, element, amount, tier));
    }
}
