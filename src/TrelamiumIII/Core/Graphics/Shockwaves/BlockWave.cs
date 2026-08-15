using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
/// The blocky vanilla-style counterpart to <see cref="Shockwave" />. That one warps the frame smoothly and
/// reads as a lens effect; this one quantises to the world tile grid and reads as terrain
/// breaking up. Both go through the same <see cref="TileShockwaveSystem" /> pass.
/// </summary>
/// <remarks>
///     <para>
///         <b>The front travels at constant speed on purpose.</b> Every other timing curve in this
///         codebase is eased, and this one deliberately is not: it moves <see cref="MaxDistance" />
///         over <see cref="Lifetime" /> frames, linearly, so anything that has to keep station with
///         the crest can be spawned with a plain velocity and left alone. An eased front would need
///         every rider to re-derive its position from the same curve every tick. All the shaping
///         lives in <see cref="Strength" /> instead, which costs nothing to ignore.
///     </para>
///     <para>
///         Distances are in world pixels, which are also target texels. See the note on
///         <see cref="Shockwave" /> for why no zoom factor appears anywhere.
///     </para>
/// </remarks>
internal sealed class BlockWave(Vector2 worldPosition, float maxDistance, int lifetime) : ITileWave
{
    private const float TileSize = 16f;

    private static WrapperShaderData<Assets.Shaders.TileBlockWave.Parameters>? shader;

    /// <summary>Peak lift, in world pixels. Positive lifts the ground upward.</summary>
    public float Amplitude { get; init; } = 40f;

    /// <summary>Half-width of the band, in world pixels.</summary>
    public float Thickness { get; init; } = 72f;

    /// <summary>
    ///     How far the heave carries above and below the impact height, in world pixels.
    /// </summary>
    /// <remarks>
    ///     Without this the whole column shears, cavern roof included, and it stops reading as
    ///     ground. Roughly ten tiles is a good starting point for a slam at surface level.
    /// </remarks>
    public float Reach { get; init; } = 176f;

    /// <summary>
    ///     Ripples within the band. 1 is a single hump; 3 adds a trough either side of the crest.
    /// </summary>
    /// <remarks>Odd values keep the displacement at zero on both rims, so the band stays seamless.</remarks>
    public float Cycles { get; init; } = 1f;

    /// <summary>
    ///     Displacement quantisation, in world pixels.
    /// </summary>
    /// <remarks>
    ///     1 is the texel grid: terrain slides by whole pixels, never a fraction, so the pixel art
    ///     is never resampled between texels and the motion still reads as continuous. Raising it
    ///     to <see cref="TileSize" /> gives a true block swap, a tile showing exactly the tile
    ///     some whole number of rows away. However, the ground jumps in 16px steps, which reads
    ///     as stair-stepping rather than movement.
    /// </remarks>
    public float Snap { get; init; } = 1f;

    /// <summary>
    ///     How far each block eases toward its neighbours, 0 to 1.
    /// </summary>
    /// <remarks>
    ///     At 0 every pixel of a tile takes the offset of that tile's centre: blocks are perfectly
    ///     square and meet at hard seams. At 1 the wave is evaluated per pixel and this collapses
    ///     into <see cref="Shockwave" />. In between, a block mostly holds its shape but eases
    ///     across its own width, which closes the seams without dissolving the grid.
    /// </remarks>
    public float Blend { get; init; } = 0.35f;

    /// <summary>Where the impact happened. Fixed for the wave's whole life.</summary>
    public Vector2 WorldPosition { get; } = worldPosition;

    /// <summary>How far the front travels before the wave expires, in world pixels.</summary>
    public float MaxDistance => maxDistance;

    /// <summary>Total frames the wave lives for.</summary>
    public int Lifetime => Math.Max(lifetime, 1);

    /// <summary>Frames elapsed.</summary>
    public int Age { get; private set; }

    public bool Finished => Age >= Lifetime;

    /// <summary>
    ///     World pixels the front advances per frame. Give a rider this as its speed and it stays
    ///     on the crest for the wave's whole life.
    /// </summary>
    public float Speed => MaxDistance / Lifetime;

    /// <summary>Current distance of the front from <see cref="WorldPosition" />, in world pixels.</summary>
    public float Radius => Speed * Age;

    /// <summary>
    ///     0..1 strength handed to the shader.
    /// </summary>
    /// <remarks>
    ///     Held near full for most of the run and dropped late, rather than decaying from the
    ///     start. A boss attack that visibly weakens the moment it leaves the ground reads as the
    ///     attack failing; this keeps the threat legible right up to the point it stops.
    /// </remarks>
    public float Strength
    {
        get
        {
            float progress = Math.Clamp(Age / (float)Lifetime, 0f, 1f);
            return 1f - progress * progress;
        }
    }

    public void Update()
    {
        Age++;
    }

    /// <summary>
    ///     The vertical displacement this wave applies at a world position, in world pixels.
    ///     Negative is upward, matching screen coordinates.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The exact value the shader computes for the tile containing
    ///         <paramref name="worldPosition" />, snapping included. The two formulas are kept in
    ///         step deliberately. Anything that has to sit on the moving surface (hostile
    ///         projectiles riding the crest, debris thrown off it, an entity that sells the heave
    ///         with real collision) can read its offset from here and land exactly where the
    ///         terrain appears to be.
    ///     </para>
    ///     <para>
    ///         This component is just a visual offset. collision will be handled later with special entities that move with the wave.
    ///     </para>
    /// </remarks>
    public float LiftAt(Vector2 worldPosition)
    {
        // Quantised to the world tile grid, exactly as the shader does via uGridPhase, then eased
        // back toward the raw position by Blend
        // the same lerp, so the two stay in step.
        float tileX = MathF.Floor(worldPosition.X / TileSize) * TileSize + TileSize * 0.5f;
        float tileY = MathF.Floor(worldPosition.Y / TileSize) * TileSize + TileSize * 0.5f;

        float blend = Math.Clamp(Blend, 0f, 1f);
        float atX = MathHelper.Lerp(tileX, worldPosition.X, blend);
        float atY = MathHelper.Lerp(tileY, worldPosition.Y, blend);

        float distance = Math.Abs(atX - WorldPosition.X);
        float band = (distance - Radius) / Math.Max(Thickness, 1e-4f);

        float travel = Hermite(Math.Clamp(1f - Math.Abs(band), 0f, 1f));
        float depth = Hermite(Math.Clamp(1f - Math.Abs(atY - WorldPosition.Y) / Math.Max(Reach, 1e-4f), 0f, 1f));

        float shape = travel * depth * MathF.Cos(band * MathHelper.PiOver2 * Cycles);

        float lift = -shape * Amplitude * Math.Clamp(Strength, 0f, 1f);

        float snap = Math.Max(Snap, 1f);

        return MathF.Floor(lift / snap + 0.5f) * snap;
    }

    public bool Intersects(RenderTarget2D target, Vector2 sceneOrigin)
    {
        var centre = WorldPosition - sceneOrigin;

        // Vertical first: a band sitting entirely above or below the target does nothing no matter
        // how far the front has travelled.
        if (centre.Y + Reach < 0f || centre.Y - Reach > target.Height)
            return false;

        float outer = Radius + Thickness;
        float inner = Math.Max(Radius - Thickness, 0f);

        // The two bands sit at centre.X +- [inner, outer]. Nearest and farthest horizontal distance
        // from the centre to the target span that pair, so one interval overlap covers both.
        float nearest = Math.Max(0f, Math.Max(-centre.X, centre.X - target.Width));
        float farthest = Math.Max(Math.Abs(centre.X), Math.Abs(target.Width - centre.X));

        return farthest >= inner && nearest <= outer;
    }

    public ShaderData PrepareShader(RenderTarget2D write, Vector2 sceneOrigin)
    {
        shader ??= Assets.Shaders.TileBlockWave.CreateP0();

        var parameters = shader.Parameters;

        parameters.uSize = new Vector2(write.Width, write.Height);
        parameters.uCentre = WorldPosition - sceneOrigin;

        // Where the world tile grid falls within the target. Target texel p is world position
        // sceneOrigin + p, so a tile boundary lands wherever p is congruent to -sceneOrigin
        // modulo 16 -- which the shader reproduces by adding this phase before flooring.
        parameters.uGridPhase = new Vector2(Wrap(sceneOrigin.X), Wrap(sceneOrigin.Y));

        parameters.uRadius = Radius;
        parameters.uThickness = Thickness;
        parameters.uAmplitude = Amplitude;
        parameters.uReach = Reach;
        parameters.uCycles = Cycles;
        parameters.uSnap = Snap;
        parameters.uBlend = Blend;
        parameters.uMix = Strength;

        return shader;
    }

    private static float Hermite(float x) => x * x * (3f - 2f * x);

    /// <summary>Positive remainder modulo the tile size. C#'s % keeps the sign of the dividend.</summary>
    private static float Wrap(float value) => (value % TileSize + TileSize) % TileSize;
}
