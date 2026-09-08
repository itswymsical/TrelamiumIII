using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Content.Jobs;

internal static class HomeForagerMusic
{
    public const string MAIN = "Main";

    public const MusicGrid FILL_GRID = MusicGrid.Beat;

    public static MusicScore Create() => new() {
        Name = "Home Forager",

        Bpm = 102,
        BeatsPerBar = 4,
        LoopSeconds = 28,

        GridOffset = 0f,

        StaysActive = static () => NPC.AnyNPCs(NPCID.Guide),

        Layers = [
            new MusicLayer 
            {
                Name = MAIN,
                Slot = Assets.Music.HomeForagerMusic.Slot,
                StartsOn = true,
        }],
    };
}