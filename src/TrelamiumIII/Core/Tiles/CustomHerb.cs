using System.Collections.Generic;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Metadata;
using Terraria.ObjectData;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;

namespace TrelamiumIII.Core.Tiles;

/// <summary>
/// A plant that works like a vanilla herb: it grows through stages on random updates and roots in planter boxes, clay pots
/// and seed beds as well as its own ground. Seed beds speed it up, Magnolia Powder and <see cref="FarmingSystem.TryGrowHerb"/>
/// advance it, a grown one can be replanted over, and what it drops depends on its stage. Also the base for bushes, shrubs
/// and other botanica. Frame X holds the stage, and the sheet has one frame per stage unless
/// <see cref="ModTile.SetDrawPositions"/> picks another; frame Y is unused.
/// </summary>
public abstract class CustomHerb : ModTile
{
    private int frameWidth;

    /// <summary> Growth stages including the planted and grown ones. A plant in its first stage drops nothing. </summary>
    public virtual int StageCount => 3;

    /// <summary> 1 in this many random updates advances a stage. On a seed bed it rolls the bed's attempts, at the better of the two chances. </summary>
    public virtual int GrowthChance => 1;

    /// <summary> The item this plant is grown for. </summary>
    protected abstract int HarvestItem { get; }

    /// <summary> The item that plants it, if that isn't <see cref="HarvestItem"/> itself. </summary>
    protected virtual int SeedItem => ItemID.None;

    protected abstract Color MapColor { get; }

    /// <summary> Whether the plant draws through <see cref="SpecialDraw"/> so that <see cref="DrawOverlays"/> can sway with it. </summary>
    protected virtual bool HasOverlays => false;

    /// <summary> Ground it grows on; read once while loading. Planter boxes, clay pots and seed beds are always allowed. </summary>
    protected abstract int[] GetAnchorTiles();

    /// <summary> Called just before <see cref="TileObjectData.addTile"/>, to adjust the copy of <see cref="TileObjectData.StyleAlch"/>. </summary>
    protected virtual void PreAddObjectData() { }

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] =
            Main.tileObsidianKill[Type] =
            Main.tileCut[Type] =
            Main.tileNoFail[Type] = true;

        // Not Main.tileAlch, which hands the tile to vanilla herb logic and stops ReplaceTileBreakUp working.
        TileID.Sets.ReplaceTileBreakUp[Type] =
            TileID.Sets.IgnoredInHouseScore[Type] =
            TileID.Sets.IgnoredByGrowingSaplings[Type] =
            TileID.Sets.SwaysInWindBasic[Type] = true;

        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        AddMapEntry(MapColor, CreateMapEntryName());

        TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
        TileObjectData.newTile.AnchorValidTiles = GetAnchorTiles();
        TileObjectData.newTile.AnchorAlternateTiles = [TileID.ClayPot, TileID.PlanterBox, .. SeedBedItem.TileTypes];
        PreAddObjectData();
        frameWidth = TileObjectData.newTile.CoordinateWidth + TileObjectData.newTile.CoordinatePadding;
        TileObjectData.addTile(Type);

        HitSound = SoundID.Grass;
    }

    public int GetStage(int i, int j) => Math.Min(Main.tile[i, j].TileFrameX / frameWidth, StageCount - 1);

    public bool IsGrown(int i, int j) => GetStage(i, j) == StageCount - 1;

    /// <summary> Moves the plant at (<paramref name="i"/>, <paramref name="j"/>) on a stage; false if it's already grown. </summary>
    public bool TryAdvance(int i, int j)
    {
        if (IsGrown(i, j))
            return false;

        Tile tile = Main.tile[i, j];
        tile.TileFrameX = (short)(tile.TileFrameX + frameWidth);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, i, j, 1);

        return true;
    }

    public override void RandomUpdate(int i, int j)
    {
        int attempts = 1;
        int chance = GrowthChance;
        Tile below = Framing.GetTileSafely(i, j + 1);

        if (below.HasTile && FarmingSystem.SeedBedTypes.TryGetValue(below.TileType, out SeedBedTile? bed))
        {
            attempts = bed.GrowthAttempts;
            chance = Math.Min(chance, bed.GrowthChance);
        }

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (WorldGen.genRand.NextBool(chance))
            {
                TryAdvance(i, j);
                return;
            }
        }
    }

    /// <summary> Replaces foliage and grown plants, breaking them first as vanilla seeds do. A plant still growing stays. </summary>
    public override bool CanPlace(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);

        if (!tile.HasTile)
            return true;

        int tileType = tile.TileType;

        if (TileLoader.GetTile(tileType) is CustomHerb herb && !herb.IsGrown(i, j))
            return false;

        if (tileType == Type)
            return true;

        if (!Main.tileCut[tileType] && !TileID.Sets.BreakableWhenPlacing[tileType] && tileType is not (TileID.WaterDrip or TileID.LavaDrip or TileID.HoneyDrip or TileID.SandDrip))
            return false;

        bool foliageGrass = tileType is TileID.Plants or TileID.Plants2;
        bool moddedFoliage = tileType >= TileID.Count && (Main.tileCut[tileType] || TileID.Sets.BreakableWhenPlacing[tileType]);
        bool harvestableVanillaHerb = Main.tileAlch[tileType] && WorldGen.IsHarvestableHerbWithSeed(tileType, tile.TileFrameX / 18);

        if (!foliageGrass && !moddedFoliage && !harvestableVanillaHerb)
            return false;

        WorldGen.KillTile(i, j);

        if (!tile.HasTile && Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, i, j);

        return true;
    }

    public override bool CanDrop(int i, int j) => GetStage(i, j) > 0;

    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        Player nearest = Main.player[Player.FindClosest(new Vector2(i, j).ToWorldCoordinates(), 16, 16)];
        bool regrowth = nearest.active && nearest.HeldItem.type is ItemID.StaffofRegrowth or ItemID.AcornAxe;

        RollYield(GetStage(i, j), regrowth, out int harvest, out int seeds);

        if (HarvestItem > ItemID.None && harvest > 0)
            yield return new Item(HarvestItem, harvest);

        if (SeedItem > ItemID.None && seeds > 0)
            yield return new Item(SeedItem, seeds);
    }

    /// <summary> Stacks dropped at <paramref name="stage"/>, past the first; <paramref name="regrowth"/> means a Staff of Regrowth or Acorn Axe is held. </summary>
    protected virtual void RollYield(int stage, bool regrowth, out int harvest, out int seeds)
    {
        harvest = regrowth ? Main.rand.Next(1, 3) : 1;
        seeds = regrowth ? Main.rand.Next(3, 5) : stage == StageCount - 1 ? 2 : 1;
    }

    public override bool IsTileSpelunkable(int i, int j) => IsGrown(i, j);

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 0)
            spriteEffects = SpriteEffects.FlipHorizontally;
    }

    /// <summary> Plants without overlays draw through vanilla's swaying grass pass. </summary>
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        if (!HasOverlays)
            return true;

        Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
        return false;
    }

    /// <summary> Vanilla's swaying grass pass, which can't draw anything over a tile, with <see cref="DrawOverlays"/> added. Drawn to the screen, so no off-screen offset. </summary>
    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        TileDrawing renderer = Main.instance.TilesRenderer;
        Tile tile = Main.tile[i, j];

        if (!tile.HasTile || !TileDrawing.IsVisible(tile))
            return;

        short frameX = tile.TileFrameX;
        short frameY = tile.TileFrameY;
        renderer.GetTileDrawData(i, j, tile, Type, ref frameX, ref frameY, out int width, out int height, out int top, out _, out _, out _, out SpriteEffects effects, out _, out _, out _);

        float wind = WallID.Sets.AllowsWind[tile.WallType] && renderer.InAPlaceWithWind(i, j, 1, 1) ? renderer.GetWindCycle(i, j, TileWindSystem.GrassCounter) : 0f;
        wind += renderer.GetWindGridPush(i, j, 20, 0.35f);

        Color light = Lighting.GetColor(i, j);
        renderer.DrawAnimatedTile_AdjustForVisionChangers(i, j, tile, Type, frameX, frameY, ref light, Main.rand.NextBool(4));

        var root = new Vector2(i * 16 + 8 + wind - (int)Main.screenPosition.X, j * 16 + 16 + MathF.Abs(wind) - (int)Main.screenPosition.Y);
        var draw = new HerbDraw(root, wind * 0.1f, new Vector2(width / 2, 16 - top), effects, light);

        if (renderer.GetTileDrawTexture(tile, i, j) is { } texture)
            spriteBatch.Draw(texture, draw.Root, new Rectangle(frameX, frameY, width, height), draw.Light, draw.Rotation, draw.Origin, 1f, draw.Effects, 0f);

        DrawOverlays(i, j, spriteBatch, draw);
    }

    /// <summary> Draws over the plant when <see cref="HasOverlays"/> is set. A sprite the size of a frame lines up when drawn with <paramref name="draw"/> as is. </summary>
    protected virtual void DrawOverlays(int i, int j, SpriteBatch spriteBatch, HerbDraw draw) { }

    /// <summary> Where the plant is this frame: <paramref name="Root"/> is the bottom middle of its tile as it sways, and <paramref name="Origin"/> maps a frame onto it. </summary>
    protected readonly record struct HerbDraw(Vector2 Root, float Rotation, Vector2 Origin, SpriteEffects Effects, Color Light);
}
