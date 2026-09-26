namespace TrelamiumIII.Common.ChemicalClass;

public class ChemicalPlayerStats : ModPlayer
{
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

    public override void ResetEffects()
    {
        ChemicalDamageBonus = 1f;
        AfflictionRate = 1f;
        AfflictionSpeed = 20f;
        ReactionChance = 5f; // 5% base reaction chance before accessories
        ReactionDamageBonus = 1f;
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

    public override bool GetPrefixInheritance(DamageClass damageClass)
    {
        return damageClass.GetPrefixInheritance(Throwing) && damageClass.GetPrefixInheritance(Ranged);
    }
}