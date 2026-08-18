using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout;

/// <summary>
///     Everything about how a block is set on the page: face, size, leading, alignment, indents and
///     the space it reserves above and below itself.
/// </summary>
/// <remarks>
///     <para>
///         Style is authored in code alongside the block, never in localization. A translator supplies
///         prose; they should not be able to change the leading, and they should not have to
///         re-specify it when a page reflows. Presets below cover the common cases, and
///         <c>with</c> adjusts one from there:
///         <c>BlockStyle.Body with { Align = TextAlign.Justify }</c>.
///     </para>
/// </remarks>
public readonly record struct BlockStyle()
{
    public BookFont Font { get; init; } = BookFont.Body;

    /// <summary>
    ///     Which set of real faces <see cref="Font" /> resolves against, or null to inherit.
    /// </summary>
    /// <remarks>
    ///     Left null by every preset below, because a preset describes a role and not a look. The
    ///     builder stamps the book's or section's typeface onto anything still null when the block is
    ///     declared, so setting this explicitly is an element-level override that outranks both.
    /// </remarks>
    public BookTypeface? Typeface { get; init; } = null;

    /// <summary>This style's role bound to its typeface. Pass this to <see cref="BookText" />, not <see cref="Font" />.</summary>
    public BookFace Face => new(Font, Typeface);

    /// <summary>
    ///     Another role bound to this style's typeface, for the places a block sets something in a
    ///     face other than its own -- a drop cap, a contents header.
    /// </summary>
    public BookFace FaceFor(BookFont role) => new(role, Typeface);

    /// <summary>Text size multiplier.</summary>
    public float Scale { get; init; } = 0.82f;

    /// <summary>Extra pixels between lines, on top of the font's own height.</summary>
    public float LineSpacing { get; init; } = 3f;

    /// <summary>Vertical space reserved before the block. Collapses against the top of a page.</summary>
    public float SpaceBefore { get; init; } = 0f;

    /// <summary>Vertical space reserved after the block.</summary>
    public float SpaceAfter { get; init; } = 8f;

    public TextAlign Align { get; init; } = TextAlign.Left;

    /// <summary>Inset from the column's left edge.</summary>
    public float IndentLeft { get; init; } = 0f;

    /// <summary>Inset from the column's right edge.</summary>
    public float IndentRight { get; init; } = 0f;

    /// <summary>Additional inset applied to the first line only. Negative gives a hanging indent.</summary>
    public float FirstLineIndent { get; init; } = 0f;

    public Color Color { get; init; } = new(62, 44, 30);

    /// <summary>
    ///     Enlarges the paragraph's opening letter across the first lines. Ignored when the block does
    ///     not start with a letter.
    /// </summary>
    public bool DropCap { get; init; } = false;

    /// <summary>Printed body text.</summary>
    public static BlockStyle Body => new();

    /// <summary>Chapter heading. Sits tight against the rule that usually follows it.</summary>
    public static BlockStyle Heading => new()
    {
        Font = BookFont.Display,
        Scale = 0.62f,
        Color = new Color(78, 48, 24),
        SpaceBefore = 0f,
        SpaceAfter = 2f,
    };

    /// <summary>Indented and lightened, for quoted or excerpted matter.</summary>
    public static BlockStyle Quote => new()
    {
        Scale = 0.76f,
        Color = new Color(92, 72, 54),
        IndentLeft = 20f,
        IndentRight = 14f,
        SpaceBefore = 4f,
        SpaceAfter = 10f,
    };

    /// <summary>Small centred text beneath a figure.</summary>
    public static BlockStyle Caption => new()
    {
        Scale = 0.68f,
        Color = new Color(104, 84, 66),
        Align = TextAlign.Center,
        LineSpacing = 1f,
        SpaceBefore = 3f,
        SpaceAfter = 10f,
    };

    /// <summary>Handwriting, for marginalia and anything a later reader added.</summary>
    public static BlockStyle Hand => new()
    {
        Font = BookFont.Hand,
        Scale = 0.5f,
        Color = new Color(46, 58, 104),
        LineSpacing = 1f,
    };
}
