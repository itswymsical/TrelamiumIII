using Terraria.Audio;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>How a <see cref="VoiceProfile"/> picks among its sounds while typing.</summary>
public enum VoiceMode
{
    /// <summary>Always play the first sound.</summary>
    Single,
    /// <summary>Pick a random sound each keystroke.</summary>
    Random,
    /// <summary>Cycle through the sounds in order, one per keystroke.</summary>
    Sequential
}

/// <summary>
///   A speaker's typing voice: one or more sounds plus a playback <see cref="VoiceMode"/>.
///   Sequential playback state is owned by the caller through a cursor, so a
///   single profile instance can be shared safely across every line that uses it.
/// </summary>
public sealed class VoiceProfile
{
    public static readonly VoiceProfile Default =
        new(VoiceMode.Single, SoundID.MenuTick with { Volume = 0.7f, MaxInstances = 0 });

    private readonly SoundStyle[] sounds;
    public VoiceMode Mode { get; }

    /// <summary>
    ///   Frames between the beats this voice may play on. 1 is every frame, which is the old
    ///   behaviour; larger values space the speech out and are what stop samples overlapping.
    /// </summary>
    /// <remarks>
    ///   Read as a tempo, not a delay. Characters typed between two beats collapse into the single
    ///   hit on the later one, so raising this thins the speech out rather than slowing the typing
    ///   down, and the ticks stay evenly spaced however unevenly the letters arrive. Roughly: 3 is
    ///   chattery, 4 is conversational, 6 and up is deliberate.
    /// </remarks>
    public int Subdivision { get; init; } = 4;

    public VoiceProfile(VoiceMode mode, params SoundStyle[] sounds)
    {
        Mode = mode;
        this.sounds = sounds ?? [];
    }

    /// <summary>
    ///   Selects the next sound to play. <paramref name="cursor"/> is advanced for
    ///   <see cref="VoiceMode.Sequential"/>; the caller keeps one cursor per active line.
    ///   Returns null when the profile has no sounds.
    /// </summary>
    public SoundStyle? Next(ref int cursor)
    {
        if (sounds.Length == 0)
            return null;

        switch (Mode)
        {
            case VoiceMode.Sequential:
                var sound = sounds[cursor % sounds.Length];
                cursor++;
                return sound;
            case VoiceMode.Random:
                return sounds[Main.rand.Next(sounds.Length)];
            default:
                return sounds[0];
        }
    }
}
