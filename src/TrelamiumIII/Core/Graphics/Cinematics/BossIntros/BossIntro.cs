namespace TrelamiumIII.Core.Graphics.Cinematics.BossIntros;

/// <summary>
///     One boss's title sequence: what the card says, and the beats it says it on.
/// </summary>
/// <remarks>
///     <para>
///         Purely a description. <see cref="BossIntroSystem" /> owns the clock, the letterbox, the
///         shader and the shake; a boss owns nothing but an instance of this and the two calls that
///         start and end it. That split is what keeps the second boss from being a copy of the first
///         one's cutscene class.
///     </para>
///     <para>
///         <b>Ticks are measured from <see cref="BossIntroSystem.Play" />.</b> The one number the
///         intro cannot know is when the boss actually finishes arriving -- that depends on how far
///         it had to fall -- so the tail is not on the timeline at all. The boss calls
///         <see cref="BossIntroSystem.Settle" /> when it lands, and <see cref="MaxLength" /> is only
///         there in case it never does.
///     </para>
/// </remarks>
internal sealed class BossIntro
{
    /// <summary>Name across the top bar.</summary>
    public required string Name { get; init; }

    /// <summary>Optional line under the name. Kept short -- it is a subtitle, not a description.</summary>
    public string? Epithet { get; init; }

    public required string TrackTitle { get; init; }

    public required string TrackArtist { get; init; }

    /// <summary>
    ///     Colour of the name, the rules and the track marker.
    /// </summary>
    /// <remarks>
    ///     Deliberately per-boss rather than one house colour: the card is the first thing the fight
    ///     says, and it should already be the right temperature for what is about to walk out.
    /// </remarks>
    public Color Accent { get; init; } = new(255, 196, 104);

    /// <summary>
    ///     Frame the letterbox, the lines and the card all arrive on.
    /// </summary>
    /// <remarks>
    ///     Everything before this is the approach -- interface clearing, ground rumbling -- and the
    ///     boss should break cover on exactly this tick. A boss whose own arrival is a frame off from
    ///     the bars reads as two unrelated events.
    /// </remarks>
    public int RevealTick { get; init; } = 60;

    /// <summary>Camera shake held through the approach.</summary>
    public int RumbleShake { get; init; } = 5;

    /// <summary>One-off shake spike on <see cref="RevealTick" />.</summary>
    public int RevealShake { get; init; } = 15;

    /// <summary>Frames after <see cref="RevealTick" /> before the track card slides in.</summary>
    public int CardDelay { get; init; } = 24;

    /// <summary>
    ///     Frames the track card stays up once it has arrived.
    /// </summary>
    /// <remarks>
    ///     Independent of the rest of the sequence, and usually longer: the bars come off as soon as
    ///     the fight starts, but the card is the only place the soundtrack is ever credited.
    /// </remarks>
    public int CardHold { get; init; } = 60 * 7;

    /// <summary>Frames the bars, lines and card take to leave once the fight starts.</summary>
    public int OutroTicks { get; init; } = 45;

    /// <summary>
    ///     Hard stop, in case the boss never reports having landed.
    /// </summary>
    /// <remarks>
    ///     A boss killed, despawned or knocked into a pit mid-entrance would otherwise leave the
    ///     interface hidden behind a letterbox with nothing left alive to take it down.
    /// </remarks>
    public int MaxLength { get; init; } = 60 * 14;

    public float LineIntensity { get; init; } = 0.8f;

    public float LineDensity { get; init; } = 48f;

    public int LineRamp { get; init; } = 26;

    public float LetterboxHeight { get; init; } = 0.085f;

    public int LetterboxTicks { get; init; } = 34;
}
