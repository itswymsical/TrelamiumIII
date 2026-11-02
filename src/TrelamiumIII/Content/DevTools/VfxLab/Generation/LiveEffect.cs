using ReLogic.Content;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary>
///     One <see cref="Asset{T}" /> whose effect is replaced while the game runs. Everything that takes a shader holds an
///     asset and reads its value when it draws, so whatever holds this one picks up each new effect with no change of its own.
/// </summary>
/// <param name="name"> Names the asset; nothing looks it up. </param>
internal sealed class LiveEffect(string name)
{
    public Asset<Effect> Asset { get; } = new(name);

    /// <summary> False until the first <see cref="Swap" />, and the asset's value is null until then. </summary>
    public bool IsReady => Asset.IsLoaded;

    /// <summary> Replaces the effect and disposes the old one, so call it on the main thread and never from inside a draw. </summary>
    /// <remarks> A bad effect throws here and leaves the current one in place. </remarks>
    public void Swap(byte[] code) => Asset.SubmitLoadedContent(new Effect(Main.instance.GraphicsDevice, code), null!);

    public void Release() => Asset.Unload();
}
