using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     The Warden's score. Lengths are decoded MP3 lengths including encoder padding: the loop is 58 bars at 151 BPM
///     (92.185 s) plus 0.047 s, the fill three bars plus 0.032 s. Measured with MP3Sharp, the decoder tModLoader uses.
/// </summary>
internal static class WardenMusic
{
    public const string NAME = "Wooden Warden";

    public const float BPM = 151f;

    private const float LOOP_SECONDS = 92.232f;

    private const float FILL_SECONDS = 4.8f;

    public const string MAIN = "Main";

    public const string DEATH_FILL = "DeathFill";

    /// <summary>A beat, so the fill lands within 0.4 s of the death starting.</summary>
    public const MusicGrid FILL_GRID = MusicGrid.Beat;

    /// <summary>Builds a fresh score per fight, since <see cref="MusicScore.StaysActive" /> must query the live world.</summary>
    public static MusicScore Create() => new()
    {
        Name = NAME,

        Bpm = BPM,
        BeatsPerBar = 4,
        LoopSeconds = LOOP_SECONDS,

        StaysActive = static () => NPC.AnyNPCs(ModContent.NPCType<WoodenWardenBoss>()),

        Layers =
        [
            new MusicLayer
            {
                Name = MAIN,
                Slot = Assets.Music.Ingress.WoodenWarden.Slot,
                StartsOn = true,
            },

            new MusicLayer
            {
                Name = DEATH_FILL,
                Slot = Assets.Music.Ingress.WoodenWarden_DeathFill.Slot,
                Kind = MusicLayerKind.Cue,
                Seconds = FILL_SECONDS,
            },
        ],
    };
}
