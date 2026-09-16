using System.Collections.Generic;
using Terraria.UI;

namespace TrelamiumIII.Content.DevTools;

/// <summary>Shows the wand's layer picker while the wand is the held item.</summary>
internal sealed class FieldWandSystem : ModSystem
{
    private UserInterface? ui;
    private FieldWandUIState? state;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        state = new FieldWandUIState();
        state.Activate();

        ui = new UserInterface();
        ui.SetState(state);
    }

    public override void Unload()
    {
        ui = null;
        state = null;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Holding())
            ui?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

        if (index == -1)
            return;

        layers.Insert(index, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Field Wand",
            () =>
            {
                if (Holding())
                    ui?.Draw(Main.spriteBatch, new GameTime());

                return true;
            },
            InterfaceScaleType.UI));
    }

    private static bool Holding()
        => !Main.gameMenu && Main.LocalPlayer is { active: true, dead: false } player && player.HeldItem?.ModItem is FieldWand;
}
