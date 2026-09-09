using System;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core.Text;

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
        // Font is optional and defaults to the box's own. Set it only on characters whose lines you
        // author end to end -- TrelamiumFonts.Speech covers 122 glyphs, so a line that interpolates
        // a player's name or gets translated will come out mostly as substitution marks.
        Register(new SpeakerProfile
        {
            Id = "Guide",
            TexturePath = Assets.Textures.UI.Dialogue.Portraits.Guide.KEY,
            FrameCount = 7,
            Color = new(100, 150, 200),
            Voice = new VoiceProfile(VoiceMode.Single, [Assets.SFX.Dialogue.Guide.Speech.Asset]),
        });

        Register(new SpeakerProfile
        {
            Id = "Merchant",
            TexturePath = Assets.Textures.UI.Dialogue.Portraits.Merchant.KEY,
            FrameCount = 3,
            Color = new(100, 150, 200),
            Voice = new VoiceProfile(VoiceMode.Single, [Assets.SFX.Dialogue.Merchant.Speech.Asset]),
        });
        Register(new SpeakerProfile
        {
            Id = "Sonder",
            Color = new(55, 55, 55),
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Sonder.Speech.Asset]),
            Font = TrelamiumFonts.AaarghFont,
        });

        Register(new SpeakerProfile
        {
            Id = "Signia",
            Color = new(255, 214, 95),
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Signia.Speech.Asset]),
        });

        Register(new SpeakerProfile
        {
            Id = "Sophie",
            Color = new(158, 85, 153),
            Font = TrelamiumFonts.AndikaBold,
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Sophie.Speech.Asset]),
        });

        Register(new SpeakerProfile
        {
            Id = "NerdySlime",
            TexturePath = Assets.Textures.UI.Dialogue.Portraits.OnePointFourFive.SlimeNerdy.KEY,
            FrameCount = 1,
            Color = new(100, 150, 200),
        });
    }

    public static void Unload() => profiles.Clear();
}
