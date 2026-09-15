using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress;

/// <summary> A 1x1 mushroom cluster whose 24x24 frames sit side by side with no padding; the sprite sinks 2 pixels into the grass. </summary>
public class RootMushroomTile : ModTile
{
    public const int Styles = 2;

    private const int FrameSize = 24;
    private const int SinkDepth = 2;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileCut[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.CoordinateWidth = FrameSize;
        TileObjectData.newTile.CoordinateHeights = [FrameSize];
        TileObjectData.newTile.CoordinatePadding = 0;
        TileObjectData.newTile.DrawYOffset = 16 - FrameSize + SinkDepth;
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<IngressGrass>()];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(196, 150, 92));
        DustType = DustID.Hay;
        HitSound = SoundID.Grass;
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 4;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 1)
            spriteEffects = SpriteEffects.FlipHorizontally;
    }
}
