using ReLogic.Content;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///   Contract the dialogue system consumes to present a speaker: portrait, framing,
///   panel color, and voice. Most characters are plain data via <see cref="SpeakerProfile"/>
///   registered in <see cref="SpeakerRegistry"/>; implement this directly only when a
///   character needs custom runtime behavior (e.g. a color or portrait that changes with state).
/// </summary>
public interface ISpeakerProfile
{
    /// <summary>Stable identifier used as the speaker key (matched case-insensitively), e.g. "Guide".</summary>
    string Id { get; }

    /// <summary>Portrait sheet, or null for speakers drawn without a portrait (e.g. the player).</summary>
    Asset<Texture2D> Portrait { get; }

    /// <summary>Number of vertical emote frames in the portrait sheet.</summary>
    int FrameCount { get; }

    /// <summary>Panel / accent color for this speaker.</summary>
    Color Color { get; }

    /// <summary>Voice played while typing this speaker's lines.</summary>
    VoiceProfile Voice { get; }
}
