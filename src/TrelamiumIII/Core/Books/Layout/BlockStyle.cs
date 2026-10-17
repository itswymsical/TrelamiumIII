using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout;

/// <summary>
///     How a block is set: face, size, leading, alignment, indents, spacing. Authored in code, never localization; adjust presets with <c>with</c>.
/// </summary>
public readonly record struct BlockStyle()
{
    public BookFont Font { get; init; } = BookFont.Body;

    /// <summary>
    ///     Typeface <see cref="Font" /> resolves against, or null to inherit the book's or section's; setting it is an element override.
    /// </summary>
    public BookTypeface? Typeface { get; init; } = null;

    /// <summary>This style's role bound to its typeface. Pass this to <see cref="BookText" />, not <see cref="Font" />.</summary>
    public BookFace Face => new(Font, Typeface);

    /// <summary>Another role in this style's typeface, e.g. for a drop cap or contents header.</summary>
    public BookFace FaceFor(BookFont role) => new(role, Typeface);

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

    /// <summary>Explicit ink. Null takes the book palette's ink for <see cref="InkRole" />, stamped in when the block is declared.</summary>
    public Color? Color { get; init; } = null;

    public InkRole InkRole { get; init; } = InkRole.Body;

    /// <summary>The ink to draw with; a style no book has stamped falls back to the default palette.</summary>
    public Color DrawColor => Color ?? BookPalette.Default().InkFor(InkRole);

    /// <summary>Enlarges the opening letter across the first lines; ignored if the block doesn't start with a letter.</summary>
    public bool DropCap { get; init; } = false;

    /// <summary>Printed body text.</summary>
    public static BlockStyle Body => new();

    /// <summary>Chapter heading. Sits tight against the rule that usually follows it.</summary>
    public static BlockStyle Heading => new()
    {
        Font = BookFont.Display,
        Scale = 0.62f,
        InkRole = InkRole.Heading,
        SpaceBefore = 0f,
        SpaceAfter = 2f,
    };

    /// <summary>Indented and lightened, for quoted or excerpted matter.</summary>
    public static BlockStyle Quote => new()
    {
        Scale = 0.76f,
        InkRole = InkRole.Quote,
        IndentLeft = 20f,
        IndentRight = 14f,
        SpaceBefore = 4f,
        SpaceAfter = 10f,
    };

    /// <summary>Small centred text beneath a figure.</summary>
    public static BlockStyle Caption => new()
    {
        Scale = 0.68f,
        InkRole = InkRole.Caption,
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
        InkRole = InkRole.Hand,
        LineSpacing = 1f,
    };
}
