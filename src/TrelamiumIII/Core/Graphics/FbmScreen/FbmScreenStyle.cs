using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.FbmScreen;

/// <summary>
/// Settings for an <see cref="FbmScreenRenderer"/>, mapped onto <c>ScreenFbm.fx</c>'s uniforms of the same names.
/// Pixel lengths are at 1080p and scale with the target height.
/// </summary>
internal sealed class FbmScreenStyle
{
    /// <summary>Tiling noise the FBM sums. Null uses Perlin, which <see cref="Pivot"/> and <see cref="Expand"/> default to.</summary>
    public Asset<Texture2D>? Noise { get; init; }

    /// <summary>Median of the raw four-octave sum for <see cref="Noise"/>: Perlin 0.468, FbmNoise2 0.680, Turbulence_09 0.581.</summary>
    public float Pivot { get; init; } = 0.468f;

    /// <summary>Stretch after <see cref="Pivot"/>. Perlin's 10-90% band is 0.16 wide, so 3.2 spreads it over ~0.5.</summary>
    public float Expand { get; init; } = 3.2f;

    public float Scale { get; init; } = 1.6f;
    public float Warp { get; init; } = 0.9f;
    public float Swirl { get; init; } = 0.25f;
    public float SwirlCore { get; init; } = 0.4f;

    /// <summary>Radians per second.</summary>
    public float Spin { get; init; } = 0.02f;

    /// <summary>Noise units per second.</summary>
    public Vector2 Drift { get; init; } = new(0.01f, -0.006f);

    public float Density { get; init; } = 0.55f;
    public float Contrast { get; init; } = 0.22f;

    /// <summary>Colour steps in the ramp; 0 keeps it continuous.</summary>
    public int Bands { get; init; }

    public Color VoidColor { get; init; } = new(4, 8, 14);
    public Color LowColor { get; init; } = new(14, 30, 52);
    public Color HighColor { get; init; } = new(40, 76, 120);
    public Color GlowColor { get; init; } = new(120, 190, 255);
    public float Glow { get; init; } = 0.3f;

    /// <summary>True closes the iris from the screen edge toward the focus; false grows it out from the focus.</summary>
    public bool Inward { get; init; } = true;

    /// <summary>Iris edge displacement by the FBM, in radius normalised to the farthest corner.</summary>
    public float EdgeNoise { get; init; } = 0.35f;

    public float EdgeSoft { get; init; } = 0.02f;
    public float EdgeWidth { get; init; } = 0.05f;
    public Color EdgeColor { get; init; } = new(150, 210, 255);

    public float Vignette { get; init; } = 0.35f;

    /// <summary>Grid cell edge at 1080p; 1 or less skips the pixel pass.</summary>
    public float PixelSize { get; init; } = 4f;

    public float BloomIntensity { get; init; } = 0.45f;
    public Color BloomTint { get; init; } = new(180, 225, 255);
}
