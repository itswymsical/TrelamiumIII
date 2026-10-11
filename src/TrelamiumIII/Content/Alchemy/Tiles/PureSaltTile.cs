namespace TrelamiumIII.Content.Alchemy.Tiles;

/// <summary>Salt crust left where brine meets water. Drops and is placed by the Pure Salt solute.</summary>
[LegacyName("SaltBlockTile")]
public class PureSaltTile : ModTile
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

        AddMapEntry(new Color(205, 215, 250), CreateMapEntryName());
    }
}
