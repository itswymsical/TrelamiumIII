using Terraria.GameContent;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     The bound cover sitting behind an open spread: boards, gold trim and the spine in the gutter.
/// </summary>
/// <remarks>
///     Drawn under the composite rather than into it, so that leaves folding shut reveal it rather
///     than taking it with them.
/// </remarks>
public static class BookCoverFrame
{
    /// <summary>How far the boards overhang the paper on every edge.</summary>
    public const int COVER_MARGIN = 14;

    private const int SHADOW_OFFSET = 6;

    private static readonly Color CoverFill = new(58, 36, 18);
    private static readonly Color SpineFill = new(30, 18, 8);
    private static readonly Color GoldTrim = new(150, 110, 60);
    private static readonly Color DropShadow = new(0, 0, 0, 120);

    /// <param name="origin">Top-left of the spread, where the paper starts.</param>
    public static void Draw(
        SpriteBatch spriteBatch,
        Vector2 origin,
        int pageWidth,
        int pageHeight,
        int gutter,
        float opacity,
        float scale = 1f)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        int spreadWidth = (int)((pageWidth * 2 + gutter) * scale);
        int spreadHeight = (int)(pageHeight * scale);
        int margin = (int)(COVER_MARGIN * scale);
        int shadow = (int)(SHADOW_OFFSET * scale);
        int inset = (int)(4 * scale);
        int farInset = (int)(5 * scale);

        var cover = new Rectangle(
            (int)origin.X - margin,
            (int)origin.Y - margin,
            spreadWidth + margin * 2,
            spreadHeight + margin * 2);

        spriteBatch.Draw(pixel, new Rectangle(cover.X + shadow, cover.Bottom, cover.Width, shadow), DropShadow * opacity);
        spriteBatch.Draw(pixel, new Rectangle(cover.Right, cover.Y + shadow, shadow, cover.Height), DropShadow * opacity);

        spriteBatch.Draw(pixel, cover, CoverFill * opacity);

        Color trim = GoldTrim * opacity;

        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Y + inset, cover.Width - inset * 2, 1), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Bottom - farInset, cover.Width - inset * 2, 1), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Y + inset, 1, cover.Height - inset * 2), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.Right - farInset, cover.Y + inset, 1, cover.Height - inset * 2), trim);

        spriteBatch.Draw(
            pixel,
            new Rectangle((int)origin.X + (int)(pageWidth * scale), cover.Y + inset, (int)(gutter * scale), cover.Height - inset * 2),
            SpineFill * opacity);
    }
}
