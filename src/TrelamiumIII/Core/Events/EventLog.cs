using TrelamiumIII.Common.Configs;

namespace TrelamiumIII.Core.Events;

/// <summary>
/// Diagnostic logging for the event pipeline, behind <c>DebugConfig.DebugMode</c>.
///
/// <para>
/// Every broadcast used to write an unconditional <c>Logger.Info</c> line, which meant digging a tunnel
/// wrote one log entry per dirt tile with no way to turn it off. Gating it costs nothing when the toggle is
/// off, and makes it worth enriching when it is on.
/// </para>
/// </summary>
public static class EventLog
{
    /// <summary>
    /// Null-checked: the config instance does not exist yet during the earliest part of load, and a
    /// diagnostic has no business throwing there.
    /// </summary>
    public static bool Enabled => DebugConfig.Instance is { DebugMode: true };

    /// <summary>
    /// Logs at Info rather than Debug on purpose - this is opt-in already, and Debug is easy to have
    /// filtered out of the log file just when someone has turned tracing on to read it.
    /// </summary>
    public static void Trace(string message)
    {
        if (Enabled)
            TrelamiumIII.Instance.Logger.Info(message);
    }

    /// <summary>A one-line description of a broadcast, naming the content involved rather than just the event type.</summary>
    public static string Describe(in GameEvent evt)
    {
        string who = evt.Player?.name ?? "?";

        return evt.Type switch
        {
            GameEventType.TileBreak or GameEventType.TilePlace or GameEventType.TileInteract
                => $"[Event] {evt.Type} {TileName(evt.TileType)} at {evt.TileX},{evt.TileY} | {who}",

            GameEventType.NPCKill or GameEventType.NPCHit or GameEventType.NPCChat
                => $"[Event] {evt.Type} {NpcName(evt.NPCType)} | amount {evt.Amount} | {who}",

            GameEventType.ItemCraft or GameEventType.ItemPickup
            or GameEventType.ItemUse or GameEventType.ItemConsume or GameEventType.CaughtFish
                => $"[Event] {evt.Type} {ItemName(evt.ItemType)} x{evt.Amount} | {who}",

            GameEventType.HousingChecked
                => $"[Event] HousingChecked at {evt.TileX},{evt.TileY} | Room {evt.RoomId} | "
                 + $"Valid {evt.Amount == 1} | Occupied {evt.IsOccupied} | Issue {evt.Issue} | Manual {evt.IsManualCheck} | {who}",

            _ => $"[Event] {evt.Type} | amount {evt.Amount} | {who}"
        };
    }

    #region Content names

    public static string TileName(int? type)
    {
        if (type is not int id || id < 0)
            return "?";

        if (id < TileID.Count)
            return TileID.Search.TryGetName(id, out string name) ? $"{name}({id})" : id.ToString();

        return $"{TileLoader.GetTile(id)?.Name ?? "modded"}({id})";
    }

    public static string NpcName(int? type)
    {
        if (type is not int id)
            return "?";

        if (id >= 0 && id < NPCID.Count)
            return NPCID.Search.TryGetName(id, out string name) ? $"{name}({id})" : id.ToString();

        return $"{NPCLoader.GetNPC(id)?.Name ?? "modded"}({id})";
    }

    public static string ItemName(int? type)
    {
        if (type is not int id || id < 0)
            return "?";

        if (id < ItemID.Count)
            return ItemID.Search.TryGetName(id, out string name) ? $"{name}({id})" : id.ToString();

        return $"{ItemLoader.GetItem(id)?.Name ?? "modded"}({id})";
    }

    #endregion
}
