using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.Menus;

/// <summary>
///     The Illuminant Inkiness theme and its intense mix as one score. Both decode to the same length, so they run side by side
///     and the intense one fades up: <c>MusicDirector.Play(Create())</c>, then <c>MusicDirector.Crossfade(BASE, INTENSE, fade)</c>.
/// </summary>
internal static class IlluminantInkMusic
{
    public const string BASE = "Base";

    public const string INTENSE = "Intense";

    /// <summary>Decoded length. Both arrangements agree on it exactly, which is why this works.</summary>
    private const float LOOP_SECONDS = 232.776f;

    /// <summary>TODO: the real tempo. Crossfades don't use it, but it must be right before cues are scheduled on this score.</summary>
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
