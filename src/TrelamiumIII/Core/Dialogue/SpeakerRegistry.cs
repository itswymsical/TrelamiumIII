using System;
using System.Collections.Generic;
using Terraria.Audio;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///   Central registry of speaker profiles. Resolution is case-insensitive;
///   "you" and "player" both map to the player.
/// </summary>
public static class SpeakerRegistry
{
    private static readonly Dictionary<string, ISpeakerProfile> profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fallback for unknown speakers: neutral gray, no portrait, soft tick voice.</summary>
    public static readonly ISpeakerProfile Default = new SpeakerProfile { Id = "Unknown" };

    public static void Register(ISpeakerProfile profile) => profiles[profile.Id] = profile;

    /// <summary>Resolves a speaker by id. Unknown ids return <see cref="Default"/> rather than null.</summary>
    public static ISpeakerProfile Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Default;

        if (id.Equals("you", StringComparison.OrdinalIgnoreCase))
            id = "Player";

        return profiles.TryGetValue(id, out var profile) ? profile : Default;
    }

    /// <summary>True for the speaker ids that represent the local player ("you"/"player").</summary>
    public static bool IsPlayer(string id) =>
        id != null && (id.Equals("you", StringComparison.OrdinalIgnoreCase)
                    || id.Equals("player", StringComparison.OrdinalIgnoreCase));

    /// <summary>Helper for voice clips living under Assets/Sounds/Dialogue.</summary>
    private static SoundStyle Speech(string path) =>
        new($"{nameof(TrelamiumIII)}/Assets/Sounds/Dialogue/{path}") { Volume = 0.7f, MaxInstances = 0 };

    public static void Load()
    {
        // The player speaks with no portrait; the box already hides the portrait frame for them.
        Register(new SpeakerProfile
        {
            Id = "Player",
            Color = new(100, 150, 200),
        });

        // Register one profile per speaking character. Portraits are vertical emote sheets
        // (92x92 per frame) under Assets/Textures/UI/Dialogue/Characters.
        //
        // Register(new SpeakerProfile
        // {
        //     Id = "Guide",
        //     TexturePath = Assets.Textures.UI.Dialogue.Characters.Guide.KEY,
        //     FrameCount = 7,
        //     Color = new(100, 150, 200),
        //     Voice = new VoiceProfile(VoiceMode.Single, Speech("Guide/Speech")),
        // });
        //
        // A VoiceMode.Random profile picks a different blip each keystroke:
        //
        // Voice = new VoiceProfile(VoiceMode.Random,
        //     Speech("Someone/Speech_0"),
        //     Speech("Someone/Speech_1"),
        //     Speech("Someone/Speech_2")),
    }

    public static void Unload() => profiles.Clear();
}
