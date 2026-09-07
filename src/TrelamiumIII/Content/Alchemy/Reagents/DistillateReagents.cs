using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     The fuel tier: what comes off the still, plus the two powders. Made at the Alchemy Station
///     in <c>AlchemyMode.Alchemy</c>.
/// </summary>
/// <remarks>
///     Spirit of Oil and Pitch come from the same barrel of oil -- distillation splits a thing
///     rather than consuming it, so they are registered as a pair. Design record:
///     <c>docs/Alchemy/ConventionalReagents.md</c>.
/// </remarks>
public abstract class DistillateReagentItem : ModItem
{
    public override string Texture => AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(silver: 2);
    }
}

/// <summary>
///     Naphtha. The lightest thing to come off the still and the most eager to catch.
/// </summary>
public class SpiritOfOilItem : DistillateReagentItem;

/// <summary>
///     What is left in the bottom of the still. Slow, sticky, and reluctant.
/// </summary>
public class PitchItem : DistillateReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 80);
    }
}

/// <summary>
///     Six parts niter to one of fuel. The intended anchor for
///     <c>ChemicalDamageType.Kinetic</c>, which currently has no matchup rows at all.
/// </summary>
public class BlackPowderItem : DistillateReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.sellPrice(silver: 6);
    }
}

/// <summary>
///     Phosphorus. Catches on contact with plain air, with no spark offered to it.
/// </summary>
public class ColdFireItem : DistillateReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.sellPrice(silver: 15);
    }
}
