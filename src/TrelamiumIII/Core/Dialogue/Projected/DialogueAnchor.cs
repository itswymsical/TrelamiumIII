namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>What a projected panel hangs off, and where that lands on screen this frame.</summary>
/// <remarks>
///     <para>
///         Held as an NPC slot plus the type that was in it, not as an <see cref="NPC" /> reference.
///         Slots are recycled, so a speaker that dies mid-line would otherwise be silently replaced by
///         whatever spawned into its index -- and the panel would fly across the world to follow a
///         zombie. Checking the type back is what turns that into a clean fall back to the player.
///     </para>
/// </remarks>
public readonly struct DialogueAnchor
{
    private readonly int npcIndex;
    private readonly int npcType;

    private DialogueAnchor(int npcIndex, int npcType)
    {
        this.npcIndex = npcIndex;
        this.npcType = npcType;
    }

    /// <summary>Hovers centred over the local player. The default when no speaker is known.</summary>
    public static DialogueAnchor OnPlayer => new(-1, 0);

    /// <summary>Hangs above the NPC's head and follows it.</summary>
    public static DialogueAnchor OnSpeaker(NPC? npc) =>
        npc is { active: true } ? new DialogueAnchor(npc.whoAmI, npc.type) : OnPlayer;

    /// <summary>
    ///     Finds whoever is speaking this line and hangs off them, falling back to
    ///     <paramref name="fallback" /> when nobody in the world answers to the name.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Resolved per line rather than per conversation, dialogue entries can swap speakers mid conversation..
    ///     </para>
    ///     <para>
    ///         This is also what lets a dialogue be started from anywhere: a job step, a world
    ///         event, etc.
    ///     </para>
    /// </remarks>
    public static DialogueAnchor ForSpeaker(DialogueData? line, DialogueAnchor fallback)
    {
        if (line is null)
            return fallback;

        if (line.IsPlayer)
            return OnPlayer;

        NPC? match = FindByName(line.SpeakerType) ?? FindByName(line.Speaker);

        return match is not null ? OnSpeaker(match) : fallback;
    }

    /// <summary>
    ///     The nearest active NPC answering to this name, by given name or by type name.
    /// </summary>
    private static NPC? FindByName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        NPC? nearest = null;
        float best = float.MaxValue;
        Vector2 from = Main.LocalPlayer.Center;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active)
                continue;

            bool named = string.Equals(npc.GivenName, name, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(npc.TypeName, name, StringComparison.OrdinalIgnoreCase);

            if (!named)
                continue;

            float distance = Vector2.DistanceSquared(npc.Center, from);

            if (distance >= best)
                continue;

            best = distance;
            nearest = npc;
        }

        return nearest;
    }

    /// <summary>True while this anchor still names a live speaker.</summary>
    public bool HasSpeaker => Speaker is not null;

    /// <summary>
    ///     The NPC this anchor names, or null if the slot no longer holds them.
    /// </summary>
    /// <remarks>
    ///     Re-resolved on every read rather than cached, which is what makes it safe to hold an
    ///     anchor across a conversation: a speaker that dies or despawns starts answering null on its
    ///     own, and anything waiting on it stops waiting.
    /// </remarks>
    public NPC? Speaker
    {
        get
        {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
                return null;

            NPC candidate = Main.npc[npcIndex];
            return candidate is { active: true } && candidate.type == npcType ? candidate : null;
        }
    }

    /// <summary>
    ///     The point the panel's tail points at, in world coordinates: the top of the speaker's
    ///     head, or the top of the player's.
    /// </summary>
    public Vector2 WorldPoint()
    {
        NPC? speaker = Speaker;

        if (speaker is not null)
            return speaker.Top + new Vector2(0f, -8f);

        Player player = Main.LocalPlayer;
        return player.Top + new Vector2(0f, -8f);
    }

    /// <summary>
    ///     The same point in raw screen pixels.
    /// </summary>
    /// <remarks>
    ///     The panel draws on the UI layer but lives in the world, so it has to be taken through the
    ///     game view transform by hand. Without it the bubble drifts off its speaker the moment the
    ///     camera zooms, which the dialogue system does on its own for cutscenes.
    /// </remarks>
    public Vector2 ScreenPoint() =>
        Vector2.Transform(WorldPoint() - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
}
