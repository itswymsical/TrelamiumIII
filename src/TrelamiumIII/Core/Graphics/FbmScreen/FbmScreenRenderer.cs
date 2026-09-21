using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.Utilities;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Core.Graphics.FbmScreen;

/// <summary>
/// Full-screen FBM mist with <see cref="ScreenObject"/>s over it, rendered at full resolution and then flattened to
/// a pixel grid, the same way the Illuminant Ink menu and the skill tree backdrop are. Hosts call
/// <see cref="Advance"/>, then <see cref="Render"/> with no batch open, then <see cref="Draw"/>.
/// </summary>
internal sealed class FbmScreenRenderer(FbmScreenStyle style)
{
    public const int StepsPerSecond = 60;

    /// <summary>A stall longer than this many steps is dropped rather than caught up.</summary>
    private const int MaxStepsPerAdvance = 4;

    private readonly InkTargets targets = new();
    private readonly List<ScreenObject> objects = [];
    private readonly List<ScreenObject> spawned = [];

    private WrapperShaderData<Assets.Shaders.ScreenFbm.Parameters>? field;
    private WrapperShaderData<Assets.Shaders.InkPixelate.Parameters>? pixelate;
    private WrapperShaderData<Assets.Shaders.BloomShader.Parameters>? bloom;

    private float pendingSeconds;
    private int width;
    private int height;

    public FbmScreenStyle Style { get; set; } = style;

    /// <summary>Separate from <c>Main.rand</c>, which world generation reseeds per pass while a loading screen draws.</summary>
    public UnifiedRandom Random { get; } = new();

    public float Time { get; private set; }

    /// <summary>Iris and swirl centre, 0..1 of the target.</summary>
    public Vector2 Focus { get; set; } = new(0.5f);

    /// <summary>0 clear, 1 fully covered.</summary>
    public float Coverage { get; set; }

    /// <summary>Push toward <see cref="Focus"/>; the field is enlarged by exp(Zoom).</summary>
    public float Zoom { get; set; }

    /// <summary>Applied when <see cref="Draw"/> composites, not in the target.</summary>
    public float Opacity { get; set; } = 1f;

    public int ObjectCount => objects.Count + spawned.Count;

    /// <summary>The last frame built by <see cref="Render"/>, or null before the first and after <see cref="Release"/>.</summary>
    public RenderTarget2D? Result { get; private set; }

    public ScreenFrame Frame => new(width, height, Time, Focus * new Vector2(width, height), Coverage, Style);

    /// <summary>Added after the current step, so an object may spawn others from its own update.</summary>
    public void Spawn(ScreenObject item) => spawned.Add(item);

    /// <summary>Clears objects and state; keeps the targets.</summary>
    public void Reset()
    {
        objects.Clear();
        spawned.Clear();

        Time = 0f;
        pendingSeconds = 0f;
        Focus = new Vector2(0.5f);
        Coverage = 0f;
        Zoom = 0f;
        Opacity = 1f;
    }

    /// <summary>Steps objects at <see cref="StepsPerSecond"/>. Objects wait for the first <see cref="Render"/> to learn the frame size.</summary>
    public void Advance(float seconds)
    {
        Time += seconds;

        if (width <= 0 || height <= 0)
            return;

        const float step = 1f / StepsPerSecond;

        pendingSeconds = MathF.Min(pendingSeconds + seconds, MaxStepsPerAdvance * step);

        while (pendingSeconds >= step)
        {
            pendingSeconds -= step;
            Step();
        }
    }

    /// <summary>
    /// Builds the frame into <see cref="Result"/> at <paramref name="targetWidth"/> x <paramref name="targetHeight"/>
    /// and rebinds what was bound before. False if the size is degenerate.
    /// </summary>
    public bool Render(SpriteBatch spriteBatch, GraphicsDevice device, int targetWidth, int targetHeight)
    {
        if (!targets.Ensure(device, targetWidth, targetHeight))
        {
            Result = null;

            return false;
        }

        width = targetWidth;
        height = targetHeight;

        ScreenFrame frame = Frame;
        RenderTargetBinding[] bound = device.GetRenderTargets();

        device.SetRenderTarget(targets.Scene);
        device.Clear(Color.Transparent);

        DrawField(spriteBatch, in frame);
        DrawObjects(spriteBatch, in frame);
        Pixelate(spriteBatch, device, in frame);

        if (bound.Length == 0)
            device.SetRenderTarget(null);
        else
            device.SetRenderTargets(bound);

        Result = targets.Scene;

        return true;
    }

    /// <summary>Composites <see cref="Result"/> into <paramref name="destination"/> through bloom, in a batch of its own.</summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle destination)
    {
        if (Result is not { IsDisposed: false } result || Opacity <= 0f)
            return;

        bloom ??= Assets.Shaders.BloomShader.CreateBloomPass();

        Assets.Shaders.BloomShader.Parameters parameters = bloom.Parameters;
        parameters.uTexelSize = new Vector2(1f / Math.Max(result.Width, 1), 1f / Math.Max(result.Height, 1));
        parameters.uIntensity = Style.BloomIntensity;
        parameters.uTint = Style.BloomTint.ToVector3();

        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        bloom.Apply();

        spriteBatch.Draw(result, destination, Color.White * Opacity);

        spriteBatch.End();
    }

    /// <summary>Frees the targets. Main thread only; objects and state are kept.</summary>
    public void Release()
    {
        targets.Dispose();
        Result = null;

        // Dropped rather than reused, so a resolution change or a reload cannot leave a dead Effect bound.
        field = null;
        pixelate = null;
        bloom = null;
    }

    private void Step()
    {
        ScreenFrame frame = Frame;

        foreach (ScreenObject item in objects)
        {
            if (item.Active)
                item.Update(this, in frame);
        }

        objects.RemoveAll(static item => !item.Active);

        objects.AddRange(spawned);
        spawned.Clear();
    }

    private void DrawField(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        field ??= Assets.Shaders.ScreenFbm.CreateP0();

        Assets.Shaders.ScreenFbm.Parameters parameters = field.Parameters;
        FbmScreenStyle style = Style;
        Texture2D noise = (style.Noise ?? Assets.Textures.Noise.Perlin.Asset).Value;

        parameters.uTime = Time;
        parameters.uAspect = new Vector2(frame.Width / (float)Math.Max(frame.Height, 1), 1f);
        parameters.uFocus = Focus;
        parameters.uScale = style.Scale;
        parameters.uZoom = Zoom;
        parameters.uPivot = style.Pivot;
        parameters.uExpand = style.Expand;
        parameters.uWarp = style.Warp;
        parameters.uSwirl = style.Swirl;
        parameters.uSwirlCore = style.SwirlCore;
        parameters.uSpin = style.Spin;
        parameters.uDrift = style.Drift;
        parameters.uDensity = style.Density;
        parameters.uContrast = style.Contrast;
        parameters.uBands = style.Bands;

        parameters.uCoverage = Coverage;
        parameters.uInward = style.Inward ? 1f : 0f;
        parameters.uEdgeNoise = style.EdgeNoise;
        parameters.uEdgeSoft = style.EdgeSoft;
        parameters.uEdgeWidth = style.EdgeWidth;
        parameters.uEdgeColor = style.EdgeColor.ToVector3();

        parameters.uVignette = style.Vignette;

        parameters.uVoidColor = style.VoidColor.ToVector3();
        parameters.uLowColor = style.LowColor.ToVector3();
        parameters.uHighColor = style.HighColor.ToVector3();
        parameters.uGlowColor = style.GlowColor.ToVector3();
        parameters.uGlow = style.Glow;

        // Tiles, so it goes through the shader's own wrap sampler; SpriteBatch owns s0 and would clamp it.
        parameters.uNoiseTex = noise;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = noise,
            Sampler = SamplerState.LinearWrap,
        };

        // Opaque: the pass writes its premultiplied result, transparent where the iris is open, straight into the target.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        field.Apply();

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);

        spriteBatch.End();
    }

    private void DrawObjects(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        if (objects.Count == 0)
            return;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        foreach (ScreenObject item in objects)
            item.DrawAlpha(spriteBatch, in frame);

        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, Blends.AdditiveColor, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        foreach (ScreenObject item in objects)
            item.DrawAdditive(spriteBatch, in frame);

        spriteBatch.End();
    }

    /// <summary>Averages each grid cell into the spare target and flips, so the grid becomes the scene.</summary>
    private void Pixelate(SpriteBatch spriteBatch, GraphicsDevice device, in ScreenFrame frame)
    {
        float size = Style.PixelSize * frame.Scale;

        if (size <= 1f)
            return;

        pixelate ??= Assets.Shaders.InkPixelate.CreateP0();

        Assets.Shaders.InkPixelate.Parameters parameters = pixelate.Parameters;
        parameters.uTexel = new Vector2(1f / Math.Max(frame.Width, 1), 1f / Math.Max(frame.Height, 1));
        parameters.uPixelSize = MathF.Round(size);

        device.SetRenderTarget(targets.Swap);
        device.Clear(Color.Transparent);

        // Linear: the 4x4 tap lattice lands on texel centres only when the cell is a multiple of 4.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        pixelate.Apply();

        spriteBatch.Draw(targets.Scene, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);

        spriteBatch.End();

        targets.Flip();
    }
}
