using TrelamiumIII.Common.Globals;

namespace TrelamiumIII.Content.Ingress;
public class PeatBlock : ModItem
{
    public override void SetDefaults() 
        => Item.DefaultToPlaceableTile(ModContent.TileType<PeatTile>());
}

public class PeatTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;

        Main.tileMergeDirt[Type] = true;

        this.MergeWith(TileID.ClayBlock, TileID.Stone, TileID.Ebonstone, TileID.Crimstone, 
            TileID.Slush, TileID.Grass, TileID.SnowBlock, ModContent.TileType<IngressGrass>(), ModContent.TileType<BramblerootTile>());

        Main.tileBlockLight[Type] = true;
        MineResist = 0.15f;
        DustType = DustID.Mud;
        HitSound = SoundID.Dig;
        VanillaFallbackOnModDeletion = TileID.Dirt;

        AddMapEntry(new Color(126, 95, 74));
    }
}