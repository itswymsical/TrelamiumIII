using TrelamiumIII.Common.Globals;

namespace TrelamiumIII.Content.Ingress;
public class SlateBlock : ModItem
{
    public override void SetDefaults() 
        => Item.DefaultToPlaceableTile(ModContent.TileType<SlateTile>());
}

public class SlateTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;

        Main.tileMergeDirt[Type] = true;

        this.MergeWith(TileID.ClayBlock, TileID.Stone, TileID.Ebonstone, TileID.Crimstone, 
            TileID.Slush, TileID.Grass, TileID.SnowBlock, ModContent.TileType<IngressGrass>(), ModContent.TileType<BramblerootTile>(), ModContent.TileType<PeatTile>());

        Main.tileBlockLight[Type] = true;
        MineResist = 0.15f;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        VanillaFallbackOnModDeletion = TileID.Stone;

        AddMapEntry(new Color(37, 42, 37));
    }
}