using TrelamiumIII.Utilities.Extensions;

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

        this.Merge(TileID.ClayBlock, TileID.Stone, TileID.Ebonstone, TileID.Crimstone, 
            TileID.Slush, TileID.Grass, TileID.SnowBlock, ModContent.TileType<IngressGrass>(), ModContent.TileType<BramblerootTile>());

        Main.tileBlockLight[Type] = true;
        MineResist = 0.15f;
        DustType = DustID.Mud;
        HitSound = SoundID.Dig;
        VanillaFallbackOnModDeletion = TileID.Dirt;

        AddMapEntry(new Color(126, 95, 74));
    }
}

public class PeatWall : ModWall
{
    public override void SetStaticDefaults()
    {
        Main.wallHouse[Type] = false;

        DustType = DustID.Mud;

        AddMapEntry(new Color(102, 86, 68));
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }
}