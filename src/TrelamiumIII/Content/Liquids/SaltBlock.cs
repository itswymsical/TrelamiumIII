namespace TrelamiumIII.Content.Liquids;

public class SaltBlockItem : ModItem
{
    public override string Texture => Assets.Textures.FallbackTileItem.KEY;

    public override void SetDefaults()
    {
        Item.width = Item.height = 20;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(copper: 40);

        Item.DefaultToPlaceableTile(ModContent.TileType<SaltBlockTile>());
    }
}
public class SaltBlockTile : ModTile
{
    public override string Texture => Assets.Textures.FallbackTile.KEY;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileBlockLight[Type] = true;
        DustType = DustID.Pearlsand;
        HitSound = SoundID.Dig;
        Main.tileSpelunker[Type] = true;
        AddMapEntry(new Color(205, 215, 250));
    }
}
