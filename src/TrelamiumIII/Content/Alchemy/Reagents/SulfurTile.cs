namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     Brimstone in the ground. Placed by <see cref="Reagent.Sulfur" />, which is the only thing that
///     drops it -- the raw tier adds no worldgen.
/// </summary>
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

        AddMapEntry(new Color(94, 64, 56));
    }
}
