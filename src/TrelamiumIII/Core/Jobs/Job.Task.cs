using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// One line of a <see cref="Job"/>'s to-do list: what it asks for, how much of it, and crucially
/// which events count toward it.
/// </summary>
/// <remarks>
///     <para>
///         A task decides for itself whether an event counts, via <see cref="Matches"/>. Tasks own their progress and any bookkeeping that progress depends on.
///     </para>
/// </remarks>
public abstract class JobTask(string descriptionKey, int requiredCount = 1)
{
    public string DescriptionKey { get; } = descriptionKey;

    /// <summary>Resolved at read time, so a language switch takes effect without touching save data.</summary>
    public virtual string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Value;

    public int RequiredCount { get; set; } = requiredCount;

    public int Count { get; private set; }

    /// <summary>
    /// Derived rather than stored, which is what makes progress reversible: revoking a delivered item
    /// drops <see cref="Count"/> back under the requirement and the task genuinely re-opens. A stored
    /// flag could only ever be set, so a job could take an item back and leave the task ticked.
    /// </summary>
    public bool IsCompleted => Count >= RequiredCount;

    public bool IsVisible { get; set; } = true;

    public delegate bool VisibilityCondition(Job job);

    public VisibilityCondition VisibilityCheck { get; set; }

    public bool ShouldBeVisible(Job job) => VisibilityCheck?.Invoke(job) ?? IsVisible;

    /// <summary>Does <paramref name="evt"/> count toward this task? Checks the event type itself.</summary>
    public abstract bool Matches(GameEvent evt);

    /// <summary>
    /// How much a matching event is worth. Defaults to the event's own amount, so a stack of ten
    /// counts as ten; override to cap or flatten it.
    /// </summary>
    public virtual int CreditFor(GameEvent evt) => Math.Max(1, evt.Amount);

    /// <summary>
    /// Progress read from the world instead of from events: the amount to apply this tick, or 0. Called every tick
    /// while this task's set is current; may be negative.
    /// </summary>
    public virtual int Poll(JobContext context) => 0;

    /// <summary>
    /// Applies <paramref name="amount"/> (which may be negative) and reports whether that crossed the
    /// requirement. Clamped at both ends: progress never goes below zero, and an oversized stack does
    /// not bank credit against a later revoke.
    /// </summary>
    public bool Advance(int amount)
    {
        var wasCompleted = IsCompleted;

        Count = Math.Clamp(Count + amount, 0, RequiredCount);

        return !wasCompleted && IsCompleted;
    }

    public void Reset()
    {
        Count = 0;
        OnReset();
    }

    /// <summary>
    /// Fills the task outright. For debug commands only! ordinary progress goes through
    /// <see cref="Job.UpdateProgress"/>, which is what fires the stage and completion hooks.
    /// </summary>
    internal void ForceComplete() => Count = RequiredCount;

    /// <summary>Clears bookkeeping a subclass accumulated. Base implementation has none.</summary>
    protected virtual void OnReset() { }

    /// <summary>
    /// Persists this task's progress. Subclasses that track more than a count, which rooms were
    /// credited, which pools are full. override and call <c>base</c>.
    /// </summary>
    public virtual void SaveData(TagCompound tag) => tag["count"] = Count;

    public virtual void LoadData(TagCompound tag)
        => Count = Math.Clamp(tag.TryGet("count", out int count) ? count : 0, 0, RequiredCount);
}

/// <summary>
/// A stage of a <see cref="Job"/>: every task in it must finish before the job moves to the next one.
/// </summary>
public class TaskList(List<JobTask> tasks)
{
    public List<JobTask> Task { get; } = tasks;

    public bool IsCompleted => Task.All(task => task.IsCompleted);

    public void Reset()
    {
        foreach (var task in Task)
            task.Reset();
    }
}

#region Common tasks
/// <summary>Break <paramref name="count"/> tiles of <paramref name="tileType"/>.</summary>
public sealed class BreakTileTask(string key, int tileType, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.TileBreak && evt.TileType == tileType;
}

/// <summary>Place <paramref name="count"/> tiles of <paramref name="tileType"/>.</summary>
public sealed class PlaceTileTask(string key, int tileType, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.TilePlace && evt.TileType == tileType;
}

/// <summary>Pick up <paramref name="count"/> of <paramref name="itemType"/>.</summary>
public sealed class PickupItemTask(string key, int itemType, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.ItemPickup && evt.ItemType == itemType;
    public int ItemType { get; set; } = itemType;
    public override string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}")
    .Format(Lang.GetItemNameValue(ItemType), RequiredCount);
}

/// <summary>Craft <paramref name="count"/> of <paramref name="itemType"/>.</summary>
public sealed class CraftItemTask(string key, int itemType, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.ItemCraft && evt.ItemType == itemType;
    public int ItemType { get; set; } = itemType;
    public override string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}")
    .Format(Lang.GetItemNameValue(ItemType), RequiredCount);
}

/// <summary>Kill <paramref name="count"/> of <paramref name="npcType"/>.</summary>
public sealed class KillNPCTask(string key, int npcType, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.NPCKill && evt.NPCType == npcType;

    // One kill is one kill; GameEvent.NPCKill carries Amount 1 already, but a future bulk-kill
    // event should not silently clear a "kill 10" task in one go.
    public override int CreditFor(GameEvent evt) => 1;
}

/// <summary>Go and talk to <paramref name="npcType"/>. The bread and butter of a hand-in step.</summary>
public sealed class TalkToNPCTask(string key, int npcType) : JobTask(key, 1)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.NPCChat && evt.NPCType == npcType;

    public override int CreditFor(GameEvent evt) => 1;
}

/// <summary>
/// Hold <paramref name="count"/> of <paramref name="itemType"/>. Count mirrors the owner's inventory, so dropping items
/// re-opens it. A <see cref="HandInTask"/> in the same set takes the items; without one, holding them completes the set.
/// </summary>
/// <param name="consume">Whether <see cref="Job.TryHandIn"/> takes the items or only checks for them.</param>
public class GatherItemTask(string key, int itemType, int count = 1, bool consume = true) : JobTask(key, count)
{
    public int ItemType { get; set; } = itemType;

    public bool Consumes { get; } = consume;

    /// <summary>{0} = item name, {1} = required count.</summary>
    public override string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}")
        .Format(Lang.GetItemNameValue(ItemType), RequiredCount);

    /// <summary>Matching items in <paramref name="player"/>'s inventory, capped at the requirement. Equipped items don't count.</summary>
    public virtual int Held(Player player)
        => ItemType > ItemID.None ? Math.Min(player.CountItem(ItemType, RequiredCount), RequiredCount) : 0;

    /// <summary>Removes the required items from the owner. Called by <see cref="Job.TryHandIn"/> when <see cref="Consumes"/>.</summary>
    public virtual void Take(JobContext context) => context.Revoke(ItemType, RequiredCount);

    public override bool Matches(GameEvent evt) => false;

    public override int Poll(JobContext context) => context.IsLocal ? Held(context.Player) - Count : 0;
}

/// <summary>
/// Hold <paramref name="count"/> items of any of <paramref name="itemTypes"/>, in any mix. Taken in list order on hand-in.
/// </summary>
/// <param name="itemTypes">Resolved on first use: mod item types aren't assigned yet when jobs are registered.</param>
public class GatherAnyItemTask(string key, Func<IReadOnlyList<int>> itemTypes, int count = 1, bool consume = true)
    : GatherItemTask(key, ItemID.None, count, consume)
{
    private IReadOnlyList<int>? types;

    private IReadOnlyList<int> Types => types ??= itemTypes();

    public override string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Value;

    public override int Held(Player player)
    {
        var held = 0;

        foreach (var type in Types)
        {
            if (held >= RequiredCount)
                break;

            held += player.CountItem(type, RequiredCount - held);
        }

        return Math.Min(held, RequiredCount);
    }

    public override void Take(JobContext context)
    {
        var remaining = RequiredCount;

        foreach (var type in Types)
        {
            if (remaining <= 0)
                break;

            remaining -= context.Revoke(type, remaining);
        }
    }
}

/// <summary>
/// Give the set's gathered items to the provider. Credited only by <see cref="Job.TryHandIn"/>, and shown once every
/// other task in its set is done.
/// </summary>
public class HandInTask : JobTask
{
    /// <param name="promptKey">
    /// Dialogue the provider opens while the hand-in is ready, built with <c>Dialogue.HandIn</c>. Null when the NPC
    /// takes the hand-in some other way, e.g. a chat button.
    /// </param>
    public HandInTask(string key, string? promptKey = null) : base(key)
    {
        PromptKey = promptKey;
        VisibilityCheck = job => job.TaskList.Any(set => set.Task.Contains(this) && set.Task.All(task => task == this || task.IsCompleted));
    }

    public string? PromptKey { get; }

    public override bool Matches(GameEvent evt) => false;
}

/// <summary>
/// An escape hatch for a one-off trigger that does not justify its own type. Prefer a named subclass
/// when the same shape shows up twice -- a job reads better as <c>BreakTileTask</c> than as a lambda.
/// </summary>
public sealed class PredicateTask(string key, Func<GameEvent, bool> matches, int count = 1) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt) => matches(evt);
}
#endregion
