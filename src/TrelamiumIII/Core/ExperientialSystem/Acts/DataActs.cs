using System.Collections.Generic;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// An act defined by data, handed out by an <see cref="IActProvider"/>. Matches events of one type whose content id
/// is in <see cref="Ids"/> (which are also its group members), or passes <see cref="Test"/> when that is set.
/// </summary>
/// <remarks><paramref name="thresholds"/> is copied, so the registry sorting a malformed ladder never edits a shared curve.</remarks>
public sealed class DataAct(string id, ActCategory category, GameEventType listens, int[] thresholds) : Act
{
    private readonly int[] ladder = (int[])thresholds.Clone();

    private HashSet<int>? lookup;

    public override string ID { get; } = id;

    public override ActCategory Category { get; } = category;

    public override int[] Thresholds => ladder;

    public int[] Ids { get; init; } = [];

    /// <summary>Matches content ids by logic instead of <see cref="Ids"/>. Such an act has no group members.</summary>
    public Func<int, bool>? Test { get; init; }

    public bool Hardmode { get; init; }

    /// <summary>Replaces the default <see cref="Act.ExpMultiplier"/> when set.</summary>
    public float? Multiplier { get; init; }

    /// <summary>Raw events per action, read per event so it may depend on TileObjectData. Null is 1.</summary>
    public Func<int>? Events { get; init; }

    public override bool IsHardmodeContent => Hardmode;

    public override float ExpMultiplier => Multiplier ?? base.ExpMultiplier;

    public override int EventsPerAction => Events?.Invoke() ?? 1;

    public override GameEventType? ListensFor => listens;

    public override int[] Members => Test is null ? Ids : [];

    public override int MemberFor(in GameEvent evt) => Test is null ? IdOf(evt) : -1;

    public override bool Matches(in GameEvent evt)
    {
        if (evt.Type != listens)
            return false;

        int content = IdOf(evt);

        return Test is { } test
            ? content >= 0 && test(content)
            : (lookup ??= [.. Ids]).Contains(content);
    }

    private int IdOf(in GameEvent evt) => listens switch
    {
        GameEventType.TileBreak or GameEventType.TilePlace or GameEventType.TileInteract => evt.TileType ?? -1,
        GameEventType.NPCKill or GameEventType.NPCHit or GameEventType.NPCChat => evt.NPCType ?? -1,
        _ => evt.ItemType ?? -1
    };
}
