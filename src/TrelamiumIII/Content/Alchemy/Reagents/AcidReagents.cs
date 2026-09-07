using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     The acid tier. Made at the Alchemy Station in <c>AlchemyMode.Alchemy</c>; see
///     <c>AlchemyRecipeSystem.RegisterMinerals</c> for the recipes.
/// </summary>
/// <remarks>
///     Oil of Vitriol is the hub of the whole conventional tree -- four downstream recipes draw
///     on it, which is what makes the trip to the Underworld for sulfur worth taking. Design
///     record: <c>docs/Alchemy/ConventionalReagents.md</c>.
/// </remarks>
public abstract class AcidReagentItem : ModItem
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

public class OilOfVitriolItem : AcidReagentItem;

public class AquaFortisItem : AcidReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(silver: 5);
    }
}

public class SpiritOfSaltItem : AcidReagentItem;

/// <summary>
///     The one that eats gold. The tree's five-star capstone on the Salt line.
/// </summary>
public class AquaRegiaItem : AcidReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.sellPrice(silver: 20);
    }
}

public class BlueVitriolItem : AcidReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(silver: 4);
    }
}
