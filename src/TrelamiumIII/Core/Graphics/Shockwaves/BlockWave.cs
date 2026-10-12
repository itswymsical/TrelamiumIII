using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>An expanding heave of the solid tile layer, quantised to the world tile grid.</summary>
/// <remarks>
///     The front moves at constant <see cref="Speed" />; fading is carried by <see cref="Strength" /> only.
///     Distances are world pixels, which equal tile-target texels at any zoom.
/// </remarks>
internal sealed class BlockWave(Vector2 worldPosition, float maxDistance, int lifetime) : ITileWave
{
    private const float TileSize = 16f;

    private static WrapperShaderData<Assets.Shaders.TileBlockWave.Parameters>? shader;

    /// <summary>Peak lift, in world pixels. Positive lifts the ground upward.</summary>
    public float Amplitude { get; init; } = 40f;

    /// <summary>Half-width of the band, in world pixels.</summary>
    public float Thickness { get; init; } = 72f;

    /// <summary>Vertical falloff distance above and below the impact height, in world pixels.</summary>
    public float Reach { get; init; } = 176f;

    /// <summary>Ripples within the band: 1 is a single hump, 3 adds a trough each side. Odd values keep both rims at zero.</summary>
    public float Cycles { get; init; } = 1f;

    /// <summary>Displacement quantisation in world pixels. 1 snaps to texels; 16 swaps whole tiles.</summary>
    public float Snap { get; init; } = 1f;

    /// <summary>0 offsets each tile by its centre's lift (hard seams); 1 evaluates per pixel (smooth warp).</summary>
    public float Blend { get; init; } = 0.35f;

    /// <summary>Impact position.</summary>
    public Vector2 WorldPosition { get; } = worldPosition;

    /// <summary>How far the front travels before the wave expires, in world pixels.</summary>
    public float MaxDistance => maxDistance;

    public int Lifetime => Math.Max(lifetime, 1);

    public int Age { get; private set; }

    public bool Finished => Age >= Lifetime;

    /// <summary>World pixels the front advances per frame; a rider moving at this speed stays on the crest.</summary>
    public float Speed => MaxDistance / Lifetime;

    /// <summary>Distance of the front from <see cref="WorldPosition" />, in world pixels.</summary>
    public float Radius => Speed * Age;

    /// <summary>0..1 shader strength, 1 - progress².</summary>
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
    ///     Vertical displacement at <paramref name="worldPosition" /> in world pixels, negative upward. Visual only.
    /// </summary>
    /// <remarks>Must match TileBlockWave.fx, snapping and <see cref="Blend" /> included.</remarks>
    public float LiftAt(Vector2 worldPosition)
    {
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

        if (centre.Y + Reach < 0f || centre.Y - Reach > target.Height)
            return false;

        float outer = Radius + Thickness;
        float inner = Math.Max(Radius - Thickness, 0f);

        // Both bands sit at centre.X +- [inner, outer]; one interval test against the target's horizontal span covers them.
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

        // Texel p is world sceneOrigin + p; the shader adds this phase before flooring to the tile grid.
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

    /// <summary>Non-negative remainder modulo <see cref="TileSize" />.</summary>
    private static float Wrap(float value) => (value % TileSize + TileSize) % TileSize;
}
