namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     Additive chemical stat bonuses from the player's gear. One set holds the bonuses for every release
///     (<see cref="ChemicalPlayerStats.Global" />) and one per element (<see cref="ChemicalPlayerStats.this[ChemicalDamageType]" />);
///     a release adds the two together.
/// </summary>
public sealed class ChemicalStatSet
{
    /// <summary>Fraction added to the damage multiplier (0.04 = +4%).</summary>
    public float Damage;

    public float ReactionDamage;

    /// <summary>Percentage points added to the reaction chance.</summary>
    public float ReactionChance;

    public float AfflictionRate;

    public float AfflictionDuration;

    public float Concentration;

    public void Clear()
    {
        Damage = 0f;
        ReactionDamage = 0f;
        ReactionChance = 0f;
        AfflictionRate = 0f;
        AfflictionDuration = 0f;
        Concentration = 0f;
    }
}
