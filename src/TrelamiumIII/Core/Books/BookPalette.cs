namespace TrelamiumIII.Core.Books;

public struct BookPalette()
{
    public Color Cover { get; init; } = new(58, 36, 18);
    public Color Spine { get; init; } = new(30, 18, 8);
    public Color Trim { get; init; } = new(150, 110, 60);
    public Color Shadow { get; init; } = new(0, 0, 0, 120);
    public Color PageColor { get; init; } = new(235, 234, 211);
    public Color PageEdgeColor { get; init; } = new(150, 128, 92);

    /// <summary>
    /// Static on purpose: <c>BookUI.BookPalette</c> shares this type's name, and an instance <c>Default()</c> would bind
    /// to that property there and call it forever.
    /// </summary>
    public static BookPalette Default() => new();
}
