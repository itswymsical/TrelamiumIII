using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;

namespace TrelamiumIII.Content.Alchemy;

[LegacyName("BottledOil")]
public class BottledOilItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.value = Item.sellPrice(silver: 2);
        Item.rare = ItemRarityID.White;
        Item.maxStack = Item.CommonMaxStack;
    }

    public override void AddRecipes()
    {
        base.AddRecipes();
        CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddLiquid<CrudeOilLiquid>()
            .Register();
    }
}
