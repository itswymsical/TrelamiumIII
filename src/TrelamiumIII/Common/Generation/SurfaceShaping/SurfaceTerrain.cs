using System.Collections.Generic;
using Terraria.Utilities;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.SurfaceShaping;

/// <summary> How a region reshapes the shared height noise: its amplitude, a vertical lift, and how far it warps the sampling column. </summary>
/// <param name="Amplitude"> Rows the noise swings, up and down from the base line. </param>
/// <param name="Lift"> Rows the whole region is raised. </param>
/// <param name="Warp"> Columns the noise is sampled away from the column, along <see cref="SurfaceTerrain"/>'s warp noise. </param>
public readonly record struct SurfaceProfile(float Amplitude, float Lift, float Warp);

/// <summary> A stretch of the surface with its own <see cref="SurfaceProfile"/>: full inside <paramref name="HalfWidth"/> and eased out over <paramref name="Falloff"/> columns. </summary>
public readonly record struct SurfaceRegion(string Name, int Center, int HalfWidth, int Falloff, SurfaceProfile Profile)
{
    public float Weight(float x)
    {
        float distance = MathF.Abs(x - Center) - HalfWidth;

        if (distance <= 0f)
            return 1f;

        float t = MathF.Min(distance / Falloff, 1f);
        return 1f - t * t * (3f - 2f * t);
    }
}

/// <summary>
/// A pool bowl: the ground inside <paramref name="Radius"/> columns of <paramref name="Center"/> is dug below
/// <paramref name="Level"/>, the row of the water's surface, which is the lowest of the natural ground around so the rim holds.
/// </summary>
public readonly record struct SurfaceLake(int Center, int Radius, int Level, int Depth);

/// <summary>
/// One height noise for the whole realm surface. Regions swap the amplitude, lift and domain warp the noise is read with
/// instead of owning a noise of their own, so a region's terrain continues the base terrain at its edges. Lakes are
/// bowls dug into whatever the profile produced.
/// </summary>
public sealed class SurfaceTerrain
{
    private const float WarpFrequency = 0.011f;
    private const float BandWobbleFrequency = 0.045f;
    private const float BandWobbleRows = 5f;
    private const int BandThickness = 3;
    private const int LakeSpanMargin = 8;

    /// <summary> Columns past a lake's edge the shore strata still reach, thinning out. </summary>
    private const int ShoreReach = 16;

    /// <summary> Rows of strata under the surface at the lake's edge and below its bed. </summary>
    private const int ShoreDepth = 18;

    /// <summary> Index into the caller's strata materials per band, repeating down the column. </summary>
    private static readonly int[] BandPattern = [0, 1, 0, 2, 1, 2];

    private readonly FastNoiseLite height;
    private readonly FastNoiseLite warp;
    private readonly FastNoiseLite bands;
    private readonly List<SurfaceRegion> regions = [];
    private readonly List<SurfaceLake> lakes = [];
    private readonly SurfaceProfile baseProfile;
    private readonly int surfaceY;
    private readonly int siteX;
    private readonly int siteFlatRadius;
    private readonly int siteBlendWidth;

    public SurfaceTerrain(int seed, float frequency, int surfaceY, SurfaceProfile baseProfile, int siteX, int siteFlatRadius, int siteBlendWidth)
    {
        this.surfaceY = surfaceY;
        this.baseProfile = baseProfile;
        this.siteX = siteX;
        this.siteFlatRadius = siteFlatRadius;
        this.siteBlendWidth = siteBlendWidth;

        height = CreateNoise(seed, frequency, FastNoiseLite.NoiseType.OpenSimplex2S, fractal: true);
        warp = CreateNoise(seed + 1, WarpFrequency, FastNoiseLite.NoiseType.OpenSimplex2, fractal: false);
        bands = CreateNoise(seed + 2, BandWobbleFrequency, FastNoiseLite.NoiseType.OpenSimplex2, fractal: false);
    }

    public IReadOnlyList<SurfaceRegion> Regions => regions;
    public IReadOnlyList<SurfaceLake> Lakes => lakes;

    /// <summary> Rows below <see cref="surfaceY"/> the surface can reach, lake beds included, for sizing the world's surface layer. </summary>
    public int LowestOffset
    {
        get
        {
            float lowest = baseProfile.Amplitude;

            foreach (SurfaceRegion region in regions)
                lowest = MathF.Max(lowest, region.Profile.Amplitude - region.Profile.Lift);

            foreach (SurfaceLake lake in lakes)
                lowest = MathF.Max(lowest, lake.Level + lake.Depth + 1 - surfaceY);

            return (int)MathF.Ceiling(lowest);
        }
    }

    public void AddRegion(SurfaceRegion region) => regions.Add(region);

    /// <summary> Region weight in [0, 1] at <paramref name="x"/>, or 0 if there is no such region. </summary>
    public float WeightOf(string name, float x)
    {
        float weight = 0f;

        foreach (SurfaceRegion region in regions)
        {
            if (region.Name == name)
                weight = MathF.Max(weight, region.Weight(x));
        }

        return weight;
    }

    /// <summary>
    /// Digs <paramref name="count"/> lakes into <paramref name="region"/>, at least <paramref name="spacing"/> columns apart.
    /// Call after every region is added, since each lake's level reads the natural ground around it.
    /// </summary>
    public void AddLakes(SurfaceRegion region, int count, int radiusMin, int radiusMax, int depthMin, int depthMax, int spacing, UnifiedRandom random)
    {
        for (int attempt = 0; attempt < count * 8 && count > 0; attempt++)
        {
            int radius = random.Next(radiusMin, radiusMax + 1);
            int span = region.HalfWidth - radius;

            if (span <= 0)
                continue;

            int center = region.Center + random.Next(-span, span + 1);

            if (lakes.Exists(lake => Math.Abs(lake.Center - center) < spacing))
                continue;

            int level = int.MinValue;

            for (int x = center - radius - LakeSpanMargin; x <= center + radius + LakeSpanMargin; x++)
                level = Math.Max(level, NaturalHeight(x));

            lakes.Add(new SurfaceLake(center, radius, level, random.Next(depthMin, depthMax + 1)));
            count--;
        }
    }

    /// <summary> First solid row of column <paramref name="x"/>, before any structure is placed. </summary>
    public int HeightAt(int x)
    {
        int natural = NaturalHeight(x);

        foreach (SurfaceLake lake in lakes)
        {
            float t = (x - lake.Center) / (float)lake.Radius;

            if (MathF.Abs(t) < 1f)
                natural = Math.Max(natural, lake.Level + 1 + (int)MathF.Round(lake.Depth * (1f - t * t)));
        }

        return natural;
    }

    /// <summary>
    /// Index into the strata materials for the tile at (<paramref name="x"/>, <paramref name="y"/>) under a surface at
    /// <paramref name="top"/>, or -1 outside the lake shores. Bands run horizontally and wobble with the noise, so the
    /// layers show up the banks like cut strata.
    /// </summary>
    public int ShoreBand(int x, int y, int top)
    {
        foreach (SurfaceLake lake in lakes)
        {
            float outside = MathF.Abs(x - lake.Center) - lake.Radius;

            if (outside > ShoreReach)
                continue;

            float reach = outside <= 0f ? 1f : 1f - outside / ShoreReach;
            float depth = ShoreDepth * reach + bands.GetNoise(x * 3f, 0f) * 3f;

            if (y - top >= depth)
                continue;

            int row = y + (int)(bands.GetNoise(x, y * 4f) * BandWobbleRows);
            return BandPattern[Math.Abs(row / BandThickness) % BandPattern.Length];
        }

        return -1;
    }

    /// <summary> Height without lakes: the noise read with the blended profile, warped, and levelled toward the tree site. </summary>
    private int NaturalHeight(int x)
    {
        float lift = 0f;
        float amplitude = baseProfile.Amplitude;
        float warpStrength = baseProfile.Warp;

        foreach (SurfaceRegion region in regions)
        {
            float weight = region.Weight(x);

            amplitude = MathHelper.Lerp(amplitude, region.Profile.Amplitude, weight);
            lift = MathHelper.Lerp(lift, region.Profile.Lift, weight);
            warpStrength = MathHelper.Lerp(warpStrength, region.Profile.Warp, weight);
        }

        float sampleX = x + warp.GetNoise(x, 0f) * warpStrength;
        int natural = surfaceY - (int)MathF.Round(lift) + (int)(height.GetNoise(sampleX, 0f) * amplitude);

        float distance = MathF.Abs(x - siteX) - siteFlatRadius;

        if (distance <= 0f)
            return surfaceY;

        float blend = MathF.Min(distance / siteBlendWidth, 1f);
        return (int)MathF.Round(MathHelper.Lerp(surfaceY, natural, blend * blend * (3f - 2f * blend)));
    }

    private static FastNoiseLite CreateNoise(int seed, float frequency, FastNoiseLite.NoiseType type, bool fractal)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(type);
        noise.SetFrequency(frequency);

        if (fractal)
        {
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(8);
            noise.SetFractalGain(0.5f);
        }

        return noise;
    }
}
