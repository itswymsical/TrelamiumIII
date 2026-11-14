using System.Collections.Generic;
using Terraria.Utilities;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.SurfaceShaping;

<<<<<<< HEAD
/// <summary> How a region reshapes the shared height noise: its amplitude, a vertical lift, how far it warps the sampling column, and how sharp its crests are. </summary>
/// <param name="Amplitude"> Rows the noise swings, up and down from the base line. </param>
/// <param name="Lift"> Rows the whole region is raised; negative sinks it. </param>
/// <param name="Warp"> Columns the noise is sampled away from the column, along <see cref="SurfaceTerrain"/>'s warp noise. </param>
/// <param name="Ridge"> Rows of ridged noise added on top, which only ever raises the ground, into crags and peaks. </param>
/// <param name="Peak">
/// Above 0, the lift follows a straight falloff raised to this power instead of the region's eased weight, so it comes to a
/// point with slopes that steepen toward the top; 0 keeps the eased weight, which rounds the top off.
/// </param>
public readonly record struct SurfaceProfile(float Amplitude, float Lift, float Warp, float Ridge = 0f, float Peak = 0f);

/// <summary>
/// A stretch of the surface with its own <see cref="SurfaceProfile"/>: full inside <paramref name="HalfWidth"/> and eased
/// out over <paramref name="Falloff"/> columns, or over <paramref name="FalloffRight"/> on the right when that is set. A
/// falloff of a few columns stands the region's lift up as a cliff.
/// </summary>
public readonly record struct SurfaceRegion(string Name, int Center, int HalfWidth, int Falloff, SurfaceProfile Profile, int FalloffRight = -1)
{
    public float Weight(float x)
    {
        float t = FalloffProgress(x);
        return 1f - t * t * (3f - 2f * t);
    }

    /// <summary> Weight the lift is blended with: <see cref="Weight"/>, or the pointed falloff of a <see cref="SurfaceProfile.Peak"/>. </summary>
    public float LiftWeight(float x) => Profile.Peak > 0f ? MathF.Pow(1f - FalloffProgress(x), Profile.Peak) : Weight(x);

    /// <summary> 0 inside <see cref="HalfWidth"/>, rising to 1 at the outer edge of the falloff. </summary>
    private float FalloffProgress(float x)
    {
        float distance = MathF.Abs(x - Center) - HalfWidth;

        if (distance <= 0f)
            return 0f;

        int falloff = x > Center && FalloffRight >= 0 ? FalloffRight : Falloff;
        return MathF.Min(distance / Math.Max(falloff, 1), 1f);
    }

    /// <summary> First and last column the region has any weight in. </summary>
    public int Left => Center - HalfWidth - Falloff;

    public int Right => Center + HalfWidth + (FalloffRight >= 0 ? FalloffRight : Falloff);
}

/// <summary>
/// A pool dug into the ground: the ground inside <paramref name="Radius"/> columns of <paramref name="Center"/> is dug
/// below <paramref name="Level"/>, the row of the water's surface, which is the lowest of the natural ground around so the
/// rim holds. A bowl deepens toward its centre; a <paramref name="Channel"/> keeps a flat bed and steepens at its ends.
/// </summary>
public readonly record struct SurfaceLake(int Center, int Radius, int Level, int Depth, bool Channel = false)
{
    /// <summary> Rows dug under the water at <paramref name="t"/>, -1 to 1 across the span. </summary>
    public float DepthAt(float t)
    {
        float edge = Channel ? MathF.Pow(MathF.Abs(t), ChannelBankExponent) : t * t;
        return Depth * (1f - edge);
    }

    /// <summary> Higher keeps more of a channel at full depth and pulls its banks in. </summary>
    private const float ChannelBankExponent = 6f;
}

/// <summary>
/// One height noise for the whole realm surface. Regions swap the amplitude, lift, domain warp and ridging the noise is
/// read with instead of owning a noise of their own, so a region's terrain continues the base terrain at its edges. Lakes
/// and river channels are dug into whatever the profile produced.
=======
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
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
/// </summary>
public sealed class SurfaceTerrain
{
    private const float WarpFrequency = 0.011f;
<<<<<<< HEAD
    private const float RidgeFrequency = 0.008f;
    private const float RidgeSharpness = 1.4f;
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    private const float BandWobbleFrequency = 0.045f;
    private const float BandWobbleRows = 5f;
    private const int BandThickness = 3;
    private const int LakeSpanMargin = 8;

    /// <summary> Columns past a lake's edge the shore strata still reach, thinning out. </summary>
    private const int ShoreReach = 16;

    /// <summary> Rows of strata under the surface at the lake's edge and below its bed. </summary>
    private const int ShoreDepth = 18;

<<<<<<< HEAD
    /// <summary> Rows of clay lining a river channel's bed, before the wobble. </summary>
    private const float RiverbedRows = 5f;

    private const float RiverbedWobbleRows = 2f;

    /// <summary> Columns past a channel's end its clay lining still reaches, thinning out up the bank. </summary>
    private const int RiverbedBankReach = 10;

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    /// <summary> Index into the caller's strata materials per band, repeating down the column. </summary>
    private static readonly int[] BandPattern = [0, 1, 0, 2, 1, 2];

    private readonly FastNoiseLite height;
    private readonly FastNoiseLite warp;
<<<<<<< HEAD
    private readonly FastNoiseLite ridge;
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
        height = CreateNoise(seed, frequency, FastNoiseLite.NoiseType.OpenSimplex2S, FastNoiseLite.FractalType.FBm, 8);
        warp = CreateNoise(seed + 1, WarpFrequency, FastNoiseLite.NoiseType.OpenSimplex2, FastNoiseLite.FractalType.None, 1);
        bands = CreateNoise(seed + 2, BandWobbleFrequency, FastNoiseLite.NoiseType.OpenSimplex2, FastNoiseLite.FractalType.None, 1);
        ridge = CreateNoise(seed + 3, RidgeFrequency, FastNoiseLite.NoiseType.OpenSimplex2S, FastNoiseLite.FractalType.Ridged, 4);
=======
        height = CreateNoise(seed, frequency, FastNoiseLite.NoiseType.OpenSimplex2S, fractal: true);
        warp = CreateNoise(seed + 1, WarpFrequency, FastNoiseLite.NoiseType.OpenSimplex2, fractal: false);
        bands = CreateNoise(seed + 2, BandWobbleFrequency, FastNoiseLite.NoiseType.OpenSimplex2, fractal: false);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }

    public IReadOnlyList<SurfaceRegion> Regions => regions;
    public IReadOnlyList<SurfaceLake> Lakes => lakes;

<<<<<<< HEAD
    /// <summary> Row the base profile swings around; regions lift and sink from it. </summary>
    public int BaseY => surfaceY;

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
    /// <summary> Rows above <see cref="surfaceY"/> the surface can reach, for keeping peaks clear of the sky layer. </summary>
    public int HighestOffset
    {
        get
        {
            float highest = baseProfile.Amplitude + baseProfile.Ridge;

            foreach (SurfaceRegion region in regions)
                highest = MathF.Max(highest, region.Profile.Amplitude + region.Profile.Lift + region.Profile.Ridge);

            return (int)MathF.Ceiling(highest);
        }
    }

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
            if (lakes.Exists(lake => Math.Abs(lake.Center - center) < spacing + lake.Radius))
                continue;

            lakes.Add(new SurfaceLake(center, radius, LevelAround(center, radius), random.Next(depthMin, depthMax + 1)));
=======
            if (lakes.Exists(lake => Math.Abs(lake.Center - center) < spacing))
                continue;

            int level = int.MinValue;

            for (int x = center - radius - LakeSpanMargin; x <= center + radius + LakeSpanMargin; x++)
                level = Math.Max(level, NaturalHeight(x));

            lakes.Add(new SurfaceLake(center, radius, level, random.Next(depthMin, depthMax + 1)));
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
            count--;
        }
    }

<<<<<<< HEAD
    /// <summary>
    /// Runs one flat-bedded channel along most of <paramref name="region"/>'s core, leaving <paramref name="margin"/> columns
    /// of bank at each end. Call after every region is added, like <see cref="AddLakes"/>.
    /// </summary>
    public void AddRiver(SurfaceRegion region, int margin, int depthMin, int depthMax, UnifiedRandom random)
    {
        int radius = region.HalfWidth - margin;

        if (radius <= 0)
            return;

        lakes.Add(new SurfaceLake(region.Center, radius, LevelAround(region.Center, radius), random.Next(depthMin, depthMax + 1), Channel: true));
    }

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    /// <summary> First solid row of column <paramref name="x"/>, before any structure is placed. </summary>
    public int HeightAt(int x)
    {
        int natural = NaturalHeight(x);

        foreach (SurfaceLake lake in lakes)
        {
            float t = (x - lake.Center) / (float)lake.Radius;

            if (MathF.Abs(t) < 1f)
<<<<<<< HEAD
                natural = Math.Max(natural, lake.Level + 1 + (int)MathF.Round(lake.DepthAt(t)));
=======
                natural = Math.Max(natural, lake.Level + 1 + (int)MathF.Round(lake.Depth * (1f - t * t)));
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
        }

        return natural;
    }

    /// <summary>
    /// Index into the strata materials for the tile at (<paramref name="x"/>, <paramref name="y"/>) under a surface at
<<<<<<< HEAD
    /// <paramref name="top"/>, or -1 outside the shores. Bands run horizontally and wobble with the noise, so the layers
    /// show up the banks like cut strata. <paramref name="bowl"/> says the shore is a bowl lake's rather than a river
    /// channel's, for a caller that lines the small pools with one material.
    /// </summary>
    public int ShoreBand(int x, int y, int top, out bool bowl)
=======
    /// <paramref name="top"/>, or -1 outside the lake shores. Bands run horizontally and wobble with the noise, so the
    /// layers show up the banks like cut strata.
    /// </summary>
    public int ShoreBand(int x, int y, int top)
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
            bowl = !lake.Channel;
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
            int row = y + (int)(bands.GetNoise(x, y * 4f) * BandWobbleRows);
            return BandPattern[Math.Abs(row / BandThickness) % BandPattern.Length];
        }

<<<<<<< HEAD
        bowl = false;
        return -1;
    }

    /// <summary> Rows of clay lining the ground at <paramref name="x"/>: the bed of a river channel, thinning up its banks; 0 elsewhere. </summary>
    public int RiverbedDepth(int x)
    {
        float deepest = 0f;

        foreach (SurfaceLake lake in lakes)
        {
            if (!lake.Channel)
                continue;

            float outside = MathF.Abs(x - lake.Center) - lake.Radius;

            if (outside > RiverbedBankReach)
                continue;

            float reach = outside <= 0f ? 1f : 1f - outside / RiverbedBankReach;
            deepest = MathF.Max(deepest, (RiverbedRows + bands.GetNoise(x * 2f, 500f) * RiverbedWobbleRows) * reach);
        }

        return (int)MathF.Round(deepest);
    }

    /// <summary> Water level for a pool across <paramref name="radius"/> columns of <paramref name="center"/>: the lowest natural ground around it. </summary>
    private int LevelAround(int center, int radius)
    {
        int level = int.MinValue;

        for (int x = center - radius - LakeSpanMargin; x <= center + radius + LakeSpanMargin; x++)
            level = Math.Max(level, NaturalHeight(x));

        return level;
    }

    /// <summary> Height without lakes: the noise read with the blended profile, warped, ridged, and levelled toward the tree site. </summary>
    private int NaturalHeight(int x)
    {
        float lift = baseProfile.Lift;
        float amplitude = baseProfile.Amplitude;
        float warpStrength = baseProfile.Warp;
        float ridgeRows = baseProfile.Ridge;
=======
        return -1;
    }

    /// <summary> Height without lakes: the noise read with the blended profile, warped, and levelled toward the tree site. </summary>
    private int NaturalHeight(int x)
    {
        float lift = 0f;
        float amplitude = baseProfile.Amplitude;
        float warpStrength = baseProfile.Warp;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        foreach (SurfaceRegion region in regions)
        {
            float weight = region.Weight(x);

<<<<<<< HEAD
            if (weight <= 0f)
                continue;

            amplitude = MathHelper.Lerp(amplitude, region.Profile.Amplitude, weight);
            lift = MathHelper.Lerp(lift, region.Profile.Lift, region.LiftWeight(x));
            warpStrength = MathHelper.Lerp(warpStrength, region.Profile.Warp, weight);
            ridgeRows = MathHelper.Lerp(ridgeRows, region.Profile.Ridge, weight);
        }

        float sampleX = x + warp.GetNoise(x, 0f) * warpStrength;
        float crest = ridgeRows > 0f ? MathF.Pow((ridge.GetNoise(sampleX, 0f) + 1f) * 0.5f, RidgeSharpness) * ridgeRows : 0f;
        int natural = surfaceY - (int)MathF.Round(lift + crest) + (int)(height.GetNoise(sampleX, 0f) * amplitude);
=======
            amplitude = MathHelper.Lerp(amplitude, region.Profile.Amplitude, weight);
            lift = MathHelper.Lerp(lift, region.Profile.Lift, weight);
            warpStrength = MathHelper.Lerp(warpStrength, region.Profile.Warp, weight);
        }

        float sampleX = x + warp.GetNoise(x, 0f) * warpStrength;
        int natural = surfaceY - (int)MathF.Round(lift) + (int)(height.GetNoise(sampleX, 0f) * amplitude);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        float distance = MathF.Abs(x - siteX) - siteFlatRadius;

        if (distance <= 0f)
            return surfaceY;

        float blend = MathF.Min(distance / siteBlendWidth, 1f);
        return (int)MathF.Round(MathHelper.Lerp(surfaceY, natural, blend * blend * (3f - 2f * blend)));
    }

<<<<<<< HEAD
    private static FastNoiseLite CreateNoise(int seed, float frequency, FastNoiseLite.NoiseType type, FastNoiseLite.FractalType fractal, int octaves)
=======
    private static FastNoiseLite CreateNoise(int seed, float frequency, FastNoiseLite.NoiseType type, bool fractal)
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(type);
        noise.SetFrequency(frequency);
<<<<<<< HEAD
        noise.SetFractalType(fractal);

        if (fractal != FastNoiseLite.FractalType.None)
        {
            noise.SetFractalOctaves(octaves);
=======

        if (fractal)
        {
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(8);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
            noise.SetFractalGain(0.5f);
        }

        return noise;
    }
}
