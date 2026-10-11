using Terraria.ObjectData;

namespace TrelamiumIII.Content.ObsidianForest;

/// <summary>Cuttable plants on tuff. Placeholder: the vanilla ash plant sheet.</summary>
public class TuffFoliageTile : ModTile
{
    /// <summary>Frames in the ash plant sheet: 198 px of 18 px columns.</summary>
    public const int Styles = 11;

    public override string Texture => $"Terraria/Images/Tiles_{TileID.AshPlants}";

    public override void SetStaticDefaults()
    {
        Main.tileCut[Type] = true;
        Main.tileSolid[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;

        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<TuffTile>()];
        TileObjectData.newTile.CoordinateHeights = [20];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.RandomStyleRange = Styles;
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.addTile(Type);

        DustType = DustID.Ash;
        HitSound = SoundID.Grass;

        AddMapEntry(new Color(88, 78, 70));
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
        => spriteEffects = i % 2 == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
}
