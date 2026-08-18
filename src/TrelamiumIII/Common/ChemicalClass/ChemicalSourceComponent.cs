using TrelamiumIII.Core.Components.Items;
using System.Collections.Generic;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Component attached to any item (weapon, utility, flask, etc) that
/// emits chemistry. Carries the source identity through delivery and exposes
/// a per-instance reaction callback so two items of the same chemical type
/// can produce entirely different reactions.
/// </summary>
public class ChemicalSourceComponent : ItemComponent, IChemicalSource
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

    private Color ChemicalColor()
    {
        return ChemicalType switch
        {
            ChemicalDamageType.Caustic => Color.Green,
            ChemicalDamageType.Cryonic => Color.Cyan,
            ChemicalDamageType.Thermal => Color.Orange,
            ChemicalDamageType.Necrotic => Color.MediumPurple,
            ChemicalDamageType.Kinetic => Color.Magenta,
            _ => Color.White
        };
    }

    // TODO: Localization
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!item.TryGetGlobalItem<ChemicalSourceComponent>(out var component))
            return;

        if (component.ChemicalType == ChemicalDamageType.None)
            return;

        int insertionIndex = tooltips.FindIndex(line => line.Name == "Damage");

        var chemicalType = new TooltipLine(Mod, "ChemicalType",
            $"({component.ChemicalDamage} {component.ChemicalType.ToString().ToLower()}/sec)")
        {
            OverrideColor = component.ChemicalColor(),
        };
        tooltips.Insert(insertionIndex + 1, chemicalType);

        var reactionChance = new TooltipLine(Mod, "ReactionChance",
            $"{component.ReactionChance * 1000}% chemical reaction chance");
        tooltips.Insert(insertionIndex + 2, reactionChance);

        if (component.ReactionDamage > 0f)
        {
            var reactionDamage = new TooltipLine(Mod, "ReactionDamage",
                $"reaction burst deals {component.ReactionDamage:0} {component.ChemicalType.ToString().ToLower()} damage")
            {
                OverrideColor = component.ChemicalColor(),
            };
            tooltips.Insert(insertionIndex + 3, reactionDamage);
        }

        int insertionIndex2 = tooltips.FindIndex(line => line.Name == "Speed");
        var afflictionDuration = new TooltipLine(Mod, "AfflictionDuration",
            $"deals {component.AfflictionDuration / 60}s of affliction");
        tooltips.Insert(insertionIndex2 + 1, afflictionDuration);
    }
}