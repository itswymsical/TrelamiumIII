using Terraria.GameContent;
using TrelamiumIII.Core.UI.AlchemyAlmanac;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// Draws a book cover behind the <see cref="AlchemyAlmanacUI"/> and <see cref="AlchemyBookUI"/>.
/// </summary>
public static class BookCoverDrawer
{
    public const int COVER_MARGIN = 14;
    private const int SHADOW_OFFSET = 6;

    private static readonly Color CoverFill = new(58, 36, 18);
    private static readonly Color SpineFill = new(30, 18, 8);
    private static readonly Color GoldTrim = new(150, 110, 60);
    private static readonly Color DropShadow = new(0, 0, 0, 120);

    /// <summary>
    /// <paramref name="origin"/> is the top-left of the spread (where pages start).
    /// </summary>
    public static void Draw(SpriteBatch sb, Vector2 origin, int pageWidth, int pageHeight, int gutter, float opacity, float scale = 1f)
    {
        var px = TextureAssets.MagicPixel.Value;
        int spreadW = (int)((pageWidth * 2 + gutter) * scale);
        int spreadH = (int)(pageHeight * scale);
        int margin = (int)(COVER_MARGIN * scale);
        int shadow = (int)(SHADOW_OFFSET * scale);
        int inset = (int)(4 * scale);
        int inset2 = (int)(5 * scale);

        var coverRect = new Rectangle(
            (int)origin.X - margin,
            (int)origin.Y - margin,
            spreadW + margin * 2,
            spreadH + margin * 2);

        // Drop shadow on the bottom-right edge
        sb.Draw(px,
            new Rectangle(coverRect.X + shadow, coverRect.Bottom, coverRect.Width, shadow),
            DropShadow * opacity);
        sb.Draw(px,
            new Rectangle(coverRect.Right, coverRect.Y + shadow, shadow, coverRect.Height),
            DropShadow * opacity);

        // Cover body
        sb.Draw(px, coverRect, CoverFill * opacity);

        // Inner gold trim, inset a few pixels
        var trim = GoldTrim * opacity;
        sb.Draw(px, new Rectangle(coverRect.X + inset, coverRect.Y + inset, coverRect.Width - inset * 2, 1), trim);
        sb.Draw(px, new Rectangle(coverRect.X + inset, coverRect.Bottom - inset2, coverRect.Width - inset * 2, 1), trim);
        sb.Draw(px, new Rectangle(coverRect.X + inset, coverRect.Y + inset, 1, coverRect.Height - inset * 2), trim);
        sb.Draw(px, new Rectangle(coverRect.Right - inset2, coverRect.Y + inset, 1, coverRect.Height - inset * 2), trim);

        // Spine fill in the gutter
        sb.Draw(px,
            new Rectangle((int)origin.X + (int)(pageWidth * scale), coverRect.Y + inset, (int)(gutter * scale), coverRect.Height - inset * 2),
            SpineFill * opacity);
    }
}
