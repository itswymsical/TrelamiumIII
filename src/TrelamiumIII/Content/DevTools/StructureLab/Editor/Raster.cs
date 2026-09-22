using System.Collections.Generic;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>Cells covered by the editor's shapes, in tile coordinates. Both corners of a shape are included.</summary>
internal static class Raster
{
    public static Rectangle Box(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

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

    public static IEnumerable<Point> Rectangle(Point a, Point b, bool filled)
    {
        Rectangle box = Box(a, b);

        for (int x = box.Left; x < box.Right; x++)
        {
            for (int y = box.Top; y < box.Bottom; y++)
            {
                if (filled || x == box.Left || x == box.Right - 1 || y == box.Top || y == box.Bottom - 1)
                    yield return new Point(x, y);
            }
        }
    }

    /// <summary>The ellipse inscribed in the box, by cell centre. Its outline is the inside cells with an outside neighbour.</summary>
    public static IEnumerable<Point> Ellipse(Point a, Point b, bool filled)
    {
        Rectangle box = Box(a, b);
        float centreX = box.X + box.Width / 2f;
        float centreY = box.Y + box.Height / 2f;
        float radiusX = box.Width / 2f;
        float radiusY = box.Height / 2f;

        bool Inside(int x, int y)
        {
            float nx = (x + 0.5f - centreX) / radiusX;
            float ny = (y + 0.5f - centreY) / radiusY;
            return nx * nx + ny * ny <= 1f;
        }

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

    /// <summary>A shape tool's cells, with outlines and lines thickened by the brush.</summary>
    public static HashSet<Point> Shape(EditorTool tool, Point anchor, Point current, bool filled, int brush)
    {
        IEnumerable<Point> outline = tool switch
        {
            EditorTool.Line => Line(anchor, current),
            EditorTool.Rectangle => Rectangle(anchor, current, filled),
            _ => Ellipse(anchor, current, filled)
        };

        bool thicken = brush > 1 && (tool == EditorTool.Line || !filled);
        HashSet<Point> cells = [];

        foreach (Point point in outline)
        {
            if (thicken)
                cells.UnionWith(Brush(point, brush));
            else
                cells.Add(point);
        }

        return cells;
    }

    /// <summary>Snaps a line to the nearest 45° and a box to a square, as Shift does.</summary>
    public static Point Constrain(EditorTool tool, Point anchor, Point current)
    {
        int dx = current.X - anchor.X;
        int dy = current.Y - anchor.Y;
        int side = Math.Max(Math.Abs(dx), Math.Abs(dy));

        if (tool != EditorTool.Line)
            return new Point(anchor.X + (dx < 0 ? -side : side), anchor.Y + (dy < 0 ? -side : side));

        if (Math.Abs(dx) > 2 * Math.Abs(dy))
            return new Point(current.X, anchor.Y);

        if (Math.Abs(dy) > 2 * Math.Abs(dx))
            return new Point(anchor.X, current.Y);

        return new Point(anchor.X + Math.Sign(dx) * side, anchor.Y + Math.Sign(dy) * side);
    }
}
