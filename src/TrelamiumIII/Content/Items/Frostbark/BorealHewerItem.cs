using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Components.Items;

namespace TrelamiumIII.Content.Items.Frostbark;

public class BorealHewerItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 8;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 50;
        Item.useTime = Item.useAnimation = 24;
        Item.knockBack = 1.2f;
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
        component.ChemicalDamage = 3;
        component.debuffDuration = 40;
        component.ReactionChance = 13;
        component.ReactionDamage = 4;
    }

    /// <summary><see cref="Player.altFunctionUse"/> while an alt use is running.</summary>
    private const int ALT_USE = 2;

    /// <summary>Right click calls a thrown axe back rather than throwing another.</summary>
    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
        => player.altFunctionUse == ALT_USE
            ? BorealHewerProj.AnyOut(player)
            : !BorealHewerProj.AnyOut(player);

    public override bool? UseItem(Player player)
    {
        if (player.altFunctionUse != ALT_USE)
            return null;

        BorealHewerProj.RecallFor(player);

        return true;
    }

    /// <summary>The recall is the whole of the right click; it must not also throw.</summary>
    public override bool CanShoot(Player player) => player.altFunctionUse != ALT_USE;
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.BorealWood, 16);
        recipe.AddIngredient(ItemID.IceBlock, 8);
        recipe.AddRecipeGroup(RecipeGroupID.IronBar, 4);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}
