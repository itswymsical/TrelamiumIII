using System.Collections.Generic;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
/// A dialogue an NPC opens in place of its vanilla chat window, and how to present it.
///
/// <para>
/// <see cref="None"/> - the default value - means "no takeover": the NPC keeps the
/// scrolling text box, its shop and its help button, exactly as vanilla built it.
/// </para>
/// </summary>
public readonly record struct ChatEncounter
{
    // Stored inverted so a default-constructed encounter still roots the speaker.
    // Built by object initializer it would otherwise silently opt out.
    private bool RootingWaived { get; init; }

    /// <param name="dialogueKey">Key into the <see cref="DialogueRegistry"/>, e.g. "Kraken.Greeting".</param>
    public ChatEncounter(string dialogueKey) => DialogueKey = dialogueKey;

    /// <summary>The sequence to play. Null or empty means no takeover.</summary>
    public string DialogueKey { get; init; }

    /// <summary>Pull the camera in while the dialogue runs.</summary>
    public bool ZoomIn { get; init; }

    /// <summary>Hide the HUD behind letterbox bars while the dialogue runs.</summary>
    public bool Letterbox { get; init; }

    /// <summary>Music to swap to for the duration, or null to leave it alone.</summary>
    public int? Music { get; init; }

    /// <summary>
    /// Root the speaker in place, face it at the listener and animate a talking loop
    /// until the dialogue closes. Needs an <see cref="NPCDirector"/>; quietly skipped
    /// for NPCs that won't take one. On by default.
    /// </summary>
    public bool RootWhileSpeaking => !RootingWaived;

    /// <summary>The NPC keeps its vanilla chat window.</summary>
    public static ChatEncounter None => default;

    public bool Exists => !string.IsNullOrEmpty(DialogueKey);

    /// <summary>Shorthand for <c>new ChatEncounter(key)</c>, for use in expression bodies.</summary>
    public static ChatEncounter Play(string dialogueKey) => new(dialogueKey);

    /// <summary>With zoom and letterbox.</summary>
    public ChatEncounter AsScene() => this with { ZoomIn = true, Letterbox = true };

    public ChatEncounter WithMusic(int music) => this with { Music = music };

    /// <summary>Leave the speaker to whatever it was already doing.</summary>
    public ChatEncounter Unrooted() => this with { RootingWaived = true };
}

/// <summary>
/// Implemented by a <see cref="ModNPC"/> whose conversation is narrative rather than
/// flavour text - one that exists to say something specific and has no business with
/// the vanilla chat box.
///
/// <para>
/// Return <see cref="ChatEncounter.None"/> to fall back to the vanilla window for that
/// click; an NPC can be narrative only some of the time.
/// </para>
/// </summary>
public interface INarrativeSpeaker
{
    /// <summary>What this NPC opens when <paramref name="player"/> talks to it.</summary>
    ChatEncounter GetEncounter(Player player);
}

/// <summary>
/// Decides, per click, whether an NPC talks through the <see cref="DialogueManager"/>
/// or through the vanilla chat window, and plays the result.
///
/// <para>
/// Three ways in, asked in this order:
/// <list type="number">
///   <item>an ongoing <see cref="Job"/> whose provider is this NPC - work underway
///         outranks whatever the NPC would otherwise say;</item>
///   <item>a resolver registered against the NPC's type via <see cref="Register"/> -
///         how a <em>vanilla</em> NPC gets narrative chat without a subclass;</item>
///   <item>the NPC itself, when its <see cref="ModNPC"/> is an <see cref="INarrativeSpeaker"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// <see cref="NarrativeChatSystem"/> wires this into the one place vanilla opens a chat
/// window, so nothing has to be routed through it by hand.
/// </para>
/// </summary>
public static class NarrativeChat
{
    /// <summary>What an NPC type says to a given player, or <see cref="ChatEncounter.None"/>.</summary>
    public delegate ChatEncounter ChatResolver(NPC npc, Player player);

    private static readonly Dictionary<int, List<ChatResolver>> resolversByType = [];

    /// <summary>
    /// Routes an NPC type's chat through the dialogue system whenever
    /// <paramref name="resolver"/> returns an encounter. Register during load.
    /// <para>
    /// Several resolvers may share a type; the most recently registered is asked first,
    /// so a later system can shadow an earlier one.
    /// </para>
    /// </summary>
    public static void Register(int npcType, ChatResolver resolver)
    {
        if (resolver == null)
            return;

        if (!resolversByType.TryGetValue(npcType, out List<ChatResolver>? resolvers))
            resolversByType[npcType] = resolvers = [];

        resolvers.Add(resolver);
    }

    public static void Unregister(int npcType, ChatResolver resolver)
    {
        if (resolversByType.TryGetValue(npcType, out List<ChatResolver>? resolvers))
            resolvers.Remove(resolver);
    }

    internal static void Unload() => resolversByType.Clear();

    /// <summary>
    /// What <paramref name="npc"/> would open for <paramref name="player"/> instead of the
    /// vanilla chat window. <see cref="ChatEncounter.None"/> means it keeps it.
    /// </summary>
    public static ChatEncounter Resolve(NPC npc, Player player)
    {
        if (npc == null || !npc.active || player == null)
            return ChatEncounter.None;

        // Work already underway with this NPC has more to say than its greeting does.
        foreach (Job job in player.GetModPlayer<JobPlayer>().ActiveJobs())
        {
            if (!job.IsProvidedBy(npc))
                continue;

            ChatEncounter fromJob = job.GetChatEncounter(npc, player);
            if (Playable(fromJob))
                return fromJob;
        }

        if (resolversByType.TryGetValue(npc.type, out List<ChatResolver>? resolvers))
        {
            for (int i = resolvers.Count - 1; i >= 0; i--)
            {
                ChatEncounter registered = resolvers[i](npc, player);
                if (Playable(registered))
                    return registered;
            }
        }

        if (npc.ModNPC is INarrativeSpeaker speaker)
        {
            ChatEncounter own = speaker.GetEncounter(player);
            if (Playable(own))
                return own;
        }

        return ChatEncounter.None;
    }

    /// <summary>
    /// True when this encounter's line is already on screen or queued behind one. The
    /// <see cref="DialogueManager"/> queues rather than drops, so without this a held
    /// right-click would line the same box up several times over.
    /// </summary>
    public static bool IsPending(ChatEncounter encounter)
        => encounter.Exists
        && (DialogueManager.Instance.IsDialogueActive(encounter.DialogueKey)
         || DialogueManager.Instance.IsDialogueQueued(encounter.DialogueKey));

    /// <summary>
    /// Plays <paramref name="encounter"/>, rooting the speaker for its duration.
    /// Returns false if the dialogue would not open.
    /// </summary>
    /// <remarks>
    /// The speaker is no longer rooted from here. The box does it, per line, through
    /// <see cref="DialogueBehavior"/> - which is what makes a two-hander face the player with
    /// whoever is actually talking, and what gives a dialogue opened from a job step or a
    /// command the same behaviour as one opened by clicking an NPC. All this passes on is
    /// whether the encounter wanted it at all.
    /// </remarks>
    public static bool Begin(NPC npc, ChatEncounter encounter)
    {
        if (!encounter.Exists)
            return false;

        return DialogueManager.Instance.StartDialogue(
            encounter.DialogueKey, encounter.ZoomIn, encounter.Letterbox, encounter.Music,
            DialogueAnchor.OnSpeaker(npc), encounter.RootWhileSpeaking);
    }

    /// <summary>
    /// A key with no registered sequence builds an empty box, so an encounter naming one
    /// counts as absent and the NPC keeps its vanilla chat rather than opening a blank panel.
    /// </summary>
    private static bool Playable(ChatEncounter encounter)
        => encounter.Exists && DialogueRegistry.Has(encounter.DialogueKey);
}
