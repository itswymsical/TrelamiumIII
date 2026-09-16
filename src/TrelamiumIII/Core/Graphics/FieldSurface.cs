namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     Allocation of the off-screen surfaces every accumulated field in this mod sums its stamps
///     into, and the one place their format is chosen.
/// </summary>
/// <remarks>
///     <para>
///         These were built by <c>ARenderTargetContentByRequest.PrepareARenderTarget_AndListenToEvents</c>,
///         which takes the format from <c>PresentationParameters.BackBufferFormat</c>.
///     </para>
/// </remarks>
internal static class FieldSurface
{
    /// <summary>
    ///     Points <paramref name="target" /> at a surface of the requested size, rebuilding it if it
    ///     is missing, disposed, or the wrong size.
    /// </summary>
    /// <remarks>
    ///     The vanilla helper this replaces also subscribes to <c>ContentLost</c>, which is a
    ///     lost-device notification from D3D9 and never raised under the OpenGL backend FNA runs
    ///     here. The size check below is what actually rebuilds these, and it is kept.
    /// </remarks>
    public static void Ensure(ref RenderTarget2D? target, GraphicsDevice device, int width, int height)
    {
        if (target is { IsDisposed: false } existing && existing.Width == width && existing.Height == height)
            return;

        target?.Dispose();

        target = new RenderTarget2D(
            device,
            width,
            height,
            mipMap: false,
            SurfaceFormat.HdrBlendable,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.PreserveContents);
    }

    /// <summary>The size a field target needs to cover the screen at <paramref name="pixelScale" />.</summary>
    /// <remarks>
    ///     Ceiling: an odd screen size would otherwise drop the last row or column and leave a seam
    ///     down the edge of the composite.
    /// </remarks>
    public static (int Width, int Height) ScreenSize(int pixelScale)
        => ((int)Math.Ceiling(Main.screenWidth / (float)pixelScale), (int)Math.Ceiling(Main.screenHeight / (float)pixelScale));
}
