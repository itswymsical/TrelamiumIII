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
    Rectangle,
    Ellipse,
    Fill,
    Eyedropper
}

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
        EditorTool.Rectangle => Keys.U,
        EditorTool.Ellipse => Keys.O,
        EditorTool.Fill => Keys.G,
        _ => Keys.I
    };

    /// <summary>Tools that drag out a shape and write it on release.</summary>
    public static bool IsShape(this EditorTool tool) => tool is EditorTool.Line or EditorTool.Rectangle or EditorTool.Ellipse;

    public static bool UsesBrush(this EditorTool tool) => tool is EditorTool.Pencil or EditorTool.Eraser or EditorTool.Line
        or EditorTool.Rectangle or EditorTool.Ellipse;
}
