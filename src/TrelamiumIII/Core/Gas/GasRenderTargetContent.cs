using Terraria.GameContent;

namespace TrelamiumIII.Core.Gas;

public class GasRenderTargetContent : ARenderTargetContentByRequest
{
    private readonly GasLayer _layer;
    public int PixelScale { get; set; } = 2;

    public GasRenderTargetContent(GasLayer layer)
    {
        _layer = layer;
    }

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        // 1. Use Math.Ceiling so we don't lose the edge pixel on odd-numbered resolutions
        int neededWidth = (int)Math.Ceiling(Main.screenWidth / (float)PixelScale);
        int neededHeight = (int)Math.Ceiling(Main.screenHeight / (float)PixelScale);

        PrepareARenderTarget_AndListenToEvents(ref _target, device, neededWidth, neededHeight, RenderTargetUsage.PreserveContents);

        device.SetRenderTarget(_target);
        device.Clear(Color.Transparent);

        // 2. Reconstruct the zoom matrix manually centered on the smaller render target
        Vector2 fullScreenCenter = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
        Vector2 scaledCenter = fullScreenCenter / PixelScale;

        Matrix scaleMatrix = Matrix.CreateTranslation(-fullScreenCenter.X, -fullScreenCenter.Y, 0f)
                             * Matrix.CreateScale(Main.GameViewMatrix.Zoom.X / PixelScale, Main.GameViewMatrix.Zoom.Y / PixelScale, 1f)
                             * Matrix.CreateTranslation(scaledCenter.X, scaledCenter.Y, 0f);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                          DepthStencilState.None, RasterizerState.CullNone, null, scaleMatrix);

        _layer.DrawNodes(spriteBatch);

        spriteBatch.End();

        device.SetRenderTarget(null);
        _wasPrepared = true;
    }
}