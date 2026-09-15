using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Metadata;
using Terraria.Localization;
using Terraria.Utilities;
using TrelamiumIII.Common.Globals;

namespace TrelamiumIII.Core.Tiles;

/// <summary>
/// A tree grown as a one-tile-wide trunk column. Every trunk tile draws a random frame from <see cref="Sheet"/>, and the top
/// tile also draws the canopy and its leaf clumps every frame so they sway. Frame X holds the trunk frame index; frame Y
/// is <see cref="CanopyFrameY"/> on the tile carrying the canopy, so a felled tree's stump stays bare.
/// </summary>
public abstract class CustomTree : ModTile
{
    protected const short CanopyFrameY = 1;

    private const float CanopySway = 0.08f;
    private const float CanopyShift = 2f;
    private const float LeafSway = 0.12f;
    private const int WorldMargin = 20;

    /// <summary> Set while <see cref="Place"/> writes tiles, so <see cref="TileFrame"/> doesn't reject the half-built column. </summary>
    private static bool growing;

    private int[] anchorTiles = [];

    public abstract TreeSheet Sheet { get; }

    /// <summary> Trunk tiles, including the canopy tile. </summary>
    public virtual int MinHeight => 5;

    public virtual int MaxHeight => 10;

    public virtual int WoodType => ItemID.Wood;

    /// <summary> 1 in this many axe hits that don't break a tile shake the tree. </summary>
    public virtual int ShakeChance => 8;

    /// <summary> Tiles the trunk can stand on; read once while loading. The anchor also has to be a full solid block. </summary>
    protected abstract int[] GetAnchorTiles();

    public override void SetStaticDefaults()
    {
        anchorTiles = GetAnchorTiles();

        Main.tileSolid[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;
        Main.tileAxe[Type] = true;

        TileID.Sets.IsATreeTrunk[Type] = true;
        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
        TileID.Sets.PreventsTileReplaceIfOnTopOfIt[Type] = true;
        TileID.Sets.PreventsTileHammeringIfOnTopOfIt[Type] = true;
        TileID.Sets.GetsDestroyedForMeteors[Type] = true;
        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        AddMapEntry(new Color(151, 107, 75), Language.GetText("MapObject.Tree"));
        DustType = DustID.WoodFurniture;
        HitSound = SoundID.Dig;
    }

    public static bool TryGrow<T>(int x, int baseY) where T : CustomTree => ModContent.GetInstance<T>().TryGrow(x, baseY);

    /// <summary>
    /// Grows a tree whose bottom trunk tile is (<paramref name="x"/>, <paramref name="baseY"/>), rolling a height and
    /// falling back to shorter ones down to <see cref="MinHeight"/>. The trunk column may only hold saplings, plants or
    /// other cuttable tiles, which are replaced; the canopy's tiles must be free of solid blocks and tree trunks.
    /// </summary>
    public bool TryGrow(int x, int baseY)
    {
        if (!WorldGen.InWorld(x, baseY, WorldMargin) || !IsAnchor(x, baseY + 1))
            return false;

        for (int height = WorldGen.genRand.Next(MinHeight, MaxHeight + 1); height >= MinHeight; height--)
        {
            if (!HasRoom(x, baseY, height))
                continue;

            Place(x, baseY, height);
            return true;
        }

        return false;
    }

    private bool HasRoom(int x, int baseY, int height)
    {
        int topY = baseY - height + 1;

        if (!WorldGen.InWorld(x, topY, WorldMargin))
            return false;

        for (int y = topY; y <= baseY; y++)
        {
            if (!IsReplaceable(x, y) || IsTrunk(x - 1, y) || IsTrunk(x + 1, y))
                return false;
        }

        Rectangle canopy = CanopyTiles(x, topY);

        for (int canopyX = canopy.Left; canopyX < canopy.Right; canopyX++)
        {
            for (int canopyY = canopy.Top; canopyY < canopy.Bottom; canopyY++)
            {
                if (!WorldGen.InWorld(canopyX, canopyY, WorldMargin))
                    return false;

                Tile tile = Main.tile[canopyX, canopyY];

                if (tile.HasTile && (Main.tileSolid[tile.TileType] || TileID.Sets.IsATreeTrunk[tile.TileType]))
                    return false;
            }
        }

        return true;
    }

    /// <summary> Tile rows above the top trunk tile at (<paramref name="x"/>, <paramref name="topY"/>) that the canopy covers. </summary>
    private Rectangle CanopyTiles(int x, int topY)
    {
        TreeCanopy canopy = Sheet.Canopy;
        float left = x * 16 + 8 - canopy.Anchor.X;
        float top = topY * 16 + canopy.Overlap - canopy.Anchor.Y;

        int tileLeft = (int)MathF.Floor(left / 16f);
        int tileTop = (int)MathF.Floor(top / 16f);
        int tileRight = (int)MathF.Ceiling((left + canopy.Source.Width) / 16f);

        return new Rectangle(tileLeft, tileTop, tileRight - tileLeft, Math.Max(topY - tileTop, 0));
    }

    private void Place(int x, int baseY, int height)
    {
        int topY = baseY - height + 1;
        growing = true;

        try
        {
            for (int y = baseY; y >= topY; y--)
            {
                if (Main.tile[x, y].HasTile)
                    WorldGen.KillTile(x, y, noItem: true);

                Tile tile = Main.tile[x, y];
                tile.HasTile = true;
                tile.TileType = Type;
                tile.TileFrameX = (short)WorldGen.genRand.Next(Sheet.Trunk.Count);
                tile.TileFrameY = y == topY ? CanopyFrameY : (short)0;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
            }
        }
        finally
        {
            growing = false;
        }

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, x, topY, 1, height);

        if (!WorldGen.gen && !Main.dedServ && WorldGen.PlayerLOS(x, topY))
            LeafBurst(x, topY);
    }

    private bool IsAnchor(int x, int y) => WorldGen.SolidTile(x, y) && Array.IndexOf(anchorTiles, (int)Main.tile[x, y].TileType) >= 0;

    private static bool IsTrunk(int x, int y) => Main.tile[x, y].HasTile && TileID.Sets.IsATreeTrunk[Main.tile[x, y].TileType];

    /// <summary> Empty and dry, or a sapling, plant or other tile that breaks when something is placed over it. </summary>
    private static bool IsReplaceable(int x, int y)
    {
        Tile tile = Main.tile[x, y];

        if (tile.LiquidAmount > 0)
            return false;

        return !tile.HasTile || TileLoader.GetTile(tile.TileType) is SaplingTile || Main.tileCut[tile.TileType] || TileID.Sets.BreakableWhenPlacing[tile.TileType];
    }

    /// <summary> A trunk tile without a trunk or anchor below breaks, and killing it reframes the tile above, so the rest of the column falls with it. </summary>
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        if (!WorldGen.gen && !growing && !(Main.tile[i, j + 1].HasTile && Main.tile[i, j + 1].TileType == Type) && !IsAnchor(i, j + 1))
            WorldGen.KillTile(i, j);

        return false;
    }

    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        yield return new Item(WoodType, Main.rand.Next(1, 3));
    }

    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!fail || effectOnly || !WorldGen.genRand.NextBool(ShakeChance) || !TryFindCanopy(i, j, out int canopyY))
            return;

        if (!Main.dedServ)
            LeafBurst(i, canopyY);

        if (Main.netMode != NetmodeID.MultiplayerClient && GetShakeDrops().Get() is > ItemID.None and int drop)
            Item.NewItem(new EntitySource_ShakeTree(i, canopyY), new Vector2(i, canopyY - 2) * 16f, drop);
    }

    protected virtual WeightedRandom<int> GetShakeDrops()
    {
        var drops = new WeightedRandom<int>();
        drops.Add(ItemID.None, 0.7f);
        drops.Add(ItemID.Acorn, 0.2f);
        drops.Add(WoodType, 0.08f);
        drops.Add(ItemID.LifeCrystal, 0.02f);
        return drops;
    }

    /// <summary> Leaf dust across the canopy above the trunk tile at (<paramref name="i"/>, <paramref name="canopyY"/>). </summary>
    protected virtual void LeafBurst(int i, int canopyY)
    {
        TreeCanopy canopy = Sheet.Canopy;
        var center = new Vector2(i * 16 + 8, canopyY * 16 + canopy.Overlap - canopy.Anchor.Y * 0.5f);

        for (int k = 0; k < 12; k++)
        {
            Vector2 offset = Main.rand.NextVector2Circular(canopy.Source.Width * 0.4f, canopy.Anchor.Y * 0.4f);
            Dust dust = Dust.NewDustDirect(center + offset, 0, 0, DustID.GrassBlades, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 1f));
            dust.fadeIn = 1.2f;
        }
    }

    private bool TryFindCanopy(int i, int j, out int canopyY)
    {
        for (canopyY = j; canopyY > WorldMargin && Main.tile[i, canopyY].HasTile && Main.tile[i, canopyY].TileType == Type; canopyY--)
        {
            if (Main.tile[i, canopyY].TileFrameY == CanopyFrameY)
                return true;
        }

        return false;
    }

    /// <summary> Draws the trunk frame into the tile target; the canopy tile also registers for <see cref="SpecialDraw"/>, which runs every frame. </summary>
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        TrunkFrames trunk = Sheet.Trunk;
        Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        var position = new Vector2(i * 16 + trunk.Offset.X - (int)Main.screenPosition.X, j * 16 + trunk.Offset.Y - (int)Main.screenPosition.Y);

        spriteBatch.Draw(TextureAssets.Tile[Type].Value, position + offscreen, trunk.Source(tile.TileFrameX), Lighting.GetColor(i, j), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        if (tile.TileFrameY == CanopyFrameY)
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);

        return false;
    }

    /// <summary>
    /// The canopy rocks around its anchor like a vanilla tree top; leaf clumps follow the canopy and add their own sway
    /// on the faster grass wind counter. Drawn to the screen, so no off-screen offset.
    /// </summary>
    public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
    {
        TreeSheet sheet = Sheet;
        TreeCanopy canopy = sheet.Canopy;
        Texture2D texture = TextureAssets.Tile[Type].Value;

        float wind = Main.instance.TilesRenderer.GetWindCycle(i, j, TileSwaySystem.Instance.TreeWindCounter);
        float rotation = wind * CanopySway;
        var anchor = new Vector2(
            i * 16 + 8 + wind * CanopyShift - (int)Main.screenPosition.X,
            j * 16 + canopy.Overlap + MathF.Abs(wind) * CanopyShift - (int)Main.screenPosition.Y);

        Color light = Lighting.GetColor(i, j - (int)((canopy.Anchor.Y - canopy.Overlap) / 32f));

        spriteBatch.Draw(texture, anchor, canopy.Source, light, rotation, canopy.Anchor, 1f, SpriteEffects.None, 0f);

        for (int k = 0; k < sheet.Leaves.Length; k++)
        {
            TreeLeaf leaf = sheet.Leaves[k];
            Vector2 mount = anchor + (leaf.Mount - canopy.Anchor).RotatedBy(rotation);
            float leafWind = Main.instance.TilesRenderer.GetWindCycle(i + k * 3 + 1, j, TileSwaySystem.Instance.GrassWindCounter);

            spriteBatch.Draw(texture, mount, leaf.Source, light, rotation + leafWind * LeafSway, leaf.Pivot, 1f, SpriteEffects.None, 0f);
        }
    }
}
