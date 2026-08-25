using System.Collections.Generic;
using System.IO;
using Terraria.GameContent;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Component that marks items as having contributed to Job progress,
/// preventing duplicate progress updates when the same item is picked up multiple times.
/// <para>
/// Marked items cannot be stacked with other instances that don't hold the same data or Job ID/type.
/// </para>
/// </summary>
public class MarkedItem : GlobalItem
{
    public override bool InstancePerEntity => true;

    /// <summary>
    /// Indicates this item is marked for job consumption.
    /// Each item instance has its own isolated flag (InstancePerEntity).
    /// </summary>
    private bool _isMarkedForJob = false;

    public override bool AppliesToEntity(Item item, bool lateInstantiation) => true; 

    public override bool CanStack(Item destination, Item source)
    {
        if (destination.TryGetGlobalItem(out MarkedItem destItem) &&
            source.TryGetGlobalItem(out MarkedItem sourceItem))
        {
            // Can't stack if job marking differs
            if (destItem._isMarkedForJob != sourceItem._isMarkedForJob)
                return false;
        }
        return base.CanStack(destination, source);
    }

    public override bool CanStackInWorld(Item destination, Item source) => CanStack(destination, source);

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!_isMarkedForJob)
            return;

        if (_isMarkedForJob)
        {
            tooltips.Add(new TooltipLine(Mod, "JobMarked", "Marked for job delivery")
            {
                OverrideColor = Color.Gold
            });
        }
    }

    #region Job Marking Helpers
    /// <summary>
    /// Marks an item as needing delivery for a job
    /// Uses InstancePerEntity to prevent stacking with unmarked items.
    /// </summary>
    public static void MarkForJob(Item item)
    {
        var marked = item.GetGlobalItem<MarkedItem>();
        marked._isMarkedForJob = true;
        item.value = 0;
        item.favorited = true;
        item.questItem = true;
        item.rare = ItemRarityID.Quest;
    }

    /// <summary>
    /// Removes the job marking from an item.
    /// </summary>
    public static void UnmarkForJob(Item item)
    {
        var marked = item.GetGlobalItem<MarkedItem>();
        marked._isMarkedForJob = false;
        item.questItem = false;
        item.favorited = false;
    }

    /// <summary>
    /// Checks if an item is marked for job delivery.
    /// </summary>
    public static bool IsMarkedForJob(Item item)
    {
        var marked = item.GetGlobalItem<MarkedItem>();
        return marked._isMarkedForJob;
    }
    #endregion

    public override void SaveData(Item item, TagCompound tag)
    {
        if (_isMarkedForJob)
            tag["MarkedForJob"] = true;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        _isMarkedForJob = tag.ContainsKey("MarkedForJob") ? tag.GetBool("MarkedForJob") : false;
    }

    public override void NetSend(Item item, BinaryWriter writer)
    {
        writer.Write(_isMarkedForJob);
    }

    public override void NetReceive(Item item, BinaryReader reader)
    {
        _isMarkedForJob = reader.ReadBoolean();
    }
}