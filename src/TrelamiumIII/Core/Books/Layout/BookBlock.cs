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
///     Per-frame state handed to blocks as they draw, and the channel they report interaction back on.
/// </summary>
/// <remarks>
///     Pages are drawn into a render target, so a block cannot consult <see cref="Main.MouseScreen" />
///     to work out whether it is hovered -- the cursor has to arrive already converted into page-local
///     space. <see cref="LocalMouse" /> is null whenever the page is not the one under the cursor, or
///     while the book is animating, which is also what suppresses hover during a page turn.
/// </remarks>
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

    /// <summary>
    ///     Transform the batch currently carries, non-identity inside a tilted decoration. A block
    ///     that has to restart the batch passes this back so it does not drop the tilt.
    /// </summary>
    public Matrix Transform { get; set; } = Matrix.Identity;

    public bool IsHovering(in BlockRect rect) =>
        LocalMouse is { } mouse && rect.Contains(mouse);
}

/// <summary>
///     One authored element of a lore book: a paragraph, a figure, a rule, a panel.
/// </summary>
/// <remarks>
///     <para>
///         Blocks are created fresh each time a book is opened, so their text is resolved in the
///         language that is current at that moment and no state survives between readings. They are
///         measured against a column width, then flowed onto pages by <see cref="PageComposer" />;
///         nothing in a book declares which page it is on.
///     </para>
///     <para>
///         That is the whole point of the model. Authoring pages directly -- as
///         <c>Page1</c>/<c>Page2</c> localization keys -- bakes one language's line count into the
///         content, and any translation that runs longer silently overflows.
///     </para>
/// </remarks>
public abstract class BookBlock
{
    public BlockStyle Style { get; init; } = BlockStyle.Body;

    private float measuredForWidth = float.NaN;
    private float measuredHeight;

    /// <summary>
    ///     Height this block occupies at the given column width, excluding
    ///     <see cref="BlockStyle.SpaceBefore" /> and <see cref="BlockStyle.SpaceAfter" />. Cached,
    ///     since the composer measures a block at least twice when deciding where it lands.
    /// </summary>
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
    ///     Whether this block has nothing to show. Empty blocks are skipped entirely: no height, no
    ///     spacing, no chrome, as though they had never been declared.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is what lets one chapter template serve chapters that do not all have the same
    ///         parts. A book declares the arrangement once -- opening, aside, excerpt, closing -- and
    ///         the chapters that have no aside simply omit the localization entry, rather than every
    ///         chapter having to carry a key for every slot.
    ///     </para>
    ///     <para>
    ///         Blocks with chrome of their own have to answer this honestly, or a missing entry leaves
    ///         its frame behind: a panel around nothing still draws a border and its padding, which is
    ///         a box floating on the page with no content in it.
    ///     </para>
    ///     <para>
    ///         Deliberately silent rather than logged. A missing entry here is an authoring choice, not
    ///         a mistake, and warning on it would bury a real problem under noise on every open.
    ///     </para>
    /// </remarks>
    public virtual bool IsEmpty => false;

    /// <summary>
    ///     Whether this block can be broken across a page boundary. Prose can; a figure cannot, and is
    ///     pushed whole onto the next page instead.
    /// </summary>
    public virtual bool Splittable => false;

    /// <summary>
    ///     Divides the block so that <paramref name="head" /> fits within <paramref name="available" />
    ///     height, returning whatever is left over, or null if it all fit.
    /// </summary>
    /// <remarks>
    ///     Returning a null <paramref name="head" /> means nothing useful fit and the whole block
    ///     should move to the next page -- a paragraph will not leave a single orphan line behind.
    /// </remarks>
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
