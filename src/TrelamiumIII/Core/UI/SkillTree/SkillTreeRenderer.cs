using Terraria.Graphics.Shaders;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>
/// Builds the node field's backdrop off screen, the same way the Illuminant Ink menu does: the nebula is
/// rendered at full resolution and then flattened to a pixel grid, so the result is pixel art rather than
/// a blur. The field blits <see cref="Result"/> through the bloom pass.
/// </summary>
/// <remarks>
/// Runs from <c>CheckMonoliths</c>, which is where Terraria expects a mod to bind its own render targets;
/// doing it inside the interface layer would fight whatever surface the UI is already drawing to.
/// </remarks>
internal static class SkillTreeRenderer
{
    /// <summary>Grid cell edge in UI pixels, before the interface scale is applied.</summary>
    private const float PIXEL_SIZE = 3f;

    private const float BLOOM_INTENSITY = 0.5f;

    private static readonly Vector3 BloomTint = new(0.62f, 0.80f, 1.00f);

    private static readonly SkillTreeBackdrop Backdrop = new();
    private static readonly SkillBlob[] Blobs = new SkillBlob[SkillTreeBackdrop.BLOB_SLOTS];

    private static InkTargets? _targets;
    private static WrapperShaderData<Assets.Shaders.InkPixelate.Parameters>? _pixelate;
    private static WrapperShaderData<Assets.Shaders.BloomShader.Parameters>? _bloom;

    private static Rectangle _area;
    private static Vector2 _pan;
    private static int _blobCount;

    /// <summary>The finished backdrop, or null until the field has submitted a frame and it has been built.</summary>
    public static RenderTarget2D? Result { get; private set; }

    public static void Load() => On_Main.CheckMonoliths += BuildFrame;

    public static void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        // Mods unload on a worker thread, and FNA3D only frees graphics resources on the main one.
        Main.QueueMainThreadAction(Release);
    }

    /// <summary>Records what the next frame should draw. Called from the field's update, in UI pixels.</summary>
    public static void Submit(Rectangle area, Vector2 pan, ReadOnlySpan<SkillBlob> blobs)
    {
        _area = area;
        _pan = pan;
        _blobCount = Math.Min(blobs.Length, Blobs.Length);

        blobs[.._blobCount].CopyTo(Blobs);
    }

    /// <summary>The bloom pass the field applies while blitting <see cref="Result"/> to the screen.</summary>
    public static ShaderData? Bloom()
    {
        if (Result is null)
            return null;

        _bloom ??= Assets.Shaders.BloomShader.CreateBloomPass();

        Assets.Shaders.BloomShader.Parameters parameters = _bloom.Parameters;
        parameters.uTexelSize = new Vector2(1f / Math.Max(Result.Width, 1), 1f / Math.Max(Result.Height, 1));
        parameters.uIntensity = BLOOM_INTENSITY;
        parameters.uTint = BloomTint;

        return _bloom;
    }

    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        if (Main.dedServ || Main.gameMenu || SkillTreeSystem.Openness <= 0f || _area.Width <= 0 || _area.Height <= 0)
        {
            Release();

            return;
        }

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        SpriteBatch spriteBatch = Main.spriteBatch;

        // Backbuffer pixels, so the field is sampled 1:1 at the size it is actually blitted at.
        int width = (int)MathF.Ceiling(_area.Width * Main.UIScale);
        int height = (int)MathF.Ceiling(_area.Height * Main.UIScale);

        _targets ??= new InkTargets();

        if (!_targets.Ensure(device, width, height))
        {
            Result = null;

            return;
        }

        RenderTargetBinding[] bound = device.GetRenderTargets();

        device.SetRenderTarget(_targets.Scene);
        device.Clear(Color.Transparent);

        var full = new Rectangle(0, 0, width, height);

        Backdrop.Draw(spriteBatch, full, _area, _pan, Blobs.AsSpan(0, _blobCount));

        Pixelate(spriteBatch, device, width, height);

        // Vanilla's own CheckMonoliths leaves the device on the back buffer, so an empty binding set is
        // the state to put back rather than something to pass to SetRenderTargets.
        if (bound.Length == 0)
            device.SetRenderTarget(null);
        else
            device.SetRenderTargets(bound);

        Result = _targets.Scene;
    }

    /// <summary>Flattens the scene onto the grid and flips the pair, so the grid becomes the scene.</summary>
    private static void Pixelate(SpriteBatch spriteBatch, GraphicsDevice device, int width, int height)
    {
        _pixelate ??= Assets.Shaders.InkPixelate.CreateP0();

        Assets.Shaders.InkPixelate.Parameters parameters = _pixelate.Parameters;
        parameters.uTexel = new Vector2(1f / Math.Max(width, 1), 1f / Math.Max(height, 1));
        parameters.uPixelSize = MathF.Max(PIXEL_SIZE * Main.UIScale, 1f);

        device.SetRenderTarget(_targets!.Swap);
        device.Clear(Color.Transparent);

        // Linear, not point: the 4x4 tap lattice only lands on texel centres when the cell is a multiple
        // of 4, and at any other size point sampling drops whole columns of the cell from the average.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer);

        _pixelate.Apply();

        spriteBatch.Draw(_targets.Scene, new Rectangle(0, 0, width, height), Color.White);

        spriteBatch.End();

        _targets.Flip();
    }

    /// <summary>Frees the surfaces as soon as the tree is gone, rather than holding two of them all session.</summary>
    private static void Release()
    {
        Result = null;

        if (_targets is null)
            return;

        _targets.Dispose();
        _targets = null;

        Backdrop.Reset();
        _pixelate = null;
        _bloom = null;
    }
}
