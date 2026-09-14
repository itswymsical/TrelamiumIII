using System.Collections.Generic;

namespace TrelamiumIII.Common.Generation.Utils;

/// <summary> A capsule whose radius changes linearly from <see cref="Start"/> to <see cref="End"/>, in tile units. </summary>
public readonly record struct TaperedSegment(Vector2 Start, Vector2 End, float StartRadius, float EndRadius)
{
    /// <summary> Signed distance from <paramref name="point"/> to the surface, negative inside; approximate where the taper is steep. </summary>
    public float Distance(Vector2 point)
    {
        Vector2 axis = End - Start;
        float lengthSquared = axis.LengthSquared();
        float t = lengthSquared > 0f ? MathHelper.Clamp(Vector2.Dot(point - Start, axis) / lengthSquared, 0f, 1f) : 0f;

        return Vector2.Distance(point, Start + axis * t) - MathHelper.Lerp(StartRadius, EndRadius, t);
    }

    public Rectangle Bounds(float padding)
    {
        int left = (int)MathF.Floor(MathF.Min(Start.X - StartRadius, End.X - EndRadius) - padding);
        int top = (int)MathF.Floor(MathF.Min(Start.Y - StartRadius, End.Y - EndRadius) - padding);
        int right = (int)MathF.Ceiling(MathF.Max(Start.X + StartRadius, End.X + EndRadius) + padding);
        int bottom = (int)MathF.Ceiling(MathF.Max(Start.Y + StartRadius, End.Y + EndRadius) + padding);

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }
}

/// <summary>
/// Signed distances sampled at tile centres over <see cref="Area"/>. Each path is a hard union of its segments and
/// joins the field through a smooth minimum, so separate limbs blend into each other.
/// </summary>
public sealed class DistanceField
{
    private const float Far = 1e4f;

    public readonly Rectangle Area;
    private readonly float[] values;

    public DistanceField(Rectangle area)
    {
        Area = area;
        values = new float[area.Width * area.Height];
        Array.Fill(values, Far);
    }

    public float this[int x, int y] => values[(y - Area.Y) * Area.Width + (x - Area.X)];

    /// <param name="smoothing"> Blend width in tiles where the path meets what's already in the field; 0 is a hard union. </param>
    public void AddPath(IReadOnlyList<TaperedSegment> path, float smoothing)
    {
        if (path.Count == 0)
            return;

        float padding = smoothing + 1f;
        Rectangle bounds = path[0].Bounds(padding);

        for (int i = 1; i < path.Count; i++)
            bounds = Rectangle.Union(bounds, path[i].Bounds(padding));

        bounds = Rectangle.Intersect(bounds, Area);
        if (bounds.IsEmpty)
            return;

        var pathValues = new float[bounds.Width * bounds.Height];
        Array.Fill(pathValues, Far);

        foreach (TaperedSegment segment in path)
        {
            Rectangle segmentBounds = Rectangle.Intersect(segment.Bounds(padding), bounds);

            for (int y = segmentBounds.Top; y < segmentBounds.Bottom; y++)
            {
                for (int x = segmentBounds.Left; x < segmentBounds.Right; x++)
                {
                    int index = (y - bounds.Y) * bounds.Width + (x - bounds.X);
                    pathValues[index] = MathF.Min(pathValues[index], segment.Distance(new Vector2(x + 0.5f, y + 0.5f)));
                }
            }
        }

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                int index = (y - Area.Y) * Area.Width + (x - Area.X);
                values[index] = SmoothMin(values[index], pathValues[(y - bounds.Y) * bounds.Width + (x - bounds.X)], smoothing);
            }
        }
    }

    /// <summary> Polynomial smooth minimum with blend width <paramref name="k"/>; a plain minimum when k is 0. </summary>
    private static float SmoothMin(float a, float b, float k)
    {
        if (k <= 0f)
            return MathF.Min(a, b);

        float h = MathHelper.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
        return MathHelper.Lerp(b, a, h) - k * h * (1f - h);
    }
}
