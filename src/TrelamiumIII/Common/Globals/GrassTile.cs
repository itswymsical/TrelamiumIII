// Adapted from Spirit: Reforged, by Team Spirit
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/TileCommon/PresetTiles/GrassTile.cs

using Terraria.ObjectData;
using System.Linq;
using System.Collections.Generic;

namespace TrelamiumIII.Common.Globals;

public abstract class GrassTile : ModTile
{
    protected virtual int DirtType => TileID.Dirt;
    /// <summary>Spread chance per random update; lower values spread more often.</summary>
    public virtual int spreadChance => 2;

    public virtual bool CanGrowPlants => true;

    public virtual List<int> PlantTypes => [TileID.Plants];
    protected void AllowAnchor(params int[] types)
    {
        foreach (var type in types)
        {
            var data = TileObjectData.GetTileData(type, 0);
            if (data != null)
                data.AnchorValidTiles = data.AnchorValidTiles.Concat([Type]).ToArray();
        }
    }

    /// <summary>
    /// <inheritdoc/><para/>Also automatically controls common grass tile settings.
    /// </summary>
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileBlendAll[Type] = true;

        MergeGrass(DirtType, TileID.Grass);
        AllowAnchor(TileID.Sunflower);

        TileID.Sets.Grass[Type] = true;
        TileID.Sets.CanBeDugByShovel[Type] = true;
        TileID.Sets.NeedsGrassFramingDirt[Type] = DirtType;
        TileID.Sets.NeedsGrassFraming[Type] = true;
    }
    public override void ModifyFrameMerge(int i, int j, ref int up, ref int down, ref int left, ref int right, ref int upLeft, ref int upRight, ref int downLeft, ref int downRight)
    {
        base.ModifyFrameMerge(i, j, ref up, ref down, ref left, ref right, ref upLeft, ref upRight, ref downLeft, ref downRight);
        // todo: figure out how vanilla does grass framing
    }
    public override void RandomUpdate(int i, int j)
    {
        if (!Main.rand.NextBool(spreadChance)) return;

        int[] directions = { -1, 1 };
        foreach (int xDir in directions)
        {
            int x = i + xDir;
            if (x < 0 || x >= Main.maxTilesX) continue;

            foreach (int yDir in directions)
            {
                int y = j + yDir;
                if (y < 0 || y >= Main.maxTilesY) continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.TileType == DirtType)
                {
                    if (!Main.tile[x, y - 1].HasTile || !Main.tileSolid[Main.tile[x, y - 1].TileType])
                    {
                        tile.TileType = (ushort)Type;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendTileSquare(-1, x, y, 1);
                    }
                }
            }
        }

        if (CanGrowPlants && !Main.tile[i, j - 1].HasTile && Main.rand.NextBool(8))
        {
            int plantType = PlantTypes[Main.rand.Next(PlantTypes.Count)];
            WorldGen.PlaceTile(i, j - 1, plantType, true, style: Main.rand.Next(18));

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, i, j - 1, 1);
        }
    }

    public override bool CanExplode(int i, int j)
    {
        WorldGen.KillTile(i, j, false, false, true); //Makes the tile completely go away instead of reverting to dirt
        return true;
    }

    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!effectOnly) //Change self into dirt
        {
            fail = true;
            Framing.GetTileSafely(i, j).TileType = (ushort)DirtType;
        }
    }
    public void MergeGrass(params int[] otherIds)
    {
        foreach (int id in otherIds)
        {
            Main.tileMerge[Type][id] = true;
            Main.tileMerge[id][Type] = true;
        }
    }
}