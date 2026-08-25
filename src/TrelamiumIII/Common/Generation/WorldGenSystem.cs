using System.Collections.Generic;
using Terraria.WorldBuilding;

namespace TrelamiumIII.Common.Generation;

public class WorldGenSystem : ModSystem
{
    //public override void Load()
    //{
    //    WorldGen.DetourPass((PassLegacy)WorldGen.VanillaGenPasses["Shinies"], Detour_Ores);
    //}

    //private void Detour_Ores(WorldGen.orig_GenPassDetour orig, object self, GenerationProgress progress, GameConfiguration configuration)
    //{
    //    var orePass = new OrePass();

    //    orePass.Apply(progress, configuration);
    //}

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        var shinies = tasks.FindIndex(genPass => genPass.Name.Equals("Shinies"));
        if (shinies != 1)
        {
            tasks.Insert(shinies + 1, new OrePass());
        }

        var shroomIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Spreading Grass"));
        if (shroomIndex != 1)
        {
            tasks.Insert(shroomIndex + 1, new SurfacePlantPass());
        }

        var oilIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Weeds"));
        if (oilIndex != 1)
        {
            tasks.Insert(shinies + 2, new OilPass());
        }
    }
}
