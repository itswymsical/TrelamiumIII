using TrelamiumIII.Core;
using TrelamiumIII.Core.UI.AlchemyAlmanac;

namespace TrelamiumIII.Content.Items.Alchemy;

public class AlchemistsAlmanacItem : ModItem
{
    public override string Texture => AssetReferences.Content.Items.Alchemy.AlchemyBookItem.KEY;

    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(gold: 1);
        Item.width = Item.height = 28;
        Item.useTime = Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.HoldUp;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer != player.whoAmI) return true;
        AlchemyAlmanacUI.ShowAlmanac();
        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ItemID.Book)
        .AddIngredient(ItemID.Leather)
        .AddTile(TileID.WorkBenches)
        .Register();

        CreateRecipe()
        .AddIngredient(ItemID.Book)
        .AddIngredient(ItemID.Silk)
        .AddTile(TileID.WorkBenches)
        .Register();
    }
}