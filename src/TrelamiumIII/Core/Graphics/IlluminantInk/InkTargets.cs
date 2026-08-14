namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The pair of render targets the scene is composed and post-processed in, plus their
///     lifetime.
/// </summary>
/// <remarks>
///     Two targets, ping-ponged: a post pass cannot read and write the same surface, so each
///     pass reads <see cref="Scene" />, writes <see cref="Swap" />, and then the pair is
///     <see cref="Flip">flipped</see> so the next pass sees its predecessor's output as
///     <see cref="Scene" /> again. The chain therefore costs two targets no matter how many
///     passes it grows to.
/// </remarks>
internal sealed class InkTargets : IDisposable
{
    private RenderTarget2D? scene;
    private RenderTarget2D? swap;

    /// <summary>Width of both targets, in real backbuffer pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Height of both targets, in real backbuffer pixels.</summary>
    public int Height { get; private set; }

    /// <summary>True once <see cref="Ensure" /> has allocated a usable pair.</summary>
    public bool Ready => scene is { IsDisposed: false } && swap is { IsDisposed: false };

    /// <summary>The surface holding the current image.</summary>
    public RenderTarget2D Scene => scene!;

    /// <summary>The spare surface the next pass writes into.</summary>
    public RenderTarget2D Swap => swap!;

    /// <summary>
    ///     Allocates the pair, or reallocates it if the resolution changed.
    /// </summary>
    /// <returns>False if the requested size is degenerate, in which case nothing was drawn.</returns>
    public bool Ensure(GraphicsDevice device, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (Ready && Width == width && Height == height)
        {
            return true;
        }

        Dispose();

        Width = width;
        Height = height;
        scene = Create(device, width, height);
        swap = Create(device, width, height);

        return true;
    }

    /// <summary>Exchanges the two surfaces after a post pass has written to <see cref="Swap" />.</summary>
    public void Flip()
    {
        (scene, swap) = (swap, scene);
    }

    public void Dispose()
    {
        scene?.Dispose();
        swap?.Dispose();
        scene = null;
        swap = null;
        Width = 0;
        Height = 0;
    }

    private static RenderTarget2D Create(GraphicsDevice device, int width, int height)
    {
        // PreserveContents matches what Terraria uses for its own targets. Every pass here
        // overwrites the whole surface anyway, so the choice is about not tripping over
        // driver-specific discard behaviour rather than about correctness.
        return new RenderTarget2D(
            device,
            width,
            height,
            false,
            device.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
    }
}
