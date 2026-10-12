namespace TrelamiumIII.Content.Items.Medical;

// Placeholder supplies for the Nurse's job (docs/act-one.md). No sprites yet: PlaceholderTextures draws them until
// a PNG named after the class is added next to this file.

public class BandageRoll : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(copper: 40);
        Item.rare = ItemRarityID.White;
    }

    public override void AddRecipes()
    {
        CreateRecipe(3)
            .AddIngredient(ItemID.Silk)
            .AddTile(TileID.Loom)
            .Register();
    }
}

/// <summary>No recipe yet: meant to come out of the alchemy station as a brew.</summary>
public class AntisepticTincture : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 14;
        Item.height = 24;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(silver: 1);
        Item.rare = ItemRarityID.White;
    }
}

public class SutureKit : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 22;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(silver: 2);
        Item.rare = ItemRarityID.White;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddRecipeGroup(RecipeGroupID.IronBar)
            .AddIngredient(ItemID.Silk, 2)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
