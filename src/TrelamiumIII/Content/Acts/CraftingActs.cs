using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

internal sealed class CraftingActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        new DataAct("act.bars", ActCategory.Crafting, GameEventType.ItemCraft, ActCurves.Craft) { Ids = ActSets.Bars },
        new DataAct("act.planting", ActCategory.Building, GameEventType.TilePlace, ActCurves.Deliberate) { Ids = ActSets.HerbTiles },
    ];
}
