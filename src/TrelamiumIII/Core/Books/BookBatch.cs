namespace TrelamiumIII.Core.Books;

/// <summary>
///     Sprite batch settings for book pages. Blocks drawing pixel art swap samplers via <see cref="Switch" /> so the restored batch matches.
/// </summary>
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

    /// <summary>Restarts the page batch under another sampler, keeping <paramref name="transform" /> so tilted decorations stay tilted.</summary>
    public static void Switch(SpriteBatch spriteBatch, SamplerState sampler, Matrix? transform = null)
    {
        spriteBatch.End();
        BeginPage(spriteBatch, sampler, transform);
    }
}
