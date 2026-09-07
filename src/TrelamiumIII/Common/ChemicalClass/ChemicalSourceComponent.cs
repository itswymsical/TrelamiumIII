using TrelamiumIII.Core.Components.Items;
using System.Collections.Generic;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Component attached to any item (weapon, utility, flask, etc) that
/// emits chemistry. Carries the source identity through delivery and exposes
/// a per-instance reaction callback so two items of the same chemical type
/// can produce entirely different reactions.
/// </summary>
public class ChemicalSourceComponent : ItemComponent, IChemicalSource, ITooltipBlockProvider
{
    public ChemicalDamageType ChemicalType = ChemicalDamageType.None;

    /// <summary>
    /// Flat affliction tick damage before matchup scaling.
    /// Distinct from the item's base throwing damage.
    /// </summary>
    public float ChemicalDamage = 5f;

    /// <summary>Base affliction duration in ticks. Scaled by player's AfflictionRate.</summary>
    public int AfflictionDuration = 180;

    /// <summary>Per-source reaction chance, 0.0–1.0. Added to player's ReactionChance.</summary>
    public float ReactionChance = 0f;

    /// <summary>How often affliction damage is dealt, in ticks.</summary>
    public float AfflictionRate = 20;

    /// <summary>
    /// Burst damage dealt when this source triggers a reaction. Scaled by
    /// composition matchup and the firing player's ReactionDamageBonus.
    /// </summary>
    public float ReactionDamage = 0f;

    /// <summary>
    /// Per-instance reaction behavior. Assigned in SetDefaults; invoked when this
    /// specific source rolls a reaction on a target. Two items with the same
    /// ChemicalType can carry different handlers and produce different reactions.
    /// </summary>
    public Action<ChemicalReactionEvent> ReactionHandler;

    ChemicalDamageType IChemicalSource.ChemicalType => ChemicalType;
    float IChemicalSource.ChemicalDamage => ChemicalDamage;
    int IChemicalSource.AfflictionDuration => AfflictionDuration;
    float IChemicalSource.AfflictionRate => AfflictionRate;
    float IChemicalSource.ReactionChance => ReactionChance;
    float IChemicalSource.ReactionDamage => ReactionDamage;

    public void OnChemicalReaction(in ChemicalReactionEvent e)
        => ReactionHandler?.Invoke(e);

    /// <summary>
    /// The stat rows at the foot of the tooltip -- per-second damage, reaction burst, duration --
    /// in place of a second damage-type line. See <see cref="ChemicalStatsTooltip"/>.
    /// </summary>
    public void AddTooltipBlocks(Item item, List<TooltipBlock> blocks)
    {
        if (ChemicalStatsTooltip.Build(this) is { } block)
            blocks.Add(block);
    }
}