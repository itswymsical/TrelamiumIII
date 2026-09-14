using Terraria.Utilities;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.Caves;

/// <summary>
/// Clears tiles in cave shapes and tracks the area it touched; walls and liquids are kept. Positions and sizes are in
/// tiles. Outlines are displaced by noise, so a shape can clear up to its warp distance past its nominal size.
/// </summary>
public sealed class CaveCarver
{
    private const int WorldMargin = 10;

    private const float OutlineWarpFrequency = 0.04f;
    private const float FloorRoughness = 2f;

    private const float TunnelStep = 2f;
    private const float TunnelRadius = 5.85f;
    private const float TunnelRadiusVariation = 0.45f;
    private const float TunnelRadiusFrequency = 0.13f;
    private const float TunnelTurnAngle = 14f * MathF.PI / 180f;
    private const float TunnelTurnFrequency = 0.097f;
    private const float TunnelTargetPull = 0.11f;
    private const float TunnelWallWarp = 0.7f;
    private const float TunnelWallWarpFrequency = 0.087f;

    private readonly FastNoiseLite outlineWarp;
    private readonly FastNoiseLite tunnelTurn;
    private readonly FastNoiseLite tunnelRadius;
    private readonly FastNoiseLite tunnelWallWarp;

    private int left = int.MaxValue;
    private int top = int.MaxValue;
    private int right = int.MinValue;
    private int bottom = int.MinValue;

    public CaveCarver(int seed)
    {
        var random = new UnifiedRandom(seed);

        outlineWarp = CreateNoise(random, FastNoiseLite.NoiseType.Value, OutlineWarpFrequency);
        tunnelTurn = CreateNoise(random, FastNoiseLite.NoiseType.OpenSimplex2S, TunnelTurnFrequency);
        tunnelRadius = CreateNoise(random, FastNoiseLite.NoiseType.Perlin, TunnelRadiusFrequency);
        tunnelRadius.SetFractalType(FastNoiseLite.FractalType.FBm);
        tunnelWallWarp = CreateNoise(random, FastNoiseLite.NoiseType.OpenSimplex2, TunnelWallWarpFrequency);
    }

    /// <summary> Bounding box of every tile cleared so far; empty before the first. </summary>
    public Rectangle CarvedArea => right < left ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);

    public void Clear(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, WorldMargin))
            return;

        Tile tile = Main.tile[x, y];
        tile.ClearTile();

        left = Math.Min(left, x);
        top = Math.Min(top, y);
        right = Math.Max(right, x);
        bottom = Math.Max(bottom, y);
    }

    /// <summary> A disc of <paramref name="radius"/> whose outline is displaced by up to <paramref name="warp"/>. </summary>
    public void CarveBlob(Vector2 center, float radius, float warp)
    {
        int reach = (int)MathF.Ceiling(radius + warp);
        int centerX = (int)center.X;
        int centerY = (int)center.Y;

        for (int x = centerX - reach; x <= centerX + reach; x++)
        {
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                if (Vector2.DistanceSquared(Warp(x, y, warp), center) < radius * radius)
                    Clear(x, y);
            }
        }
    }

    /// <summary> An ellipse warped like <see cref="CarveBlob"/>, left solid below a slightly uneven <paramref name="floorY"/>. </summary>
    public void CarveEllipse(Vector2 center, Vector2 radii, float warp, float floorY)
    {
        int reachX = (int)MathF.Ceiling(radii.X + warp);
        int reachY = (int)MathF.Ceiling(radii.Y + warp);
        int centerX = (int)center.X;
        int centerY = (int)center.Y;

        for (int x = centerX - reachX; x <= centerX + reachX; x++)
        {
            float floor = floorY + outlineWarp.GetNoise(x, -1000f) * FloorRoughness;

            for (int y = centerY - reachY; y <= centerY + reachY && y < floor; y++)
            {
                if (((Warp(x, y, warp) - center) / radii).LengthSquared() < 1f)
                    Clear(x, y);
            }
        }
    }

    /// <summary>
    /// Walks from <paramref name="from"/> toward <paramref name="to"/> in noise-bent steps, clearing a disc at each.
    /// Gives up after three times the straight-line step count; <paramref name="to"/> is cleared either way.
    /// </summary>
    public void CarveTunnel(Vector2 from, Vector2 to)
    {
        Vector2 position = from;
        float angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        int maxSteps = (int)(Vector2.Distance(from, to) / TunnelStep * 3f);

        for (int i = 0; i < maxSteps && Vector2.Distance(position, to) > TunnelStep * 3f; i++)
        {
            float toTarget = MathF.Atan2(to.Y - position.Y, to.X - position.X);
            angle += tunnelTurn.GetNoise(position.X, position.Y) * TunnelTurnAngle + MathHelper.WrapAngle(toTarget - angle) * TunnelTargetPull;
            position += angle.ToRotationVector2() * TunnelStep;

            if (!WorldGen.InWorld((int)position.X, (int)position.Y))
                break;

            CarveTunnelSection(position);
        }

        CarveTunnelSection(to);
    }

    private void CarveTunnelSection(Vector2 position)
    {
        int centerX = (int)position.X;
        int centerY = (int)position.Y;
        float radius = TunnelRadius + tunnelRadius.GetNoise(position.X, position.Y) * TunnelRadiusVariation;
        int reach = (int)(radius + TunnelWallWarp + 2f);

        for (int x = centerX - reach; x <= centerX + reach; x++)
        {
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                float dx = x + tunnelWallWarp.GetNoise(x, y) * TunnelWallWarp - centerX;
                float dy = y + tunnelWallWarp.GetNoise(x + 300f, y + 300f) * TunnelWallWarp - centerY;

                if (dx * dx + dy * dy < radius * radius)
                    Clear(x, y);
            }
        }
    }

    private Vector2 Warp(int x, int y, float amount) =>
        new(x + outlineWarp.GetNoise(x, y) * amount, y + outlineWarp.GetNoise(x + 500f, y + 500f) * amount);

    private static FastNoiseLite CreateNoise(UnifiedRandom random, FastNoiseLite.NoiseType type, float frequency)
    {
        var noise = new FastNoiseLite(random.Next());
        noise.SetNoiseType(type);
        noise.SetFrequency(frequency);
        return noise;
    }
}
