using System.Collections.Generic;
using Terraria.UI;
using TrelamiumIII.Core.Dialogue;

namespace TrelamiumIII.Common.Systems;

public class TrelamiumMainSystem : ModSystem
{
    public static TrelamiumMainSystem Instance => ModContent.GetInstance<TrelamiumMainSystem>();

    public override void UpdateUI(GameTime gameTime)
    {
        base.UpdateUI(gameTime);

        DialogueManager.Instance.Update();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));

        if (resourceBarIndex != -1)
        {
            layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer(
                "TrelamiumIII: Dialogue Manager",
                delegate
                {
                    var anchor = new Vector2(Main.screenWidth / 2f, Main.screenHeight);
                    DialogueManager.Instance.Draw(Main.spriteBatch, anchor);
                    return true;
                },
                InterfaceScaleType.UI)
            );
        }
    }
}
