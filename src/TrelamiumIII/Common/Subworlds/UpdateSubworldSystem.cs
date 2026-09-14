using SubworldLibrary;
using Terraria.DataStructures;

namespace TrelamiumIII.Common.Subworlds;

public class UpdateSubworldSystem : ModSystem
{
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