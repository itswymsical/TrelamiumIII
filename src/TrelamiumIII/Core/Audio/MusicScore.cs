namespace TrelamiumIII.Core.Audio;

/// <summary>How a layer relates to the rest of the score.</summary>
internal enum MusicLayerKind
{
    /// <summary>Starts with the score and never restarts; only gain changes, so swapping between stems is seamless.</summary>
    Stem,

    /// <summary>Started from its own beginning on a musical boundary when fired, e.g. a death fill or stinger.</summary>
    Cue,
}

/// <summary>Where in the bar a <see cref="MusicLayerKind.Cue" /> may land.</summary>
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

    /// <summary>Static trim on top of the fader, for a stem bounced hotter or quieter than the rest.</summary>
    public float Gain { get; init; } = 1f;

    /// <summary>Whether a stem is audible the moment the score starts.</summary>
    public bool StartsOn { get; init; }

    /// <summary>Decoded length in seconds, cues only. tModLoader always loops music, so cues must be stopped by the clock.</summary>
    public float Seconds { get; init; }
}

/// <summary>
///     A stateless description of a piece's tracks and tempo grid, run by <see cref="MusicDirector" />. Build one per playthrough.
/// </summary>
internal sealed class MusicScore
{
    public required string Name { get; init; }

    /// <summary>The layers, in order; the first <see cref="MusicLayerKind.Stem" /> is the primary.</summary>
    public required MusicLayer[] Layers { get; init; }

    public float Bpm { get; init; } = 120f;

    public int BeatsPerBar { get; init; } = 4;

    /// <summary>Seconds into the file where beat one lands; MP3 exports always carry some head padding.</summary>
    public float GridOffset { get; init; }

    /// <summary>
    ///     Decoded primary stem length (0 lets the grid run free), so the beat grid restarts with the audio loop. Leave 0 for sample-exact formats.
    /// </summary>
    public float LoopSeconds { get; init; }

    /// <summary>Checked every frame; the score releases itself when this returns false. Deferred while a cue is playing.</summary>
    public Func<bool>? StaysActive { get; init; }

    public float SecondsPerBeat => 60f / Math.Max(Bpm, 1f);

    public float SecondsPerBar => SecondsPerBeat * Math.Max(BeatsPerBar, 1);

    /// <summary>The stem vanilla is told is the current track. Everything else rides alongside it.</summary>
    public MusicLayer Primary => Array.Find(Layers, layer => layer.Kind == MusicLayerKind.Stem) ?? Layers[0];

    public int IndexOf(string name) => Array.FindIndex(Layers, layer => layer.Name == name);
}
