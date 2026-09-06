using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TrelamiumIII.Core.Events;

/// <summary>
/// Used to check what the player is currently doing, or what they have done over an accumulated period of time.
/// <para>Useful for tasks and triggers in Jobs that aren't explicitly tied to the task sets.</para>
/// </summary>
public static class ConditionChecker
{
    private static readonly List<ConditionTracker> Active = [];

    public static void Update()
    {
        for (var i = Active.Count - 1; i >= 0; i--)
        {
            var tracker = Active[i];
            if (tracker.Expired)
            {
                Active.RemoveAt(i);
                continue;
            }
            tracker.Tick();
        }
    }

    public static void OnGameEvent(GameEvent evt)
    {
        foreach (var tracker in Active)
            tracker.Feed(evt);
    }

    /// <summary>
    /// Begins tracking a set of conditions. Returns the tracker so the caller can hold a reference.
    /// </summary>
    public static ConditionTracker Track(Player player, Action onFulfilled, params EventCondition[] conditions)
    {
        var tracker = new ConditionTracker(player, onFulfilled, conditions);
        Active.Add(tracker);
        return tracker;
    }

    public static void CancelAll(Player player)
    {
        for (var i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i].Player == player)
                Active.RemoveAt(i);
        }
    }
}

/// <summary>
/// Watches a fixed set of conditions for one player. Fires <see cref="OnFulfilled"/>
/// once when every condition is satisfied, then expires.
/// </summary>
public class ConditionTracker
{
    public Player Player { get; }
    public bool Expired { get; private set; }
    public bool Fulfilled { get; private set; }

    private readonly EventCondition[] _conditions;
    private readonly Action _onFulfilled;

    /// <summary>
    /// The tracker expires when this reaches zero, even
    /// if conditions haven't been met. -1 = no limit.
    /// </summary>
    public int TicksRemaining { get; private set; } = -1;

    public ConditionTracker(Player player, Action onFulfilled, EventCondition[] conditions)
    {
        Player = player;
        _onFulfilled = onFulfilled;
        _conditions = conditions;
    }

    /// <summary>Limits how long this tracker stays alive (in ticks)</summary>
    public ConditionTracker WithTimeout(int ticks) { TicksRemaining = ticks; return this; }

    public void Cancel() => Expired = true;

    internal void Tick()
    {
        if (Expired) return;

        if (TicksRemaining > 0)
        {
            TicksRemaining--;
            if (TicksRemaining == 0)
            {
                Expired = true;
                return;
            }
        }

        // Poll-based conditions check every tick.
        foreach (var c in _conditions)
        {
            if (!c.UseEvents)
                c.Poll(Player);
        }

        CheckFulfillment();
    }

    internal void Feed(GameEvent evt)
    {
        if (Expired || evt.Player != Player) return;

        foreach (var c in _conditions)
        {
            if (c.UseEvents)
                c.Accumulate(evt);
        }

        CheckFulfillment();
    }

    private void CheckFulfillment()
    {
        foreach (var c in _conditions)
        {
            if (!c.IsMet) return;
        }

        Fulfilled = true;
        Expired = true;
        _onFulfilled?.Invoke();
    }
}

/// <summary>
/// A single trackable condition. Either event-driven (accumulates from
/// <see cref="GameEvent"/> broadcasts) or poll-driven (checked each tick
/// against player state).
/// </summary>
public abstract class EventCondition
{
    public bool IsMet { get; protected set; }

    /// <summary>
    /// When true, the tracker feeds this condition via <see cref="Accumulate"/>.
    /// When false, the tracker drives it via <see cref="Poll"/> each tick.
    /// </summary>
    public virtual bool UseEvents => true;

    /// <summary>Called by the tracker each tick for poll-based conditions.</summary>
    public virtual void Poll(Player player) { }

    /// <summary>Called by the tracker when a game event is broadcast.</summary>
    public virtual void Accumulate(GameEvent evt) { }
}

public class PickedUpItem(int itemType, int count = 1) : EventCondition
{
    private int _accumulated;

    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.ItemPickup) return;
        if (evt.ItemType != itemType) return;

        _accumulated += evt.Amount;
        if (_accumulated >= count)
            IsMet = true;
    }
}

/// <summary>
/// Met once <paramref name="count"/> <i>distinct</i> rooms have passed a housing check.
/// Re-checking a room that has already counted is ignored, so the player can't satisfy a
/// multi-house objective by repeatedly checking one house.
/// </summary>
public class ValidatedHousing(int count = 1, bool mustBeUnoccupied = true) : EventCondition
{
    private readonly HashSet<Point> _countedRooms = [];

    /// <summary>Distinct rooms validated so far. Useful for "3/5 houses" style progress display.</summary>
    public int Progress => _countedRooms.Count;

    public int Required => count;

    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.HousingChecked) return;

        bool isRoomValid = evt.Amount == 1;
        if (!isRoomValid) return;

        if (mustBeUnoccupied && evt.IsOccupied)
            return;

        // No resolvable room identity means we can't prove this is a new house, so don't credit it.
        if (evt.RoomId is not Point roomId)
            return;

        // Add returns false if we've already credited this room.
        if (!_countedRooms.Add(roomId))
            return;

        if (_countedRooms.Count >= count)
            IsMet = true;
    }
}

public class CraftedItem(int itemType, int count = 1) : EventCondition
{
    private int _accumulated;

    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.ItemCraft) return;
        if (evt.ItemType != itemType) return;

        _accumulated += evt.Amount;
        if (_accumulated >= count)
            IsMet = true;
    }
}

public class KilledNPC(int npcType, int count = 1) : EventCondition
{
    private int _accumulated;

    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.NPCKill) return;
        if (evt.NPCType != npcType) return;

        _accumulated++;
        if (_accumulated >= count)
            IsMet = true;
    }
}

public class BrokeTile(int tileType, int count = 1) : EventCondition
{
    private int _accumulated;

    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.TileBreak) return;
        if (evt.TileType != tileType) return;

        _accumulated++;
        if (_accumulated >= count)
            IsMet = true;
    }
}

public class HasItemInInventory(int itemType, int count = 1) : EventCondition
{
    public override bool UseEvents => false;

    public override void Poll(Player player)
    {
        if (IsMet) return;

        var total = 0;
        for (var i = 0; i < player.inventory.Length; i++)
        {
            var slot = player.inventory[i];
            if (slot.type == itemType)
                total += slot.stack;
        }

        IsMet = total >= count;
    }
}

public class SpentTimeInBiome(Func<Player, bool> zonePredicate, int seconds) : EventCondition
{
    public override void Accumulate(GameEvent evt)
    {
        if (IsMet) return;
        if (evt.Type != GameEventType.BiomeEnter) return;
        if (!zonePredicate(evt.Player)) return;
        if ((evt.seconds ?? 0) >= seconds * 60)
            IsMet = true;
    }
}

public class NearNPC(int npcType, float range) : EventCondition
{
    public override bool UseEvents => false;

    public override void Poll(Player player)
    {
        if (IsMet) return;

        var rangeSq = range * range;
        for (var i = 0; i < Main.maxNPCs; i++)
        {
            var npc = Main.npc[i];
            if (!npc.active || npc.type != npcType) continue;
            if (Vector2.DistanceSquared(player.Center, npc.Center) <= rangeSq)
            {
                IsMet = true;
                return;
            }
        }
    }
}