using System.Collections.Generic;
using Terraria.UI;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.UI.Projected;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     Updates <see cref="DialogueManager" /> and draws it before vanilla's resource bars. Keeps the chat window shut
///     while a <see cref="DialogueBox" /> is live, so Enter advances the conversation.
/// </summary>
public sealed class DialogueSystem : ModSystem
{
    public override void Load() => On_Main.OpenPlayerChat += SuppressChatWhileSpeaking;

    public override void UpdateUI(GameTime gameTime) => DialogueManager.Instance.Update();

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int resourceBars = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));

        if (resourceBars == -1)
            return;

        layers.Insert(resourceBars, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Dialogue Manager",
            delegate
            {
                DialogueManager.Instance.Draw(Main.spriteBatch, new Vector2(Main.screenWidth / 2f, Main.screenHeight));
                return true;
            },
            InterfaceScaleType.UI));
    }

    /// <remarks>Refuses to open rather than swallowing Enter, so chat already open stays open.</remarks>
    private static void SuppressChatWhileSpeaking(On_Main.orig_OpenPlayerChat orig)
    {
        if (ProjectedPanelSystem.AnyLive<DialogueBox>())
            return;

        orig();
    }
}
