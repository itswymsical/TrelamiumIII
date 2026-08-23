namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     Blend states the framework does not provide.
/// </summary>
internal static class Blends
{
    /// <summary>
    ///     Additive blending for a source that is already premultiplied by its own alpha.
    /// </summary>
    /// <remarks>
    ///     <see cref="BlendState.Additive" /> is <c>(SourceAlpha, One)</c>, so it multiplies the
    ///     incoming colour by alpha on the way in. That is right for ordinary straight-alpha sprites
    ///     and wrong for anything a shader has already premultiplied, which comes out darkened by a
    ///     second factor of alpha -- an effect at half opacity landing at a quarter, and one that
    ///     fades in appearing to fade in twice as slowly as its own envelope says.
    ///     <para>
    ///         Alpha accumulates too, which matters when the surface is a field rather than an image:
    ///         two overlapping emitters have to sum to a denser field, not to whichever of them was
    ///         drawn second.
    ///     </para>
    /// </remarks>
    public static readonly BlendState PremultipliedAdditive = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,

        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };
}
