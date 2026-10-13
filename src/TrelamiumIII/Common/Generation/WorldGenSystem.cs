using System.Collections.Generic;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Ores;

namespace TrelamiumIII.Common.Generation;

public class WorldGenSystem : ModSystem
{
    public override void Load()
    {
        WorldGen.DetourPass((PassLegacy)WorldGen.VanillaGenPasses["Shinies"], GenerateOres);
    }

    /// <summary>
    /// Replaces Shinies with <see cref="OreGenerator"/>. Remix worlds keep vanilla Shinies for its inverted depths,
    /// run a second time with every tier flipped so both variants generate; drunk worlds already place both.
    /// </summary>
    private static void GenerateOres(WorldGen.orig_GenPassDetour orig, object self, GenerationProgress progress, GameConfiguration configuration)
    {
        if (WorldGen.remixWorldGen)
        {
            orig(self, progress, configuration);

            if (!WorldGen.drunkWorldGen)
                RunWithOtherVariants(() => orig(self, progress, configuration));
        }

        OreGenerator.Generate(progress, includeVanillaOres: !WorldGen.remixWorldGen);
    }

    /// <summary> Runs <paramref name="generate"/> with each ore tier and the world evil swapped, then restores them. </summary>
    private static void RunWithOtherVariants(Action generate)
    {
        (int copper, int iron, int silver, int gold, bool crimson) = (GenVars.copper, GenVars.iron, GenVars.silver, GenVars.gold, WorldGen.crimson);

        try
        {
            GenVars.copper = copper == TileID.Copper ? TileID.Tin : TileID.Copper;
            GenVars.iron = iron == TileID.Iron ? TileID.Lead : TileID.Iron;
            GenVars.silver = silver == TileID.Silver ? TileID.Tungsten : TileID.Silver;
            GenVars.gold = gold == TileID.Gold ? TileID.Platinum : TileID.Gold;
            WorldGen.crimson = !crimson;

            generate();
        }
        finally
        {
            (GenVars.copper, GenVars.iron, GenVars.silver, GenVars.gold, WorldGen.crimson) = (copper, iron, silver, gold, crimson);
        }
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        var shroomIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Spreading Grass"));
        if (shroomIndex != -1)
        {
            tasks.Insert(shroomIndex + 1, new SurfacePlantPass());
        }

        var oilIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Final Cleanup"));
        if (oilIndex != -1)
        {
            tasks.Insert(oilIndex + 1, new OilPass());
            tasks.Insert(oilIndex + 2, new BrinePass());
            tasks.Insert(oilIndex + 3, new ObsidianForestPass());
            tasks.Insert(oilIndex + 4, new SilvianHousePass());
            tasks.Insert(oilIndex + 5, new AbandonedCabinPass());
        }
    }
}
