namespace TrelamiumIII.Common.ChemicalClass;

public class ChemicalPlayerStats : ModPlayer
{
    private static readonly int ElementCount = Enum.GetValues<ChemicalDamageType>().Length;

    private readonly float[] elementDamage = new float[ElementCount];
    private readonly float[] elementReactionDamage = new float[ElementCount];
    private readonly float[] elementReactionChance = new float[ElementCount];
    private readonly float[] elementDamageBonus = new float[ElementCount];
    private readonly float[] elementAfflictionSpeed = new float[ElementCount];
    private readonly float[] elementAfflictionRate = new float[ElementCount];

    /// <summary>Multiplier on all chemical damage (affliction ticks).</summary>
    public float ChemicalDamageBonus;

    /// <summary>Multiplier on affliction duration from all sources (1.5 = 50% longer).</summary>
    public float AfflictionRate;

    /// <summary>Immunity frames between affliction ticks.</summary>
    public float AfflictionSpeed;

    /// <summary>Reaction odds in percent added to the source's own, like the player's crit chance.</summary>
    public float ReactionChance;

    /// <summary>Multiplier on reaction burst damage, separate from sustained affliction damage.</summary>
    public float ReactionDamageBonus;

    /// <summary>Affliction damage bonus for one element only, as a fraction (0.04 = +4%).</summary>
    public ref float ElementDamage(ChemicalDamageType type) => ref elementDamage[(int)type];

    /// <summary>Multiplier on <paramref name="type" />'s affliction damage: <see cref="ChemicalDamageBonus" /> plus that element's bonus.</summary>
    public float AfflictionDamage(ChemicalDamageType type) => ChemicalDamageBonus + elementDamage[(int)type];

    /// <summary>Reaction damage bonus for one element only, as a fraction (0.04 = +4%).</summary>
    public ref float ElementReactionDamage(ChemicalDamageType type) => ref elementReactionDamage[(int)type];

    /// <summary>Multiplier on <paramref name="type" />'s reaction damage: <see cref="ReactionDamageBonus" /> plus that element's bonus.</summary>
    public float ReactionDamage(ChemicalDamageType type) => ReactionDamageBonus + elementReactionDamage[(int)type];

    /// <summary>Reaction chance bonus for one element only, in percent (5 = +5%).</summary>
    public ref float ElementReactionChance(ChemicalDamageType type) => ref elementReactionChance[(int)type];

    /// <summary>Reaction odds for <paramref name="type" />: <see cref="ReactionChance" /> plus that element's bonus.</summary>
    public float ReactionOdds(ChemicalDamageType type) => ReactionChance + elementReactionChance[(int)type];

    /// <summary>Damage bonus for one element only, as a fraction (0.04 = +4%).</summary>
    public ref float ElementDamageBonus(ChemicalDamageType type) => ref elementDamageBonus[(int)type];

    /// <summary>Multiplier on <paramref name="type" />'s chemical damage: <see cref="ChemicalDamageBonus" /> plus that element's bonus.</summary>
    public float ChemicalDamage(ChemicalDamageType type) => ChemicalDamageBonus + elementDamageBonus[(int)type];

    /// <summary>Affliction speed bonus for one element only, in frames (higher is faster).</summary>
    public ref float ElementAfflictionSpeed(ChemicalDamageType type) => ref elementAfflictionSpeed[(int)type];

    /// <summary>Immunity frames between affliction ticks for <paramref name="type" />: <see cref="AfflictionSpeed" /> plus that element's bonus.</summary>
    public float AfflictionTickSpeed(ChemicalDamageType type) => AfflictionSpeed + elementAfflictionSpeed[(int)type];

    /// <summary>Affliction rate multiplier for one element only, as a fraction (0.5 = +50% duration).</summary>
    public ref float ElementAfflictionRate(ChemicalDamageType type) => ref elementAfflictionRate[(int)type];

    /// <summary>Multiplier on <paramref name="type" />'s affliction duration: <see cref="AfflictionRate" /> multiplied by that element's rate.</summary>
    public float AfflictionDuration(ChemicalDamageType type) => AfflictionRate * (1f + elementAfflictionRate[(int)type]);

    public override void ResetEffects()
    {
        ChemicalDamageBonus = 1f;
        AfflictionRate = 1f;
        AfflictionSpeed = 20f;
        ReactionChance = 5f; // 5% base reaction chance before accessories
        ReactionDamageBonus = 1f;
        Array.Clear(elementDamage);
        Array.Clear(elementReactionDamage);
        Array.Clear(elementReactionChance);
        Array.Clear(elementDamageBonus);
        Array.Clear(elementAfflictionSpeed);
        Array.Clear(elementAfflictionRate);
    }
}

/// <summary>
///     The controller class: area control over single-target damage. Every weapon works through
///     <see cref="IChemicalSource" />; payloads with no element use <see cref="ChemicalDamageType.Solute" />.
/// </summary>
public class ChemicalDamageClass : DamageClass
{
    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == Generic)
            return StatInheritanceData.Full;

        return new StatInheritanceData(
            damageInheritance: 0f,
            critChanceInheritance: 0f,
            attackSpeedInheritance: 0f,
            armorPenInheritance: 0f,
            knockbackInheritance: 0f
        );
    }

    /// <summary>Knockback is removed: it would push targets out of the zone the weapon just built.</summary>
    public override void SetDefaultStats(Player player)
    {
        player.GetArmorPenetration<ChemicalDamageClass>() += 2;
        player.GetKnockback<ChemicalDamageClass>() *= 0;
    }

    // Reactions replace crits, so standard crit rolls are off.
    public override bool UseStandardCritCalcs => false;

    public override bool ShowStatTooltipLine(Player player, string lineName)
    {
        return lineName switch
        {
            "Knockback" => false,
            "CritChance" => false,
            _ => true
        };
    }

    /// <summary>No vanilla prefix categories: chemical weapons roll chemical prefixes only, through <see cref="ChemicalPrefixRolls" />.</summary>
    public override bool GetPrefixInheritance(DamageClass damageClass) => false;
}