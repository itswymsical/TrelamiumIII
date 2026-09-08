using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     How a dialogue box asks the speaking NPC to act like it is speaking.
/// </summary>
/// <remarks>
///     <para>
///         One convention, owned by the box rather than by the thing that opened it. Rooting used to
///         be applied at the point an NPC was clicked, which meant it only ever happened for that one
///         entry: a conversation started from a job step, a world event or a command rooted nobody,
///         and a conversation with two people in it rooted whoever was clicked for the whole of it,
///         including the lines they were not saying.
///     </para>
///     <para>
///         Driving it from the line instead means the speaker faces the player because they are
///         speaking, not because they were talked to.
///     </para>
///     <para>
///         Nothing here has to be torn down. The behaviour is an interrupt that ends itself the
///         moment <c>stillSpeaking</c> goes false -- which covers the line moving to someone else, the
///         box closing, and the NPC dying mid-sentence -- so whatever it was doing before resumes on
///         its own.
///     </para>
/// </remarks>
public static class DialogueBehavior
{
    /// <summary>
    ///     Roots <paramref name="npc" /> facing the player, animating while it has words coming out.
    /// </summary>
    /// <param name="stillSpeaking">Ends the behaviour when it goes false.</param>
    /// <param name="voicing">
    ///     Whether characters are arriving right now. Drives the mouth: a written pause holds the
    ///     rest frame rather than carrying on mouthing at nothing.
    /// </param>
    /// <param name="emotes">Idle chatter bubbles. Off by default -- the box is already talking.</param>
    public static void Root(NPC? npc, Func<bool> stillSpeaking, Func<bool> voicing, bool emotes = false)
    {
        if (Main.dedServ || npc is not { active: true })
            return;

        // Quietly skipped for NPCs that will not take a director, which is most of them.
        if (!npc.TryEnable(out NPCDirector director))
            return;

        director.Interrupt(npc, new Talk(_ => stillSpeaking())
        {
            Speaking = _ => voicing(),
            EmoteInterval = emotes ? 200 : 0,
        });
    }
}
