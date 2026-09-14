using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     How a dialogue box asks the speaking NPC to act like it is speaking.
/// </summary>
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
