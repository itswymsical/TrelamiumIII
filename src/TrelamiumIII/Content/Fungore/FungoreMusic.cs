using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     Fungore's score. Durations are decoded MP3 lengths including encoder padding (124.176 s, not the 124.082 s of
///     76 bars at 147 BPM); the musical figure would drift the beat every loop. An .ogg export would loop exactly.
/// </summary>
internal static class FungoreMusic
{
    public const float BPM = 147f;

    /// <summary>Decoded length of the loop. See the note on this class.</summary>
    private const float LOOP_SECONDS = 124.176f;

    /// <summary>Decoded length of the fill: four bars, plus the same encoder padding.</summary>
    private const float FILL_SECONDS = 6.624f;

    public const string MAIN = "Main";

    public const string DEATH_FILL = "DeathFill";

    /// <summary>A beat, so the fill lands within 0.4 s of the kill; <see cref="MusicGrid.Bar" /> would wait up to 1.6 s.</summary>
    public const MusicGrid FILL_GRID = MusicGrid.Beat;

    /// <summary>Builds a fresh score per fight, since <see cref="MusicScore.StaysActive" /> must query the live world.</summary>
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
                Slot = Assets.Music.Fungore.FungalFracas.Slot,
                StartsOn = true,
            },

            new MusicLayer
            {
                Name = DEATH_FILL,
                Slot = Assets.Music.Fungore.FungalFracas_DeathFill.Slot,
                Kind = MusicLayerKind.Cue,
                Seconds = FILL_SECONDS,
            },
        ],
    };
}
