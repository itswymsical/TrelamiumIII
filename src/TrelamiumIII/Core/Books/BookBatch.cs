namespace TrelamiumIII.Core.Books;

/// <summary>
///     The sprite batch settings every book page is drawn under, in one place.
/// </summary>
/// <remarks>
///     Pages render into a target with linear filtering, which is right for text and for the paper
///     itself but turns pixel art to mush. A block that draws a sprite swaps the sampler with
///     <see cref="Switch" /> and swaps it back; going through here rather than calling
///     <see cref="SpriteBatch.Begin" /> directly is what keeps the restored batch identical to the one
///     the renderer opened.
/// </remarks>
public static class BookBatch
{
    public static void BeginPage(SpriteBatch spriteBatch, SamplerState? sampler = null, Matrix? transform = null) =>
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            sampler ?? SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            transform ?? Matrix.Identity);

    /// <summary>
    ///     Restarts the page batch under a different sampler, keeping <paramref name="transform" />.
    /// </summary>
    /// <remarks>
    ///     The transform has to be passed back in: a block drawing inside a tilted decoration is in a
    ///     rotated batch, and restarting without it would drop the rotation for that block alone.
    /// </remarks>
    public static void Switch(SpriteBatch spriteBatch, SamplerState sampler, Matrix? transform = null)
    {
        spriteBatch.End();
        BeginPage(spriteBatch, sampler, transform);
    }
}
