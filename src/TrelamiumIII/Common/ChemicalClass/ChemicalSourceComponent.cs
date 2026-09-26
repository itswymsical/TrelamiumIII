using TrelamiumIII.Core.Components.Items;
using System.Collections.Generic;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Item component for anything that emits chemistry. Carries the source identity through delivery, with a
/// per-instance reaction callback so two items of the same type can react differently.
/// </summary>
public class ChemicalSourceComponent : ItemComponent, IChemicalSource, ITooltipBlockProvider
{
    public ChemicalDamageType ChemicalType = ChemicalDamageType.None;

    /// <summary>
    /// Flat affliction damage per tick, like <see cref="Item.damage" />, before matchup scaling. Separate from
    /// the item's own damage, which is the direct hit.
    /// </summary>
    public int ChemicalDamage = 5;

    /// <summary>Base affliction duration in ticks. Scaled by player's AfflictionRate.</summary>
    public int debuffDuration = 180;

    /// <summary>Reaction odds in percent, like <see cref="Item.crit" />. Added to player's ReactionChance.</summary>
    public int ReactionChance = 0;

    /// <summary>How often affliction damage is dealt, in ticks.</summary>
    public float debuffRate = 20;

    /// <summary>Reaction burst damage, scaled by composition matchup and the player's ReactionDamageBonus.</summary>
    public int ReactionDamage = 0;

    /// <summary>
    /// Per-instance reaction behaviour, assigned in SetDefaults and invoked when this source rolls a reaction.
    /// </summary>
    public Action<ChemicalReactionEvent> ReactionHandler;

    ChemicalDamageType IChemicalSource.ChemicalType => ChemicalType;
    int IChemicalSource.ChemicalDamage => ChemicalDamage;
    int IChemicalSource.AfflictionDuration => debuffDuration;
    float IChemicalSource.AfflictionRate => debuffRate;
    int IChemicalSource.ReactionChance => ReactionChance;
    int IChemicalSource.ReactionDamage => ReactionDamage;

    public void OnChemicalReaction(in ChemicalReactionEvent e)
        => ReactionHandler?.Invoke(e);

    /// <summary>Adds the chemical stat rows in place of a second damage-type line.</summary>
    public void AddTooltipBlocks(Item item, List<TooltipBlock> blocks)
    {
        if (ChemicalStatsTooltip.Build(this) is { } block)
            blocks.Add(block);
    }
}