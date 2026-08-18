using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Metadata;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Alchemy;

[LegacyName("FlyAgaric")]
public class FlyAgaricItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.maxStack = Item.CommonMaxStack;
    }    
}

[LegacyName("FlyAgaric")]
public class FlyAgaricTile : ModTile
{
    private const int WIDTH = 40;
    private const int HEIGHT = 38;

    public override void SetStaticDefaults()
    {

        Main.tileFrameImportant[Type] =
        Main.tileObsidianKill[Type] =
        Main.tileNoFail[Type] = true;

        TileID.Sets.ReplaceTileBreakUp[Type] =
        TileID.Sets.IgnoredInHouseScore[Type] =
        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;

        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        DustType = DustID.BrownMoss;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.CoordinateWidth = WIDTH;
        TileObjectData.newTile.CoordinateHeights = [HEIGHT];

        TileObjectData.newTile.DrawYOffset = -(HEIGHT - 22);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.AnchorValidTiles = [
            TileID.Grass,
            TileID.Dirt
        ];

        TileObjectData.newTile.AnchorAlternateTiles = [
                TileID.ClayPot,
                TileID.PlanterBox,
        ];

        TileObjectData.addTile(Type);
        RegisterItemDrop(ModContent.ItemType<FlyAgaricItem>());
        AddMapEntry(new Color(255, 121, 106));
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        base.NumDust(i, j, fail, ref num);

        num = fail ? 3 : 14;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        base.SetSpriteEffects(i, j, ref spriteEffects);

        spriteEffects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
    }
}