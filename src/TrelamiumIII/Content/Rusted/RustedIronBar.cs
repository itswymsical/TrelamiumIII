namespace TrelamiumIII.Content.Rusted;

[LegacyName("Rusted Bar")]
public class RustedIronBar : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        Item.width = Item.height = 18;
        Item.maxStack = Item.CommonMaxStack;
        Item.rare = ItemRarityID.Gray;
        Item.value = Item.sellPrice(Item.buyPrice(copper: 3));
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient<RustedNugget>(3)
        .AddTile(TileID.Furnaces)
        .Register();
    }
}
