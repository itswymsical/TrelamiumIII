namespace TrelamiumIII.Content.Alchemy.Tiles;

public class NiterTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;
        Main.tileSpelunker[Type] = true;

        DustType = DustID.Pearlsand;
        HitSound = SoundID.Dig;

        AddMapEntry(new Color(232, 234, 221), CreateMapEntryName());
    }
}