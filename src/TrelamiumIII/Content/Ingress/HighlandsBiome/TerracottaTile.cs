using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class TerracottaBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<TerracottaTile>());
}

public class TerracottaTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;

        Main.tileMergeDirt[Type] = true;

        this.Merge(TileID.ClayBlock, TileID.Stone, TileID.Ebonstone, TileID.Crimstone,
            TileID.Slush, TileID.Grass, TileID.SnowBlock, ModContent.TileType<HighlandsGrass>());

        Main.tileBlockLight[Type] = true;
        MineResist = 0.35f;
        DustType = DustID.Mud;
        HitSound = SoundID.Dig;
        VanillaFallbackOnModDeletion = TileID.ClayBlock;

        AddMapEntry(new Color(240, 152, 136));
    }
}

// TODO: color variants as internal types in GrayTerracotta.
