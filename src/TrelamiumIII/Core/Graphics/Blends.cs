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
}
