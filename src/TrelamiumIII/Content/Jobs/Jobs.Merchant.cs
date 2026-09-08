using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Jobs;

/// <summary>The task sets of <see cref="GoldMine"/>, in the order they run.</summary>
public enum GoldMineTask
{
    TalkToMerchant = 0,
    CollectCopper = 1,
    ReturnToMerchant = 2
}

/// <summary>
/// The Merchant wants copper, in variety rather than in bulk: ore, bars, furniture and tools each cap
/// out, so the player has to go and find a different kind rather than mine one vein forever.
/// </summary>
public sealed class GoldMine : Job
{
    private CopperPoolTask CollectionTask => (CopperPoolTask)TaskList[(int)GoldMineTask.CollectCopper].Task[0];

    public GoldMine() : base(
        JobID.GoldMine, "GoldMine.Name", "GoldMine.Description",
        taskList:
        [
            [new TalkToNPCTask("GoldMine.Tasks.Set0.obj0", NPCID.Merchant)],
            [new CopperPoolTask("GoldMine.Tasks.Set1.obj0")],
            [new TalkToNPCTask("GoldMine.Tasks.Set2.obj0", NPCID.Merchant)]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 50),
            new JobReward(ItemID.SilverBar, 14),
            new JobReward("TrelamiumIII/StalagMagMKI", 1)
        ],
        providerNPC: NPCID.Merchant)
    { }

    public override void OnJobStart()
        => Context.Play("Merchant.GoldMine_Greeting", style: DialogueStyle.Projected);

    /// <summary>
    /// Nudges the player off a category they have already maxed out. This runs before the task sees
    /// the event, so a pool that reads full here was full <em>before</em> this pickup -- the line for
    /// a pool that has only just filled comes from <see cref="OnTaskAdvanced"/> instead.
    /// </summary>
    public override void OnEvent(GameEvent evt)
    {
        if (CurrentTaskSet != (int)GoldMineTask.CollectCopper)
            return;

        var task = CollectionTask;

        // Everything is full; the set is wrapping up on its own, no need to redirect anyone.
        if (task.IsCompleted)
            return;

        if (task.FindPool(evt) is { IsFull: true } pool)
            Speak(pool.SurplusDialogue);
    }

    /// <summary>Says its piece when a category fills up, as the player hands over the one that did it.</summary>
    public override void OnTaskAdvanced(JobTask task, int taskIndex, GameEvent evt)
    {
        if (task is CopperPoolTask pools && pools.FindPool(evt) is { IsFull: true } pool)
            Speak(pool.SurplusDialogue);
    }
}

/// <summary>
/// A gathering task split into capped buckets: each family of item counts for only so much, so the
/// requirement can't be met by grinding a single one.
/// </summary>
public sealed class CopperPoolTask : JobTask
{
    /// <summary>
    /// The pools, in the order they are tested. Mutually exclusive, so the first that matches is the
    /// only one that can.
    /// </summary>
    private readonly CollectionPool[] pools =
    [
        new("copperOre", 10, "Merchant.GoldMine_SurplusOre", evt => evt.ItemType == ItemID.CopperOre),
        new("copperBar", 15, "Merchant.GoldMine_SurplusBars", evt => evt.ItemType == ItemID.CopperBar),
        new("copperFurn", 25, "Merchant.GoldMine_SurplusFurniture", ItemUtils.IsCopperFurniture),
        new("copperTool", 3, "Merchant.GoldMine_SurplusTools", ItemUtils.IsCopperTool)
    ];

    public CopperPoolTask(string key) : base(key)
    {
        // The pools are the source of truth. Asking for more than they can supply makes the set
        // impossible; asking for less ends it before the player has seen every category.
        RequiredCount = pools.Sum(pool => pool.Max);
    }

    /// <summary>Copper counts whether it was dug up or crafted -- most copper furniture is never found loose.</summary>
    private static bool IsCollectionEvent(GameEvent evt)
        => evt.Type is GameEventType.ItemPickup or GameEventType.ItemCraft;

    /// <summary>The pool this item belongs to, full or not, or null if it isn't copper at all.</summary>
    public CollectionPool FindPool(GameEvent evt)
        => IsCollectionEvent(evt) ? pools.FirstOrDefault(pool => pool.Matches(evt)) : null;

    public override bool Matches(GameEvent evt)
        => FindPool(evt) is { IsFull: false };

    public override int CreditFor(GameEvent evt)
    {
        var pool = FindPool(evt);

        if (pool is null || pool.IsFull)
            return 0;

        // A stack only counts for what's left in its pool; the other forty copper ore are the
        // player's problem.
        var credited = Math.Min(Math.Max(1, evt.Amount), pool.Max - pool.Current);

        pool.Current += credited;

        return credited;
    }

    protected override void OnReset()
    {
        foreach (var pool in pools)
            pool.Current = 0;
    }

    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);

        foreach (var pool in pools)
            tag[pool.SaveKey] = pool.Current;
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);

        // Clamped: a pool's cap may have been retuned since the save was written.
        foreach (var pool in pools)
            pool.Current = Math.Clamp(tag.GetInt(pool.SaveKey), 0, pool.Max);
    }
}

/// <summary>
/// One capped bucket of a gathering task: what counts toward it, how much of it counts, and what the
/// quest giver says once it is full.
/// </summary>
public sealed class CollectionPool(string saveKey, int max, string surplusDialogue, Func<GameEvent, bool> matches)
{
    public string SaveKey { get; } = saveKey;

    public int Max { get; } = max;

    public int Current { get; set; }

    /// <summary>Played when this pool fills, and again if the player keeps gathering the same thing.</summary>
    public string SurplusDialogue { get; } = surplusDialogue;

    public bool Matches(GameEvent evt) => matches(evt);

    public bool IsFull => Current >= Max;
}
