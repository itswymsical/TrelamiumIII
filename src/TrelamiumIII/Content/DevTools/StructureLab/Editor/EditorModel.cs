using Microsoft.Xna.Framework.Input;

namespace TrelamiumIII.Content.DevTools.StructureLab.Editor;

/// <summary>The part of a cell a tool writes; the others are left alone.</summary>
public enum EditorChannel
{
    Blocks,
    Walls,
    Liquid,
    Paint
}

/// <summary>Which layer the paint channel colours.</summary>
public enum PaintTarget
{
    Blocks,
    Walls,
    Both
}

public enum EditorTool
{
    Pencil,
    Eraser,
    Line,
    Curve,
    Shape,
    Fill,
    Eyedropper,
    Canvas
}

public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Triangle,
    Trapezoid,
    Diamond,
    Polygon,
    Dome,
    Crescent
}

/// <summary>Where a shape points: a triangle's apex, a trapezoid's narrow side, the bulge of a dome or crescent.</summary>
public enum ShapeFacing
{
    Up,
    Right,
    Down,
    Left
}

/// <param name="Amount">The kind's adjustable value: polygon sides, trapezoid top width or crescent thickness (percent).</param>
public readonly record struct ShapeSettings(ShapeKind Kind, ShapeFacing Facing, int Amount);

/// <summary>A shape kind's adjustable value and its range.</summary>
public readonly record struct ShapeAmount(string Label, int Min, int Max, int Step, int Default, bool Percent);

public enum SwatchKind : byte
{
    /// <summary>No block, wall, liquid or paint.</summary>
    Empty,

    /// <summary>The channel is outside the structure; see <see cref="Core.Structures.CellFlags.TileVoid" />.</summary>
    Void,

    Tile,
    Wall,
    Liquid,
    Paint
}

/// <summary>What a tool writes into its channel.</summary>
/// <param name="Type">Tile, wall or liquid type, or paint colour.</param>
/// <param name="Style">Place style, for tiles.</param>
public readonly record struct Swatch(SwatchKind Kind, int Type = 0, int Style = 0)
{
    public static readonly Swatch Empty = new(SwatchKind.Empty);

    public static readonly Swatch Void = new(SwatchKind.Void);
}

public static class EditorTools
{
    public static Keys Shortcut(this EditorTool tool) => tool switch
    {
        EditorTool.Pencil => Keys.B,
        EditorTool.Eraser => Keys.E,
        EditorTool.Line => Keys.L,
        EditorTool.Curve => Keys.P,
        EditorTool.Shape => Keys.U,
        EditorTool.Fill => Keys.G,
        EditorTool.Eyedropper => Keys.I,
        _ => Keys.C
    };

    /// <summary>Tools that drag out a shape and write it on release.</summary>
    public static bool IsShape(this EditorTool tool) => tool is EditorTool.Line or EditorTool.Shape;

    public static bool UsesBrush(this EditorTool tool) => tool is EditorTool.Pencil or EditorTool.Eraser or EditorTool.Line or EditorTool.Curve
        or EditorTool.Shape;

    public static ShapeAmount? Amount(this ShapeKind kind) => kind switch
    {
        ShapeKind.Polygon => new ShapeAmount("Sides", 3, 12, 1, 6, false),
        ShapeKind.Trapezoid => new ShapeAmount("Top", 10, 90, 10, 50, true),
        ShapeKind.Crescent => new ShapeAmount("Thickness", 10, 90, 10, 40, true),
        _ => null
    };

    /// <summary>Whether <see cref="ShapeFacing" /> changes the kind.</summary>
    public static bool HasFacing(this ShapeKind kind) => kind is ShapeKind.Triangle or ShapeKind.Trapezoid or ShapeKind.Polygon
        or ShapeKind.Dome or ShapeKind.Crescent;

    public static ShapeFacing Turned(this ShapeFacing facing, int quarters) => (ShapeFacing)(((int)facing + quarters % 4 + 4) % 4);

    public static bool IsSideways(this ShapeFacing facing) => facing is ShapeFacing.Right or ShapeFacing.Left;
}
