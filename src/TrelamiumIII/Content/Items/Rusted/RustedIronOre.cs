namespace TrelamiumIII.Content.Items.Rusted;

public class RustedIronOre : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileBlendAll[Type] = true;
        HitSound = SoundID.Tink;
        DustType = DustID.Iron;
        RegisterItemDrop(ItemID.MudBlock);
        AddMapEntry(new Color(108, 98, 88));
    }
    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;
}
