using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The post-processing chain the composed scene is pushed through on its way to the screen.
/// </summary>
/// <remarks>
///     Three passes, in an order that is not arbitrary:
///     <list type="number">
///         <item>
///             <b>Lens.</b> <c>PinchShader</c> bends the frame inward around the singularity.
///             This is the gravitational lensing, and it has to come first because it is meant
///             to act on <em>light that already exists</em> -- stars, streaks, ink. Running it
///             after the blur would smear first and bend the smear, which reads as a defect.
///         </item>
///         <item>
///             <b>Radial blur.</b> <c>RadialBlur</c> centred on the same point, at low mix. The
///             falloff keeps the focus sharp and lets the streak build toward the edges, so it
///             suggests motion into the well rather than a soft frame.
///         </item>
///         <item>
///             <b>Bloom.</b> Last, and folded into the composite instead of getting its own
///             target -- it is the only pass whose output nothing else reads.
///         </item>
///     </list>
///     The first two ping-pong through <see cref="InkTargets" />; the third is handed to the
///     caller to apply while blitting to the screen.
/// </remarks>
internal sealed class InkPostChain
{
    private WrapperShaderData<Assets.Shaders.PinchShader.Parameters>? lens;
    private WrapperShaderData<Assets.Shaders.RadialBlur.Parameters>? blur;
    private WrapperShaderData<Assets.Shaders.BloomShader.Parameters>? bloom;

    public void Reset()
    {
        lens = null;
        blur = null;
        bloom = null;
    }

    /// <summary>
    ///     Runs the warp and blur passes over <paramref name="targets" />.
    /// </summary>
    /// <returns>The surface holding the processed frame.</returns>
    public RenderTarget2D Run(SpriteBatch spriteBatch, GraphicsDevice device, InkTargets targets, in InkFrame frame)
    {
        lens ??= Assets.Shaders.PinchShader.CreateP0();
        blur ??= Assets.Shaders.RadialBlur.CreateP0();

        var center = frame.SingularityUv;
        var aspect = frame.Aspect;

        var lensParameters = lens.Parameters;
        lensParameters.uCenter = center;
        lensParameters.uAspect = aspect;
        lensParameters.uPower = InkStyle.LensPower;
        lensParameters.uRadius = InkStyle.LensRadius;
        lensParameters.uSoftness = InkStyle.LensSoftness;
        lensParameters.uSwirl = InkStyle.LensSwirl;
        lensParameters.uMix = InkStyle.LensMix * frame.Fade;

        Pass(spriteBatch, device, targets, lens);

        var blurParameters = blur.Parameters;
        blurParameters.uCenter = center * new Vector2(0.801f, 
            -0.33f + (float)Math.Sin(IlluminantInkScene.time * .333f) * .333f);
        blurParameters.uAspect = aspect;
        blurParameters.uStrength = 0.893f;
        blurParameters.uSpin = InkStyle.BlurSpin;
        blurParameters.uFalloff = InkStyle.BlurFalloff;
        blurParameters.uMix = InkStyle.BlurMix * frame.Fade;

        Pass(spriteBatch, device, targets, blur);

        return targets.Scene;
    }

    /// <summary>
    ///     Prepares the bloom pass for the caller's composite blit.
    /// </summary>
    /// <remarks>
    ///     Returned rather than applied so the composite is a single draw straight to whatever
    ///     surface Terraria had bound, instead of one more full-screen round trip.
    /// </remarks>
    public ShaderData PrepareBloom(in InkFrame frame)
    {
        bloom ??= AssetReferences.Assets.Shaders.BloomShader.CreateBloomPass();

        var parameters = bloom.Parameters;
        parameters.uTexelSize = new Vector2(1f / Math.Max(frame.Width, 1), 1f / Math.Max(frame.Height, 1));
        parameters.uIntensity = InkStyle.BloomIntensity * frame.Fade;
        parameters.uTint = InkStyle.BloomTint;

        return bloom;
    }

    /// <summary>
    ///     Blits <see cref="InkTargets.Scene" /> into <see cref="InkTargets.Swap" /> through one
    ///     shader, then flips the pair so the result becomes the new scene.
    /// </summary>
    private static void Pass(SpriteBatch spriteBatch, GraphicsDevice device, InkTargets targets, ShaderData shader)
    {
        device.SetRenderTarget(targets.Swap);
        device.Clear(Color.Transparent);

        // Opaque: the source already covers the target completely, and blending it against
        // the cleared surface would only cost fill rate. LinearClamp because both shaders
        // sample off-centre and wrapping would fold the far edge into the near one.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        shader.Apply();

        spriteBatch.Draw(targets.Scene, new Rectangle(0, 0, targets.Width, targets.Height), Color.White);

        spriteBatch.End();

        targets.Flip();
    }
}
