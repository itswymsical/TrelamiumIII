namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Everything a layer needs to know about the frame it is drawing into, resolved once by
///     <see cref="IlluminantInkScene" /> and handed down.
/// </summary>
/// <remarks>
///     Layers never read <see cref="Main" /> for geometry. During the menu Terraria rescales
///     <see cref="Main.screenWidth" /> into UI space, so those fields disagree with the render
///     target the layers actually draw into; resolving the conversion once, here, is what keeps
///     that disagreement from leaking into every layer.
/// </remarks>
internal readonly struct InkFrame
{
    /// <summary>Render target width, in real backbuffer pixels.</summary>
    public readonly int Width;

    /// <summary>Render target height, in real backbuffer pixels.</summary>
    public readonly int Height;

    /// <summary>Seconds since the menu was entered.</summary>
    public readonly float Time;

    /// <summary>Singularity position, in target pixels.</summary>
    public readonly Vector2 Singularity;

    /// <summary>Cursor position, converted from the menu's UI space into target pixels.</summary>
    public readonly Vector2 Cursor;

    /// <summary>0..1 master fade, so entering the menu doesn't pop.</summary>
    public readonly float Fade;

    public InkFrame(int width, int height, float time, Vector2 singularity, Vector2 cursor, float fade)
    {
        Width = width;
        Height = height;
        Time = time;
        Singularity = singularity;
        Cursor = cursor;
        Fade = fade;
    }

    /// <summary>(width/height, 1), the aspect correction the shaders expect.</summary>
    public Vector2 Aspect => new(Width / (float)Math.Max(Height, 1), 1f);

    /// <summary>The singularity in 0..1 screen space, for shader uniforms.</summary>
    public Vector2 SingularityUv => new(Singularity.X / Math.Max(Width, 1), Singularity.Y / Math.Max(Height, 1));

    /// <summary>
    ///     Pixels per half-height unit. Multiply a scene distance by this to get pixels.
    /// </summary>
    public float UnitToPixels => Height;

    /// <summary>
    ///     Edge of the nebula's pixel grid for this frame, in target pixels.
    /// </summary>
    /// <remarks>
    ///     <see cref="InkStyle.NebulaPixelSize" /> is authored against 1080p and scaled here,
    ///     so the grid keeps the same apparent chunkiness at any resolution instead of
    ///     dissolving on a high-DPI panel. 1 means no pixelation and the pass is skipped.
    /// </remarks>
    public int PixelSize => Math.Max(1, (int)MathF.Round(InkStyle.NebulaPixelSize * Height / 1080f));

    /// <summary>Converts a polar offset from the singularity into a target pixel position.</summary>
    public Vector2 Orbit(float angle, float radius)
    {
        return Singularity + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * UnitToPixels);
    }

    /// <summary>True if <paramref name="position" /> is near enough the frame to be worth drawing.</summary>
    public bool OnScreen(Vector2 position, float margin)
    {
        return position.X >= -margin && position.X <= Width + margin
            && position.Y >= -margin && position.Y <= Height + margin;
    }
}

/// <summary>
///     One element of the menu scene, drawn over the nebula ground.
/// </summary>
/// <remarks>
///     Split into two draw calls because the scene batches by blend mode, not by layer: ink
///     <em>occludes</em> and light <em>adds</em>, and interleaving those per-layer would mean a
///     <see cref="SpriteBatch" /> restart for every layer instead of two for the whole stack.
///     A layer implements whichever half it needs and leaves the other empty.
/// </remarks>
internal interface IInkLayer
{
    /// <summary>Rebuilds the layer's contents. Called on entry and on resolution change.</summary>
    void Reset(in InkFrame frame);

    /// <summary>Advances simulation by one tick. Never draws.</summary>
    void Update(in InkFrame frame);

    /// <summary>Drawn under <see cref="BlendState.AlphaBlend" />, before the light pass.</summary>
    void DrawInk(SpriteBatch spriteBatch, in InkFrame frame);

    /// <summary>Drawn under <see cref="BlendState.Additive" />, after the ink pass.</summary>
    void DrawLight(SpriteBatch spriteBatch, in InkFrame frame);
}
