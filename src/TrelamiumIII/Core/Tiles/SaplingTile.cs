using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Metadata;
using Terraria.Localization;
using Terraria.ObjectData;

namespace TrelamiumIII.Core.Tiles;

/// <summary>
/// A 1x2 sapling that grows into <see cref="Tree"/> on random updates. Doesn't set <see cref="TileID.Sets.TreeSapling"/>,
/// which would hand it to vanilla tree growth.
/// </summary>
public abstract class SaplingTile : ModTile
{
    /// <summary> 1 in this many random updates, on either tile, tries to grow. </summary>
    public virtual int GrowthChance => 8;

    /// <summary> The tree to grow; read on every attempt, so it can pick one at random. </summary>
    protected abstract CustomTree Tree { get; }

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.Width = 1;
        TileObjectData.newTile.Height = 2;
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.DrawFlipHorizontal = true;
        TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.RandomStyleRange = 3;
        TileObjectData.newTile.StyleMultiplier = 3;
        PreAddObjectData();
        TileObjectData.addTile(Type);

        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        AddMapEntry(new Color(170, 120, 100), Language.GetText("MapObject.Sapling"));

        DustType = DustID.WoodFurniture;
        AdjTiles = [TileID.Saplings];
    }

    /// <summary> Called just before <see cref="TileObjectData.addTile"/>; set <see cref="TileObjectData.AnchorValidTiles"/> here. </summary>
    public abstract void PreAddObjectData();

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

    public override void RandomUpdate(int i, int j, bool underground)
    {
        if (WorldGen.genRand.NextBool(GrowthChance))
            TryGrow(i, j);
    }

    /// <summary> Replaces the sapling with <see cref="Tree"/>. Either of its tiles can be passed; the bottom one has frame Y 18. </summary>
    public bool TryGrow(int i, int j) => Tree.TryGrow(i, Main.tile[i, j].TileFrameY >= 18 ? j : j + 1);

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects effects)
    {
        if (i % 2 == 0)
            effects = SpriteEffects.FlipHorizontally;
    }
}
