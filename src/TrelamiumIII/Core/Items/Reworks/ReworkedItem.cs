using System.Collections.Generic;

namespace TrelamiumIII.Core.Items.Reworks;

/// <summary>
/// Routes the vanilla item hooks to whichever <see cref="ItemRework"/> is live for the holder, and skips
/// vanilla mining for a reworked tool.
/// </summary>
/// <remarks>
///     Stateless and shared: the rework is resolved from the item and the player on every call, so a
///     respec takes effect on the next swing with nothing to invalidate.
/// </remarks>
public sealed class ReworkedItem : GlobalItem
{
    public override void Load() => On_Player.ItemCheck_UseMiningTools += SkipReworkedMining;

    public override void Unload() => On_Player.ItemCheck_UseMiningTools -= SkipReworkedMining;

    private static void SkipReworkedMining(On_Player.orig_ItemCheck_UseMiningTools orig, Player self, Item sItem)
    {
        if (ItemReworkRegistry.Active(sItem, self)?.SuppressesMining(sItem, self) == true)
            return;

        orig(self, sItem);
    }

    public override bool AltFunctionUse(Item item, Player player)
        => ItemReworkRegistry.Active(item, player)?.AltFunctionUse(item, player) ?? false;

    public override bool CanUseItem(Item item, Player player)
        => ItemReworkRegistry.Active(item, player)?.CanUse(item, player) ?? true;

    public override bool? UseItem(Item item, Player player)
        => ItemReworkRegistry.Active(item, player)?.Use(item, player);

    public override void UseStyle(Item item, Player player, Rectangle heldItemFrame)
        => ItemReworkRegistry.Active(item, player)?.UseStyle(item, player, heldItemFrame);

    /// <remarks>Tooltips are drawn for whoever is reading them, so this is the local player's rework.</remarks>
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        => ItemReworkRegistry.Active(item, Main.LocalPlayer)?.ModifyTooltips(item, tooltips);
}
