namespace TrelamiumIII.Content.Frostbark;

public class FlailstormItem : ModItem
{
    public override void SetDefaults()
    {
        // Floors of the charge band: FlailstormProj scales damage and knockback by CHARGE_SCALE at full wind-up (25 and
        // 11.5) and ramps crit to 45%.
        Item.damage = 10;
        Item.knockBack = 4.6f;
        Item.crit = 2;

        Item.width = Item.height = 38;
        Item.useTime = Item.useAnimation = 38;
        Item.value = Item.sellPrice(silver: 14);
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Shoot;
        Item.UseSound = SoundID.DD2_MonkStaffSwing;
        Item.shootSpeed = 10.5f;

        // MeleeNoSpeed: attack speed would shorten the wind-up, which is the weapon.
        Item.DamageType = DamageClass.MeleeNoSpeed;
        Item.noUseGraphic =
            Item.channel = Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<FlailstormProj>();
    }

    public override bool MeleePrefix() => true;

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] < 1;

    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.BorealWood, 8);
        recipe.AddIngredient(ItemID.IceBlock, 20);
        recipe.AddRecipeGroup(nameof(ItemID.SilverBar), 8);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}
