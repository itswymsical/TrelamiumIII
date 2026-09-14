using TrelamiumIII.Utilities;

namespace TrelamiumIII.Common.Generation.Caves;

/// <summary>
/// A long, low cave running <paramref name="length"/> tiles from <paramref name="start"/> toward
/// <paramref name="direction"/> (-1 left, 1 right). The ceiling arches up to <paramref name="height"/> tiles above the
/// centre line and closes at both ends; the floor stays level except where it rises into the ends.
/// </summary>
public sealed class GrottoNode(Vector2 start, int direction, float length, float height, int seed) : CaveNode
{
    /// <summary> Tunnels attach this fraction of the length in from each end, where the cave is already open. </summary>
    private const float TunnelInset = 0.06f;

    private const float ArchExponent = 0.6f;
    private const float FloorDepthRatio = 0.45f;
    private const float FloorRise = 3f;
    private const float Drift = 6f;
    private const float DriftFrequency = 0.012f;
    private const float CeilingRoughness = 4f;
    private const float FloorRoughness = 1.5f;
    private const float EdgeRoughness = 1.5f;
    private const float RoughnessFrequency = 0.08f;

    private float Left => MathF.Min(start.X, start.X + direction * length);

    public override Vector2 Center => AlongLength(0.5f);

    public override Vector2 Entry => AlongLength(TunnelInset);

    public override Vector2 Exit => AlongLength(1f - TunnelInset);

    public override Rectangle Bounds
    {
        get
        {
            int left = (int)MathF.Floor(Left);
            int top = (int)MathF.Floor(start.Y - height - Drift - CeilingRoughness - EdgeRoughness);
            float bottom = start.Y + height * FloorDepthRatio + Drift + FloorRoughness + EdgeRoughness;

            return new Rectangle(left, top, (int)MathF.Ceiling(length) + 2, (int)MathF.Ceiling(bottom) - top + 1);
        }
    }

    public override void Carve(CaveCarver carver)
    {
        FastNoiseLite drift = CreateNoise(seed, DriftFrequency);
        FastNoiseLite roughness = CreateNoise(seed + 1, RoughnessFrequency);
        float left = Left;

        for (int x = (int)MathF.Floor(left); x <= (int)MathF.Ceiling(left + length); x++)
        {
            float t = Math.Clamp((x - left) / length, 0f, 1f);
            float arch = MathF.Pow(MathF.Max(MathF.Sin(MathHelper.Pi * t), 0f), ArchExponent);
            float centerY = start.Y + drift.GetNoise(x, 0f) * Drift * arch;
            float ceiling = centerY - height * arch + roughness.GetNoise(x, 0f) * CeilingRoughness * arch;
            float floor = centerY + height * FloorDepthRatio * MathF.Min(arch * FloorRise, 1f) + roughness.GetNoise(x, 1000f) * FloorRoughness;

            for (int y = (int)MathF.Floor(ceiling - EdgeRoughness); y <= (int)MathF.Ceiling(floor + EdgeRoughness); y++)
            {
                float edge = roughness.GetNoise(x, y) * EdgeRoughness;

                if (y > ceiling + edge && y < floor + edge)
                    carver.Clear(x, y);
            }
        }
    }

    /// <summary> The centre-line point <paramref name="fraction"/> of the way from the start to the far end. </summary>
    private Vector2 AlongLength(float fraction) => start + new Vector2(direction * length * fraction, 0f);

    private static FastNoiseLite CreateNoise(int seed, float frequency)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        return noise;
    }
}
