using System.Collections.Generic;
using Terraria.Utilities;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.Trees;

/// <summary>
/// Canopy tree trunk and roots. The trunk leans in a slow S-curve, tapers, and flares into surface roots that arch to
/// the ground, run just under it, then dive; taproots go straight down. A hollow room with a side entrance opens onto
/// a shaft down the main taproot to a chamber. Limbs are tapered segment paths blended in a distance field.
/// </summary>
public sealed class CanopyTreeGenerator
{
    private const float StepLength = 2f;
    private const int WorldMargin = 10;

    // Trunk
    private const int TrunkHeightMin = 180;
    private const int TrunkHeightMax = 240;
    private const float TrunkBaseRadiusMin = 8f;
    private const float TrunkBaseRadiusMax = 10f;
    private const float TrunkTopRadius = 3.5f;
    private const float TrunkBuriedDepth = 6f;
    private const float FlareStrength = 0.8f;
    private const float FlareHeight = 22f;
    private const float LeanAngleMin = 0.22f;
    private const float LeanAngleMax = 0.38f;
    private const float LeanWavesMin = 1.1f;
    private const float LeanWavesMax = 1.5f;
    private const float WobbleAngle = 0.1f;
    private const float WobbleFrequency = 0.02f;
    private const float BulgeStrength = 0.1f;
    private const float BulgeFrequency = 0.06f;

    // Roots
    private const int SurfaceRootCountMin = 6;
    private const int SurfaceRootCountMax = 10;
    private const float SurfaceRootLengthMin = 40f;
    private const float SurfaceRootLengthMax = 90f;
    private const float SurfaceRootRadiusMin = 0.35f;
    private const float SurfaceRootRadiusMax = 0.6f;
    private const float CrawlDepth = 5f;
    private const float CrawlUntil = 0.45f;
    private const float TaprootLengthMin = 90f;
    private const float TaprootLengthMax = 140f;
    private const float TaprootRadiusScale = 0.5f;
    private const float RootTipRadius = 0.8f;
    private const float RootWiggleAngle = 0.12f;
    private const float RootWiggleFrequency = 0.05f;
    private const float ChildRootChance = 0.06f;
    private const float ChildRootScale = 0.65f;
    private const int MaxRootLevel = 2;

    // Surface
    private const float TrunkRootBlend = 5f;
    private const float ChildRootBlend = 2.5f;
    private const float BarkRoughness = 0.9f;
    private const float BarkFrequency = 0.14f;

    // Hollow
    private const float WallThickness = 4f;
    private const float HollowRadiusMin = 2.5f;
    private const float HollowRadiusMax = 7f;
    private const float HollowHeightFraction = 0.6f;
    private const float EntranceRadius = 2.5f;
    private const float ShaftRadius = 2.5f;
    private const float ShaftLengthFraction = 0.85f;
    private const float ChamberRadius = 7f;

    private readonly Point ground;
    private readonly UnifiedRandom random;
    private readonly FastNoiseLite wobble;
    private readonly FastNoiseLite bulge;
    private readonly FastNoiseLite rootWiggle;

    private readonly List<(LimbPath Path, float Blend)> wood = [];
    private readonly List<LimbPath> hollow = [];

    private CanopyTreeGenerator(Point ground, int seed)
    {
        this.ground = ground;
        random = new UnifiedRandom(seed);
        wobble = CreateNoise(WobbleFrequency);
        bulge = CreateNoise(BulgeFrequency);
        rootWiggle = CreateNoise(RootWiggleFrequency);
    }

    /// <summary> Grows a tree whose trunk stands on the surface tile <paramref name="ground"/>; returns the tile area written. </summary>
    public static Rectangle Grow(Point ground, int seed) => new CanopyTreeGenerator(ground, seed).Build();

    private Rectangle Build()
    {
        float baseRadius = random.NextFloat(TrunkBaseRadiusMin, TrunkBaseRadiusMax);
        int height = random.Next(TrunkHeightMin, TrunkHeightMax + 1);

        LimbPath trunk = GrowTrunk(baseRadius, height);
        wood.Add((trunk, 0f));

        int rootCount = random.Next(SurfaceRootCountMin, SurfaceRootCountMax + 1);
        for (int i = 0; i < rootCount; i++)
            GrowSurfaceRoot(baseRadius, i, rootCount);

        LimbPath mainTaproot = GrowTaproots(baseRadius);
        AddHollow(trunk, mainTaproot, baseRadius, height);

        return Rasterize();
    }

    private LimbPath GrowTrunk(float baseRadius, int height)
    {
        float lean = random.NextFloat(LeanAngleMin, LeanAngleMax) * (random.NextBool(2) ? 1f : -1f);
        float leanWaves = random.NextFloat(LeanWavesMin, LeanWavesMax);
        int steps = (int)((height + TrunkBuriedDepth) / StepLength);

        var trunk = new LimbPath();
        var position = new Vector2(ground.X + 0.5f, ground.Y + TrunkBuriedDepth);

        for (int i = 0; i <= steps; i++)
        {
            float progress = i / (float)steps;
            float distance = i * StepLength;

            if (i > 0)
            {
                // Angle from vertical: a partial sine wave gives the lean and its correction back.
                float angle = lean * MathF.Sin(progress * MathHelper.Pi * leanWaves) + wobble.GetNoise(distance, 0f) * WobbleAngle;
                position += new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * StepLength;
            }

            float heightAboveGround = MathF.Max(ground.Y - position.Y, 0f);
            float flare = 1f + FlareStrength * MathF.Exp(-heightAboveGround / FlareHeight);
            float taper = MathHelper.Lerp(baseRadius, TrunkTopRadius, MathF.Pow(progress, 0.75f));

            trunk.Add(position, taper * flare * (1f + bulge.GetNoise(distance, 0f) * BulgeStrength));
        }

        return trunk;
    }

    /// <summary> Roots alternate sides; later ones start lower and steeper so the flare fans out around the base. </summary>
    private void GrowSurfaceRoot(float baseRadius, int index, int count)
    {
        int side = index % 2 == 0 ? 1 : -1;
        float order = index / (float)Math.Max(count - 1, 1);
        float tilt = MathHelper.Lerp(0.1f, 0.8f, order) + random.NextFloat(-0.1f, 0.1f);

        var start = new Vector2(
            ground.X + 0.5f + side * baseRadius * random.NextFloat(0.1f, 0.6f),
            ground.Y - MathHelper.Lerp(12f, 2f, order) + random.NextFloat(-2f, 2f));

        GrowRoot(start, side > 0 ? tilt : MathHelper.Pi - tilt,
            random.NextFloat(SurfaceRootLengthMin, SurfaceRootLengthMax),
            baseRadius * random.NextFloat(SurfaceRootRadiusMin, SurfaceRootRadiusMax),
            level: 0, taproot: false, TrunkRootBlend);
    }

    /// <summary> The main taproot carries the shaft, so it never branches. </summary>
    private LimbPath GrowTaproots(float baseRadius)
    {
        var start = new Vector2(ground.X + 0.5f, ground.Y + TrunkBuriedDepth);
        float length = random.NextFloat(TaprootLengthMin, TaprootLengthMax);

        LimbPath main = GrowRoot(start, MathHelper.PiOver2 + random.NextFloat(-0.15f, 0.15f), length,
            baseRadius * TaprootRadiusScale, level: MaxRootLevel, taproot: true, TrunkRootBlend);

        int side = random.NextBool(2) ? 1 : -1;
        GrowRoot(start + new Vector2(side * baseRadius * 0.5f, 0f), MathHelper.PiOver2 + side * random.NextFloat(0.25f, 0.5f),
            length * random.NextFloat(0.5f, 0.75f), baseRadius * TaprootRadiusScale * 0.8f,
            level: 1, taproot: true, TrunkRootBlend);

        return main;
    }

    /// <summary> Angles are screen-space radians: 0 points right, pi/2 points down. </summary>
    private LimbPath GrowRoot(Vector2 start, float angle, float length, float startRadius, int level, bool taproot, float blend)
    {
        int side = MathF.Cos(angle) >= 0f ? 1 : -1;
        int steps = Math.Max((int)(length / StepLength), 2);

        var root = new LimbPath();
        root.Add(start, startRadius);
        Vector2 position = start;

        for (int i = 1; i <= steps; i++)
        {
            float progress = i / (float)steps;
            float radius = MathHelper.Lerp(startRadius, RootTipRadius, MathF.Pow(progress, 0.8f));
            float heading = taproot ? MathHelper.PiOver2 : SurfaceRootHeading(position.Y, progress, side);

            angle += MathHelper.WrapAngle(heading - angle) * (taproot ? 0.08f : 0.12f);
            angle += rootWiggle.GetNoise(position.X, position.Y) * RootWiggleAngle;
            position += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * StepLength;
            root.Add(position, radius);

            if (level < MaxRootLevel && progress > 0.2f && progress < 0.8f && random.NextFloat() < ChildRootChance)
            {
                float childAngle = angle + (random.NextBool(2) ? 1f : -1f) * random.NextFloat(0.35f, 0.8f);
                GrowRoot(position, childAngle, (steps - i) * StepLength * random.NextFloat(0.5f, 0.85f),
                    radius * ChildRootScale, level + 1, taproot: false, ChildRootBlend);
            }
        }

        wood.Add((root, blend));
        return root;
    }

    /// <summary> Steeply down while above ground, nearly level while shallow early in the root, then diving. </summary>
    private float SurfaceRootHeading(float y, float progress, int side)
    {
        float depth = y - ground.Y;
        float belowHorizontal = depth < 0f ? 1.2f : depth < CrawlDepth && progress < CrawlUntil ? 0.15f : 0.9f;

        return side > 0 ? belowHorizontal : MathHelper.Pi - belowHorizontal;
    }

    private void AddHollow(LimbPath trunk, LimbPath mainTaproot, float baseRadius, int height)
    {
        var interior = new LimbPath();

        for (int i = 0; i < trunk.Count; i++)
        {
            float radius = MathF.Min(trunk.Radii[i] - WallThickness, HollowRadiusMax);
            float heightAboveGround = ground.Y - trunk.Points[i].Y;

            // Starting one radius up keeps the room's floor on the row above the surface.
            if (heightAboveGround < radius + 0.5f)
                continue;

            if (heightAboveGround > height * HollowHeightFraction || radius < HollowRadiusMin)
                break;

            interior.Add(trunk.Points[i], radius);
        }

        float entranceY = ground.Y - 1f - EntranceRadius;
        float entranceLength = baseRadius * (1f + FlareStrength) + 6f;
        int entranceSide = random.NextBool(2) ? 1 : -1;

        var entrance = new LimbPath();
        entrance.Add(new Vector2(ground.X + 0.5f, entranceY), EntranceRadius);
        entrance.Add(new Vector2(ground.X + 0.5f + entranceSide * entranceLength, entranceY), EntranceRadius);

        var shaft = new LimbPath();
        shaft.Add(new Vector2(ground.X + 0.5f, entranceY), ShaftRadius);

        int shaftPoints = Math.Max((int)(mainTaproot.Count * ShaftLengthFraction), 1);
        for (int i = 0; i < shaftPoints; i++)
            shaft.Add(mainTaproot.Points[i], ShaftRadius);

        var chamber = new LimbPath();
        chamber.Add(shaft.Points[^1], ChamberRadius);
        chamber.Add(shaft.Points[^1], ChamberRadius);

        hollow.AddRange([interior, entrance, shaft, chamber]);
    }

    private Rectangle Rasterize()
    {
        var woodSegments = new List<(List<TaperedSegment> Segments, float Blend)>();
        foreach ((LimbPath path, float blend) in wood)
            woodSegments.Add((path.ToSegments(), blend));

        var hollowSegments = new List<List<TaperedSegment>>();
        foreach (LimbPath path in hollow)
            hollowSegments.Add(path.ToSegments());

        Rectangle? union = null;
        float padding = TrunkRootBlend + BarkRoughness + 2f;

        void Include(List<TaperedSegment> segments)
        {
            foreach (TaperedSegment segment in segments)
            {
                Rectangle bounds = segment.Bounds(padding);
                union = union is { } current ? Rectangle.Union(current, bounds) : bounds;
            }
        }

        foreach ((List<TaperedSegment> segments, _) in woodSegments)
            Include(segments);

        foreach (List<TaperedSegment> segments in hollowSegments)
            Include(segments);

        var world = new Rectangle(WorldMargin, WorldMargin, Main.maxTilesX - WorldMargin * 2, Main.maxTilesY - WorldMargin * 2);
        Rectangle area = Rectangle.Intersect(union ?? Rectangle.Empty, world);

        if (area.IsEmpty)
            return area;

        var woodField = new DistanceField(area);
        foreach ((List<TaperedSegment> segments, float blend) in woodSegments)
            woodField.AddPath(segments, blend);

        var hollowField = new DistanceField(area);
        foreach (List<TaperedSegment> segments in hollowSegments)
            hollowField.AddPath(segments, 0f);

        FastNoiseLite bark = CreateNoise(BarkFrequency);
        ushort woodType = (ushort)ModContent.TileType<BramblerootTile>();

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
            {
                float woodDistance = woodField[x, y] + bark.GetNoise(x, y) * BarkRoughness;
                Tile tile = Main.tile[x, y];

                if (hollowField[x, y] < 0f)
                {
                    tile.ClearTile();
                    tile.LiquidAmount = 0;

                    // Walls only inside the silhouette or underground, not where the entrance pokes into open air.
                    if (woodDistance < 0f || y > ground.Y)
                        tile.WallType = WallID.LivingWoodUnsafe;
                }
                else if (woodDistance < 0f)
                {
                    tile.HasTile = true;
                    tile.TileType = woodType;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                }
            }
        }

        return area;
    }

    private FastNoiseLite CreateNoise(float frequency)
    {
        var noise = new FastNoiseLite(random.Next());
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }

    /// <summary> Centre points and radii of a limb, in tiles; consecutive pairs become tapered segments. </summary>
    private sealed class LimbPath
    {
        public readonly List<Vector2> Points = [];
        public readonly List<float> Radii = [];

        public int Count => Points.Count;

        public void Add(Vector2 point, float radius)
        {
            Points.Add(point);
            Radii.Add(radius);
        }

        public List<TaperedSegment> ToSegments()
        {
            var segments = new List<TaperedSegment>(Math.Max(Count - 1, 0));

            for (int i = 1; i < Count; i++)
                segments.Add(new TaperedSegment(Points[i - 1], Points[i], Radii[i - 1], Radii[i]));

            return segments;
        }
    }
}
