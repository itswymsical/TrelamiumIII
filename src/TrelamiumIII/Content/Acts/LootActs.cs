using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

internal sealed class LootActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        Pickup("act.heartpickup", ActCategory.Collection, [ItemID.Heart, ItemID.CandyApple, ItemID.CandyCane]),
        Pickup("act.manapickup", ActCategory.Collection, [ItemID.Star, ItemID.SoulCake, ItemID.SugarPlum]),
    ];

    private static DataAct Pickup(string id, ActCategory category, int[] items)
        => new(id, category, GameEventType.ItemPickup, ActCurves.Scavenge) { Ids = items };
}