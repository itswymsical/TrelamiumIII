namespace TrelamiumIII.Core.Books.Layout;

/// <summary>A block's box in page-local coordinates.</summary>
public readonly record struct BlockRect(float X, float Y, float Width, float Height)
{
    public Vector2 TopLeft => new(X, Y);

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public Rectangle ToRectangle() => new((int)X, (int)Y, (int)Width, (int)Height);

    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
}

/// <summary>
///     Per-frame draw state and interaction channel for blocks. Pages are render targets, so the cursor arrives in page-local space.
/// </summary>
public sealed class BookPageContext
{
    public required int PageWidth { get; init; }

    public required int PageHeight { get; init; }

    /// <summary>Cursor in page-local coordinates, or null when this page is not interactive.</summary>
    public Vector2? LocalMouse { get; set; }

    /// <summary>Tooltip a block wants shown, set during draw and consumed by the UI after compositing.</summary>
    public string? HoverText { get; set; }

    /// <summary>Book key a hovered cross-reference points at. The UI turns a click here into a jump.</summary>
    public string? HoverLink { get; set; }

    /// <summary>Page a hovered row would turn to, set by contents rows and indexes.</summary>
    public int? HoverPage { get; set; }

    /// <summary>The batch's current transform (non-identity in a tilted decoration); pass it back when restarting the batch.</summary>
    public Matrix Transform { get; set; } = Matrix.Identity;

    public bool IsHovering(in BlockRect rect) =>
        LocalMouse is { } mouse && rect.Contains(mouse);
}

/// <summary>
///     One authored lore book element, created fresh per opening and flowed by <see cref="PageComposer" /> so translations never overflow fixed pages.
/// </summary>
public abstract class BookBlock
{
    public BlockStyle Style { get; init; } = BlockStyle.Body;

    private float measuredForWidth = float.NaN;
    private float measuredHeight;

    /// <summary>Height at a column width, excluding space before and after. Cached, since the composer measures blocks repeatedly.</summary>
    public float Measure(float width)
    {
        if (measuredForWidth != width)
        {
            measuredHeight = MeasureCore(ContentWidth(width));
            measuredForWidth = width;
        }

        return measuredHeight;
    }

    protected abstract float MeasureCore(float width);

    /// <summary>Draws into page-local space. <paramref name="rect" /> already has indents applied.</summary>
    public abstract void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context);

    /// <summary>
    ///     Whether this block has nothing to show. Empty blocks are skipped entirely, so chapters can omit optional parts; blocks with chrome must answer honestly.
    /// </summary>
    public virtual bool IsEmpty => false;

    /// <summary>Whether this block must share a page with what follows, e.g. a heading moves with its content.</summary>
    public virtual bool KeepWithNext => false;

    /// <summary>Whether this block can break across pages; unsplittable blocks move whole to the next page.</summary>
    public virtual bool Splittable => false;

    /// <summary>
    ///     Splits so <paramref name="head" /> fits in <paramref name="available" />, returning the rest (null if all fit). A null head moves the whole block.
    /// </summary>
    public virtual BookBlock? Split(float width, float available, out BookBlock? head)
    {
        head = null;
        return this;
    }

    /// <summary>Column width left after the style's indents.</summary>
    protected float ContentWidth(float width) =>
        Math.Max(1f, width - Style.IndentLeft - Style.IndentRight);

    /// <summary>Applies the style's indents to a column-width rect.</summary>
    internal BlockRect Inset(in BlockRect rect) => rect with
    {
        X = rect.X + Style.IndentLeft,
        Width = ContentWidth(rect.Width),
    };
}
