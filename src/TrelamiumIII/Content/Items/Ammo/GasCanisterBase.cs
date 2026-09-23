using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Items.Ammo;

/// <summary>
/// used for alchemy.
/// </summary>
public class GasCanisterBase : ModItem
{
    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.GasCanister.KEY;

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 16;
        Item.maxStack = Item.CommonMaxStack;
    }

    public override void AddRecipes()
    {
        CreateRecipe(15)
        .AddRecipeGroup(RecipeGroupID.IronBar, 5)
        .AddTile(TileID.Anvils)
        .Register();
    }
}