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
    ReturnToMerchant = 2,
}

public sealed class GoldMine : Job
{
    /// <summary>Ticks between Merchant remarks, so one big haul can't stack up a queue of lines.</summary>
    private const int SurplusCooldown = 60 * 20;

    /// <summary>
    /// A family of copper items and how much of it counts. The predicates are mutually exclusive, so
    /// the first pool that matches is the only one that can.
    /// </summary>
    private readonly CollectionMatch[] CopperMatches =
    [
        new() {
            SaveKey = "copperOre", MaxContributions = 5,
            SurplusDialogue = "Merchant.GoldMine.SurplusOre",
            Matches = evt => evt.ItemType == ItemID.CopperOre
        },
        new() {
            SaveKey = "copperBar", MaxContributions = 10,
            SurplusDialogue = "Merchant.GoldMine.SurplusBars",
            Matches = evt => evt.ItemType == ItemID.CopperBar
        },
        new() {
            SaveKey = "copperFurn", MaxContributions = 35,
            SurplusDialogue = "Merchant.GoldMine.SurplusFurniture",
            Matches = evt => ItemUtils.IsCopperFurniture(evt)
        },
        new() {
            SaveKey = "copperTool", MaxContributions = 15,
            SurplusDialogue = "Merchant.GoldMine.SurplusTools",
            Matches = evt => ItemUtils.IsCopperTool(evt)
        }
    ];

    private string pendingDialogue;

    private int lastSurplusTick = -SurplusCooldown;

    private Task CollectionTask => TaskList[(int)GoldMineTask.CollectCopper].Task[0];

    public GoldMine() : base(
        JobID.GoldMine, "GoldMine.Name", "GoldMine.Description",
        taskList:
        [
            [("GoldMine.Tasks.Set0.obj0", 1)],
            [("GoldMine.Tasks.Set1.obj0", 1)], // real count comes from the pools below
            [("GoldMine.Tasks.Set2.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 50),
            new JobReward(ItemID.SilverBar, 14),
            new JobReward("TrelamiumIII.StalagMagMKI", 1)
        ],
        NPCID.Merchant)
    {
        // The pools are the source of truth. Asking for more than they can supply makes the set
        // impossible; asking for less ends it before the player has seen every category.
        CollectionTask.RequiredCount = CopperMatches.Sum(m => m.MaxContributions);
    }

    public override void Reset()
    {
        base.Reset();

        foreach (var match in CopperMatches)
            match.CurrentContributions = 0;

        pendingDialogue = null;
        lastSurplusTick = -SurplusCooldown;
    }

    public override void Update()
    {
        base.Update();

        if (pendingDialogue == null || Main.dedServ)
            return;

        // Only speak while the Merchant is alive to say it.
        if (NPC.AnyNPCs(ProviderNPC))
            DialogueManager.Instance.StartDialogue(pendingDialogue);

        pendingDialogue = null;
    }

    public override void OnSetAdvanced(int previousSet, int newSet)
    {
        if (newSet == (int)GoldMineTask.CollectCopper)
            Speak("Merchant.GoldMine.Greeting");
    }

    /// <summary>
    /// Nudges the player off a category they've already maxed out. This runs before the task loop sees
    /// the event, so a pool that reads full here was full <em>before</em> this pickup - the line for a
    /// pool that just filled is queued from <see cref="OnMatchedEvent"/> instead.
    /// </summary>
    public override void OnEvent(GameEvent evt)
    {
        if (CurrentTaskSet != (int)GoldMineTask.CollectCopper || !IsCollectionEvent(evt))
            return;

        // Everything is full; the set is wrapping up on its own, no need to redirect anyone.
        if (CopperMatches.All(m => m.IsFull))
            return;

        var pool = CopperMatches.FirstOrDefault(m => m.Matches(evt));

        if (pool != null && pool.IsFull)
            Speak(pool.SurplusDialogue);
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (CurrentTaskSet != (int)GoldMineTask.CollectCopper || !IsCollectionEvent(evt))
            return base.OnMatchedEvent(evt, taskIndex);

        var pool = FindOpenPool(evt);
        if (pool == null)
            return false;

        // A stack only counts for what's left in its pool; the other forty copper ore are the
        // player's problem.
        var credited = Math.Min(evt.Amount, pool.MaxContributions - pool.CurrentContributions);
        if (credited <= 0)
            return false;

        pool.CurrentContributions += credited;

        if (pool.IsFull)
            Speak(pool.SurplusDialogue);

        return UpdateProgress(taskIndex, credited);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        var set = (GoldMineTask)currentSet;

        if (set is GoldMineTask.TalkToMerchant or GoldMineTask.ReturnToMerchant)
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && evt.NPCType == ProviderNPC;

        if (set == GoldMineTask.CollectCopper)
            return IsCollectionEvent(evt) && FindOpenPool(evt) != null;

        return false;
    }

    public override void SaveCustomData(TagCompound tag)
    {
        foreach (var match in CopperMatches)
            tag[match.SaveKey] = match.CurrentContributions;
    }

    public override void LoadCustomData(TagCompound tag)
    {
        // Clamped: a pool's cap may have been retuned since the save was written.
        foreach (var match in CopperMatches)
            match.CurrentContributions = Math.Min(tag.GetInt(match.SaveKey), match.MaxContributions);
    }

    /// <summary>Copper counts whether it was dug up or crafted - most copper furniture is never found loose.</summary>
    private static bool IsCollectionEvent(GameEvent evt) =>
        evt.Type is GameEventType.ItemPickup or GameEventType.ItemCraft;

    /// <summary>The pool this item belongs to, or null if it isn't copper or that pool is already full.</summary>
    private CollectionMatch FindOpenPool(GameEvent evt) =>
        CopperMatches.FirstOrDefault(m => !m.IsFull && m.Matches(evt));

    /// <summary>Queues a line, unless the Merchant has just said one.</summary>
    private void Speak(string dialogueKey)
    {
        if (string.IsNullOrEmpty(dialogueKey))
            return;

        var currentTick = (int)Main.GameUpdateCount;

        if (currentTick - lastSurplusTick < SurplusCooldown)
            return;

        lastSurplusTick = currentTick;
        pendingDialogue = dialogueKey;
    }
}

/// <summary>
/// One capped bucket of a gathering task: what counts toward it, how much of it counts, and what the
/// quest giver says once it is full.
/// </summary>
public class CollectionMatch
{
    public string SaveKey { get; set; }
    public int MaxContributions { get; set; }
    public int CurrentContributions { get; set; }

    /// <summary>Played when this pool fills, and again if the player keeps gathering the same thing.</summary>
    public string SurplusDialogue { get; set; }

    public Func<GameEvent, bool> Matches { get; set; }

    public bool IsFull => CurrentContributions >= MaxContributions;
}
