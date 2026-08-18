using Terraria.ModLoader.IO;
using Terraria.DataStructures;
using System.Collections.Generic;
using TrelamiumIII.Core.Components.Items;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Component that marks items as having contributed to Job progress,
/// preventing duplicate progress updates when the same item is picked up multiple times.
/// </summary>
public class JobItemComponent : ItemComponent
{
    #region Fields
    /// <summary>
    ///     Indicates whether this item has already contributed to Job progress.
    /// </summary>
    private bool hasContributed;

    /// <summary>
    ///     Indicates whether this item is marked for a Job.
    /// </summary>
    private bool markedForJob;
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets whether this item has already contributed to Job progress.
    /// </summary>
    public bool HasContributedToProgress
    {
        get => hasContributed;
        set => hasContributed = value;
    }

    /// <summary>
    /// Gets or sets whether this item is marked for a Job.
    /// </summary>
    public bool MarkedForJob
    {
        get => markedForJob;
        set => markedForJob = value;
    }
    #endregion

    #region Initialization
    public JobItemComponent()
    {
        Enabled = true;
    }
    #endregion

    #region Override Methods
    public override GlobalItem Clone(Item from, Item to)
    {
        var clone = (JobItemComponent)base.Clone(from, to);
        clone.hasContributed = hasContributed;
        clone.markedForJob = markedForJob;
        return clone;
    }

    public override void SaveData(Item item, TagCompound tag)
    {
        tag["hasContributed"] = hasContributed;
        tag["markedForJob"] = markedForJob;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        hasContributed = tag.GetBool("hasContributed");
        markedForJob = tag.GetBool("markedForJob");
    }

    public override void OnCreated(Item item, ItemCreationContext context)
    {
        // prevent pickup from triggering when you craft shit
        base.OnCreated(item, context);
        var component = GetOrCreate(item);
        component.HasContributedToProgress = true;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    ///     Gets the JobItemComponent for a given item, creating it if it doesn't exist.
    /// </summary>
    public static JobItemComponent GetOrCreate(Item item)
    {
        return item.GetGlobalItem<JobItemComponent>();
    }

    /// <summary>
    ///     Marks an item as having contributed to Job progress.
    /// </summary>
    public static void MarkAsContributed(Item item)
    {
        var component = GetOrCreate(item);
        component.HasContributedToProgress = true;
    }

    /// <summary>
    ///     Marks an item for a Job, making it a protected quest item.
    /// </summary>
    public static void MarkForJob(Item item)
    {
        item.value = 0;
        item.favorited = true;
        item.questItem = true;
        item.rare = ItemRarityID.Quest;
        var component = GetOrCreate(item);
        component.MarkedForJob = true;
    }

    /// <summary>
    /// Checks if an item has already contributed to Job progress.
    /// </summary>
    public static bool HasItemContributed(Item item)
    {
        var component = GetOrCreate(item);
        return component.HasContributedToProgress;
    }

    /// <summary>
    /// Checks if an item is marked for a Job.
    /// </summary>
    public static bool IsMarkedForJob(Item item)
    {
        var component = GetOrCreate(item);
        return component.MarkedForJob;
    }

    /// <summary>
    /// Removes Job marking from an item.
    /// </summary>
    public static void UnmarkForJob(Item item)
    {
        var component = GetOrCreate(item);
        component.MarkedForJob = false;
        item.questItem = false;
        item.favorited = false;
    }
    #endregion

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        base.ModifyTooltips(item, tooltips);
        //if (IsMarkedForJob(item))
        //{
        //    tooltips.Add(new TooltipLine(Mod, "Job Item", "Job Item")
        //    {
        //        OverrideColor = Color.Gold
        //    });
        //}
    }
}