using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Common.Systems;

public sealed class RecipeConditions : ModSystem
{
    public static Condition NearSunflowerOil { get; private set; } = null!;

    public override void Load()
    {
        base.Load();
        NearSunflowerOil = new Condition(Mod.GetLocalization("Conditions.NearSunflowerOil", () => "Near Sunflower Oil"), () => IsNear(Main.LocalPlayer, OilLiquids.Instance.Sunflower));
    }

    public static bool IsNear(Player player, LiquidCondition condition)
    {
        int centerX = (int)((player.position.X + player.width * 0.5f) / 16f);
        int feetY = (int)((player.position.Y + player.height) / 16f);

        for (int x = centerX - 4; x <= centerX + 4; x++)
        {
            for (int y = feetY - 3; y < feetY + 3; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount > 200 && tile.LiquidType == OilLiquids.Instance.Type && tile.LiquidCondition() == condition.Index)
                    return true;
            }
        }

        return false;
    }
}
