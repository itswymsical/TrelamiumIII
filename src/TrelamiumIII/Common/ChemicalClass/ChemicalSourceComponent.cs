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

    /// <inheritdoc cref="IChemicalSource.Concentration" />
    public int Concentration = 10;

    /// <inheritdoc cref="IChemicalSource.Catalysis" />
    public float Catalysis;

    /// <inheritdoc cref="IChemicalSource.ClingBehaviour" />
    public ClingBehaviour? ClingBehaviour;

    /// <summary>Modifiers every release of this item carries. Assigned in SetDefaults; never mutated afterwards.</summary>
    [CloneByReference]
    public ChemicalModifier[] Modifiers = [];

    /// <summary>
    /// Per-instance reaction behaviour, assigned in SetDefaults and invoked when this source rolls a reaction.
    /// </summary>
    [CloneByReference]
    public Action<ChemicalReactionEvent> ReactionHandler;

    ChemicalDamageType IChemicalSource.ChemicalType => ChemicalType;
    int IChemicalSource.ChemicalDamage => ChemicalDamage;
    int IChemicalSource.AfflictionDuration => debuffDuration;
    float IChemicalSource.AfflictionRate => debuffRate;
    int IChemicalSource.ReactionChance => ReactionChance;
    int IChemicalSource.ReactionDamage => ReactionDamage;
    int IChemicalSource.Concentration => Concentration;
    float IChemicalSource.Catalysis => Catalysis;
    ClingBehaviour? IChemicalSource.ClingBehaviour => ClingBehaviour;

    /// <summary>The item's component when it is enabled and carries chemistry, otherwise null.</summary>
    public static ChemicalSourceComponent? Of(Item item)
        => item.TryGetGlobalItem(out ChemicalSourceComponent component)
           && component.Enabled
           && component.ChemicalType != ChemicalDamageType.None
            ? component
            : null;

    public void OnChemicalReaction(in ChemicalReactionEvent e)
        => ReactionHandler?.Invoke(e);

    /// <summary>The item's own damage takes its element's damage bonus; the global bonus is already GetDamage&lt;ChemicalDamageClass&gt;.</summary>
    public override void ModifyWeaponDamage(Item item, Player player, ref Terraria.ModLoader.StatModifier damage)
    {
        if (!Enabled || ChemicalType == ChemicalDamageType.None)
            return;

        damage += player.GetModPlayer<ChemicalPlayerStats>()[ChemicalType].Damage;
    }

    /// <summary>
    ///     Adds the chemical stat rows in place of a second damage-type line, prefix included, then a block per innate
    ///     modifier. The prefix explains itself in vanilla's prefix lines.
    /// </summary>
    public void AddTooltipBlocks(Item item, List<TooltipBlock> blocks)
    {
        ChemicalModifier[] modifiers = ChemicalPrefix.Of(item) is { } prefix ? [.. Modifiers, prefix.Modifier] : Modifiers;

        if (ChemicalStatsTooltip.Build(this, modifiers, Main.gameMenu ? null : Main.LocalPlayer) is not { } block)
            return;

        blocks.Add(block);

        foreach (ChemicalModifier modifier in Modifiers)
            blocks.Add(ChemicalStatsTooltip.BuildModifier(modifier, this));
    }
}