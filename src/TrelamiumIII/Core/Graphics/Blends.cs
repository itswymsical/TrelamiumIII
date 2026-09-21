namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     Blend states.
/// </summary>
internal static class Blends
{
    public static readonly BlendState PremultipliedAdditive = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,

        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>
    ///     Adds colour and leaves the destination's alpha as it was, so light drawn into a transparent target still adds
    ///     when that target is composited premultiplied.
    /// </summary>
    public static readonly BlendState AdditiveColor = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,

        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };
}
