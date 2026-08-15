using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
///     One expanding ring of smooth tile displacement, anchored to a world position.
/// </summary>
/// <remarks>
///     <para>
///         Displaces every pixel independently, so tile edges bend and the frame reads as warped
///         glass. For terrain that breaks into rigid chunks instead, see <see cref="BlockWave" />.
///     </para>
///     <para>
///         Every distance on this type is in world pixels, and no zoom factor is ever applied to
///         any of them. That is not an oversight: the tile target is rendered at unzoomed size and
///         then composited through <see cref="Main.Transform" />, so one target texel is one world
///         pixel no matter what <see cref="Main.GameZoomTarget" /> is doing. Scaling anything here
///         would double-apply the zoom.
///     </para>
/// </remarks>
internal sealed class Shockwave(Vector2 worldPosition, float maxRadius, int lifetime) : ITileWave
{
    private static WrapperShaderData<Assets.Shaders.TileShockwave.Parameters>? shader;

    /// <summary>Peak displacement at the crest, in world pixels.</summary>
    public float Amplitude { get; init; } = 18f;

    /// <summary>
    ///     Half-width of the displaced band at full expansion, in world pixels.
    /// </summary>
    /// <remarks>
    ///     Nothing outside <see cref="Radius" /> +- this is touched, which is also what makes the
    ///     off-screen rejection in the system a safe test rather than an approximation.
    /// </remarks>
    public float Thickness { get; init; } = 56f;

    /// <summary>Where the ring is centred. Fixed for the wave's whole life.</summary>
    public Vector2 WorldPosition { get; } = worldPosition;

    /// <summary>Frames elapsed. Drives everything else.</summary>
    public int Age { get; private set; }

    public bool Finished => Age >= lifetime;

    private float Progress => Math.Clamp(Age / (float)Math.Max(lifetime, 1), 0f, 1f);

    /// <summary>
    ///     Eased 0..1 expansion. Ease-out cubic, so the ring leaves fast and settles slowly.
    /// </summary>
    private float Expansion
    {
        get
        {
            float inverse = 1f - Progress;
            return 1f - inverse * inverse * inverse;
        }
    }

    /// <summary>Current ring radius in world pixels.</summary>
    public float Radius => maxRadius * Expansion;

    /// <summary>
    ///     Current band half-width in world pixels.
    /// </summary>
    /// <remarks>
    ///     Broadens as the ring travels. A constant-width band looks like a rolling pin at large
    ///     radii, because the same thickness spread over a much longer circumference reads as a
    ///     hard-edged annulus rather than a wave losing coherence.
    /// </remarks>
    public float CurrentThickness => Thickness * (0.35f + 0.65f * Expansion);

    /// <summary>
    ///     0..1 strength handed to the shader's <c>uMix</c>.
    /// </summary>
    /// <remarks>
    ///     Squared rather than linear so the tail is short. A linear fade leaves a faint ring
    ///     visibly crawling outwards for the back half of the lifetime, which draws the eye to
    ///     the effect ending instead of to the impact.
    /// </remarks>
    public float Strength
    {
        get
        {
            float inverse = 1f - Progress;
            return inverse * inverse;
        }
    }

    public void Update()
    {
        Age++;
    }

    /// <summary>
    ///     Whether the ring's band overlaps the target at all.
    /// </summary>
    /// <remarks>
    ///     Exact rather than conservative, because the shader guarantees nothing outside
    ///     <c>Radius +- Thickness</c> is touched. A ring that has already expanded past the far
    ///     corner is as skippable as one that has not reached the near edge yet, and both are
    ///     common: rings usually outgrow the screen well before they expire.
    /// </remarks>
    public bool Intersects(RenderTarget2D target, Vector2 sceneOrigin)
    {
        var centre = WorldPosition - sceneOrigin;

        float thickness = CurrentThickness;
        float outer = Radius + thickness;
        float inner = Math.Max(Radius - thickness, 0f);

        var nearest = new Vector2(
            Math.Clamp(centre.X, 0f, target.Width),
            Math.Clamp(centre.Y, 0f, target.Height)
        );

        if (Vector2.DistanceSquared(centre, nearest) > outer * outer)
            return false;

        float farthestX = Math.Max(Math.Abs(centre.X), Math.Abs(target.Width - centre.X));
        float farthestY = Math.Max(Math.Abs(centre.Y), Math.Abs(target.Height - centre.Y));

        return farthestX * farthestX + farthestY * farthestY >= inner * inner;
    }

    public ShaderData PrepareShader(RenderTarget2D write, Vector2 sceneOrigin)
    {
        shader ??= Assets.Shaders.TileShockwave.CreateP0();

        float width = write.Width;

        var parameters = shader.Parameters;

        // Texel (0,0) of the tile target is world position sceneOrigin, NOT Main.screenPosition.
        // Centring on the latter would leave the ring sliding around by up to offScreenRange
        // whenever the camera moved between tile re-renders.
        parameters.uCenter = (WorldPosition - sceneOrigin) / new Vector2(write.Width, write.Height);

        // Aspect-corrected on width, so one unit of shader-space distance is one target texel on
        // both axes and every world-pixel measurement below converts with a single divide.
        parameters.uAspect = new Vector2(1f, write.Height / width);
        parameters.uRadius = Radius / width;
        parameters.uThickness = CurrentThickness / width;
        parameters.uAmplitude = Amplitude / width;
        parameters.uMix = Strength;

        return shader;
    }
}