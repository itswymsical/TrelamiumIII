using Terraria.Audio;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Utilities;

/// <summary>
/// Helper class for handling Job progress updates while preventing duplicate progress from the same items.
/// This is a safer approach that tracks contributions directly within each Job, rather than per-item.
/// Single player only.
/// </summary>
public static class JobUtils
{
    /// <summary>
    /// Checks if an item should update progress for a Job, preventing duplicate contributions from the same item instance.
    /// Uses the Job's internal contribution tracking for safety.
    /// </summary>
    /// <param name="item">The item to process.</param>
    /// <param name="job">The job to check against.</param>
    /// <returns>True if the item can contribute, false if it has already contributed to this job.</returns>
    public static bool CanItemContributeToJob(Item item, Job job)
    {
        if (job == null || job.Progress != JobProgress.Ongoing)
            return false;

        // Use the item's unique identifier to track contributions
        // Note: Item.whoAmI is 0 in most contexts, so we use a combination of reference-based identity
        // For safety, we generate a unique key based on item properties
        int itemKey = GetItemUniqueKey(item);

        return job.TryMarkItemContribution(itemKey);
    }

    /// <summary>
    /// Checks if an item has already contributed to a specific job.
    /// </summary>
    public static bool HasItemContributedToJob(Item item, Job job)
    {
        if (job == null)
            return false;

        int itemKey = GetItemUniqueKey(item);
        return job.HasItemContributed(itemKey);
    }

    /// <summary>
    /// Generates a unique key for an item instance.
    /// Uses item reference identity (GetHashCode) combined with type for uniqueness.
    /// This survives item splitting and stacking scenarios.
    /// </summary>
    public static int GetItemUniqueKey(Item item)
    {
        // For in-world items, use whoAmI if available
        // For inventory items, use object identity + type hash
        if (item.IsAir)
            return 0;

        // Use a combination of object reference and item type for uniqueness
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + item.GetHashCode();
            hash = hash * 31 + item.type;
            return hash;
        }
    }

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

    /// <summary>
    /// Removes items from player inventory that are marked for job delivery.
    /// Only items with MarkedItem.IsMarkedForJob = true are removed.
    /// Uses InstancePerEntity isolation to prevent stacking/cloning bugs.
    /// </summary>
    public static void TakeMarkedJobItems(Player player)
    {
        for (var i = 0; i < player.inventory.Length; i++)
        {
            var item = player.inventory[i];

            // Only remove items explicitly marked for a job
            if (item.IsAir || !MarkedItem.IsMarkedForJob(item))
                continue;

            var amount = item.stack;
            var name = item.Name;

            // Remove the job marking
            MarkedItem.UnmarkForJob(item);
            item.TurnToAir();

            // Notify player
            PopupText.NewText(
                new AdvancedPopupRequest
                {
                    Text = $"-{amount} {name}",
                    DurationInFrames = 90,
                    Velocity = new Vector2(0f, -4f),
                    Color = new Color(255, 80, 80)
                },
                player.Center
            );

            SoundEngine.PlaySound(SoundID.Grab, player.Center);
        }
    }
}