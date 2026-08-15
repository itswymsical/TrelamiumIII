using Terraria.Localization;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Fungore;

public class FungoreTrophy : ModItem
{
	public override void SetDefaults()
    {
        Item.width = Item.height = 32;
		Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(gold: 1);

        Item.DefaultToPlaceableTile(ModContent.TileType<FungoreTrophyTile>());
	}
}

public class FungoreTrophyTile : ModTile
{
	public override void SetStaticDefaults() {
		Main.tileFrameImportant[Type] = true;
		Main.tileLavaDeath[Type] = true;
		TileID.Sets.FramesOnKillWall[Type] = true;

		TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
		TileObjectData.addTile(Type);
		AddMapEntry(new Color(120, 85, 60), Language.GetText("MapObject.Trophy"));
		DustType = DustID.WoodFurniture;
	}
}