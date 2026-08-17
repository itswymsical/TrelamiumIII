using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     Fungore's score: the loop, and the fill that finishes it.
/// </summary>
/// <remarks>
///     <para>
///         <b>On the numbers below.</b> <see cref="MusicScore.LoopSeconds" /> is the length of the
///         decoded audio, not of the music. At 147 BPM the loop is 76 bars, which is 124.082 seconds,
///         but the file decodes to 124.176 -- MP3 cannot express "start here", so an encoder pads the
///         first and last frames and every decoder plays that padding. The extra 94 ms is that
///         padding, and giving the grid the real figure is what stops the beat count from sliding a
///         further 94 ms out of step on every loop.
///     </para>
///     <para>
///         Re-export as <c>.ogg</c> and the problem disappears: Vorbis loops sample-exactly, and both
///         of these constants can then be replaced by the musical figures.
///     </para>
/// </remarks>
internal static class FungoreMusic
{
    public const float BPM = 147f;

    /// <summary>Decoded length of the loop. See the note on this class.</summary>
    private const float LOOP_SECONDS = 124.176f;

    /// <summary>Decoded length of the fill: four bars, plus the same encoder padding.</summary>
    private const float FILL_SECONDS = 6.624f;

    public const string MAIN = "Main";

    public const string DEATH_FILL = "DeathFill";

    /// <summary>
    ///     Where the fill is allowed to land.
    /// </summary>
    /// <remarks>
    ///     A beat, so it arrives within 0.4 s of the killing blow and the music reacts while the
    ///     player is still watching the hit land. Move it to <see cref="MusicGrid.Bar" /> if the fill
    ///     is written to open on a downbeat -- that costs up to 1.6 s of waiting, which is most of the
    ///     death animation.
    /// </remarks>
    public const MusicGrid FILL_GRID = MusicGrid.Beat;

    /// <summary>
    ///     Builds a score for one fight.
    /// </summary>
    /// <remarks>
    ///     Fresh each time rather than a shared static, because <see cref="MusicScore.StaysActive" />
    ///     is what releases the soundtrack when the fight ends and it has to be asking about a live
    ///     world.
    /// </remarks>
    public static MusicScore Create() => new()
    {
        Name = "Fungal Fracas",

        Bpm = BPM,
        BeatsPerBar = 4,
        LoopSeconds = LOOP_SECONDS,

        // Nudge this up if the fill consistently lands a hair early: it is the head padding of the
        // MP3, which is where beat one actually sits.
        GridOffset = 0f,

        StaysActive = static () => NPC.AnyNPCs(ModContent.NPCType<FungoreBoss>()),

        Layers =
        [
            new MusicLayer
            {
                Name = MAIN,
                Slot = Assets.Music.FungalFracas.Slot,
                StartsOn = true,
            },

            new MusicLayer
            {
                Name = DEATH_FILL,
                Slot = Assets.Music.FungalFracas_DeathFill.Slot,
                Kind = MusicLayerKind.Cue,
                Seconds = FILL_SECONDS,
            },
        ],
    };
}
