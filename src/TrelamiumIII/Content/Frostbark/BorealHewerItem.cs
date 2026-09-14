using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Frostbark;
using TrelamiumIII.Core.Components.Items;

namespace TrelamiumIII.Content.Frostbark;

public class BorealHewerItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 9;
        Item.DamageType = DamageClass.Melee;
        Item.width = Item.height = 50;
        Item.useTime = Item.useAnimation = 24;
        Item.knockBack = 1.2f;
        Item.crit = 9;
        Item.value = Item.sellPrice(silver: 12);
        Item.rare = ItemRarityID.Blue;
        Item.useTurn = false;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.UseSound = SoundID.DD2_MonkStaffSwing;
        Item.shootSpeed = 10.5f;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<BorealHewerProj>();

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Cryonic;
        component.AfflictionDuration = 40;
        component.ReactionChance = 0.013f;
        component.ChemicalDamage = 0.35f;
    }

    public override bool CanUseItem(Player player)
    {
        return player.ownedProjectileCounts[ModContent.ProjectileType<BorealHewerProj>()] <= 0;
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.BorealWood, 16);
        recipe.AddIngredient(ItemID.IceBlock, 8);
        recipe.AddRecipeGroup(nameof(ItemID.SilverBar), 4);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}
