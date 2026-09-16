using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>A screen-sized surface, divided by a pixel scale, that a field draws into at <c>CheckMonoliths</c>.</summary>
internal sealed class FieldRenderTarget(int pixelScale) : ARenderTargetContentByRequest
{
    private Action<SpriteBatch>? contents;
    private Matrix transform;
    private BlendState blend = BlendState.AlphaBlend;

    /// <summary>Screen pixels per target texel.</summary>
    public int PixelScale { get; } = Math.Max(pixelScale, 1);

    /// <summary>Draw frame the target was last built in.</summary>
    public ulong BuiltFrame { get; private set; } = ulong.MaxValue;

    /// <summary>The target if it was built this draw frame, otherwise null, so stale contents never composite.</summary>
    public RenderTarget2D? Current => IsReady && BuiltFrame == FlairFieldSystem.DrawFrame ? GetTarget() : null;

    /// <summary>
    ///     Clears the target and runs <paramref name="draw" /> in a deferred batch under <paramref name="batchTransform" /> scaled
    ///     down to the target, so it draws in the reporting batch's coordinates.
    /// </summary>
    public void Build(Matrix batchTransform, BlendState batchBlend, Action<SpriteBatch> draw)
    {
        transform = batchTransform;
        blend = batchBlend;
        contents = draw;

        Request();
        PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);

        contents = null;
        BuiltFrame = FlairFieldSystem.DrawFrame;
    }

    /// <summary>Forgets the target and disposes it on the main thread.</summary>
    public void Free()
    {
        RenderTarget2D? target = _target;

        Reset();

        if (target is not null)
            Main.QueueMainThreadAction(target.Dispose);
    }

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        (int width, int height) = FieldSurface.ScreenSize(PixelScale);

        // Not the vanilla helper: it takes the back buffer's eight-bit format, which clamps this
        // sum flat the moment two blobs overlap. See FieldSurface.
        RenderTarget2D? surface = _target;
        FieldSurface.Ensure(ref surface, device, width, height);
        _target = surface!;

        device.SetRenderTarget(_target);
        device.Clear(Color.Transparent);

        spriteBatch.Begin(SpriteSortMode.Deferred, blend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null,
            transform * Matrix.CreateScale(1f / PixelScale, 1f / PixelScale, 1f));

        contents?.Invoke(spriteBatch);

        spriteBatch.End();

        device.SetRenderTarget(null);
        _wasPrepared = true;
    }
}
