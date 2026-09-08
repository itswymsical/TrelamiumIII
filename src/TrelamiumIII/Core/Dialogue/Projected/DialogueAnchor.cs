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

    /// <summary>True while this anchor still names a live speaker.</summary>
    public bool HasSpeaker => Speaker is not null;

    private NPC? Speaker
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
    ///     camera zooms -- which the dialogue system does on its own, for scenes.
    /// </remarks>
    public Vector2 ScreenPoint() =>
        Vector2.Transform(WorldPoint() - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
}
