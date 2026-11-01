using Terraria.DataStructures;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class BasaltBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<BasaltTile>());
}

/// <summary> Dark mountain rock: the cores, crags and boulders of the highland peaks. </summary>
public class BasaltTile : ModTile
{
    private static readonly Color Tint = new(128, 132, 148);

    /// <summary> Vanilla stone, darkened by <see cref="Tint"/>, until basalt has its own sprite. </summary>
    public override string Texture => $"Terraria/Images/Tiles_{TileID.Stone}";

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileBlockLight[Type] = true;

        this.Merge(TileID.Stone, TileID.ClayBlock, TileID.SnowBlock, TileID.IceBlock, ModContent.TileType<SlateTile>(), ModContent.TileType<PeatTile>(),
            ModContent.TileType<HighlandsGrass>(), ModContent.TileType<SnowHighlandsGrass>(), ModContent.TileType<GravelTile>());

        MineResist = 1.2f;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        VanillaFallbackOnModDeletion = TileID.Stone;

        AddMapEntry(new Color(64, 66, 76));
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) => drawData.colorTint = Tint;
}
