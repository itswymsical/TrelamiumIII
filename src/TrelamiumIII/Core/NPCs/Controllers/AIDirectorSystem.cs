using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Core.Components.NPCs;

namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// The registry that turns "this NPC type is directed" into a live
/// <see cref="NPCDirector"/> on every one that spawns.
///
/// <para>
/// Register a program for an NPC type and every instance of it gets that program
/// attached at spawn. This is where boss reworks live: the vanilla fight keeps its
/// identity because the vanilla AI is still in there running, and the program
/// decides when to let it run and when to cut to something new.
/// </para>
///
/// <example>
/// A King Slime that spends ten seconds being King Slime, then does something else:
/// <code>
/// AIDirectorSystem.Register(NPCID.KingSlime, npc =&gt; Behavior.Forever(() =&gt; Behavior.Seq(
///     Behavior.Vanilla(duration: 600),
///     Behavior.Preserving(new SlimeRainBarrage())
/// )));
/// </code>
/// The <c>Preserving</c> wrapper hands the boss its own AI slots back afterwards,
/// so it resumes its pattern instead of restarting it.
/// </example>
/// </summary>
public sealed class AIDirectorSystem : ModSystem
{
    /// <summary>A registered program plus the condition under which it applies.</summary>
    public readonly record struct Registration(
        Func<NPC, IBehaviorModule> Program,
        Func<NPC, bool> Condition);

    private static readonly Dictionary<int, Registration> registrations = [];

    /// <summary>
    /// Registers a program for an NPC type. <paramref name="condition"/>, if given,
    /// is checked at spawn - use it to gate a rework behind world progression or a
    /// config toggle.
    /// </summary>
    public static void Register(int npcType, Func<NPC, IBehaviorModule> program, Func<NPC, bool> condition = null)
    {
        if (program == null)
            throw new ArgumentNullException(nameof(program));

        registrations[npcType] = new Registration(program, condition);
    }

    /// <summary>Removes a registration. Instances already spawned keep their director.</summary>
    public static void Unregister(int npcType) => registrations.Remove(npcType);

    /// <summary>True when <paramref name="npcType"/> has a program registered.</summary>
    public static bool IsRegistered(int npcType) => registrations.ContainsKey(npcType);

    /// <summary>
    /// Attaches and starts the registered program on <paramref name="npc"/>.
    /// Returns false when nothing is registered or the condition rejected it.
    /// </summary>
    public static bool TryAttach(NPC npc)
    {
        if (!registrations.TryGetValue(npc.type, out Registration registration))
            return false;

        if (registration.Condition != null && !registration.Condition(npc))
            return false;

        if (!npc.TryEnable(out NPCDirector director))
            return false;

        IBehaviorModule program = registration.Program(npc);
        if (program == null)
            return false;

        director.Play(npc, program);
        return true;
    }

    public override void Unload() => registrations.Clear();

    /// <summary>
    /// Watches every spawn and attaches directors to registered types.
    /// Kept nested so the registry and the thing that reads it stay together.
    /// </summary>
    private sealed class Attachment : GlobalNPC
    {
        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (registrations.Count > 0)
                TryAttach(npc);
        }
    }
}
