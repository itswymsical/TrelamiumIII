using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

internal sealed class CoinActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        Pickup("act.coins", ActCategory.Exploration, [ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin]),
    ];

    private static DataAct Pickup(string id, ActCategory category, int[] items)
        => new(id, category, GameEventType.ItemPickup, ActCurves.Coins) { Ids = items };
}