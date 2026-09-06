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

/// <summary>
///     The controller class: "controlled damage". Weapons whose output is measured in what the
///     battlefield does afterwards rather than in the number over an enemy's head.
/// </summary>
/// <remarks>
///     <para>
///         <b>Area control and spatial advantage.</b> The whole advantage of this class is that it
///         claims ground and the ground keeps working -- a cloud that lingers, a residue that sits
///         there for twenty seconds, a floor nobody can cross at speed. It is profoundly good at
///         mobbing, event clearing and any kind of crowd control, and the trade is single-target
///         focus: a boss is one target standing in a zone built to punish ten.
///     </para>
///     <para>
///         That trade is the point, not a gap to be patched. Particular subclasses step up against
///         bosses on their own terms -- proximity traps that pay off on a target that has to come to
///         you, chemical guns and agents that put the zone directly on something -- and the class as
///         a whole stays honest about being a crowd tool. The delivery vocabulary it is being built
///         out across: powder bags, chemical guns with solvent carts, tranquiliser darts, gas
///         grenades, gas chains, flasks, proximity traps, and support or defensive pools.
///     </para>
///     <para>
///         <b>Chemistry is the mechanism, not the boundary.</b> Every weapon here works through
///         <see cref="IChemicalSource" /> -- an affliction ticking over time, optionally an element
///         with a composition matchup and a reaction. A weapon with no element at all is still a
///         member in good standing: see <see cref="ChemicalDamageType.Solute" />, which is what gel,
///         powder and inert gas use to afflict over time without ever rolling a reaction. That case
///         is why this class is not split into a "controller" umbrella and a "chemical" branch;
///         there is one class, and the element is a property of the payload rather than a
///         requirement of membership.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     Armour is a smaller obstacle to chemistry than to a projectile, and knockback is removed
    ///     outright.
    /// </summary>
    /// <remarks>
    ///     Knockback is not a stylistic choice. An affliction has no impact to deliver -- the damage
    ///     arrives as a tick some time after whatever caused it -- and more to the point, knocking a
    ///     target out of the zone the weapon just built is the class undoing its own work.
    /// </remarks>
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