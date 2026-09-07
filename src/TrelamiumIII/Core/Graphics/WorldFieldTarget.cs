using Terraria.GameContent;

namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     An off-screen surface that world-space geometry is accumulated into, so a later pass can read
///     the whole accumulation as one texture.
/// </summary>
/// <remarks>
///     <para>
///         This is the surface half of any effect that has to know about more than one pixel at a
///         time -- a metaball field that thresholds a sum, a lens that differentiates a mask to find
///         its normal, a glow that blurs. None of those can be done from a sprite draw, because a
///         sprite draw only ever sees the fragment it is on.
///     </para>
///     <para>
///         <see cref="PixelScale" /> holds it below screen resolution. For fields that is free: the
///         contents are blurry data rather than an image, and a shader that quantises its decisions
///         to this grid gets one field texel per chunky output pixel out of it. For a lens mask it is
///         better than free, because the coarser the mask the smoother its gradient.
///     </para>
///     <para>
///         Built through <see cref="ARenderTargetContentByRequest" /> rather than a bare
///         <see cref="RenderTarget2D" />, which is what gets the surface reallocated on a resolution
///         change or a device reset instead of quietly rendering at the old size.
///     </para>
/// </remarks>
internal sealed class WorldFieldTarget(Action<SpriteBatch> drawContents) : ARenderTargetContentByRequest
{
    /// <summary>Divisor on screen resolution. 1 is full size.</summary>
    public int PixelScale { get; set; } = 2;

    public BlendState Blend { get; set; } = BlendState.Additive;

    public SamplerState Sampler { get; set; } = SamplerState.PointClamp;

    /// <summary>
    ///     An effect the accumulation itself is drawn through, or null to stamp sprites as they are.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Distinct from the shader that later reads the finished field, and needed for the same
    ///         reason a field is: what goes into the surface is not always what a sprite looks like.
    ///         A coverage mask stamped from tile sprites wants the sprite's <em>alpha</em>, because
    ///         that is the true silhouette of a slope or a half block, and emphatically does not want
    ///         its colour -- which is dirt brown or stone grey and would be read back as data.
    ///     </para>
    ///     <para>
    ///         Replacing the sampled colour with the vertex colour is what frees the field's three
    ///         colour channels to carry something per stamp. That is the whole mechanism behind
    ///         per-tile heat: the burn does not need a second surface, only permission to stop
    ///         drawing the block's own pigment into the one it already has.
    ///     </para>
    /// </remarks>
    public Effect? StampEffect { get; set; }

    /// <summary>The finished surface, or null until one has been built this session.</summary>
    public RenderTarget2D? Field => IsReady ? GetTarget() : null;

    /// <summary>
    ///     Rebuilds the surface from scratch.
    /// </summary>
    /// <remarks>
    ///     Call from <c>Main.CheckMonoliths</c>. That is the one point in the frame where Terraria
    ///     expects mods to be binding their own render targets -- it runs before the scene is drawn
    ///     and nothing has claimed the backbuffer yet, so anywhere later means stealing a target
    ///     mid-scene and putting back whatever was there.
    ///     <para>
    ///         Safe to call on a frame with nothing to draw: the surface is cleared first, so an
    ///         emitter that has just stopped leaves an empty field rather than its last one. That
    ///         matters because the target preserves contents between frames.
    ///     </para>
    /// </remarks>
    public void Build()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        Request();
        PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);
    }

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        int scale = Math.Max(PixelScale, 1);

        // Ceiling: an odd screen width would otherwise drop the last column and leave a seam down
        // the edge of anything that reads this back.
        int width = (int)Math.Ceiling(Main.screenWidth / (float)scale);
        int height = (int)Math.Ceiling(Main.screenHeight / (float)scale);

        PrepareARenderTarget_AndListenToEvents(ref _target, device, width, height, RenderTargetUsage.PreserveContents);

        device.SetRenderTarget(_target);
        device.Clear(Color.Transparent);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            Blend,
            Sampler,
            DepthStencilState.None,
            RasterizerState.CullNone,
            StampEffect,
            ViewMatrix(scale, width, height)
        );

        drawContents(spriteBatch);

        spriteBatch.End();

        device.SetRenderTarget(null);
        _wasPrepared = true;
    }

    /// <summary>
    ///     The game view matrix, rebuilt about a surface of a different size.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="Main.GameViewMatrix" /> cannot be used directly: it is built for a
    ///         full-size surface, so its translation would put the zoom pivot off-centre here and the
    ///         field would drift away from the world it belongs to as the player zoomed.
    ///     </para>
    ///     <para>
    ///         The target's centre is taken from the target's real dimensions rather than from the
    ///         screen's over the scale, and the two are not the same number. The surface is sized by
    ///         a ceiling, so on any resolution the pixel scale does not divide evenly -- an odd width
    ///         at half scale is enough -- dividing the screen centre instead lands the pivot up to
    ///         half a target texel off, which is a whole screen pixel of drift at a scale of two.
    ///     </para>
    /// </remarks>
    private static Matrix ViewMatrix(int scale, int width, int height)
    {
        Vector2 screenCentre = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
        Vector2 targetCentre = new Vector2(width, height) / 2f;

        return Matrix.CreateTranslation(-screenCentre.X, -screenCentre.Y, 0f)
               * Matrix.CreateScale(Main.GameViewMatrix.Zoom.X / scale, Main.GameViewMatrix.Zoom.Y / scale, 1f)
               * Matrix.CreateTranslation(targetCentre.X, targetCentre.Y, 0f);
    }
}
