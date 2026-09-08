using Terraria.Audio;
using Terraria.GameContent.UI;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Utilities;

/// <summary>
/// Inventory operations a <see cref="Job"/> needs to perform on its owner: taking items back, and
/// telling the player it happened.
/// </summary>
/// <remarks>
/// Jobs reach these through <see cref="JobContext"/> rather than calling them directly, so the
/// owning player is always explicit rather than assumed to be <see cref="Main.LocalPlayer"/>.
/// </remarks>
public static class JobUtils
{
    /// <summary>
    /// Takes up to <paramref name="amount"/> of <paramref name="itemType"/> out of the player's
    /// inventory, and returns how much was actually there to take.
    /// </summary>
    public static int TakePlayerItems(Player player, int itemType, int amount = 1)
    {
        var remaining = amount;

        for (var i = 0; i < player.inventory.Length && remaining > 0; i++)
        {
            var item = player.inventory[i];

            if (item.type != itemType || item.IsAir)
                continue;

            var taken = Math.Min(remaining, item.stack);

            remaining -= taken;
            item.stack -= taken;

            if (item.stack <= 0)
                item.TurnToAir();
        }

        var total = amount - remaining;

        if (total > 0)
            ReportLoss(player, itemType, total);

        return total;
    }

    /// <summary>
    /// Takes back every item marked for <paramref name="jobId"/>, and returns how many were taken.
    /// </summary>
    /// <remarks>
    /// Scoped to one job: two jobs asking for copper at the same time each get their own marked
    /// stacks back, which an unscoped sweep could not tell apart.
    /// </remarks>
    public static int TakeMarkedJobItems(Player player, int jobId)
    {
        var total = 0;

        for (var i = 0; i < player.inventory.Length; i++)
        {
            var item = player.inventory[i];

            if (item.IsAir || !MarkedItem.IsMarkedFor(item, jobId))
                continue;

            var amount = item.stack;
            var type = item.type;

            MarkedItem.Unmark(item);
            item.TurnToAir();

            ReportLoss(player, type, amount);
            total += amount;
        }

        return total;
    }

    /// <summary>
    /// The red counterpart to a pickup popup, so items leaving the inventory for a job are as
    /// visible as items arriving for one.
    /// </summary>
    public static void ReportLoss(Player player, int itemType, int amount)
    {
        if (amount <= 0 || player.whoAmI != Main.myPlayer)
            return;

        var template = new Item();
        template.SetDefaults(itemType);

        PopupText.NewText(new AdvancedPopupRequest
        {
            Text = $"-{amount} {template.Name}",
            DurationInFrames = 90,
            Velocity = new Vector2(0f, -4f),
            Color = new Color(255, 80, 80)
        }, player.Center);

        SoundEngine.PlaySound(SoundID.Grab, player.Center);
    }
}
