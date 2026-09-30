namespace TrelamiumIII.Core.Graphics.Cinematics.BossIntros;

/// <summary>
///     One boss's title sequence: what the card says, and the beats it says it on.
/// </summary>
/// <remarks>
///     Purely a description; <see cref="BossIntroSystem" /> owns the clock and every effect. Ticks are
///     measured from <see cref="BossIntroSystem.Play" />. With <see cref="AutoSettle" /> the system also
///     ends the entrance; without it the boss calls <see cref="BossIntroSystem.Settle" /> on arrival.
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
    ///     Whether the system settles the entrance itself, <see cref="Duration" /> after <see cref="RevealTick" />.
    ///     Turn off for an arrival that ends on an event (a landing) and call <see cref="BossIntroSystem.Settle" /> there.
    /// </summary>
    public bool AutoSettle { get; init; } = true;

    /// <summary>Frames after <see cref="RevealTick" /> an auto-settled entrance holds before its outro.</summary>
    public int Duration { get; init; } = 100;

    /// <summary>
    ///     Hard stop for a manual settle that never comes, e.g. a boss killed or despawned mid-entrance. Also caps
    ///     <see cref="Duration" />.
    /// </summary>
    public int MaxLength { get; init; } = 60 * 14;

    public float LineIntensity { get; init; } = 0.8f;

    public float LineDensity { get; init; } = 48f;

    public int LineRamp { get; init; } = 26;

    public float LetterboxHeight { get; init; } = 0.085f;

    public int LetterboxTicks { get; init; } = 34;
}
