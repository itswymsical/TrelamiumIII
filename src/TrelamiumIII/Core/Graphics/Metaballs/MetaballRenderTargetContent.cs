using Terraria.GameContent;

namespace TrelamiumIII.Core.Graphics.Metaballs;

/// <summary>
///     The off-screen surface a <see cref="MetaballField" /> sums its blobs into.
/// </summary>
/// <remarks>
///     Kept at <see cref="PixelScale" /> below screen resolution for two reasons: the field is
///     blurry data rather than an image, so it loses nothing to the downscale, and the shader
///     quantises its decisions to this grid. One field texel becomes one chunky output pixel.
/// </remarks>
internal sealed class MetaballRenderTargetContent(MetaballField layer) : ARenderTargetContentByRequest
{
    public int PixelScale { get; set; } = 2;

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        // Ceiling: an odd screen width would otherwise drop the last column and
        // leave a seam down the edge of the composite.
        int neededWidth = (int)Math.Ceiling(Main.screenWidth / (float)PixelScale);
        int neededHeight = (int)Math.Ceiling(Main.screenHeight / (float)PixelScale);

        PrepareARenderTarget_AndListenToEvents(ref _target, device, neededWidth, neededHeight, RenderTargetUsage.PreserveContents);

        device.SetRenderTarget(_target);
        device.Clear(Color.Transparent);

        // Main.GameViewMatrix cannot be used directly: it is built for a full-size surface, so its
        // translation would put the zoom pivot off-centre here. Rebuild it about the smaller target.
        Vector2 fullScreenCenter = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
        Vector2 scaledCenter = fullScreenCenter / PixelScale;

        Matrix scaleMatrix = Matrix.CreateTranslation(-fullScreenCenter.X, -fullScreenCenter.Y, 0f)
                             * Matrix.CreateScale(Main.GameViewMatrix.Zoom.X / PixelScale, Main.GameViewMatrix.Zoom.Y / PixelScale, 1f)
                             * Matrix.CreateTranslation(scaledCenter.X, scaledCenter.Y, 0f);

        // Additive is the point: overlapping blobs have to sum for neighbouring nodes to fuse.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            scaleMatrix
        );

        layer.DrawNodes(spriteBatch);

        spriteBatch.End();

        device.SetRenderTarget(null);
        _wasPrepared = true;
    }
}
