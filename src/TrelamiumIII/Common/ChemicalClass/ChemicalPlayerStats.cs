namespace TrelamiumIII.Common.ChemicalClass;

public class ChemicalPlayerStats : ModPlayer
{
    /// <summary>Reaction chance, in percentage points, every release starts from before bonuses.</summary>
    private const float BaseReactionChance = 4f;

    private readonly ChemicalStatSet[] perElement = CreateSets();

    private static ChemicalStatSet[] CreateSets()
    {
        var sets = new ChemicalStatSet[Enum.GetValues<ChemicalDamageType>().Length];

        for (int i = 0; i < sets.Length; i++)
            sets[i] = new ChemicalStatSet();

        return sets;
    }

    /// <summary>Bonuses that apply to every element: <c>player.Global.Damage += 0.1f</c>.</summary>
    public ChemicalStatSet Global { get; } = new();

    /// <summary>Bonuses for one element only: <c>player[ChemicalDamageType.Cryonic].Damage += 0.04f</c>.</summary>
    public ChemicalStatSet this[ChemicalDamageType type] => perElement[(int)type];

    /// <summary>Folds the global and <paramref name="type" /> bonuses into <paramref name="stats" /> for one release.</summary>
    public void Apply(ref ChemicalStats stats, ChemicalDamageType type)
    {
        ChemicalStatSet element = this[type];

        stats.DamageMultiplier *= 1f + Global.Damage + element.Damage;
        stats.DurationMultiplier *= 1f + Global.AfflictionDuration + element.AfflictionDuration;
        stats.AfflictionRateMultiplier *= 1f + Global.AfflictionRate + element.AfflictionRate;
        stats.ReactionDamageMultiplier *= 1f + Global.ReactionDamage + element.ReactionDamage;
        stats.ConcentrationMultiplier *= 1f + Global.Concentration + element.Concentration;
        stats.ReactionChance += BaseReactionChance + Global.ReactionChance + element.ReactionChance;
    }

    public override void ResetEffects()
    {
        Global.Clear();

        foreach (ChemicalStatSet set in perElement)
            set.Clear();
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