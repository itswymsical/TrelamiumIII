namespace TrelamiumIII.Content.Alchemy.Tiles;

/// <summary>Brimstone in the ground. Only placed by <see cref="SoluteID.Sulfur" />; there is no worldgen.</summary>
public class SulfurTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;

        DustType = DustID.BeachShell;
        MineResist = 0.75f;
        MinPick = 40;
        HitSound = SoundID.Tink;

        AddMapEntry(new Color(150, 123, 13), CreateMapEntryName());
    }
    public override bool IsTileSpelunkable(int i, int j) => true;

}
