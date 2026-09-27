using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Content.Prefixes;

/// <summary>
///     The chemical prefixes, each a <see cref="ChemicalPrefix" /> instance: one per temperament, carrying the same
///     <see cref="TemperamentModifier" /> a flask has innately, and three grades of concentration.
/// </summary>
public sealed class ChemicalPrefixID : ICustomAutoload
{
    // Vanilla squares these for the price. Rarity moves one step at 1.05 and two at 1.2, compared as doubles, so
    // 1.05f itself falls just short of the first step.
    private const float GOOD_VALUE = 1.1f;

    private const float BEST_VALUE = 1.2f;

    private const float POOR_VALUE = 0.9f;

    #region Temperaments

    public static int Acidic { get; private set; }

    public static int Alkaline { get; private set; }

    public static int Volatile { get; private set; }

    public static int Kinetic { get; private set; }

    public static int Catalytic { get; private set; }

    public static int Inert { get; private set; }

    #endregion

    #region Concentration

    public static int Diluted { get; private set; }

    public static int Concentrated { get; private set; }

    public static int Saturated { get; private set; }

    #endregion

    public static void Autoload(Mod mod)
    {
        // Temperaments are sidegrades, so each is an ordinary good prefix.
        Acidic = Add(mod, new ChemicalPrefix(TemperamentModifier.Acidic, value: GOOD_VALUE));
        Alkaline = Add(mod, new ChemicalPrefix(TemperamentModifier.Alkaline, value: GOOD_VALUE));
        Volatile = Add(mod, new ChemicalPrefix(TemperamentModifier.Volatile, value: GOOD_VALUE));
        Kinetic = Add(mod, new ChemicalPrefix(TemperamentModifier.Kinetic, value: GOOD_VALUE));
        Catalytic = Add(mod, new ChemicalPrefix(TemperamentModifier.Catalytic, value: GOOD_VALUE));
        Inert = Add(mod, new ChemicalPrefix(TemperamentModifier.Inert, value: GOOD_VALUE));

        Diluted = Add(mod, new ChemicalPrefix(ConcentrationModifier.Diluted, value: POOR_VALUE));
        Concentrated = Add(mod, new ChemicalPrefix(ConcentrationModifier.Concentrated, value: GOOD_VALUE));
        Saturated = Add(mod, new ChemicalPrefix(ConcentrationModifier.Saturated, rollChance: 0.5f, value: BEST_VALUE));
    }

    private static int Add(Mod mod, ChemicalPrefix prefix)
    {
        mod.AddContent(prefix);

        return prefix.Type;
    }
}
