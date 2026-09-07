using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The Illuminant Ink renderer: targets, layer stack and post chain, with no opinion about who
///     is driving it or what it is being drawn over.
/// </summary>
/// <remarks>
///     <para>
///         Everything host-specific was lifted out of here, and what is left is deliberately
///         incurious. This type does not know what a menu is, does not build its own
///         <see cref="InkFrame" />, and does not own a clock -- a host resolves the frame geometry,
///         the time and the fade, and hands them down. That is what lets the same renderer be the
///         title screen's sky and a singularity opened over an arena.
///     </para>
///     <para>
///         <b>The two knobs that make it general.</b> Both were already latent in the pipeline:
///         <list type="number">
///             <item>
///                 <b>Coverage mask.</b> <c>InkNebula.fx</c> reads <c>s0</c> as a mask and multiplies
///                 its whole composed colour by it, void included. Passing
///                 <see cref="Terraria.GameContent.TextureAssets.MagicPixel" /> gives a full-frame
///                 field; passing a radial falloff confines the ink to a disc. No shader change, and
///                 the shader's own header calls this out as the intended use.
///             </item>
///             <item>
///                 <b>Composite blend</b>, which is the host's business rather than this type's. A
///                 full-frame sky replaces what is behind it and composites opaque. An effect standing
///                 in a lit world has to <em>add</em> instead, and it can: masked-out regions leave
///                 <c>RGB = 0</c>, which contributes exactly nothing additively.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Known limitation in world use.</b> The lens and blur passes act on this renderer's own
///         target, so they warp the ink and the stars but not the world behind them -- in the menu the
///         target <em>is</em> the whole frame, so there was no distinction to make. Lensing the actual
///         scene needs a <see cref="Terraria.Graphics.Effects.Filters" /> pass over Terraria's
///         captured frame, the way <c>MagnetizerFieldRenderer</c> does it. That is an addition rather
///         than a correction; nothing here has to change to allow it.
///     </para>
/// </remarks>
internal sealed class InkScene
{
    private readonly InkTargets targets = new();
    private readonly NebulaLayer nebula = new();
    private readonly InkPostChain post = new();
    private readonly IInkLayer[] stack;

    private bool seeded;
    private int seededWidth;
    private int seededHeight;

    /// <summary>
    ///     Builds a scene over <paramref name="layers" />.
    /// </summary>
    /// <remarks>
    ///     The stack is a constructor argument rather than a fixed list because it is the part that
    ///     genuinely differs between hosts. The mote layer chases <see cref="InkFrame.Cursor" /> and
    ///     the starfield fills the frame corner to corner: both are right for a title screen and both
    ///     are wrong for a hole hanging over an arena, where a scatter of stars across the sky would
    ///     read as a rendering fault rather than as depth.
    /// </remarks>
    public InkScene(params IInkLayer[] layers)
    {
        stack = layers;
    }

    public bool Ready => targets.Ready;

    public int Width => targets.Width;

    public int Height => targets.Height;

    /// <summary>
    ///     Allocates the target pair, reallocating on a resolution change.
    /// </summary>
    /// <returns>False if the size is degenerate, in which case nothing can be drawn this frame.</returns>
    public bool Ensure(GraphicsDevice device, int width, int height)
    {
        if (!targets.Ensure(device, width, height))
            return false;

        if (!seeded || seededWidth != width || seededHeight != height)
            Seed(width, height);

        return true;
    }

    /// <summary>Advances every layer by one tick. Never draws.</summary>
    public void Update(in InkFrame frame)
    {
        if (!targets.Ready)
            return;

        foreach (var layer in stack)
            layer.Update(in frame);
    }

    /// <summary>
    ///     Renders the scene and runs the post chain over it.
    /// </summary>
    /// <remarks>
    ///     Whatever the caller had bound is saved and put back before returning, so a host can call
    ///     this mid-frame without discarding a capture it was in the middle of -- Terraria may have
    ///     the backbuffer redirected into <c>screenTarget</c> for a scene filter, and clobbering that
    ///     would drop the capture.
    ///     <para>
    ///         No <see cref="SpriteBatch" /> is open on entry or on exit. The caller closes its own
    ///         batch first and reopens whatever it needs afterwards.
    ///     </para>
    /// </remarks>
    /// <returns>The surface holding the finished image.</returns>
    public RenderTarget2D Render(SpriteBatch spriteBatch, GraphicsDevice device, in InkFrame frame, Texture2D mask, Rectangle area)
    {
        var restore = device.GetRenderTargets();

        // Before the scene target is claimed, so a layer that sums its own field off-screen can
        // claim one without stealing this pass's out from under it. See IInkLayer.Prepare.
        foreach (var layer in stack)
            layer.Prepare(spriteBatch, device, in frame);

        device.SetRenderTarget(targets.Scene);
        device.Clear(Color.Black);

        Compose(spriteBatch, device, in frame, mask, area);

        var result = post.Run(spriteBatch, device, targets, in frame);

        if (restore.Length > 0)
            device.SetRenderTargets(restore);
        else
            device.SetRenderTarget(null);

        return result;
    }

    /// <summary>
    ///     The bloom pass, for the host to apply while blitting.
    /// </summary>
    /// <remarks>
    ///     Handed back rather than applied here so the composite stays a single draw straight to
    ///     whatever surface the host is targeting, instead of one more full-screen round trip.
    /// </remarks>
    public ShaderData PrepareBloom(in InkFrame frame) => post.PrepareBloom(in frame);

    public void Release()
    {
        targets.Dispose();
        nebula.Reset();
        post.Reset();

        // A layer that owns a surface says so by being disposable, rather than by a Release on
        // IInkLayer that every other layer would have to implement empty. Disposing one and then
        // seeding it again is fine and is what happens on a resolution change: the same contract
        // InkTargets keeps, where Ensure reallocates after a Dispose.
        foreach (var layer in stack)
        {
            if (layer is IDisposable disposable)
                disposable.Dispose();
        }

        seeded = false;
        seededWidth = 0;
        seededHeight = 0;
    }

    /// <summary>Lays the stack down over the nebula ground, batched by blend mode.</summary>
    /// <remarks>
    ///     Two batches for the whole stack rather than one per layer: ink <em>occludes</em> and light
    ///     <em>adds</em>, and interleaving those per layer would restart the batch every time.
    ///     <para>
    ///         The nebula takes the device because it may retarget mid-pass to run its pixel grid. It
    ///         leaves <see cref="InkTargets.Scene" /> bound either way.
    ///     </para>
    /// </remarks>
    private void Compose(SpriteBatch spriteBatch, GraphicsDevice device, in InkFrame frame, Texture2D mask, Rectangle area)
    {
        nebula.Draw(spriteBatch, device, targets, in frame, mask, area);

        if (stack.Length == 0)
            return;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        foreach (var layer in stack)
            layer.DrawInk(spriteBatch, in frame);

        spriteBatch.End();

        // Between the two shared passes, with nothing open: each of these runs its own shader, so
        // each opens and closes a batch of its own.
        foreach (var layer in stack)
            layer.Composite(spriteBatch, in frame);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        foreach (var layer in stack)
            layer.DrawLight(spriteBatch, in frame);

        spriteBatch.End();
    }

    /// <summary>
    ///     Rebuilds every layer's contents for a newly sized frame.
    /// </summary>
    /// <remarks>
    ///     Seeded against a frame with the singularity at rest and no cursor, because this runs before
    ///     the host has necessarily resolved either -- what the layers need from it is the geometry.
    /// </remarks>
    private void Seed(int width, int height)
    {
        var frame = new InkFrame(
            width,
            height,
            0f,
            InkStyle.Singularity * new Vector2(width, height),
            Vector2.Zero,
            0f
        );

        foreach (var layer in stack)
            layer.Reset(in frame);

        seeded = true;
        seededWidth = width;
        seededHeight = height;
    }
}
