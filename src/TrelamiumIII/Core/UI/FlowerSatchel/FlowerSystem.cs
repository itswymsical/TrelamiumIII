// Modified from Spirit Mod's Backpack slot
// Credits: Spirit Mod, GabeHasWon
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/UI/BackpackInterface/BackbackUISlot.cs#L1

using System.Collections.Generic;
using Terraria.UI;
using TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

namespace TrelamiumIII.Core.UI.FlowerSatchel;

public class FlowerSystem : ModSystem
{
    private UserInterface _flowerSatchelInterface;
    private FlowerSatchelUIState _flowerSatchelUI;

    public override void Load()
    {
        if (Main.dedServ) return;

        _flowerSatchelUI = new FlowerSatchelUIState();
        _flowerSatchelUI.Activate();

        _flowerSatchelInterface = new UserInterface();
        _flowerSatchelInterface.SetState(_flowerSatchelUI);
    }

    public override void Unload()
    {
        _flowerSatchelInterface = null;
        _flowerSatchelUI = null;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (ShouldDrawSatchelUI())
            _flowerSatchelInterface?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int inventoryIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
        if (inventoryIndex == -1) return;

        // Vanilla draws containers (chest grids, shop stock) inside its own inventory layer, in the
        // band this panel lives in. So while one is open the panel slots in *before* that layer and
        // yields to it rather than bleeding over the top; otherwise it stays above, where it would
        // otherwise be buried under the crafting list. ModifyInterfaceLayers runs every frame, so this
        // flips as containers open and close.
        int insertAt = VanillaInventoryLayout.ContainerOpen ? inventoryIndex : inventoryIndex + 1;

        layers.Insert(insertAt, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Flower Satchel",
            () =>
            {
                if (ShouldDrawSatchelUI())
                    _flowerSatchelInterface?.Draw(Main.spriteBatch, new GameTime());
                return true;
            },
            InterfaceScaleType.UI
        ));
    }

    /// <summary>
    /// The panel renders only when the inventory is open AND the player has chosen
    /// to display it via right-clicking the satchel. Effect calculation runs every
    /// frame in SatchelPlayer regardless and is independent of this check.
    /// </summary>
    private static bool ShouldDrawSatchelUI()
    {
        if (!Main.playerInventory) return false;

        var modPlayer = Main.LocalPlayer.GetModPlayer<SatchelPlayer>();
        return modPlayer.PanelVisible
            && modPlayer.ActiveSatchel?.ModItem is FlowerSatchelItem;
    }
}
