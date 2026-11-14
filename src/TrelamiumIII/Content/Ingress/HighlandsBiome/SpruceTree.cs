using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ObjectData;
using Terraria.Utilities;
using TrelamiumIII.Content.Ingress.HighlandsBiome.SpruceFurniture;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class SpruceSapling : SaplingTile
{
    protected override CustomTree Tree => ModContent.GetInstance<SpruceTree>();

    public override void PreAddObjectData() => TileObjectData.newTile.AnchorValidTiles = [
        ModContent.TileType<HighlandsGrass>(), ModContent.TileType<SnowHighlandsGrass>(),
        ModContent.TileType<CorruptHighlandsGrass>(), ModContent.TileType<CrimsonHighlandsGrass>(),
        ModContent.TileType<HallowHighlandsGrass>(), ModContent.TileType<PodzolGrass>()
        ];
}

public class SpruceTree : CustomTree
{
    protected override int[] GetAnchorTiles() =>
    [
        ModContent.TileType<SnowHighlandsGrass>(),
        ModContent.TileType<HighlandsGrass>(),
        ModContent.TileType<CorruptHighlandsGrass>(),
        ModContent.TileType<CrimsonHighlandsGrass>(),
        ModContent.TileType<HallowHighlandsGrass>(),
        ModContent.TileType<PodzolGrass>()
    ];

    // Trunk frame indices into the table built below; each group holds three variants.
    private const int LargeMid = 0;
    private const int MediumMid = 6;
    private const int SmallMid = 12;
    private const int Stump = 18;
    private const int StumpKinds = 4;
    private const int LeftRoot = Stump + StumpKinds * 3;
    private const int RightRoot = LeftRoot + 3;
    private const int FrameCount = RightRoot + 3;
    private const int FrameSize = 22;

    // Branch cells in Branches_Display.png: 160x60, three tiers (large, medium, small) of three variants, stacked.
    private const int BranchCellWidth = 160;
    private const int BranchCellHeight = 60;
    private const int BranchFrameY = 2;
    private const int FirstWhorl = 3;
    private const float BranchSway = 0.03f;

    /// <summary> Rows between whorls per tier, matched to the scaled cell heights so each tier overlaps the one below. </summary>
    private static readonly int[] WhorlSpacing = [3, 2, 2];

    private static readonly TreeSheet Layout = new(
        Trunk: new TrunkFrames(Size: new Point(FrameSize, FrameSize), Stride: FrameSize, Count: FrameCount, Offset: new Point(-2, -2), Frames: BuildFrames()),
        // The canopy only sizes the clearance check above the top tile; the branches draw themselves.
        Canopy: new TreeCanopy(Source: new Rectangle(0, 0, 24, 16), Anchor: new Vector2(12, 16), Overlap: 0),
        Leaves: []
    );

    private static Asset<Texture2D>? branches;

    public override int MaxHeight => 30;
    public override int MinHeight => 12;
    public override int WoodType => ModContent.ItemType<SpruceWoodItem>();
    public override TreeSheet Sheet => Layout;

    /// <summary> Cells of SpruceTree.png, as (column, row) in its 22px grid: rows 0/1 hold the medium and small trunks and their flat tops, rows 2/4 the large ones. </summary>
    private static Rectangle[] BuildFrames()
    {
        var frames = new Rectangle[FrameCount];

        void Set(int index, int column, int row) => frames[index] = new Rectangle(column * FrameSize, row * FrameSize, FrameSize, FrameSize);

        for (int v = 0; v < 3; v++)
        {
            Set(LargeMid + v, v, 2);
            Set(LargeMid + 3 + v, v, 4);
            Set(MediumMid + v, v, 1);
            Set(MediumMid + 3 + v, v, 0);
            Set(SmallMid + v, 3 + v, 1);
            Set(SmallMid + 3 + v, 3 + v, 0);

            // Stump kinds: roots on both sides, right only, left only, neither.
            Set(Stump + v, 3 + v, 4);
            Set(Stump + 3 + v, 3 + v, 2);
            Set(Stump + 6 + v, 3 + v, 3);
            Set(Stump + 9 + v, 3 + v, 5);

            Set(LeftRoot + v, v, 5);
            Set(RightRoot + v, v, 6);
        }

        return frames;
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        TileObjectData.newTile.DrawYOffset = -4;

        if (!Main.dedServ)
            branches = ModContent.Request<Texture2D>((GetType().Namespace + ".Branches_Display").Replace('.', '/'));
    }

    public override void Unload() => branches = null;

    /// <summary>
    /// Large, medium and small trunk by height, the top tile flat-cut, the base a root stump. Whorls start above
    /// <see cref="FirstWhorl"/> and run to just under the top, shrinking tier by tier so the branches stack into a cone; a
    /// whorl's cell is stored in frame Y as <see cref="BranchFrameY"/> plus its index.
    /// </summary>
    protected override (short X, short Y) RollFrame(int fromBase, int height)
    {
        int variant = WorldGen.genRand.Next(3);

        if (fromBase == 0)
            return ((short)(Stump + variant), 0);

        float progress = (fromBase - 1) / (float)Math.Max(1, height - 2);
        int group = progress < 0.4f ? LargeMid : progress < 0.75f ? MediumMid : SmallMid;

        if (fromBase == height - 1)
            return ((short)(group + 3 + variant), CanopyFrameY);

        int lastWhorl = height - 2;

        for (int row = FirstWhorl; row <= lastWhorl;)
        {
            int tier = WhorlTier(row, lastWhorl);

            if (row == fromBase)
                return ((short)(group + variant), (short)(BranchFrameY + tier * 3 + WorldGen.genRand.Next(3)));

            row += WhorlSpacing[tier];
        }

        return ((short)(group + variant), 0);
    }

    private static int WhorlTier(int row, int lastWhorl)
    {
        float along = (row - FirstWhorl) / (float)Math.Max(1, lastWhorl - FirstWhorl);
        return along < 0.4f ? 0 : along < 0.75f ? 1 : 2;
    }

    /// <summary> A trunk tile that loses the one above it swaps to its flat-cut frame. </summary>
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        bool result = base.TileFrame(i, j, ref resetFrame, ref noBreak);
        Tile tile = Main.tile[i, j];

        if (!WorldGen.gen && !growing && tile.HasTile && tile.TileType == Type && tile.TileFrameX < Stump && tile.TileFrameX % 6 < 3
            && !(Main.tile[i, j - 1].HasTile && Main.tile[i, j - 1].TileType == Type))
        {
            tile.TileFrameX += 3;
        }

        return result;
    }

    protected override bool UsesSpecialDraw(Tile tile) => tile.TileFrameY >= BranchFrameY;

    /// <summary> Draws the trunk; a stump also draws the roots its sides have room for. </summary>
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (tile.TileFrameX < Stump || tile.TileFrameX >= LeftRoot)
            return base.PreDraw(i, j, spriteBatch);

        int variant = (tile.TileFrameX - Stump) % 3;
        bool left = HasRootRoom(i - 1, j);
        bool right = HasRootRoom(i + 1, j);
        int kind = left && right ? 0 : right ? 1 : left ? 2 : 3;

        Texture2D texture = TextureAssets.Tile[Type].Value;
        TrunkFrames trunk = Sheet.Trunk;
        Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);

        void Draw(int x, int frame)
        {
            var position = new Vector2(x * 16 + trunk.Offset.X - (int)Main.screenPosition.X, j * 16 + trunk.Offset.Y - (int)Main.screenPosition.Y);
            spriteBatch.Draw(texture, position + offscreen, trunk.Source(frame), Lighting.GetColor(x, j), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        Draw(i, Stump + kind * 3 + variant);

        if (left)
            Draw(i - 1, LeftRoot + variant);

        if (right)
            Draw(i + 1, RightRoot + variant);

        return false;
    }

    private static bool HasRootRoom(int x, int y) => !Main.tile[x, y].HasTile && WorldGen.SolidTile(x, y + 1);

    /// <summary> Draws one whorl hanging from the top-centre of its trunk tile, rocking on the tree wind counter. </summary>
    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (branches?.Value is not Texture2D texture || tile.TileFrameY < BranchFrameY)
            return;

        int cell = tile.TileFrameY - BranchFrameY;
        float wind = Main.instance.TilesRenderer.GetWindCycle(i, j, TileWindSystem.TreeCounter);
        var position = new Vector2(i * 16 + 8 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y);
        var source = new Rectangle(0, cell * BranchCellHeight, BranchCellWidth, BranchCellHeight);
        SpriteEffects flip = (i * 7 + j * 13) % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        spriteBatch.Draw(texture, position, source, Lighting.GetColor(i, j), wind * BranchSway, new Vector2(BranchCellWidth / 2f, 0f), 1f, flip, 0f);
    }

    /// <summary> Needles fall from the upper cone rather than from a canopy blob. </summary>
    protected override void LeafBurst(int i, int canopyY)
    {
        var center = new Vector2(i * 16 + 8, canopyY * 16 + 60);

        for (int k = 0; k < 14; k++)
        {
            Vector2 offset = Main.rand.NextVector2Circular(56f, 70f);
            Dust dust = Dust.NewDustDirect(center + offset, 0, 0, DustID.GrassBlades, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 1f));
            dust.fadeIn = 1.2f;
        }
    }

    protected override WeightedRandom<int> GetShakeDrops()
    {
        var drops = base.GetShakeDrops();
        drops.Add(ItemID.Peach, 0.01f);
        drops.Add(ItemID.Cherry, 0.01f);
        return drops;
    }
}
