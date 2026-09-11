namespace TrelamiumIII.Content.Construction;

public class WattleFenceItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(0);
        Item.DefaultToPlaceableWall(ModContent.WallType<WattleFenceWall>());
    }
    public override void AddRecipes()
    {
        CreateRecipe(4)
            .AddIngredient<WoodWickerBlockItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

public class WattleFenceWall : ModWall
{
    public override void SetStaticDefaults()
    {
        Main.wallHouse[Type] = true;

        DustType = DustID.WoodFurniture;

        AddMapEntry(new Color(168, 121, 84));
        RegisterItemDrop(ModContent.ItemType<WattleFenceItem>());

    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }
}
