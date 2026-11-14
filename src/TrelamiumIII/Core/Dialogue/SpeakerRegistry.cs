using System.Collections.Generic;
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

        Register(new SpeakerProfile
        {
            Id = "Guide",
            TexturePath = Assets.Textures.UI.Dialogue.Portraits.Guide.KEY,
            FrameCount = 7,
            Font = TrelamiumFonts.AndikaBold,
            Color = new(100, 150, 200),
            Voice = new VoiceProfile(VoiceMode.Single, SoundID.MenuTick),
        });

        Register(new SpeakerProfile
        {
            Id = "Merchant",
            TexturePath = Assets.Textures.UI.Dialogue.Portraits.Merchant.KEY,
            FrameCount = 3,
            Color = new(100, 150, 200),
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Merchant.Speech.Asset]),
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
            Id = "Silvian",
            FrameCount = 1,
            Font = TrelamiumFonts.AndikaBold,
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Silvian.Speech.Asset]),
            Color = Color.CornflowerBlue,
        });

        Register(new SpeakerProfile
        {
            Id = "Argie",
            FrameCount = 1,
<<<<<<< HEAD
            Font = TrelamiumFonts.AgravePro_SemiBold,
=======
            Font = TrelamiumFonts.PatrickHand,
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Argie.Speech.Asset]),
            Color = new(255, 110, 80),
        });

        Register(new SpeakerProfile
        {
            Id = "Permafrost",
            FrameCount = 1,
            Font = TrelamiumFonts.Cardinal,
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Permafrost.Speech.Asset]),
            Color = Color.LightSkyBlue,
        });

        Register(new SpeakerProfile
        {
            Id = "Edith",
            FrameCount = 1,
            Font = TrelamiumFonts.AgravePro_Regular,
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Sophie.Speech.Asset]),
            Color = new(214, 178, 120),
        });

        Register(new SpeakerProfile
        {
            Id = "Goblin Tinkerer",
            FrameCount = 1,
            Font = TrelamiumFonts.RajdhaniBold,
            Color = Color.Teal,
            Voice = new VoiceProfile(VoiceMode.Single, [Assets.SFX.Dialogue.Goblin.Asset]),
        });

        Register(new SpeakerProfile
        {
            Id = "Dryad",
            FrameCount = 1,
            Font = TrelamiumFonts.AgravePro_SemiBold,
            Color = new(122, 190, 112),
            Voice = new VoiceProfile(VoiceMode.Random, [Assets.SFX.Dialogue.Dryad.Speech.Asset]),
        });

        Register(new SpeakerProfile
        {
            Id = "Nerdy Slime",
            FrameCount = 1,
            Color = new(100, 150, 200),
        });
    }

    public static void Unload() => profiles.Clear();
}
