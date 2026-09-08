using ReLogic.Content;
using ReLogic.Graphics;

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

    /// <summary>
    ///   Typeface this speaker's lines are set in, or null to use the box's own.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     A character's handwriting, in effect: the same words in a different face read as a
    ///     different voice, and it costs nothing at the point of use because every box already
    ///     lays each line out from scratch.
    ///   </para>
    ///   <para>
    ///     Line spacing comes with it. A face with taller leading gives that speaker a taller box,
    ///     which is correct and is worth knowing before setting something extreme here.
    ///   </para>
    ///   <para>
    ///     Only for faces with the coverage to survive whatever the line holds. A display face set
    ///     on a speaker whose lines interpolate a player's name will render most of that name as
    ///     the substitution glyph -- see <see cref="Text.TrelamiumFonts.Speech" />.
    ///   </para>
    /// </remarks>
    DynamicSpriteFont Font { get; }
}
