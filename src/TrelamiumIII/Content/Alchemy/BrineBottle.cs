using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;

namespace TrelamiumIII.Content.Alchemy;

[LegacyName("BrineBottle")]
public class BrineBottleItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = Item.CommonMaxStack;
    }
    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddLiquid<BrineLiquid>()
            .Register();
    }
}