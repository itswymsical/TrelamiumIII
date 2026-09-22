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
    public int PixelScale { get; set; } = 2;

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

    protected override void HandleUseRequest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        stampShader ??= Assets.Shaders.FieldStamp.Asset;

        if (stampShader is not { Value: { } stamp } || layer.StampTexture is not { } stampTexture)
            return;

        (int width, int height) = FieldSurface.ScreenSize(PixelScale);

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

        stamp.Parameters["uWorldViewProjection"]?.SetValue(Transform(width, height));
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
        _wasPrepared = true;
    }

    /// <summary>
    ///     Screen pixels to clip space, by way of the field target's own grid.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="Main.GameViewMatrix" /> cannot be used directly: it is built for a
    ///         full-size surface, so its translation would put the zoom pivot off-centre here.
    ///         Rebuild it about the smaller target, then project that onto the target.
    ///     </para>
    ///     <para>
    ///         The projection matches what <see cref="SpriteBatch" /> builds for itself, which is a
    ///         plain off-centre orthographic with no half-pixel correction -- FNA targets APIs that
    ///         do not want one. Half a texel of disagreement here would shift every cell the
    ///         composite shader quantises to by half a cell.
    ///     </para>
    /// </remarks>
    private Matrix Transform(int width, int height)
    {
        Vector2 fullScreenCenter = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
        Vector2 scaledCenter = fullScreenCenter / PixelScale;

        Matrix toTarget = Matrix.CreateTranslation(-fullScreenCenter.X, -fullScreenCenter.Y, 0f)
                          * Matrix.CreateScale(Main.GameViewMatrix.Zoom.X / PixelScale, Main.GameViewMatrix.Zoom.Y / PixelScale, 1f)
                          * Matrix.CreateTranslation(scaledCenter.X, scaledCenter.Y, 0f);

        return toTarget * Matrix.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
    }
}
