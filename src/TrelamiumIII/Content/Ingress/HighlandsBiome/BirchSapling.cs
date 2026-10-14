using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Metadata;
using Terraria.Localization;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class BirchSapling : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;

        TileObjectData.newTile.Width = 1;
        TileObjectData.newTile.Height = 2;
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<HighlandsGrass>(),
            ModContent.TileType<SnowHighlandsGrass>(),
            ModContent.TileType<CorruptHighlandsGrass>(),
            ModContent.TileType<CrimsonHighlandsGrass>(),
            ModContent.TileType<HallowHighlandsGrass>()
        ];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.DrawFlipHorizontal = true;
        TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.RandomStyleRange = 3;
        TileObjectData.newTile.StyleMultiplier = 3;

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Sapling"));

        TileID.Sets.TreeSapling[Type] = true;
        TileID.Sets.CommonSapling[Type] = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        DustType = ModContent.DustType<SpruceDust>();

        AdjTiles = [TileID.Saplings];
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }

    public override void RandomUpdate(int i, int j)
    {
        if (!WorldGen.genRand.NextBool(20))
        {
            return;
        }

        bool growSuccess = WorldGen.GrowTree(i, j);

        bool isPlayerNear = WorldGen.PlayerLOS(i, j);

        if (growSuccess && isPlayerNear)
        {
            WorldGen.TreeGrowFXCheck(i, j);
        }        
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects effects)
    {
        if (i % 2 == 0)
        {
            effects = SpriteEffects.FlipHorizontally;
        }
    }
}