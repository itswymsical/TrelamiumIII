namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The ground of the scene: one full-screen pass of <c>InkNebula.fx</c>.
/// </summary>
/// <remarks>
///     Not an <see cref="IInkLayer" />. The layer stack describes things drawn <em>onto</em> the
///     scene under a shared blend mode; this establishes what the scene is, opaquely, before any
///     of that runs, and it carries no simulation state of its own -- everything it draws is a
///     pure function of <see cref="InkFrame.Time" />.
/// </remarks>
internal sealed class NebulaLayer
{
    private WrapperShaderData<Assets.Shaders.InkNebula.Parameters>? shader;
    private WrapperShaderData<Assets.Shaders.InkPixelate.Parameters>? pixelate;

    public void Reset()
    {
        // Dropped rather than reused so a resolution change or a mod reload can't leave a
        // stale Effect bound to a dead device.
        shader = null;
        pixelate = null;
    }

    /// <summary>
    ///     Draws the field into <see cref="InkTargets.Scene" />, through the pixel grid if one
    ///     is configured.
    /// </summary>
    /// <remarks>
    ///     When pixelating, the field is rendered at full resolution into the spare surface
    ///     first and only then flattened to the grid, because the grid has to average what was
    ///     under it -- see the note at the top of <c>InkPixelate.fx</c>. Rendering it small
    ///     and scaling up would be cheaper and would turn the ink's filaments into static.
    ///     <para />
    ///     Borrowing <see cref="InkTargets.Swap" /> as the scratch surface is safe: the post
    ///     chain fully overwrites it on its first pass, and nothing reads it before then.
    /// </remarks>
    public void Draw(SpriteBatch spriteBatch, GraphicsDevice device, InkTargets targets, in InkFrame frame, Texture2D mask, Rectangle area)
    {
        var pixelSize = frame.PixelSize;

        if (pixelSize <= 1)
        {
            DrawField(spriteBatch, in frame, mask, area);
            return;
        }

        device.SetRenderTarget(targets.Swap);
        device.Clear(Color.Black);

        DrawField(spriteBatch, in frame, mask, area);

        device.SetRenderTarget(targets.Scene);

        pixelate ??= Assets.Shaders.InkPixelate.CreateP0();

        var grid = pixelate.Parameters;
        grid.uTexel = new Vector2(1f / Math.Max(targets.Width, 1), 1f / Math.Max(targets.Height, 1));
        grid.uPixelSize = pixelSize;

        // Linear, not point. The 4x4 lattice only lands on exact texel centres when the cell
        // is a multiple of 4; at any other size point sampling silently drops whole columns
        // of the cell from the average. Bilinear taps straddle the gaps and cover it.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        pixelate.Apply();

        spriteBatch.Draw(targets.Swap, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);

        spriteBatch.End();
    }

    private void DrawField(SpriteBatch spriteBatch, in InkFrame frame, Texture2D mask, Rectangle area)
    {
        shader ??= Assets.Shaders.InkNebula.CreateP0();

        var parameters = shader.Parameters;

        parameters.uTime = frame.Time;
        // TEXCOORD0 spans the quad this pass is drawn into, not the frame, so the geometry has to
        // be expressed relative to that quad. Both terms reduce exactly to the frame-wide values
        // when the quad IS the frame, which is the menu's case -- so a full-screen sky is
        // unaffected, and a localised field gets the singularity at the middle of its own disc.
        parameters.uAspect = new Vector2(area.Width / (float)Math.Max(area.Height, 1), 1f);
        parameters.uCenter = new Vector2(
            (frame.Singularity.X - area.X) / Math.Max(area.Width, 1),
            (frame.Singularity.Y - area.Y) / Math.Max(area.Height, 1)
        );

        parameters.uScale = InkStyle.NebulaScale;
        parameters.uPivot = InkStyle.NebulaPivot;
        parameters.uExpand = InkStyle.NebulaExpand;
        parameters.uWarp = InkStyle.NebulaWarp;
        parameters.uSwirl = InkStyle.NebulaSwirl;
        parameters.uSwirlCore = InkStyle.NebulaSwirlCore;
        parameters.uSpin = InkStyle.NebulaSpin;
        parameters.uDensity = InkStyle.NebulaDensity;
        parameters.uContrast = InkStyle.NebulaContrast;
        parameters.uBodyLevel = InkStyle.NebulaBodyLevel;

        parameters.uSheen = InkStyle.NebulaSheen;
        parameters.uRelief = InkStyle.NebulaRelief;
        parameters.uReliefEps = InkStyle.NebulaReliefEps;
        parameters.uGloss = InkStyle.NebulaGloss;
        parameters.uRim = InkStyle.NebulaRim;
        parameters.uRimPower = InkStyle.NebulaRimPower;
        parameters.uLightDir = InkStyle.NebulaLight;

        parameters.uWeb = InkStyle.NebulaWeb;
        parameters.uWebPower = InkStyle.NebulaWebPower;
        parameters.uReach = InkStyle.NebulaReach;
        parameters.uHalo = InkStyle.NebulaHalo;

        parameters.uHorizon = InkStyle.Horizon;
        parameters.uRing = InkStyle.RingBrightness;
        parameters.uRingWidth = InkStyle.RingWidth;
        parameters.uDisc = InkStyle.DiscBrightness;
        parameters.uDiscRadius = InkStyle.DiscRadius;
        parameters.uDiscWidth = InkStyle.DiscWidth;
        parameters.uDiscFlatten = InkStyle.DiscFlatten;
        parameters.uBeam = InkStyle.DiscBeaming;

        parameters.uVignette = InkStyle.NebulaVignette;
        parameters.uOpacity = frame.Fade;
        parameters.uInkLow = InkStyle.InkLow;
        parameters.uInkHigh = InkStyle.InkHigh;

        // Both fields tile, so they are bound through the shader's own wrap samplers rather
        // than through s0 -- SpriteBatch owns s0 and its sampler state, and it would clamp
        // these into a smear at the edges.
        parameters.uNoiseTex = Assets.Textures.Noise.Turbulence_09.Asset.Value;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.Turbulence_09.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        parameters.uCellTex = Assets.Textures.Noise.Turbulence_05.Asset.Value;
        parameters.CellSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.Turbulence_05.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        // Immediate, because the pass has to be bound between Begin and the draw that uses
        // it; Deferred would flush after End with whatever shader happened to be current.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        shader.Apply();

        // The mask is s0, which InkNebula.fx multiplies its whole composed colour by -- void
        // included. MagicPixel is white and opaque, so it reads as a no-op and the field covers
        // the frame; a radial falloff instead confines the ink to a disc. Handed in rather than
        // chosen here, because it is the one thing that differs between a full-screen sky and an
        // effect standing somewhere in a world.
        spriteBatch.Draw(
            mask,
            area,
            Color.White
        );

        spriteBatch.End();
    }
}
