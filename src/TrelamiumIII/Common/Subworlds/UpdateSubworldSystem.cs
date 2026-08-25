using SubworldLibrary;
using Terraria.DataStructures;

namespace TrelamiumIII.Common.Subworlds;

public class UpdateSubworldSystem : ModSystem
{
    public override void PreUpdateWorld()
    {
        if (SubworldSystem.AnyActive())
        {
            // Update mechanisms
            Wiring.UpdateMech();

            // Update tile entities
            TileEntity.UpdateStart();
            foreach (var te in TileEntity.ByID.Values)
            {
                te.Update();
            }
            TileEntity.UpdateEnd();

            // Update liquid
            if (++Liquid.skipCount > 1)
            {
                Liquid.UpdateLiquid();
                Liquid.skipCount = 0;
            }
        }
    }
}