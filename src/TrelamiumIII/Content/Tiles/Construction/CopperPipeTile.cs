using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Tiles.Construction;

public class CopperPipeItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<CopperPipeTile>());
        Item.width = 8;
        Item.height = 10;
    }

    public override void AddRecipes()
    {
        CreateRecipe(2)
        .AddIngredient(ItemID.CopperBar)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class CopperPipeTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolidTop[Type] = true;
        Main.tileSolid[Type] = true;

        this.Merge(Type, TileID.CopperBrick, TileID.CopperPlating);
        Main.tileBlockLight[Type] = true;
        DustType = DustID.Copper;
        MineResist = 2f;

        HitSound = SoundID.Tink;
        AddMapEntry(new Color(200, 115, 55));
    }
}