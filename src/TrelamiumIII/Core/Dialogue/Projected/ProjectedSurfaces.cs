namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>What <see cref="ProjectedSurfaces" /> needs from a box in order to draw its faces.</summary>
internal interface IProjectedContent
{
    /// <summary>Draws the bubble at the surface's origin and returns the region it filled.</summary>
    Point DrawBubble(PixelCanvas canvas);

    int ChoiceCount { get; }

    /// <summary>Draws one response slot at the canvas origin and returns the region it filled.</summary>
    Point DrawChoice(PixelCanvas canvas, int index);
}

/// <summary>
///     The off-screen faces a projected box is made of, and the one place they are drawn.
/// </summary>
/// <remarks>
///     <para>
///         Two surfaces, sized once for the largest box this style allows and thereafter sampled by
///         sub-rectangle. The alternative -- a surface per box, resized as the text grows -- would
///         reallocate a render target several times a second while a line types, which is the one
///         thing a graphics device really does not want asked of it mid-frame.
///     </para>
///     <para>
///         Both are drawn during <c>CheckMonoliths</c>, which is where render targets may still be
///         swapped. Anywhere later in the frame means taking the back buffer away from a scene that is
///         already half drawn into it.
///     </para>
/// </remarks>
internal sealed class ProjectedSurfaces : IDisposable
{
    public const int BUBBLE_WIDTH = PanelStyle.SURFACE_WIDTH;
    public const int BUBBLE_HEIGHT = PanelStyle.SURFACE_HEIGHT;

    public const int CHOICE_WIDTH = PanelStyle.CHOICE_SURFACE_WIDTH;
    public const int CHOICE_HEIGHT = PanelStyle.CHOICE_PITCH;
    public const int MAX_CHOICES = 4;

    private readonly Point[] choiceFootprints = new Point[MAX_CHOICES];

    private RenderTarget2D? bubble;
    private RenderTarget2D? choices;

    private bool requested;
    private bool disposed;

    /// <summary>The bubble's face, or null until the targets exist.</summary>
    public Texture2D? Bubble => bubble;

    public Texture2D? Choices => choices;

    /// <summary>The part of <see cref="Bubble" /> the current line actually filled.</summary>
    public Rectangle BubbleRegion { get; private set; }

    public int ChoicesDrawn { get; private set; }

    public Rectangle ChoiceRegion(int index) =>
        new(0, index * CHOICE_HEIGHT, choiceFootprints[index].X, choiceFootprints[index].Y);

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

            GraphicsDevice device = Main.graphics.GraphicsDevice;

            bubble = new RenderTarget2D(device, BUBBLE_WIDTH, BUBBLE_HEIGHT, false, SurfaceFormat.Color, DepthFormat.None);
            choices = new RenderTarget2D(device, CHOICE_WIDTH, CHOICE_HEIGHT * MAX_CHOICES, false, SurfaceFormat.Color, DepthFormat.None);
        });
    }

    /// <summary>
    ///     Redraws both faces. Call with no sprite batch open, from a point in the frame where render
    ///     targets may be swapped.
    /// </summary>
    public void Build(IProjectedContent content)
    {
        if (disposed || bubble is null || choices is null || Main.gameMenu || Main.dedServ)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        SpriteBatch spriteBatch = Main.spriteBatch;

        device.SetRenderTarget(bubble);
        device.Clear(Color.Transparent);
        Begin(spriteBatch);

        Point used = content.DrawBubble(new PixelCanvas(spriteBatch));

        spriteBatch.End();

        BubbleRegion = new Rectangle(0, 0, Math.Clamp(used.X, 1, BUBBLE_WIDTH), Math.Clamp(used.Y, 1, BUBBLE_HEIGHT));

        ChoicesDrawn = Math.Min(content.ChoiceCount, MAX_CHOICES);

        if (ChoicesDrawn > 0)
        {
            device.SetRenderTarget(choices);
            device.Clear(Color.Transparent);
            Begin(spriteBatch);

            for (int i = 0; i < ChoicesDrawn; i++)
            {
                Point footprint = content.DrawChoice(new PixelCanvas(spriteBatch).At(0, i * CHOICE_HEIGHT), i);

                choiceFootprints[i] = new Point(
                    Math.Clamp(footprint.X, 1, CHOICE_WIDTH),
                    Math.Clamp(footprint.Y, 1, CHOICE_HEIGHT));
            }

            spriteBatch.End();
        }

        device.SetRenderTarget(null);
    }

    /// <summary>Coverage at which a glyph pixel counts as ink. Lower fattens the text.</summary>
    private const float INK_THRESHOLD = 0.42f;

    /// <summary>
    ///     Point sampling and a coverage threshold, which are the same decision made twice.
    /// </summary>
    /// <remarks>
    ///     Nothing in this surface may carry a partial pixel, because the quad that reads it back
    ///     magnifies rather than filters: point sampling keeps the shapes' edges exactly where they
    ///     were placed, and the threshold does the same job for glyphs, which arrive from the font
    ///     atlas already antialiased and would otherwise contribute their grey fringe as full-size
    ///     chunky pixels.
    /// </remarks>
    private static void Begin(SpriteBatch spriteBatch)
    {
        Effect? threshold = Assets.Shaders.GlyphThreshold.Asset.Value;

        threshold?.Parameters["uThreshold"]?.SetValue(INK_THRESHOLD);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            threshold,
            Matrix.Identity);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        // Snapshot into locals before clearing the fields: the queued action runs later, and would
        // otherwise dispose whatever the fields hold by then rather than what they hold now.
        RenderTarget2D?[] pending = [bubble, choices];

        bubble = null;
        choices = null;

        Main.QueueMainThreadAction(() =>
        {
            foreach (RenderTarget2D? target in pending)
                target?.Dispose();
        });
    }
}
