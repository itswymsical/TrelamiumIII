namespace TrelamiumIII.Core.Books;

public struct BookPalette()
{
    public Color Cover { get; init; } = new(58, 36, 18);
    public Color Spine { get; init; } = new(30, 18, 8);
    public Color Trim { get; init; } = new(150, 110, 60);
    public Color Shadow { get; init; } = new(0, 0, 0, 120);
    public Color PageColor { get; init; } = new(235, 234, 211);
    public Color PageEdgeColor { get; init; } = new(150, 128, 92);

    public BookPalette Default() => new()
    {
        Cover = new Color(58, 36, 18),
        Spine = new Color(30, 18, 8),
        Trim = new Color(150, 110, 60),
        Shadow = new Color(0, 0, 0, 120),
        PageColor = new Color(235, 234, 211),
        PageEdgeColor = new Color(150, 128, 92)
    };
}
