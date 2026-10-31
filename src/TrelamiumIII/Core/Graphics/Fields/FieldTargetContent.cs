using ReLogic.Content;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     The pair of off-screen surfaces a <see cref="FieldLayer" /> sums its blobs into.
/// </summary>
/// <remarks>
///     <para>
///         Kept at <see cref="PixelScale" /> below screen resolution for two reasons: the field is
///         blurry data rather than an image, so it loses nothing to the downscale, and the shader
///         quantises its decisions to this grid. One field texel becomes one chunky output pixel.
///     </para>
///     <para>
///         Two targets, written together as a multiple render target. Drawing the nodes twice into
///         two separately-bound surfaces would encode the same numbers, but only if nothing ever
///         made the two passes disagree -- a different sampler, a dropped node, a stale budget --
///         and an attribute recovered by dividing against a coverage from a different pass is
///         silently wrong rather than visibly broken. One draw makes them the same blob by
///         construction.
///     </para>
/// </remarks>
internal sealed class FieldTargetContent(FieldLayer layer) : ARenderTargetContentByRequest
{
    /// <summary>World pixels per field texel.</summary>
    /// <remarks>
    ///     World pixels, not screen pixels. A texel tied to the screen stays the same size as the
    ///     camera zooms while the tiles around it grow, so at full zoom the field resolves finer than
    ///     the art it sits in. Tied to the world, the zoom upscales the field with everything else.
    /// </remarks>
    public int PixelScale { get; set; } = 2;

    /// <summary>
    ///     The target's size in texels: enough to cover the unzoomed screen at
    ///     <see cref="PixelScale" /> world pixels per texel, plus one for the grid's phase.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Deliberately not sized to the zoomed view. A size that follows the zoom steps a whole
    ///         texel at a time while the zoom eases continuously, and every step resizes every texel
    ///         at once -- the field re-cuts itself on each frame of a zoom and jitters. Zooming in
    ///         only shows less of the world, so the unzoomed size always covers it; the texels past
    ///         the view are clipped by <c>FieldBounds</c> and cost nothing.
    ///     </para>
    ///     <para>
    ///         The extra texel is for <see cref="Origin" />: flooring the view's corner onto the grid
    ///         can start the target up to a texel before the screen does.
    ///     </para>
    /// </remarks>
    public (int Width, int Height) Size()
    {
        // A zoom below one shows more world than the screen; nothing in vanilla goes there, but a
        // camera mod could, and an undersized target would cut the field off at its edge.
        Vector2 texel = Vector2.Min(Main.GameViewMatrix.Zoom, Vector2.One) * Math.Max(PixelScale, 1);

        return ((int)Math.Ceiling(Main.screenWidth / texel.X) + 1, (int)Math.Ceiling(Main.screenHeight / texel.Y) + 1);
    }

    /// <summary>
    ///     The world texel the target's top-left corner sits on: the view's top-left, floored onto
    ///     the <see cref="PixelScale" /> grid.
    /// </summary>
    /// <remarks>
    ///     Anchoring here is what locks the grid to the world. Anchored to the screen, the cells sit
    ///     still while the world slides and zooms under them, so every pan and every frame of a zoom
    ///     moves where each cell falls on the field.
    /// </remarks>
    public Point Origin()
    {
        int scale = Math.Max(PixelScale, 1);
        Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
        Vector2 viewTopLeft = Main.screenPosition + half - half / Main.GameViewMatrix.Zoom;

        return new Point((int)MathF.Floor(viewTopLeft.X / scale), (int)MathF.Floor(viewTopLeft.Y / scale));
    }

    /// <summary>The <see cref="Origin" /> the current contents were drawn from.</summary>
    /// <remarks>
    ///     The composite reads this rather than asking again. A grid recomputed later in the frame
    ///     only matches the one the stamps landed on if nothing has moved the camera or the zoom in
    ///     between, and when something has, the whole field slides by the difference.
    /// </remarks>
    public Point BuiltOrigin { get; private set; }

    /// <summary>The <see cref="Size" /> the current contents were drawn at.</summary>
    public (int Width, int Height) BuiltSize { get; private set; }

    /// <summary>Screen pixels per texel: <see cref="PixelScale" /> world pixels, zoomed.</summary>
    public Vector2 TexelSize() => Main.GameViewMatrix.Zoom * Math.Max(PixelScale, 1);

    /// <summary>Where a point in target texels lands on the screen.</summary>
    public Vector2 TexelToScreen(Vector2 texel, Point origin)
    {
        Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
        Vector2 world = (origin.ToVector2() + texel) * Math.Max(PixelScale, 1);

        return (world - Main.screenPosition - half) * Main.GameViewMatrix.Zoom + half;
    }

    /// <summary>A world position in target texels.</summary>
    public Vector2 WorldToTexel(Vector2 world, Point origin) => world / Math.Max(PixelScale, 1) - origin.ToVector2();

    /// <summary>
    ///     Points the composite shader's screen uniforms at this target's grid.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>GasFbm.fx</c> rebuilds world position from its uv as
    ///         <c>(uv * aspect - half) / Zoom + half + WorldOffset</c>, written for a target that
    ///         covered exactly the screen. This one covers a world-anchored rectangle instead, but the
    ///         map is still affine in uv, so solving for the <c>Zoom</c> and <c>WorldOffset</c> that
    ///         make it land on the same world position keeps the noise locked to the world with no
    ///         change to the shader.
    ///     </para>
    ///     <para>
    ///         Run after <see cref="FieldLayer.ApplyScreenParameters(Effect, Vector2)" />, which
    ///         writes the screen-covering values.
    ///     </para>
    /// </remarks>
    public void ApplyGrid(Effect effect)
    {
        (int width, int height) = BuiltSize;
        Point origin = BuiltOrigin;
        int scale = Math.Max(PixelScale, 1);
        float screenHeight = Main.screenHeight;

        Vector2 half = new Vector2(Main.screenWidth / screenHeight, 1f) * 0.5f;
        Vector2 zoom = new Vector2(Main.screenWidth / (float)(width * scale), screenHeight / (height * scale));
        Vector2 worldOffset = origin.ToVector2() * scale / screenHeight + half / zoom - half;

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(1f / width, 1f / height));
        effect.Parameters["Zoom"]?.SetValue(zoom);
        effect.Parameters["WorldOffset"]?.SetValue(worldOffset);
    }

    /// <summary>The attribute surface, or null before the first accumulation.</summary>
    public RenderTarget2D? Attributes => attributes;

    private RenderTarget2D? attributes;

    private static Asset<Effect>? stampShader;

    /// <summary>
    ///     Plain additive, with no source-alpha multiply.
    /// </summary>
    /// <remarks>
    ///     <see cref="BlendState.Additive" /> is <c>SourceAlpha, One</c>, which weights every
    ///     channel by the stamp's own alpha. That is why alpha could never hold data and why the
    ///     field accumulated the square of a node's coverage. The falloff moves into
    ///     <c>FieldStamp.fx</c> instead, leaving this a straight sum of what the shader wrote.
    /// </remarks>
    private static readonly BlendState Accumulate = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Forgets both surfaces and disposes them on the main thread.</summary>
    /// <remarks>
    ///     <see cref="ARenderTargetContentByRequest.Reset" /> only drops the reference. The vanilla
    ///     allocator these used to come from hung its own disposal off graphics-device events, and
    ///     <see cref="FieldSurface" /> does not, so the layer has to hand them back itself.
    /// </remarks>
    public void Free()
    {
        RenderTarget2D? density = _target;
        RenderTarget2D? attribute = attributes;

        Reset();
        attributes = null;

        if (density is not null)
            Main.QueueMainThreadAction(density.Dispose);

        if (attribute is not null)
            Main.QueueMainThreadAction(attribute.Dispose);
    }

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        stampShader ??= Assets.Shaders.FieldStamp.Asset;

        if (stampShader is not { Value: { } stamp } || layer.StampTexture is not { } stampTexture)
            return;

        (int width, int height) = Size();
        Point origin = Origin();

        // Not the vanilla helper: it takes the back buffer's eight-bit format, which clamps these
        // sums flat the moment two blobs overlap. See FieldSurface.
        RenderTarget2D? density = _target;
        FieldSurface.Ensure(ref density, device, width, height);
        FieldSurface.Ensure(ref attributes, device, width, height);
        _target = density!;

        int vertexCount = layer.BuildStampGeometry();

        if (vertexCount == 0)
        {
            _wasPrepared = false;
            return;
        }

        device.SetRenderTargets(_target, attributes);
        device.Clear(Color.Transparent);

        device.BlendState = Accumulate;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        stamp.Parameters["uWorldViewProjection"]?.SetValue(Transform(origin, width, height));
        stamp.Parameters["uStamp"]?.SetValue(stampTexture);

        foreach (EffectPass pass in stamp.CurrentTechnique.Passes)
        {
            pass.Apply();

            // After Apply, not before: applying a pass restores the sampler state its own
            // sampler_state block declares, which would undo the layer's choice of filtering.
            device.SamplerStates[0] = layer.StampSampler;

            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                layer.StampVertices, 0, vertexCount,
                layer.StampIndices, 0, vertexCount / 4 * 2);
        }

        device.SetRenderTargets(null);

        BuiltOrigin = origin;
        BuiltSize = (width, height);
        _wasPrepared = true;
    }

    /// <summary>
    ///     Stamp positions (world minus <see cref="Main.screenPosition" />) to clip space, by way of
    ///     the field target's own grid.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         No zoom here at all: the target holds the world at <see cref="PixelScale" /> world
    ///         pixels per texel from <see cref="Origin" />, and the composite applies the zoom when
    ///         it puts the target back on the screen.
    ///     </para>
    ///     <para>
    ///         The projection matches what <see cref="SpriteBatch" /> builds for itself, which is a
    ///         plain off-centre orthographic with no half-pixel correction -- FNA targets APIs that
    ///         do not want one. Half a texel of disagreement here would shift every cell the
    ///         composite shader quantises to by half a cell.
    ///     </para>
    /// </remarks>
    private Matrix Transform(Point origin, int width, int height)
    {
        int scale = Math.Max(PixelScale, 1);

        // Folded into one offset before it reaches the matrix: screenPosition and the origin are
        // each far from zero deep in a world, and their difference is small and exact.
        Vector2 offset = Main.screenPosition - origin.ToVector2() * scale;

        Matrix toTarget = Matrix.CreateTranslation(offset.X, offset.Y, 0f)
                          * Matrix.CreateScale(1f / scale, 1f / scale, 1f);

        return toTarget * Matrix.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
    }
}
