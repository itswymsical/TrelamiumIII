namespace TrelamiumIII.Common.ChemicalClass;

public class ChemicalPlayerStats : ModPlayer
{
    /// <summary>Multiplier on all chemical damage (affliction ticks).</summary>
    public float ChemicalDamageBonus;

    /// <summary>
    /// Scales affliction duration from all chemical sources.
    /// 1.0 = no change, 1.5 = 50% longer.
    /// </summary>
    public float AfflictionRate;

    /// <summary>Immunity frames between affliction ticks.</summary>
    public float AfflictionSpeed;

    /// <summary>
    /// Additive reaction chance bonus layered on top of the source's base ReactionChance.
    /// Represents the player's personal chemistry mastery.
    /// </summary>
    public float ReactionChance;

    /// <summary>
    /// Multiplier on reaction burst damage. Affects the single-strike spike that
    /// fires when a reaction triggers, separate from sustained affliction damage.
    /// </summary>
    public float ReactionDamageBonus;

    /// <summary>
    /// Transient: set by ChemicalWeaponGlobalItem.Shoot, read by ChemicalHandler.OnSpawn.
    /// Propagates the firing source's identity from item to all projectiles spawned
    /// that frame. Held as IChemicalSource so non-component sources (placed dispensers,
    /// utility AOEs, etc) can also flow through this channel.
    /// </summary>
    public IChemicalSource ActiveChemicalSource;

    public override void ResetEffects()
    {
        ChemicalDamageBonus = 1f;
        AfflictionRate = 1f;
        AfflictionSpeed = 20f;
        ReactionChance = 0.05f; // 5% base reaction chance before accessories
        ReactionDamageBonus = 1f;
        ActiveChemicalSource = null;
    }
}

public class ChemicalDamageClass : DamageClass
{
    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == DamageClass.Generic)
            return StatInheritanceData.Full;

        return new StatInheritanceData(
            damageInheritance: 0f,
            critChanceInheritance: 0f,
            attackSpeedInheritance: 0f,
            armorPenInheritance: 0f,
            knockbackInheritance: 0f
        );
    }

    public override void SetDefaultStats(Player player)
    {
        player.GetArmorPenetration<ChemicalDamageClass>() += 2;
        player.GetKnockback<ChemicalDamageClass>() *= 0;
    }

    // Chemical reactions replace crits entirely.
    // Standard crit rolls must be off so no natural crits can fire.
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