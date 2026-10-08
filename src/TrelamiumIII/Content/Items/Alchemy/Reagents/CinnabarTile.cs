namespace TrelamiumIII.Content.Items.Alchemy.Reagents;

public class CinnabarTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;

        DustType = DustID.GemRuby;
        MineResist = 0.55f;
        MinPick = 45;
        HitSound = SoundID.Tink;

        AddMapEntry(new Color(167, 70, 55), CreateMapEntryName());
    }
    public override bool IsTileSpelunkable(int i, int j) => true;
}
