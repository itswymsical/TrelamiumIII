namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>Brimstone in the ground. Only placed by <see cref="Reagent.Sulfur" />; there is no worldgen.</summary>
public class SulfurTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;

        DustType = DustID.GreenMoss;
        MineResist = 0.25f;
        HitSound = SoundID.Dig;

        AddMapEntry(new Color(150, 123, 13));
    }
}
