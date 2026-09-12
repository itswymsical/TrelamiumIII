namespace TrelamiumIII.Core.Books.Layout;

/// <summary>Where a decoration measures its offset from.</summary>
public enum DecorationAnchor
{
    BlockTopLeft,
    BlockTopRight,
    BlockBottomLeft,
    BlockBottomRight,

    PageTopLeft,
    PageTopRight,
    PageBottomLeft,
    PageBottomRight,
    PageCenter,
}

/// <summary>
///     Something laid over the page (note, scrawl, stain): no flow height, drawn after blocks. Anchor to a block, since page numbers shift.
/// </summary>
public sealed class BookDecoration
{
    public required BookBlock Content { get; init; }

    /// <summary>Block index this follows; negative pins it to <see cref="AnchorPageIndex" /> instead.</summary>
    public int AnchorBlockIndex { get; init; } = -1;

    /// <summary>Page this sits on when it is not anchored to a block.</summary>
    public int AnchorPageIndex { get; init; }

    public DecorationAnchor Anchor { get; init; } = DecorationAnchor.BlockTopRight;

    /// <summary>Displacement from the anchor, in page-local pixels.</summary>
    public Vector2 Offset { get; init; }

    /// <summary>Tilt in radians. A few degrees is enough to read as hand-placed.</summary>
    public float Rotation { get; init; }

    /// <summary>Width the content is laid out to. Decorations do not inherit the column width.</summary>
    public float Width { get; init; } = 120f;
}

/// <summary>A decoration resolved onto a page at a definite position.</summary>
public readonly record struct PlacedDecoration(BookDecoration Decoration, Vector2 Position, float Height);
