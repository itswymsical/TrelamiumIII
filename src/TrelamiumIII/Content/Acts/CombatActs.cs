using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>Killing families of enemies, scaled by species met on top of per-kill combat Exp.</summary>
internal sealed class CombatActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        Kill("act.slimes", ActSets.Slimes),
        Kill("act.undead", ActSets.Undead),
        Kill("act.eyeballs", ActSets.Eyes),
    ];

    private static DataAct Kill(string id, int[] npcs)
        => new(id, ActCategory.Combat, GameEventType.NPCKill, ActCurves.Combat) { Ids = npcs };
}
