namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>Geometry of the shape tool's kinds, fitted to a drag box.</summary>
internal static class EditorShapes
{
    /// <summary>Width over height of the kind at its own proportions, which Shift keeps.</summary>
    public static float Aspect(ShapeSettings settings)
    {
        float aspect = settings.Kind switch
        {
            ShapeKind.Triangle => 2f / MathF.Sqrt(3f),
            ShapeKind.Polygon => PolygonAspect(settings.Amount),
            ShapeKind.Dome or ShapeKind.Crescent => 2f,
            _ => 1f
        };

        return settings.Facing.IsSideways() ? 1f / aspect : aspect;
    }

    /// <summary>Corners of the straight-edged kinds in cell coordinates (cell centres on integers); null for the others.</summary>
    public static Vector2[]? Vertices(ShapeSettings settings, Rectangle box)
    {
        Vector2[]? unit = settings.Kind switch
        {
            ShapeKind.Triangle => [new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)],
            ShapeKind.Trapezoid => Trapezoid(settings.Amount / 100f),
            ShapeKind.Diamond => [new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f)],
            ShapeKind.Polygon => RegularPolygon(settings.Amount),
            _ => null
        };

        if (unit is null)
            return null;

        var origin = box.Location.ToVector2();
        var size = new Vector2(box.Width - 1, box.Height - 1);

        return Array.ConvertAll(unit, point => origin + Face(point, settings.Facing) * size);
    }

    /// <summary>Inside test of the curved kinds, for a point in tile units (cell edges on integers).</summary>
    public static Func<float, float, bool> Inside(ShapeSettings settings, Rectangle box)
    {
        float left = box.Left;
        float top = box.Top;
        float width = box.Width;
        float height = box.Height;

        if (settings.Kind != ShapeKind.Dome && settings.Kind != ShapeKind.Crescent)
            return Ellipse(new Vector2(left + width / 2f, top + height / 2f), new Vector2(width / 2f, height / 2f));

        // Half an ellipse, centred on the middle of the flat side and reaching across the box.
        (Vector2 centre, Vector2 radii, Vector2 depth) = settings.Facing switch
        {
            ShapeFacing.Right => (new Vector2(left, top + height / 2f), new Vector2(width, height / 2f), Vector2.UnitX),
            ShapeFacing.Down => (new Vector2(left + width / 2f, top), new Vector2(width / 2f, height), Vector2.UnitY),
            ShapeFacing.Left => (new Vector2(left + width, top + height / 2f), new Vector2(width, height / 2f), Vector2.UnitX),
            _ => (new Vector2(left + width / 2f, top + height), new Vector2(width / 2f, height), Vector2.UnitY)
        };

        Func<float, float, bool> dome = Ellipse(centre, radii);

        if (settings.Kind == ShapeKind.Dome)
            return dome;

        // A shallower dome is cut out, at least a tile short of the outer one on both axes, so the band never thins below a tile.
        Vector2 along = Vector2.One - depth;
        Vector2 shallower = Vector2.Min(radii * (Vector2.One - depth * (settings.Amount / 100f)), radii - depth);
        Vector2 cutRadii = Vector2.Max(shallower - along, new Vector2(0.01f));
        Func<float, float, bool> cut = Ellipse(centre, cutRadii);

        return (x, y) => dome(x, y) && !cut(x, y);
    }

    private static Func<float, float, bool> Ellipse(Vector2 centre, Vector2 radii) => (x, y) =>
    {
        float nx = (x - centre.X) / radii.X;
        float ny = (y - centre.Y) / radii.Y;
        return nx * nx + ny * ny <= 1f;
    };

    /// <summary>Turns a point of the unit square from facing up to <paramref name="facing" />.</summary>
    private static Vector2 Face(Vector2 point, ShapeFacing facing) => facing switch
    {
        ShapeFacing.Right => new Vector2(1f - point.Y, point.X),
        ShapeFacing.Down => new Vector2(1f - point.X, 1f - point.Y),
        ShapeFacing.Left => new Vector2(point.Y, 1f - point.X),
        _ => point
    };

    /// <param name="top">Width of the narrow side over the wide one.</param>
    private static Vector2[] Trapezoid(float top)
        => [new Vector2((1f - top) / 2f, 0f), new Vector2((1f + top) / 2f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)];

    /// <summary>A regular polygon with a corner at the top, stretched to fill the unit square.</summary>
    private static Vector2[] RegularPolygon(int sides)
    {
        Vector2[] points = UnitPolygon(sides, out Vector2 min, out Vector2 max);

        for (int i = 0; i < sides; i++)
            points[i] = (points[i] - min) / (max - min);

        return points;
    }

    private static float PolygonAspect(int sides)
    {
        UnitPolygon(sides, out Vector2 min, out Vector2 max);
        return (max.X - min.X) / (max.Y - min.Y);
    }

    private static Vector2[] UnitPolygon(int sides, out Vector2 min, out Vector2 max)
    {
        var points = new Vector2[sides];
        min = new Vector2(float.MaxValue);
        max = new Vector2(float.MinValue);

        for (int i = 0; i < sides; i++)
        {
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / sides;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        return points;
    }
}
