using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress.Trees;

public class SmallTanglewoodTree : CustomTree
{
    private static readonly TreeSheet Layout = new(
        new TrunkFrames(Size: new Point(14, 16), Stride: 16, Count: 9, Offset: new Point(1, 0)),
        new TreeCanopy(Source: new Rectangle(0, 36, 62, 90), Anchor: new Vector2(39, 90), Overlap: 4),
        Leaves: []);

    public override TreeSheet Sheet => Layout;
    public override int WoodType => ModContent.ItemType<Bramblewood>();
    public override int MinHeight => 4;

    public override int MaxHeight => 14;

    protected override int[] GetAnchorTiles() => [ModContent.TileType<IngressGrass>()];
}

public class MediumTanglewoodTree : CustomTree
{
    private static readonly TreeSheet Layout = new(
        new TrunkFrames(Size: new Point(26, 26), Stride: 28, Count: 6, Offset: new Point(-5, -4)),
        new TreeCanopy(Source: new Rectangle(0, 56, 82, 110), Anchor: new Vector2(41, 110), Overlap: 8),
        Leaves:
        [
            new TreeLeaf(Source: new Rectangle(92, 66, 10, 8), Pivot: new Vector2(8.5f, 4.5f), Mount: new Vector2(21, 67)),
            new TreeLeaf(Source: new Rectangle(86, 84, 20, 22), Pivot: new Vector2(19, 12), Mount: new Vector2(17, 87)),
            new TreeLeaf(Source: new Rectangle(112, 78, 26, 32), Pivot: new Vector2(18, 31.5f), Mount: new Vector2(66, 73))
        ]);

    public override TreeSheet Sheet => Layout;

    public override int MinHeight => 6;

    public override int MaxHeight => 18;
    public override int WoodType => ModContent.ItemType<Bramblewood>();
    protected override int[] GetAnchorTiles() => [ModContent.TileType<IngressGrass>()];
}