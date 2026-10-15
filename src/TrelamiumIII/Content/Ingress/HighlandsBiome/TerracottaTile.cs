using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class TerracottaBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<TerracottaTile>());
}
public class YellowTerracottaBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<YellowTerracottaTile>());
}
public class GrayTerracottaBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<GrayTerracottaTile>());
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

public class YellowTerracottaTile : ModTile
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

        AddMapEntry(new Color(238, 225, 132));
    }
}

public class GrayTerracottaTile : ModTile
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

        AddMapEntry(new Color(189, 191, 179));
    }
}