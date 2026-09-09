using ModLiquidLib.ModLoader;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Content.Liquids;

namespace TrelamiumIII.Common.Generation;

public class OilPass : GenPass
{
    public OilPass() : base("[T3] Oil Deposits", 150f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Seeping oil...";

        for (int k = 0; k < 200; k++)
        {
            int x = WorldGen.genRand.Next(200, Main.maxTilesX - 200);
            int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY - 200);
          
            int radius = WorldGen.genRand.Next(4, 8);

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    int worldX = x + i;
                    int worldY = y + j;

                    if (!WorldGen.InWorld(worldX, worldY))
                        continue;

                    float dist = MathF.Sqrt(i * i + j * j);

                    if (dist <= radius)
                    {
                        Main.tile[worldX, worldY].ClearTile();
                        Main.tile[worldX, worldY].liquid = 0;
                    }
                }
            }

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = 0; j <= radius; j++) // bottom half only
                {
                    int worldX = x + i;
                    int worldY = y + j;

                    if (!WorldGen.InWorld(worldX, worldY))
                        continue;

                    float dist = MathF.Sqrt(i * i + j * j);

                    if (dist <= radius)
                    {
                        byte amount = 255; // (byte)WorldGen.genRand.Next(160, 220)
                        WorldGen.PlaceLiquid(worldX, worldY, 
                            (byte)LiquidLoader.LiquidType<CrudeOilLiquid>(), amount);
                    }
                }
            }
        }
    }
}
