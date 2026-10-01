using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Content.Items.Alchemy.MagnoliaContent;
using TrelamiumIII.Content.Items.Alchemy.Reagents.Misc;
using TrelamiumIII.Content.Items.Alchemy.Tiles;
using TrelamiumIII.Content.Tiles.Desert;

namespace TrelamiumIII.Common.Generation;

public class SurfacePlantPass : GenPass
{
    public SurfacePlantPass() : base("[Substrate] Surface Plants", 30f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Planting vegetation...";

        const int boundary = 100;

        for (int x = boundary; x < Main.maxTilesX - boundary; x++)
        {
            for (int y = (int)Main.worldSurface - 300; y < (int)Main.worldSurface + 150; y++)
            {
                if (WorldGen.genRand.NextBool(50))
                {
                    GrowFlyAraric(x, y);
                }
                if (WorldGen.genRand.NextBool(75))
                {
                    GrowMagnolia(x, y);
                }
                if (WorldGen.genRand.NextBool(80))
                {
                    GrowBloodtoothFungus(x, y);
                }
                if (WorldGen.genRand.NextBool(125))
                {
                    GrowDeadBush(x, y);
                }
            }
            for (int y = 0; y < (int)Main.worldSurface - 150; y++)
            {
                if (WorldGen.genRand.NextBool(50))
                {
                    GrowDewdropBonnet(x, y);
                }
            }
        }
    }

    private void GrowFlyAraric(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        Tile tileAbove = Framing.GetTileSafely(x, y - 1);

        if (!tile.HasTile) return;

        bool validTile = 
            tile.TileType == TileID.Grass || tile.TileType == TileID.Dirt;

        if (!validTile) return;

        if (validTile && !tileAbove.HasTile && !tile.LeftSlope && !tile.RightSlope && !tile.IsHalfBlock)
        {
            if (HasSpace_Plant(x, y))
            {
                WorldGen.PlaceTile(x, y - 1, ModContent.TileType<FlyAgaricTile>(), mute: true);
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                }
            }
        }
    }

    private void GrowDewdropBonnet(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        Tile tileAbove = Framing.GetTileSafely(x, y - 1);

        if (!tile.HasTile) return;

        bool validTile =
            tile.TileType == TileID.Cloud || tile.TileType == TileID.RainCloud || tile.TileType == TileID.SnowCloud;

        if (!validTile) return;

        if (validTile && !tileAbove.HasTile && !tile.LeftSlope && !tile.RightSlope && !tile.IsHalfBlock)
        {
            if (HasSpace_Plant(x, y))
            {
                WorldGen.PlaceTile(x, y - 1, ModContent.TileType<DewdropBonnetTile>(), mute: true);
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                }
            }
        }
    }
    private void GrowDeadBush(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        Tile tileAbove = Framing.GetTileSafely(x, y - 1);

        if (!tile.HasTile) return;

        bool validTile =
            tile.TileType == TileID.Sand || tile.TileType == TileID.HardenedSand 
            || tile.TileType == TileID.Ebonsand || tile.TileType == TileID.Crimsand;

        if (!validTile) return;

        if (validTile && !tileAbove.HasTile && !tile.LeftSlope && !tile.RightSlope && !tile.IsHalfBlock)
        {
            if (HasSpace_Plant(x, y))
            {
                WorldGen.PlaceTile(x, y - 1, ModContent.TileType<DeadBushTile>(), mute: true, style: Main.rand.Next(3));
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                }
            }
        }
    }

    private void GrowMagnolia(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        Tile tileAbove = Framing.GetTileSafely(x, y - 1);

        if (!tile.HasTile) return;

        bool validTile =
            tile.TileType == TileID.Grass || tile.TileType == TileID.CorruptGrass || tile.TileType == TileID.JungleGrass;

        if (!validTile) return;

        if (validTile && !tileAbove.HasTile && !tile.LeftSlope && !tile.RightSlope && !tile.IsHalfBlock)
        {
            if (HasSpace_Plant(x, y))
            {
                WorldGen.PlaceTile(x, y - 1, ModContent.TileType<MagnoliaTile>(), mute: true);
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                }
            }
        }
    }

    private void GrowBloodtoothFungus(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        Tile tileAbove = Framing.GetTileSafely(x, y - 1);
        if (!tile.HasTile) return;
        bool validTile =
            tile.TileType == TileID.CrimsonGrass || tile.type == TileID.CorruptGrass;
        if (!validTile) return;
        if (validTile && !tileAbove.HasTile && !tile.LeftSlope && !tile.RightSlope && !tile.IsHalfBlock)
        {
            if (Main.rand.NextBool(2))
            {
                if (HasSpace_Plant(x, y))
                {
                    WorldGen.PlaceTile(x, y - 1, ModContent.TileType<BloodtoothFungusTileSmall>(), mute: true);
                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                    }
                }
            }
            else
            {
                if (HasSpace_Plant(x, y))
                {
                    WorldGen.PlaceTile(x, y - 1, ModContent.TileType<BloodtoothFungusTileSpores>(), mute: true);
                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                    }
                }
            }

            if (HasSpace_3x2(x, y))
            {
                WorldGen.PlaceTile(x, y - 1, ModContent.TileType<BloodtoothFungusTileLarge>(), mute: true);
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, x, y - 1, 1, TileChangeType.None);
                }
            }
        }
    }

    private bool HasSpace_3x2(int x, int y)
    {
        for (int i = x - 1; i <= x + 1; i++)
        {
            for (int j = y - 2; j <= y - 1; j++)
            {
                if (!WorldGen.InWorld(i, j)) return false;
                Tile checkTile = Framing.GetTileSafely(i, j);
                if (checkTile.HasTile) return false;
            }
        }
        return true;
    }

    private bool HasSpace_Plant(int x, int y)
    {
        if (!WorldGen.InWorld(x, y - 1)) return false;
        Tile checkTile = Framing.GetTileSafely(x, y - 1);

        return !checkTile.HasTile || checkTile.TileType == TileID.Plants2;
    }
}
