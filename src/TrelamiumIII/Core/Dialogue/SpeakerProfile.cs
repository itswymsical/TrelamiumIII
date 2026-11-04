using ReLogic.Content;
using ReLogic.Graphics;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///   Data-driven <see cref="ISpeakerProfile"/>. One instance per NPC, registered once in
///   <see cref="SpeakerRegistry"/>. The portrait is requested lazily from <see cref="TexturePath"/>
///   so registration is cheap and safe to run during mod load.
/// </summary>
public sealed class SpeakerProfile : ISpeakerProfile
{
    public string Id { get; init; }

    /// <summary>
    ///   Null for speakers drawn without a portrait (e.g. the player).
    /// </summary>
    public string TexturePath { get; init; }

    public int FrameCount { get; init; } = 1;
    public Color Color { get; init; } = new(180, 180, 180);
    public VoiceProfile Voice { get; init; } = VoiceProfile.Default;

    /// <summary>
    ///   Null leaves the box to pick, which is the right answer for most speakers.
    /// </summary>
    /// <remarks>
    ///   Held rather than requested by path: a baked font is assembled once at load and there is
    ///   nothing to look up per line, unlike the portrait below.
    /// </remarks>
    public DynamicSpriteFont Font { get; init; } = TrelamiumFonts.AndikaBold;

    private Asset<Texture2D> portrait;
    public Asset<Texture2D> Portrait =>
        TexturePath == null ? null : (portrait ??= ModContent.Request<Texture2D>(TexturePath));
}
