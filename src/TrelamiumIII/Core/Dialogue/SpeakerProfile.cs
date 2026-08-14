using ReLogic.Content;

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

    private Asset<Texture2D> portrait;
    public Asset<Texture2D> Portrait =>
        TexturePath == null ? null : (portrait ??= ModContent.Request<Texture2D>(TexturePath));
}
