using SubworldLibrary;
using Terraria.DataStructures;

namespace TrelamiumIII.Common.Subworlds;

public class UpdateSubworldSystem : ModSystem
{
    /// <summary>Name of the last loaded world that isn't a subworld; the loading screen title when leaving a realm.</summary>
    public static string MainWorldName { get; private set; } = string.Empty;

    public override void OnWorldLoad()
    {
        if (!SubworldSystem.AnyActive())
            MainWorldName = Main.worldName;
    }

    public override void PreUpdateWorld()
    {
        if (SubworldSystem.AnyActive())
        {
            Wiring.UpdateMech();

            TileEntity.UpdateStart();
            foreach (var te in TileEntity.ByID.Values)
            {
                te.Update();
            }
            TileEntity.UpdateEnd();

            if (++Liquid.skipCount > 1)
            {
                Liquid.UpdateLiquid();
                Liquid.skipCount = 0;
            }
        }
    }
}