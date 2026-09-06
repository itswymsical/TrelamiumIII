using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// Ready-made acts for families that differ only by data, handed out by an <see cref="IActProvider"/>.
///
/// <para>
/// Write a subclass when an act has logic of its own. Use one of these when it does not - fourteen ore
/// acts that differ by one tile id do not need fourteen files, and a table of them is far easier to read
/// as a progression than the same thing spread across a folder.
/// </para>
/// </summary>
public abstract class DataAct : SetAct
{
    private readonly int[] _thresholds;

    protected DataAct(string id, ActCategory category, int[] thresholds, bool hardmodeContent)
    {
        ID = id;
        Category = category;
        IsHardmodeContent = hardmodeContent;

        // Curves in ActCurves are shared by many acts, and the registry sorts a malformed ladder in
        // place. Copy so one bad act can never rewrite the curve every other act is using.
        _thresholds = (int[])thresholds.Clone();
    }

    public override string ID { get; }

    public override ActCategory Category { get; }

    public override bool IsHardmodeContent { get; }

    public override int[] Thresholds => _thresholds;
}

/// <summary>An act advanced by breaking any tile in a set.</summary>
public sealed class TileBreakDataAct(
    string id, int[] tiles, int[] thresholds,
    ActCategory category = ActCategory.Obstruction, bool hardmodeContent = false)
    : DataAct(id, category, thresholds, hardmodeContent)
{
    protected override int[] Ids => tiles;

    protected override GameEventType Listens => GameEventType.TileBreak;

    protected override int IdOf(in GameEvent evt) => evt.TileType ?? -1;
}

/// <summary>An act advanced by placing any tile in a set.</summary>
public sealed class TilePlaceDataAct(
    string id, int[] tiles, int[] thresholds,
    ActCategory category = ActCategory.Building, bool hardmodeContent = false)
    : DataAct(id, category, thresholds, hardmodeContent)
{
    protected override int[] Ids => tiles;

    protected override GameEventType Listens => GameEventType.TilePlace;

    protected override int IdOf(in GameEvent evt) => evt.TileType ?? -1;
}

/// <summary>An act advanced by killing any NPC in a set.</summary>
public sealed class NpcKillDataAct(
    string id, int[] npcTypes, int[] thresholds,
    ActCategory category = ActCategory.Combat, bool hardmodeContent = false)
    : DataAct(id, category, thresholds, hardmodeContent)
{
    protected override int[] Ids => npcTypes;

    protected override GameEventType Listens => GameEventType.NPCKill;

    protected override int IdOf(in GameEvent evt) => evt.NPCType ?? -1;
}

/// <summary>An act advanced by crafting any item in a set. Counts stack size, so one smelt of 20 bars counts 20.</summary>
public sealed class ItemCraftDataAct(
    string id, int[] itemTypes, int[] thresholds,
    ActCategory category = ActCategory.Crafting, bool hardmodeContent = false)
    : DataAct(id, category, thresholds, hardmodeContent)
{
    protected override int[] Ids => itemTypes;

    protected override GameEventType Listens => GameEventType.ItemCraft;

    protected override int IdOf(in GameEvent evt) => evt.ItemType ?? -1;
}
