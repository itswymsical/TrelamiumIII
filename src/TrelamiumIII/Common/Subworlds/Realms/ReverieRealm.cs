using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// Reverie, the Library of Knowledge, entered through the scarred Altar of Ingress. A bare hall until the library
/// itself is generated.
/// </summary>
public class ReverieRealm : SubworldRealm
{
    private const int FloorDepth = 40;
    private const int HallHeight = 60;

    public override int Width => 720;
    public override string Name => "Reverie";
    public override int Height => 256;

    public override List<GenPass> Tasks => [new ReverieHallPass()];
    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    /// <summary> Top row of the hall floor. </summary>
    public static int FloorY => Main.maxTilesY - FloorDepth;

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    public override void Update()
    {
        if (Main.raining)
            Main.StopRain();

        Main.slimeRain = false;
        Main.townNPCCanSpawn[default] = false;
    }

    private class ReverieHallPass() : GenPass("[T3] Reverie Hall", 1f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Shelving the library";

            int floorY = FloorY;

            Main.worldSurface = floorY + 10;
            Main.rockLayer = floorY + 20;
            Main.spawnTileX = Main.maxTilesX / 2;
            Main.spawnTileY = floorY;

            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = floorY - HallHeight; y < Main.maxTilesY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.WallType = WallID.Wood;

                    if (y < floorY)
                        continue;

                    tile.HasTile = true;
                    tile.TileType = TileID.WoodBlock;
                }
            }
        }
    }
}
