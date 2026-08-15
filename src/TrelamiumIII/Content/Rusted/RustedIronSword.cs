namespace TrelamiumIII.Content.Rusted;

[LegacyName("Rusted Cleaver")]
public class RustedIronSword : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 18;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 20;
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = 2.75f;

        Item.damage = 8;
        Item.rare = ItemRarityID.Gray;
        Item.UseSound = SoundID.Item1;
        Item.crit = 2;
        Item.useTurn = true;
        Item.value = Item.sellPrice(Item.buyPrice(copper: 8));
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient<RustedIronBar>(10)
        .AddTile(TileID.Anvils)
        .Register();
    }
}
