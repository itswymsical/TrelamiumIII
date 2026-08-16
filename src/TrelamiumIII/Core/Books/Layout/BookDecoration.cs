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
///     Something laid over the page rather than set into it: a sticky note, a margin scrawl, a stain,
///     a pressed flower.
/// </summary>
/// <remarks>
///     <para>
///         Decorations take no part in the flow. They consume no height, they never push text down,
///         and they draw after every block on their page -- which is what lets a note sit on top of a
///         paragraph the way a real one would.
///     </para>
///     <para>
///         Anchoring to a block rather than to a page is the useful default, because page numbers are
///         computed and shift the moment the prose above changes length or the book is translated. A
///         note pinned to block 4 stays beside block 4 wherever it lands.
///     </para>
/// </remarks>
public sealed class BookDecoration
{
    public required BookBlock Content { get; init; }

    /// <summary>
    ///     Index into the book's block list that this follows. Negative pins it to
    ///     <see cref="AnchorPageIndex" /> instead.
    /// </summary>
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
