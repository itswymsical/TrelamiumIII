using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Items.Alchemy.Reagents.Misc;

public class DewdropBonnetItem : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<DewdropBonnetTile>());
    }
}

public class DewdropBonnetTile : ModTile
{
    public override void SetStaticDefaults()
    {
        const int height = 32;

        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.CoordinateWidth = 28;
        TileObjectData.newTile.CoordinateHeights = [height];

        TileObjectData.newTile.DrawYOffset = -(height - 20);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.AnchorValidTiles = [TileID.Cloud, TileID.RainCloud, TileID.SnowCloud];
        TileObjectData.addTile(Type);

        AddMapEntry(Color.AliceBlue);
        DustType = DustID.Cloud;
        HitSound = SoundID.Grass;
    }
}
