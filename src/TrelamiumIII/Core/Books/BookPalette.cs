namespace TrelamiumIII.Core.Books;

/// <summary>Which of the palette's inks a block style draws in when it names no colour of its own.</summary>
public enum InkRole
{
    Body,
    Heading,
    Quote,
    Caption,
    Hand,
}

/// <summary>
///     Every colour and surface a book is drawn with. Books override one field with <c>with</c>, e.g.
///     <c>BookPalette.Default() with { Cover = new(2, 19, 27) }</c>.
/// </summary>
public struct BookPalette()
{
    #region Binding

    public Color Cover { get; init; } = new(58, 36, 18);
    public Color Spine { get; init; } = new(30, 18, 8);
    public Color Trim { get; init; } = new(150, 110, 60);
    public Color Shadow { get; init; } = new(0, 0, 0, 120);

    /// <summary>Art set inside the cover's frame, as a texture path. Null leaves the board plain.</summary>
    public string? CoverArt { get; init; } = null;

    /// <summary>The cover's title imprint and frame.</summary>
    public Color CoverInk { get; init; } = new(168, 132, 74);

    /// <summary>The contents tab along the top edge. Bookmark ribbons keep their own colours.</summary>
    public Color Tab { get; init; } = new(96, 66, 34);

    #endregion

    #region Paper

    public Color PageColor { get; init; } = new(235, 234, 211);
    public Color PageEdgeColor { get; init; } = new(150, 128, 92);

    /// <summary>Paper texture path, stretched over each leaf and mirrored on the left. Null draws <see cref="PageColor" /> with edge shading.</summary>
    public string? PageTexture { get; init; } = null;

    /// <summary>Paper behind sticky notes that don't name their own.</summary>
    public Color NotePaper { get; init; } = new(232, 214, 142);

    #endregion

    #region Ink

    public Color Ink { get; init; } = new(37, 38, 38);
    public Color HeadingInk { get; init; } = new(78, 48, 24);
    public Color QuoteInk { get; init; } = new(33, 33, 33);
    public Color CaptionInk { get; init; } = new(66, 66, 66);
    public Color HandInk { get; init; } = new(46, 32, 99);

    /// <summary>Page numbers.</summary>
    public Color FolioInk { get; init; } = new(120, 98, 74);

    #endregion

    public readonly Color InkFor(InkRole role) => role switch
    {
        InkRole.Heading => HeadingInk,
        InkRole.Quote   => QuoteInk,
        InkRole.Caption => CaptionInk,
        InkRole.Hand    => HandInk,
        _               => Ink,
    };

    /// <summary>Rebinds in <paramref name="cover" />, with the spine darkened from it to match.</summary>
    public readonly BookPalette WithCover(Color cover) => this with
    {
        Cover = cover,
        Spine = Color.Lerp(cover, Color.Black, 0.48f),
    };

    /// <summary>Sets every text ink, headings and handwriting included, to <paramref name="ink" />.</summary>
    public readonly BookPalette WithInk(Color ink) => this with
    {
        Ink = ink,
        HeadingInk = ink,
        QuoteInk = ink,
        CaptionInk = ink,
        HandInk = ink,
    };

    /// <summary>
    /// Static on purpose: <c>BookUI.BookPalette</c> shares this type's name, and an instance <c>Default()</c> would bind
    /// to that property there and call it forever.
    /// </summary>
    public static BookPalette Default() => new();
}
