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
///     This is the renderer and where the off-screen faces a projected box are handled.
/// </summary>
/// <remarks>
///     <para>
///         There are two surfaces, sized once for the largest box this style allows and thereafter sampled by
///         sub-rectangle. The alternative is a surface per box and resized as the text grows, but that would
///         reallocate an RT several times a second while a line types, so no.
///     </para>
/// </remarks>
internal sealed class ProjectedSurfaces : IDisposable
{
    public const int BUBBLE_WIDTH = PanelStyle.SURFACE_WIDTH;
    public const int BUBBLE_HEIGHT = PanelStyle.SURFACE_HEIGHT;

    public const int CHOICE_WIDTH = PanelStyle.CHOICE_SURFACE_WIDTH;
    public const int CHOICE_HEIGHT = PanelStyle.CHOICE_PITCH;
    public const int MAX_CHOICES = 4;

    /// <summary>
    ///     Whole-texel upscale applied before the quad samples the faces linearly. The softening dial:
    ///     1 is a plain bilinear blur, higher is crisper, and each step costs a bigger copy.
    /// </summary>
    /// <remarks>
    ///     Point sampling a perspective quad gives texels uneven widths -- one column two screen
    ///     pixels wide, the next one -- which reads as harsh and shimmers as the panel drifts. Blowing
    ///     each texel up into a solid block first and then filtering means only the seams between
    ///     blocks blend, over a fraction of a texel, so the chunky pixels survive with soft edges.
    /// </remarks>
    public const int PRESCALE = 2;

    private readonly Point[] choiceFootprints = new Point[MAX_CHOICES];

    private RenderTarget2D? bubble;
    private RenderTarget2D? choices;

    private RenderTarget2D? bubbleScaled;
    private RenderTarget2D? choicesScaled;

    private bool requested;
    private bool disposed;

    /// <summary>The bubble's face, prescaled, or null until the targets exist.</summary>
    public Texture2D? Bubble => bubbleScaled;

    public Texture2D? Choices => choicesScaled;

    /// <summary>The part of <see cref="Bubble" /> the current line fills, in prescaled pixels.</summary>
    public Rectangle BubbleRegion { get; private set; }

    public int ChoicesDrawn { get; private set; }

    public Rectangle ChoiceRegion(int index) =>
        new(0, index * CHOICE_HEIGHT * PRESCALE, choiceFootprints[index].X * PRESCALE, choiceFootprints[index].Y * PRESCALE);

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

            bubbleScaled = new RenderTarget2D(device, BUBBLE_WIDTH * PRESCALE, BUBBLE_HEIGHT * PRESCALE, false, SurfaceFormat.Color, DepthFormat.None);
            choicesScaled = new RenderTarget2D(device, CHOICE_WIDTH * PRESCALE, CHOICE_HEIGHT * MAX_CHOICES * PRESCALE, false, SurfaceFormat.Color, DepthFormat.None);
        });
    }

    /// <summary>
    ///     Redraws both faces. Call with no sprite batch open, from a point in the frame where render
    ///     targets may be swapped.
    /// </summary>
    public void Build(IProjectedContent content)
    {
        if (disposed || bubble is null || choices is null || bubbleScaled is null || choicesScaled is null
            || Main.gameMenu || Main.dedServ)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        SpriteBatch spriteBatch = Main.spriteBatch;

        device.SetRenderTarget(bubble);
        device.Clear(Color.Transparent);
        BeginInk(spriteBatch);

        Point used = content.DrawBubble(new PixelCanvas(spriteBatch));

        spriteBatch.End();

        Rectangle bubbleUsed = new(0, 0, Math.Clamp(used.X, 1, BUBBLE_WIDTH), Math.Clamp(used.Y, 1, BUBBLE_HEIGHT));
        Prescale(device, spriteBatch, bubble, bubbleScaled, bubbleUsed);

        BubbleRegion = new Rectangle(0, 0, bubbleUsed.Width * PRESCALE, bubbleUsed.Height * PRESCALE);

        ChoicesDrawn = Math.Min(content.ChoiceCount, MAX_CHOICES);

        if (ChoicesDrawn > 0)
        {
            device.SetRenderTarget(choices);
            device.Clear(Color.Transparent);
            BeginInk(spriteBatch);

            for (int i = 0; i < ChoicesDrawn; i++)
            {
                Point footprint = content.DrawChoice(new PixelCanvas(spriteBatch).At(0, i * CHOICE_HEIGHT), i);

                choiceFootprints[i] = new Point(
                    Math.Clamp(footprint.X, 1, CHOICE_WIDTH),
                    Math.Clamp(footprint.Y, 1, CHOICE_HEIGHT));
            }

            spriteBatch.End();

            Prescale(device, spriteBatch, choices, choicesScaled, new Rectangle(0, 0, CHOICE_WIDTH, ChoicesDrawn * CHOICE_HEIGHT));
        }

        device.SetRenderTarget(null);
    }

    /// <summary>
    ///     Copies the drawn part of a face into its prescaled twin, each texel as a solid block.
    /// </summary>
    /// <remarks>
    ///     Only the used region is copied, so a one-line box costs a one-line copy. Opaque blend
    ///     because the source already holds finished, premultiplied pixels; blending them again over
    ///     a cleared target would darken every partly transparent edge.
    /// </remarks>
    private static void Prescale(GraphicsDevice device, SpriteBatch spriteBatch, Texture2D source, RenderTarget2D target, Rectangle region)
    {
        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity);

        spriteBatch.Draw(
            source,
            new Rectangle(region.X * PRESCALE, region.Y * PRESCALE, region.Width * PRESCALE, region.Height * PRESCALE),
            region,
            Color.White);

        spriteBatch.End();
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
    internal static void BeginInk(SpriteBatch spriteBatch)
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

    /// <summary>
    ///     Point-sampled, unshaded: for sprite art that carries its own colour. The ink batch above
    ///     returns the vertex colour only, which would flatten an item sprite to a silhouette.
    /// </summary>
    internal static void BeginArt(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity);
    }

    /// <summary>
    ///     Linear-sampled, unshaded: for text drawn at native resolution into a face that is sampled
    ///     smoothly rather than magnified as chunky pixels.
    /// </summary>
    internal static void BeginSmooth(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        // Snapshot into locals before clearing the fields: the queued action runs later, and would
        // otherwise dispose whatever the fields hold by then rather than what they hold now.
        RenderTarget2D?[] pending = [bubble, choices, bubbleScaled, choicesScaled];

        bubble = null;
        choices = null;
        bubbleScaled = null;
        choicesScaled = null;

        Main.QueueMainThreadAction(() =>
        {
            foreach (RenderTarget2D? target in pending)
                target?.Dispose();
        });
    }
}
