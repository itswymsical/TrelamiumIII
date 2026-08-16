using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.Menus;

/// <summary>
///     The Illuminant Inkiness theme and its intense arrangement, as one score.
/// </summary>
/// <remarks>
///     <para>
///         The two files are the same performance, mixed twice, and decode to exactly the same length
///         -- so they can be run side by side and the intense one brought up on a fader. Nothing
///         restarts and nothing is seeked; the arrangement changes underneath a piece of music that
///         never stopped.
///     </para>
///     <para>
///         Usage is two calls. A cutscene opens with
///         <c>MusicDirector.Play(IlluminantInkMusic.Create())</c>, and when the boss arrives,
///         <c>MusicDirector.Crossfade(BASE, INTENSE, fade)</c>.
///     </para>
/// </remarks>
internal static class IlluminantInkMusic
{
    public const string BASE = "Base";

    public const string INTENSE = "Intense";

    /// <summary>Decoded length. Both arrangements agree on it exactly, which is why this works.</summary>
    private const float LOOP_SECONDS = 232.776f;

    /// <summary>
    ///     TODO: the real tempo.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="MusicLayerKind.Cue" /> scheduling and the public beat readouts use it, and
    ///     this score has neither, so a placeholder is harmless here -- a crossfade is not quantised.
    ///     It has to be right before anything is fired against this score.
    /// </remarks>
    private const float BPM = 120f;

    public static MusicScore Create() => new()
    {
        Name = "Illuminant Inkiness",

        Bpm = BPM,
        BeatsPerBar = 4,
        LoopSeconds = LOOP_SECONDS,

        Layers =
        [
            new MusicLayer
            {
                Name = BASE,
                Slot = Assets.Music.IlluminantInkiness.Slot,
                StartsOn = true,
            },

            new MusicLayer
            {
                Name = INTENSE,
                Slot = Assets.Music.IlluminantInkiness_Intense.Slot,

                // Rolling from the downbeat, silent, waiting to be faded up.
                StartsOn = false,
            },
        ],
    };
}
