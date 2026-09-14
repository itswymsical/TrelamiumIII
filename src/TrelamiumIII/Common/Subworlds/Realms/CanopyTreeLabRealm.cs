using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Trees;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// Flat prototyping world for <see cref="CanopyTreeGenerator"/>: one tree at the centre, locked to noon, regenerated
/// on every entry. <c>/canopytree</c> regrows the tree in place.
/// </summary>
public class CanopyTreeLabRealm : SubworldRealm
{
    private const int DirtDepth = 60;
    private const int SpawnDistance = 160;

    private static Rectangle treeArea;

    public override int Width => 1200;
    public override string Name => "CanopyTreeLab";
    public override int Height => 1000;

    public override List<GenPass> Tasks => [new CanopyTreeLabPass()];

    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;

    /// <summary> Surface row of the lab's flat ground. </summary>
    public static int GroundY => (int)(Main.maxTilesY * 0.52f);

    public static int Seed { get; private set; }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    public override void Update()
    {
        Main.dayTime = true;
        Main.time = Main.dayLength / 2;
    }

    /// <summary> Grows the lab tree from <paramref name="seed"/> on terrain that must already be flat. </summary>
    public static void GrowTree(int seed)
    {
        Seed = seed;
        treeArea = CanopyTreeGenerator.Grow(new Point(Main.maxTilesX / 2, GroundY), seed);
    }

    /// <summary> Flattens the previous tree's area, grows a new tree and reframes everything that changed. </summary>
    public static void Regrow(int seed)
    {
        Rectangle previous = treeArea;
        ResetTerrain(previous);
        GrowTree(seed);

        Rectangle changed = previous.IsEmpty ? treeArea : Rectangle.Union(previous, treeArea);
        WorldGen.RangeFrame(changed.Left, changed.Top, changed.Right, changed.Bottom);
    }

    /// <summary> Air above <see cref="GroundY"/>, a grass row on it, then dirt and stone with dirt walls. </summary>
    public static void ResetTerrain(Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));
        int groundY = GroundY;

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                tile.ClearEverything();

                if (y < groundY)
                    continue;

                tile.HasTile = true;
                tile.TileType = y == groundY ? TileID.Grass : y < groundY + DirtDepth ? TileID.Dirt : TileID.Stone;

                if (y > groundY + 1)
                    tile.WallType = WallID.DirtUnsafe;
            }
        }
    }

    private class CanopyTreeLabPass : GenPass
    {
        public CanopyTreeLabPass() : base("[T3] Canopy Tree Lab", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Growing the canopy tree";

            Main.worldSurface = GroundY + 20;
            Main.rockLayer = GroundY + DirtDepth;
            Main.spawnTileX = Main.maxTilesX / 2 - SpawnDistance;
            Main.spawnTileY = GroundY - 1;

            treeArea = Rectangle.Empty;
            ResetTerrain(new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));
            GrowTree(WorldGen.genRand.Next());
        }
    }
}
