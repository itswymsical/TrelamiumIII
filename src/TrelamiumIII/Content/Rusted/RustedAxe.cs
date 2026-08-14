namespace TrelamiumIII.Content.Rusted;

[LegacyName("Rusted Axe")]
public class RustedAxe : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 18;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 17;
        Item.useAnimation = 24;
        Item.axe = 9;
        Item.DamageType = DamageClass.Melee;
        Item.rare = ItemRarityID.Gray;
        Item.UseSound = SoundID.Item1;
        Item.damage = 2;
        Item.useTurn = true;
        Item.sellPrice(Item.buyPrice(copper: 15));
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient<RustedIronBar>(8)
        .AddRecipeGroup(RecipeGroupID.Wood, 4)
        .AddTile(TileID.Anvils)
        .Register();
    }
}
