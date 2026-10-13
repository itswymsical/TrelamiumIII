using System.Collections.Generic;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>Cells covered by the editor's shapes, in tile coordinates. Both corners of a shape are included.</summary>
internal static class Raster
{
    private const float Epsilon = 1e-3f;

    public static Rectangle Box(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

    /// <summary>
    ///     The box dragged from <paramref name="anchor" /> to <paramref name="current" />. <paramref name="aspect" /> (width over
    ///     height) grows the short side to match it; <paramref name="fromCentre" /> centres the box on the anchor.
    /// </summary>
    public static Rectangle DragBox(Point anchor, Point current, float? aspect, bool fromCentre)
    {
        int dx = current.X - anchor.X;
        int dy = current.Y - anchor.Y;
        int width = Math.Abs(dx) * (fromCentre ? 2 : 1) + 1;
        int height = Math.Abs(dy) * (fromCentre ? 2 : 1) + 1;

        if (aspect is { } ratio)
        {
            if (width > height * ratio)
                height = Math.Max(1, (int)MathF.Round(width / ratio, MidpointRounding.AwayFromZero));
            else
                width = Math.Max(1, (int)MathF.Round(height * ratio, MidpointRounding.AwayFromZero));

            // A centred box needs odd sides to have a middle cell.
            if (fromCentre)
            {
                width |= 1;
                height |= 1;
            }
        }

        if (fromCentre)
            return new Rectangle(anchor.X - width / 2, anchor.Y - height / 2, width, height);

        return new Rectangle(dx < 0 ? anchor.X - width + 1 : anchor.X, dy < 0 ? anchor.Y - height + 1 : anchor.Y, width, height);
    }

    /// <summary>Bresenham from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static IEnumerable<Point> Line(Point from, Point to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = -Math.Abs(to.Y - from.Y);
        int stepX = from.X < to.X ? 1 : -1;
        int stepY = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;
        int x = from.X;
        int y = from.Y;

        while (true)
        {
            yield return new Point(x, y);

            if (x == to.X && y == to.Y)
                yield break;

            int doubled = 2 * error;

            if (doubled >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    public static HashSet<Point> Line(Point from, Point to, int brush) => Thicken(Line(from, to), brush);

    /// <summary>A square of side <paramref name="size"/> around <paramref name="centre"/>; even sizes lean up and left.</summary>
    public static IEnumerable<Point> Brush(Point centre, int size)
    {
        int start = -(size / 2);

        for (int dx = start; dx < start + size; dx++)
        {
            for (int dy = start; dy < start + size; dy++)
                yield return new Point(centre.X + dx, centre.Y + dy);
        }
    }

    public static HashSet<Point> Thicken(IEnumerable<Point> cells, int brush)
    {
        HashSet<Point> thick = [];

        foreach (Point point in cells)
        {
            if (brush > 1)
                thick.UnionWith(Brush(point, brush));
            else
                thick.Add(point);
        }

        return thick;
    }

    /// <summary>A shape tool's cells in <paramref name="box" />, with outlines thickened by the brush. Too small a box for the kind draws the box.</summary>
    public static HashSet<Point> Shape(ShapeSettings settings, Rectangle box, bool filled, int brush)
    {
        HashSet<Point> cells;

        if (settings.Kind == ShapeKind.Rectangle)
            cells = [.. RectangleCells(box, filled)];
        else if (EditorShapes.Vertices(settings, box) is { } vertices)
            cells = Polygon(vertices, box, filled);
        else
            cells = [.. Region(box, EditorShapes.Inside(settings, box), filled)];

        if (cells.Count == 0)
            cells = [.. RectangleCells(box, filled)];

        return filled ? cells : Thicken(cells, brush);
    }

    /// <summary>Snaps a line to the nearest 45°, as Shift does.</summary>
    public static Point Snap45(Point anchor, Point current)
    {
        int dx = current.X - anchor.X;
        int dy = current.Y - anchor.Y;
        int side = Math.Max(Math.Abs(dx), Math.Abs(dy));

        if (Math.Abs(dx) > 2 * Math.Abs(dy))
            return new Point(current.X, anchor.Y);

        if (Math.Abs(dy) > 2 * Math.Abs(dx))
            return new Point(anchor.X, current.Y);

        return new Point(anchor.X + Math.Sign(dx) * side, anchor.Y + Math.Sign(dy) * side);
    }

    /// <summary>
    ///     A connected line of cells through <paramref name="points" /> (cell centres on integers), without the corner cells a
    ///     staircase doubles up on. When <paramref name="filled" />, the points are a closed outline and its inside is included.
    /// </summary>
    public static HashSet<Point> Path(IReadOnlyList<Vector2> points, bool filled, int brush)
    {
        List<Point> line = [];

        foreach (Vector2 point in points)
        {
            var cell = new Point((int)MathF.Floor(point.X + 0.5f), (int)MathF.Floor(point.Y + 0.5f));

            if (line.Count == 0)
                line.Add(cell);
            else if (cell != line[^1])
            {
                foreach (Point step in Line(line[^1], cell))
                {
                    if (step != line[^1])
                        line.Add(step);
                }
            }
        }

        List<Point> outline = WithoutCorners(line);

        if (!filled)
            return Thicken(outline, brush);

        HashSet<Point> cells = [.. outline];
        Rectangle bounds = Bounds(outline);

        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                if (Contains(points, x, y))
                    cells.Add(new Point(x, y));
            }
        }

        return cells;
    }

    #region Shapes
    private static IEnumerable<Point> RectangleCells(Rectangle box, bool filled)
    {
        for (int x = box.Left; x < box.Right; x++)
        {
            for (int y = box.Top; y < box.Bottom; y++)
            {
                if (filled || x == box.Left || x == box.Right - 1 || y == box.Top || y == box.Bottom - 1)
                    yield return new Point(x, y);
            }
        }
    }

    /// <summary>
    ///     The cells of <paramref name="box" /> whose centres <paramref name="inside" /> accepts (tile units, cell edges on
    ///     integers). The outline is the accepted cells with a rejected or outside neighbour.
    /// </summary>
    private static IEnumerable<Point> Region(Rectangle box, Func<float, float, bool> inside, bool filled)
    {
        bool Inside(int x, int y) => box.Contains(x, y) && inside(x + 0.5f, y + 0.5f);

        for (int x = box.Left; x < box.Right; x++)
        {
            for (int y = box.Top; y < box.Bottom; y++)
            {
                if (!Inside(x, y))
                    continue;

                if (filled || !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1))
                    yield return new Point(x, y);
            }
        }
    }

    /// <summary>
    ///     A polygon through <paramref name="vertices" /> (cell centres on integers). Corners round toward the box's middle and
    ///     split in two on a middle line between cells, and edges don't depend on their direction, so mirrored shapes come out mirrored.
    /// </summary>
    private static HashSet<Point> Polygon(IReadOnlyList<Vector2> vertices, Rectangle box, bool filled)
    {
        var centre = new Vector2(box.Left + (box.Width - 1) / 2f, box.Top + (box.Height - 1) / 2f);
        List<Vector2> corners = SnapCorners(vertices, centre);
        HashSet<Point> cells = [];

        for (int i = 0; i < corners.Count; i++)
            AddEdge(cells, corners[i], corners[(i + 1) % corners.Count], centre);

        if (!filled)
            return cells;

        for (int x = box.Left; x < box.Right; x++)
        {
            for (int y = box.Top; y < box.Bottom; y++)
            {
                if (Contains(corners, x, y))
                    cells.Add(new Point(x, y));
            }
        }

        return cells;
    }

    private static List<Vector2> SnapCorners(IReadOnlyList<Vector2> vertices, Vector2 centre)
    {
        bool splitX = IsHalf(centre.X);
        bool splitY = IsHalf(centre.Y);
        List<Vector2> corners = [];

        void Add(Vector2 corner)
        {
            if (corners.Count == 0 || corners[^1] != corner)
                corners.Add(corner);
        }

        // The half nearer the previous vertex goes first, keeping the winding.
        void AddPair(Vector2 a, Vector2 b, Vector2 previous)
        {
            bool aFirst = Vector2.DistanceSquared(a, previous) <= Vector2.DistanceSquared(b, previous);
            Add(aFirst ? a : b);
            Add(aFirst ? b : a);
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2 vertex = vertices[i];
            Vector2 previous = vertices[(i + vertices.Count - 1) % vertices.Count];

            if (splitX && MathF.Abs(vertex.X - centre.X) < Epsilon)
            {
                float y = Snap(vertex.Y, centre.Y);
                AddPair(new Vector2(MathF.Floor(centre.X), y), new Vector2(MathF.Ceiling(centre.X), y), previous);
            }
            else if (splitY && MathF.Abs(vertex.Y - centre.Y) < Epsilon)
            {
                float x = Snap(vertex.X, centre.X);
                AddPair(new Vector2(x, MathF.Floor(centre.Y)), new Vector2(x, MathF.Ceiling(centre.Y)), previous);
            }
            else
                Add(new Vector2(Snap(vertex.X, centre.X), Snap(vertex.Y, centre.Y)));
        }

        if (corners.Count > 1 && corners[^1] == corners[0])
            corners.RemoveAt(corners.Count - 1);

        return corners;
    }

    /// <summary>
    ///     The cells along a straight edge between two whole cells, the same in either direction. Halves round toward
    ///     <paramref name="centre" />, and a half on the middle line itself takes both cells.
    /// </summary>
    private static void AddEdge(HashSet<Point> cells, Vector2 a, Vector2 b, Vector2 centre)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;

        if (MathF.Abs(dx) >= MathF.Abs(dy))
        {
            if (dx == 0f)
            {
                cells.Add(new Point((int)a.X, (int)a.Y));
                return;
            }

            for (int x = (int)MathF.Min(a.X, b.X); x <= (int)MathF.Max(a.X, b.X); x++)
            {
                float y = a.Y + (x - a.X) * dy / dx;

                cells.Add(new Point(x, (int)Snap(y, centre.Y)));

                if (IsHalf(y) && MathF.Abs(y - centre.Y) < Epsilon)
                    cells.Add(new Point(x, (int)MathF.Ceiling(y)));
            }

            return;
        }

        for (int y = (int)MathF.Min(a.Y, b.Y); y <= (int)MathF.Max(a.Y, b.Y); y++)
        {
            float x = a.X + (y - a.Y) * dx / dy;

            cells.Add(new Point((int)Snap(x, centre.X), y));

            if (IsHalf(x) && MathF.Abs(x - centre.X) < Epsilon)
                cells.Add(new Point((int)MathF.Ceiling(x), y));
        }
    }

    /// <summary>Rounds to the nearest whole number, halves toward <paramref name="centre" /> (down when on it).</summary>
    private static float Snap(float value, float centre)
    {
        if (!IsHalf(value))
            return MathF.Round(value);

        float floor = MathF.Floor(value + Epsilon);
        return value < centre - Epsilon ? floor + 1f : floor;
    }

    private static bool IsHalf(float value) => MathF.Abs(value - MathF.Floor(value) - 0.5f) < Epsilon;
    #endregion

    #region Helpers
    /// <summary>Even-odd test of point (<paramref name="x" />, <paramref name="y" />) against a closed outline.</summary>
    private static bool Contains(IReadOnlyList<Vector2> outline, float x, float y)
    {
        bool inside = false;

        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            Vector2 a = outline[i];
            Vector2 b = outline[j];

            if ((a.Y > y) != (b.Y > y) && x < a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y))
                inside = !inside;
        }

        return inside;
    }

    /// <summary><paramref name="line" /> without the cell at each L-shaped corner, which a 1-wide line reads as a lump.</summary>
    private static List<Point> WithoutCorners(List<Point> line)
    {
        List<Point> kept = [];

        for (int i = 0; i < line.Count; i++)
        {
            if (kept.Count > 0 && i + 1 < line.Count)
            {
                Point previous = kept[^1];
                Point current = line[i];
                Point next = line[i + 1];

                bool diagonal = Math.Abs(previous.X - next.X) == 1 && Math.Abs(previous.Y - next.Y) == 1;
                bool corner = (current.X == previous.X || current.Y == previous.Y) && (current.X == next.X || current.Y == next.Y);

                if (diagonal && corner)
                    continue;
            }

            kept.Add(line[i]);
        }

        return kept;
    }

    private static Rectangle Bounds(List<Point> cells)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        foreach (Point cell in cells)
        {
            left = Math.Min(left, cell.X);
            top = Math.Min(top, cell.Y);
            right = Math.Max(right, cell.X);
            bottom = Math.Max(bottom, cell.Y);
        }

        return cells.Count == 0 ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }
    #endregion
}
