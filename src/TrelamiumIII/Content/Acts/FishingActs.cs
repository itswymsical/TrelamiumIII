using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

internal sealed class FishingActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        Catch("act.evilfish", ActSets.EvilBiomeFish),
        Catch("act.hallowfish", ActSets.HallowFish),
        Catch("act.jellyfishing", ActSets.JellyFih),
        Catch("act.hellfishing", [ItemID.Obsidifish, ItemID.FlarefinKoi]),
        Catch("act.commonfish", ActSets.CommonFish),
        Catch("act.uncommonfish", ActSets.UncommonFish),
        Catch("act.rarefish", ActSets.RareFish),
        Catch("act.legendfish", ActSets.LegendaryFish),
        Catch("act.icefish", [ItemID.AtlanticCod, ItemID.FrostMinnow], multiplier: 0.8f),
        Catch("act.junglefish", [ItemID.NeonTetra, ItemID.DoubleCod, ItemID.VariegatedLardfish], multiplier: 0.8f),

        Crates("act.phmcrates", static type => ItemID.Sets.IsFishingCrate[type]),
        Crates("act.hmcrates", static type => ItemID.Sets.IsFishingCrateHardmode[type]),
    ];

    private static DataAct Catch(string id, int[] items, float? multiplier = null)
        => new(id, ActCategory.Fishing, GameEventType.CaughtFish, ActCurves.Fishing) { Ids = items, Multiplier = multiplier };

    private static DataAct Crates(string id, Func<int, bool> isCrate)
        => new(id, ActCategory.Fishing, GameEventType.CaughtFish, ActCurves.Fishing) { Test = isCrate };
}
