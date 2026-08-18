using TrelamiumIII.Common.Systems.Jobs;
using Terraria.Audio;

namespace TrelamiumIII.Utilities;

/// <summary>
/// Helper class for handling Job progress updates while preventing duplicate progress from the same items.
/// Single player only.
/// </summary>
public static class JobUtils
{

    /// <summary>
    /// Checks if an item should update progress for a Job.
    /// Prevents duplicate progress from the same items.
    /// </summary>
    /// <param name="item">The item to process.</param>
    /// <param name="player">The player who picked up or has the item.</param>
    /// <returns>True if progress was updated, false if item had already contributed.</returns>
    public static bool Check_If_ItemUpdatedProgress(Item item, Player player)
    {
        if (JobItemComponent.HasItemContributed(item))
        {
            return false;
        }

        var JobPlayer = player.GetModPlayer<JobPlayer>();
        var progressUpdated = false;

        foreach (var Job in JobPlayer.ActiveJobs())
        {
            if (Job.Progress != JobProgress.Ongoing)
                continue;

            var currentSet = Job.TaskList[Job.CurrentSet];

            if (currentSet.IsCompleted)
                continue;

            progressUpdated = true;
        }

        if (progressUpdated)
        {
            JobItemComponent.MarkAsContributed(item);
        }

        return progressUpdated;
    }

    /// <summary>
    /// Removes items from player inventory for Job consumption.
    /// </summary>
    public static void TakePlayerItems(Player player, int itemType, int amount = 1)
    {
        var remainingAmount = amount;
        for (var i = 0; i < player.inventory.Length && remainingAmount > 0; i++)
        {
            var item = player.inventory[i];
            if (item.type == itemType)
            {
                var amountToTake = Math.Min(remainingAmount, item.stack);
                remainingAmount -= amountToTake;
                item.stack -= amountToTake;
                if (item.stack <= 0)
                    item.TurnToAir();
            }
        }

        var taken = amount - remainingAmount;
        if (taken <= 0) return;

        var template = new Item();
        template.SetDefaults(itemType);

        PopupText.NewText(new AdvancedPopupRequest
        {
            Text = $"-{taken} {template.Name}",
            DurationInFrames = 90,
            Velocity = new Vector2(0f, -4f),
            Color = new Color(255, 80, 80)
        }, player.Center);
        SoundEngine.PlaySound(SoundID.Grab, player.Center);
    }
}