using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.ObsidianForest;

public class TuffBlock : ModItem
{
    public override string Texture => Assets.Textures.FallbackTileItem.KEY;

    public override void SetDefaults() => Item.DefaultToPlaceableTile(ModContent.TileType<TuffTile>());
}

/// <summary>Obsidian Forest stone. Placeholder sprite.</summary>
public class TuffTile : ModTile
{
    public override void Load() => On_WorldGen.GemTreeGroundTest += GrowGemTreesOnTuff;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;

        this.Merge(TileID.Stone, TileID.Obsidian, TileID.Ash, TileID.Silt);

        MineResist = 1.2f;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        VanillaFallbackOnModDeletion = TileID.Stone;

        AddMapEntry(new Color(124, 114, 100), CreateMapEntryName());
    }

    private static bool GrowGemTreesOnTuff(On_WorldGen.orig_GemTreeGroundTest orig, int tileType)
        => orig(tileType) || tileType == ModContent.TileType<TuffTile>();
}
