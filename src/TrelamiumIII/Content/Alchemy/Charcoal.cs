namespace TrelamiumIII.Content.Alchemy;

[LegacyName("Charcoal")]
public class CharcoalItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.White;
        Item.maxStack = Item.CommonMaxStack;
    }

    public override void AddRecipes()
    {
        base.AddRecipes();
        CreateRecipe(4)
            .AddIngredient(ItemID.Wood, 10)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}