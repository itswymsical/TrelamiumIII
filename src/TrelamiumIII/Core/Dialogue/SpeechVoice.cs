using Terraria.Audio;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     The typewriter's clock and the speaker's voice, kept together because they are the same thing
///     heard twice.
/// </summary>
/// <remarks>
///     <para>
///         Both halves exist to fix a quantisation problem. Typing used to count whole frames to the
///         next character, so the only speeds available were a character every 1, 2 or 3 frames --
///         asking for thirty percent faster landed on fifty, and a <c>Slow</c> affect could not slow
///         anything by less than half. Here a character costs a fractional number of frames and the
///         remainder is carried, so any speed is reachable and per-character affects mean what they
///         say.
///     </para>
///     <para>
///         The voice has the opposite problem. Firing a sample per character means samples land
///         wherever the typing happens to put them, at intervals shorter than the samples themselves,
///         so they pile into each other and read as a rattle. Snapping playback to a fixed
///         subdivision of the game clock -- and collapsing everything typed since the last one into a
///         single hit -- puts speech on a beat instead. Nothing overlaps, and the ticks arrive
///         evenly however unevenly the letters do.
///     </para>
///     <para>
///         The grid is taken from <see cref="Main.GameUpdateCount" /> rather than counted locally, so
///         every speaker in earshot lands on the same beats.
///     </para>
/// </remarks>
public sealed class SpeechVoice
{
    /// <summary>
    ///     Global multiplier on typing speed, over and above a line's own <c>Speed</c>.
    /// </summary>
    public const float SPEED = 1.3f;

    /// <summary>Most frames of credit that may be banked, so a pause does not dump a sentence.</summary>
    private const float MAX_CREDIT = 2f;

    private float credit;

    /// <summary>
    ///     Cost of the character the clock is waiting on. The bank may always reach it plus one frame, or a character
    ///     dearer than <see cref="MAX_CREDIT" /> (any line slower than ~1.15) would never be revealed.
    /// </summary>
    private float waiting;

    private int cursor;

    private bool pending;
    private SoundStyle? pendingSound;
    private float pendingPitch = 1f;

    /// <summary>Clears per-line state. Voice cursors are not carried between lines.</summary>
    public void Reset()
    {
        credit = 0f;
        waiting = 0f;
        cursor = 0;
        pending = false;
        pendingSound = null;
        pendingPitch = 1f;
    }

    /// <summary>Grants this frame's typing budget. Call once per update, before revealing.</summary>
    public void Tick() => credit = MathF.Min(credit + 1f, MathF.Max(MAX_CREDIT, waiting + 1f));

    /// <summary>
    ///     Spends the budget for one character, if there is enough.
    /// </summary>
    /// <param name="baseDelay">Frames per character at speed 1, from the line.</param>
    /// <param name="lineSpeed">The line's own speed multiplier.</param>
    /// <param name="affectSpeed">This character's affect speed multiplier.</param>
    /// <returns>False when the next character has to wait for a later frame.</returns>
    public bool TryReveal(float baseDelay, float lineSpeed, float affectSpeed)
    {
        float rate = MathF.Max(lineSpeed * affectSpeed * SPEED, 0.01f);
        float cost = MathF.Max(baseDelay, 0.01f) / rate;

        if (credit < cost)
        {
            waiting = cost;
            return false;
        }

        credit -= cost;
        waiting = 0f;
        return true;
    }

    /// <summary>Drops any banked credit, so a hold resumes at full cost rather than catching up.</summary>
    public void Interrupt() => credit = 0f;

    /// <summary>
    ///     Queues a voice hit for the next beat.
    /// </summary>
    /// <remarks>
    ///     Whitespace is silent. At speaking speed a sample on the gap between words reads as a
    ///     stutter rather than as speech, and the gap is exactly where the ear expects a rest.
    /// </remarks>
    public void Note(char revealed, DialogueTick tick)
    {
        if (char.IsWhiteSpace(revealed))
            return;

        pending = true;

        // Last write wins. Several characters can land between two beats and only one of them is
        // going to be heard, so it may as well be the most recent.
        pendingSound = tick.Sound;
        pendingPitch = tick.Pitch;
    }

    /// <summary>
    ///     Plays at most one queued hit, on the beat. Call once per update, after revealing.
    /// </summary>
    public void Play(ISpeakerProfile? profile, Vector2 position)
    {
        if (!pending)
            return;

        int subdivision = Math.Max(profile?.Voice.Subdivision ?? 1, 1);

        if (subdivision > 1 && Main.GameUpdateCount % (uint)subdivision != 0)
            return;

        pending = false;

        SoundStyle? selected = pendingSound ?? profile?.Voice.Next(ref cursor);

        pendingSound = null;
        float pitch = pendingPitch;
        pendingPitch = 1f;

        if (!selected.HasValue)
            return;

        SoundStyle style = selected.Value;

        if (pitch != 1f)
            style = style with { Pitch = pitch - 1f };

        SoundEngine.PlaySound(style, position);
    }
}
