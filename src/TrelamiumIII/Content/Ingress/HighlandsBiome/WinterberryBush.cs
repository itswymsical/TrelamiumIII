<<<<<<< HEAD
﻿using System.Collections.Generic;
using ReLogic.Content;
using Terraria.ObjectData;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary> Harvested from a ripe bush and planted to grow another. </summary>
public class WinterberryItem : ModItem
{
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 100;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<WinterberryBushTile>());
        Item.value = Item.sellPrice(copper: 15);
    }
}

/// <summary>
/// A berry bush. The sheet's first frames are bare bushes in different shapes, picked by position; its later, baked frames
/// are only used by <see cref="ForegroundBushTile"/>. Growth shows as berries drawn over the bare bush from the extras sheet:
/// none when planted, scattered buds, then every berry when ripe. A snow cap goes over the top on snowy grass.
/// </summary>
public class WinterberryBushTile : CustomHerb
{
    private const int Budding = 1;
    private const int Ripe = 2;

    private const int Shapes = 3;

    /// <summary> Spacing of the frames on both sheets. </summary>
    private const int CellSize = 34;
    private const int FrameWidth = 32;

    /// <summary> The extras sheet holds a row of snow caps above a row of berries, one per shape. </summary>
    private const int SnowRow = 0;
    private const int BerryRow = CellSize;

    /// <summary> The berry row sits this far right of where its berries go on the bush. </summary>
    private const int BerryShift = 2;

    private const float BudScale = 0.75f;

    private static Asset<Texture2D>? extras;
    private static Rectangle[][]? berries;

    public override string Texture => AssetReferences.Content.Ingress.HighlandsBiome.WinterberryBush.KEY;

    public override int GrowthChance => 8;

    protected override int HarvestItem => ModContent.ItemType<WinterberryItem>();

    protected override Color MapColor => new(48, 116, 58);

    protected override bool HasOverlays => true;

    protected override int[] GetAnchorTiles() =>
    [
        ModContent.TileType<HighlandsGrass>(),
        ModContent.TileType<SnowHighlandsGrass>(),
        ModContent.TileType<PodzolGrass>(),
    ];

    protected override void PreAddObjectData()
    {
        TileObjectData.newTile.WaterDeath = false;
        TileObjectData.newTile.CoordinateWidth = FrameWidth;
        TileObjectData.newTile.CoordinateHeights = [CellSize];
        TileObjectData.newTile.DrawYOffset = -14;
    }

=======
﻿using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class WinterberryBushTile : ModTile
{
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

<<<<<<< HEAD
        DustType = DustID.GrassBlades;
    }

    public override void Unload()
    {
        extras = null;
        berries = null;
    }

    /// <summary> Berries only come from a ripe bush, and planting them is the only way to grow more. </summary>
    protected override void RollYield(int stage, bool regrowth, out int harvest, out int seeds)
    {
        seeds = 0;
        harvest = stage < Ripe ? 0 : regrowth ? Main.rand.Next(3, 6) : Main.rand.Next(2, 4);
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
        => tileFrameX = (short)(i % Shapes * CellSize);

    protected override void DrawOverlays(int i, int j, SpriteBatch spriteBatch, HerbDraw draw)
    {
        extras ??= ModContent.Request<Texture2D>(AssetReferences.Content.Ingress.HighlandsBiome.WinterberryBush_Extras.KEY);

        if (!extras.IsLoaded)
            return;

        Texture2D sheet = extras.Value;
        int shape = i % Shapes;
        int stage = GetStage(i, j);

        if (stage == Budding)
            DrawBuds(i, shape, sheet, spriteBatch, draw);
        else if (stage == Ripe)
            spriteBatch.Draw(sheet, draw.Root, new Rectangle(shape * CellSize + BerryShift, BerryRow, FrameWidth, CellSize), draw.Light, draw.Rotation, draw.Origin, 1f, draw.Effects, 0f);

        Tile below = Framing.GetTileSafely(i, j + 1);

        if (below.HasTile && below.TileType == ModContent.TileType<SnowHighlandsGrass>())
            spriteBatch.Draw(sheet, draw.Root, new Rectangle(shape * CellSize, SnowRow, FrameWidth, CellSize), draw.Light, draw.Rotation, draw.Origin, 1f, draw.Effects, 0f);
    }

    /// <summary> Every other berry, shrunk where it sits on the bush. Which half depends on the column, so neighbours differ. </summary>
    private static void DrawBuds(int i, int shape, Texture2D sheet, SpriteBatch spriteBatch, HerbDraw draw)
    {
        berries ??= FindBerries(sheet);

        Rectangle[] sources = berries[shape];
        var frame = new Vector2(shape * CellSize + BerryShift, BerryRow);

        for (int k = i % 2; k < sources.Length; k += 2)
        {
            Rectangle source = sources[k];
            Vector2 offset = source.Center() - frame - draw.Origin;

            if (draw.Effects.HasFlag(SpriteEffects.FlipHorizontally))
                offset.X = -offset.X;

            spriteBatch.Draw(sheet, draw.Root + offset.RotatedBy(draw.Rotation), source, draw.Light, draw.Rotation, source.Size() / 2f, BudScale, draw.Effects, 0f);
        }
    }

    /// <summary> Each separate clump of pixels in the berry row, per shape. Reads the texture, so it runs from drawing rather than loading. </summary>
    private static Rectangle[][] FindBerries(Texture2D sheet)
    {
        var pixels = new Color[sheet.Width * sheet.Height];
        sheet.GetData(pixels);

        var found = new Rectangle[Shapes][];

        for (int shape = 0; shape < Shapes; shape++)
            found[shape] = FindClumps(pixels, sheet.Width, new Rectangle(shape * CellSize, BerryRow, CellSize, CellSize));

        return found;
    }

    private static Rectangle[] FindClumps(Color[] pixels, int stride, Rectangle area)
    {
        var clumps = new List<Rectangle>();
        var seen = new HashSet<Point>();
        var open = new Stack<Point>();

        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                if (!seen.Add(new Point(x, y)) || pixels[y * stride + x].A == 0)
                    continue;

                int left = x, right = x, top = y, bottom = y;
                open.Push(new Point(x, y));

                while (open.TryPop(out Point pixel))
                {
                    left = Math.Min(left, pixel.X);
                    right = Math.Max(right, pixel.X);
                    top = Math.Min(top, pixel.Y);
                    bottom = Math.Max(bottom, pixel.Y);

                    foreach (Point next in (Point[])[new(pixel.X + 1, pixel.Y), new(pixel.X - 1, pixel.Y), new(pixel.X, pixel.Y + 1), new(pixel.X, pixel.Y - 1)])
                    {
                        if (area.Contains(next) && pixels[next.Y * stride + next.X].A > 0 && seen.Add(next))
                            open.Push(next);
                    }
                }

                clumps.Add(new Rectangle(left, top, right - left + 1, bottom - top + 1));
            }
        }

        return clumps.ToArray();
=======
        Main.tileCut[Type] = true;
        Main.tileSolid[Type] = false;
        Main.tileFrameImportant[Type] = true;

        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<HighlandsGrass>(),
            ModContent.TileType<SnowHighlandsGrass>(),
        ];

        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.WaterDeath = false;

        TileObjectData.newTile.StyleHorizontal = true;

        TileObjectData.newTile.CoordinateWidth = 32;
        TileObjectData.newTile.CoordinateHeights = [34];
        TileObjectData.newTile.DrawYOffset = (-14);
        //TileObjectData.newTile.RandomStyleRange = 12;
        TileObjectData.addTile(Type);

        DustType = DustID.GrassBlades;
        HitSound = SoundID.Grass;

        AddMapEntry(new Color(48, 116, 58));
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }
}
