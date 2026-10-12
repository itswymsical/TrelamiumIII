namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     One off-screen face for a projected panel, sized once and sampled by sub-rectangle.
/// </summary>
/// <remarks>
///     The single-target sibling of the dialogue box's <c>ProjectedSurfaces</c>, which is shaped around a
///     bubble plus a response atlas. A board or a card has one face and lays it out itself, so all
///     it needs from here is a target, a build, and the region the build filled.
/// </remarks>
internal sealed class ProjectedSurface(int width, int height, ProjectedInk ink) : IDisposable
{
    private RenderTarget2D? target;
    private bool requested;
    private bool disposed;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public Texture2D? Texture => target;

    public bool Ready => target is not null;

    /// <summary>The part of the face the last build filled. Zero until something has been built.</summary>
    public Rectangle Region { get; private set; }

    /// <summary>Queues target creation. Safe from anywhere; the work happens on the main thread.</summary>
    public void Initialize()
    {
        if (requested || disposed || Main.dedServ)
            return;

        requested = true;

        Main.QueueMainThreadAction(() =>
        {
            if (disposed)
                return;

            target = new RenderTarget2D(
                Main.graphics.GraphicsDevice, Width, Height, false, SurfaceFormat.Color, DepthFormat.None);
        });
    }

    /// <summary>
    ///     Redraws the face. Call with no sprite batch open, from a point in the frame where render
    ///     targets may be swapped. <paramref name="paint" /> returns the region it used, in texels.
    /// </summary>
    public void Build(Func<ProjectedPainter, Point> paint)
    {
        if (disposed || target is null || Main.gameMenu || Main.dedServ)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        var painter = new ProjectedPainter(Main.spriteBatch, ink);

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        Point used;

        try
        {
            used = paint(painter);
        }
        finally
        {
            painter.Finish();
            device.SetRenderTarget(null);
        }

        Region = new Rectangle(0, 0, Math.Clamp(used.X, 1, Width), Math.Clamp(used.Y, 1, Height));
    }

    /// <summary>
    ///     Stands the face up as a quad. Call inside a quad pass.
    /// </summary>
    /// <remarks>
    ///     A smooth face is sampled linearly rather than with the pass's point sampling: it holds
    ///     antialiased text drawn at screen resolution, and under a perspective divide it is minified
    ///     as often as magnified, where point sampling would drop rows out of the strokes.
    /// </remarks>
    public void Draw(ProjectedQuad quad, GraphicsDevice device, in QuadPlacement placement, Color tint)
    {
        if (target is null)
            return;

        if (ink == ProjectedInk.Pixelated)
        {
            quad.Draw(device, target, Region, placement, tint);
            return;
        }

        device.SamplerStates[0] = SamplerState.LinearClamp;
        quad.Draw(device, target, Region, placement, tint);
        device.SamplerStates[0] = SamplerState.PointClamp;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        RenderTarget2D? pending = target;
        target = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }
}
