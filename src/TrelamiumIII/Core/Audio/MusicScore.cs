namespace TrelamiumIII.Core.Audio;

/// <summary>
///     How a layer relates to the rest of the score.
/// </summary>
internal enum MusicLayerKind
{
    /// <summary>
    ///     Starts with the score and never restarts; gain is the only control it has.
    /// </summary>
    /// <remarks>
    ///     This is what makes a variant swap seamless. Two stems that have been rolling side by side
    ///     since the downbeat are, by construction, at the same place in the music, so moving between
    ///     them is a fader move rather than a transition. Nothing is ever seeked, because nothing in
    ///     this audio stack can seek.
    /// </remarks>
    Stem,

    /// <summary>
    ///     Started from its own beginning when fired, on a musical boundary.
    /// </summary>
    /// <remarks>
    ///     For material that is an <em>extension</em> of the track rather than an alternative to it --
    ///     a death fill, a stinger. It cannot ride along silently like a stem, because it has to begin
    ///     at its own first sample at a moment chosen at runtime.
    /// </remarks>
    Cue,
}

/// <summary>
///     Where in the bar a <see cref="MusicLayerKind.Cue" /> is allowed to land.
/// </summary>
internal enum MusicGrid
{
    /// <summary>Next frame, wherever that falls. Musically, a gamble.</summary>
    Immediate,

    Beat,
    HalfBar,
    Bar,
    TwoBars,
    FourBars,
}

/// <summary>One track within a score.</summary>
internal sealed class MusicLayer
{
    /// <summary>How callers refer to it. Unique within a score.</summary>
    public required string Name { get; init; }

    public required int Slot { get; init; }

    public MusicLayerKind Kind { get; init; } = MusicLayerKind.Stem;

    /// <summary>
    ///     Static trim, applied on top of the fader.
    /// </summary>
    /// <remarks>
    ///     For a stem that was bounced hotter or quieter than the rest of the set. Balance belongs in
    ///     the audio files; this is here so a mismatch can be fixed without a re-export.
    /// </remarks>
    public float Gain { get; init; } = 1f;

    /// <summary>Whether a stem is audible the moment the score starts.</summary>
    public bool StartsOn { get; init; }

    /// <summary>
    ///     Decoded length in seconds. Cues only.
    /// </summary>
    /// <remarks>
    ///     tModLoader loops every music track unconditionally and exposes no way to ask it not to, so
    ///     a cue has to be taken off by the clock or it will play its four bars over and over. Measure
    ///     the <em>decoded</em> length, not the musical one -- see <see cref="MusicScore.LoopSeconds" />.
    /// </remarks>
    public float Seconds { get; init; }
}

/// <summary>
///     A piece of music: the tracks it is made of, and the tempo grid they share.
/// </summary>
/// <remarks>
///     <para>
///         A score is a description and holds no state; <see cref="MusicDirector" /> runs it. Build a
///         fresh one per playthrough of the piece rather than caching a static instance -- they are
///         cheap, and a captured <see cref="StaysActive" /> closure usually wants the current fight.
///     </para>
/// </remarks>
internal sealed class MusicScore
{
    public required string Name { get; init; }

    /// <summary>
    ///     The layers, in order. The first <see cref="MusicLayerKind.Stem" /> is the primary.
    /// </summary>
    public required MusicLayer[] Layers { get; init; }

    public float Bpm { get; init; } = 120f;

    public int BeatsPerBar { get; init; } = 4;

    /// <summary>
    ///     Seconds into the file where beat one lands.
    /// </summary>
    /// <remarks>
    ///     Usually zero. Worth setting if the export carries silence at the head, which MP3 always
    ///     does -- the format has no way to express "start here", so an encoder pads the first frame
    ///     and every decoder plays that padding back.
    /// </remarks>
    public float GridOffset { get; init; }

    /// <summary>
    ///     Decoded length of the primary stem in seconds, or 0 to let the grid run free.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the <b>decoded</b> length, which is not the same as the musical length. Set it
    ///         and the beat grid restarts exactly when the audio loops, so quantisation error is
    ///         bounded at whatever the file's own padding is instead of compounding once per loop.
    ///     </para>
    ///     <para>
    ///         Leave it at zero for a format that loops sample-exactly, where the musical grid and the
    ///         file agree forever and wrapping the grid would only add error.
    ///     </para>
    /// </remarks>
    public float LoopSeconds { get; init; }

    /// <summary>
    ///     Checked every frame; the score releases itself when it returns false.
    /// </summary>
    /// <remarks>
    ///     So a fight's music does not have to be stopped from four different places -- death,
    ///     despawn, the player dying, the world unloading. A cue that is mid-flight defers it.
    /// </remarks>
    public Func<bool>? StaysActive { get; init; }

    public float SecondsPerBeat => 60f / Math.Max(Bpm, 1f);

    public float SecondsPerBar => SecondsPerBeat * Math.Max(BeatsPerBar, 1);

    /// <summary>The stem vanilla is told is the current track. Everything else rides alongside it.</summary>
    public MusicLayer Primary => Array.Find(Layers, layer => layer.Kind == MusicLayerKind.Stem) ?? Layers[0];

    public int IndexOf(string name) => Array.FindIndex(Layers, layer => layer.Name == name);
}
