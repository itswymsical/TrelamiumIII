using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TrelamiumIII.Core.NPCs.Trading;

public class TradeUISystem : ModSystem
{
    public override void UpdateUI(GameTime gameTime)
    {
        TradeUI.Update();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int mouseIdx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        if (mouseIdx == -1) return;

        layers.Insert(mouseIdx, new LegacyGameInterfaceLayer(
            " TrelamiumIII: Trade UI",
            () => { TradeUI.Draw(Main.spriteBatch); return true; },
            InterfaceScaleType.UI));
    }
}