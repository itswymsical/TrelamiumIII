namespace TrelamiumIII.Content.Items.Alchemy.Reagents;

public class RockSaltTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;

        DustType = DustID.Marble;
        MineResist = 0.35f;
        HitSound = SoundID.Tink;

        AddMapEntry(new Color(144, 148, 155));
    }
}
