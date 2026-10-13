using System.Collections.Generic;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>A node of a <see cref="CurvePath" /> in cell coordinates (cell centres on integers). A corner's handles sit on it.</summary>
internal struct CurveNode(Vector2 position)
{
    public Vector2 Position = position;
    public Vector2 In = position;
    public Vector2 Out = position;

    public readonly Point Cell => new((int)Position.X, (int)Position.Y);
}

/// <summary>The curve tool's path: cubic Bézier segments between nodes.</summary>
/// <param name="secondary">Whether the path writes the right button's swatch.</param>
internal sealed class CurvePath(bool secondary)
{
    /// <summary>Flattening samples per tile of a segment's control polygon.</summary>
    private const float SamplesPerTile = 3f;

    public List<CurveNode> Nodes { get; } = [];

    public bool Secondary { get; } = secondary;

    /// <summary>The path's cells, with <paramref name="next" /> as one more corner node when given.</summary>
    public HashSet<Point> Rasterize(bool closed, bool filled, int brush, Point? next = null)
    {
        List<CurveNode> nodes = [.. Nodes];

        if (next is { } extra)
            nodes.Add(new CurveNode(extra.ToVector2()));

        return Raster.Path(Flatten(nodes, closed), filled && closed, brush);
    }

    /// <summary>Points along the path, ending back at the first node when closed.</summary>
    private static List<Vector2> Flatten(List<CurveNode> nodes, bool closed)
    {
        List<Vector2> points = [];

        if (nodes.Count == 0)
            return points;

        points.Add(nodes[0].Position);
        int segments = closed ? nodes.Count : nodes.Count - 1;

        for (int i = 0; i < segments; i++)
        {
            CurveNode from = nodes[i];
            CurveNode to = nodes[(i + 1) % nodes.Count];

            if (from.Out == from.Position && to.In == to.Position)
            {
                points.Add(to.Position);
                continue;
            }

            float length = Vector2.Distance(from.Position, from.Out) + Vector2.Distance(from.Out, to.In) + Vector2.Distance(to.In, to.Position);
            int samples = Math.Max(2, (int)MathF.Ceiling(length * SamplesPerTile));

            for (int s = 1; s <= samples; s++)
                points.Add(Bezier(from.Position, from.Out, to.In, to.Position, s / (float)samples));
        }

        return points;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }
}
