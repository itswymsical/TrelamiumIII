using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books;

/// <summary>One tab along the book's top edge.</summary>
/// <param name="Label">Text on the tab; empty draws an unused slot.</param>
/// <param name="Filled">False draws the slot as available rather than as a destination.</param>
public readonly record struct BookTab(string Label, bool Filled, Color Tint, string? Tooltip = null);

/// <summary>The cover behind an open spread, drawn under the composite so folding leaves reveal it.</summary>
public static class BookCover
{
    /// <summary>How far the boards overhang the paper on every edge.</summary>
    public const int COVER_MARGIN = 14;

    private const int SHADOW_OFFSET = 6;

    /// <param name="origin">Top-left of the spread, where the paper starts.</param>
    public static void Draw(
        SpriteBatch spriteBatch,
        Vector2 origin,
        int pageWidth,
        int pageHeight,
        int gutter,
        float opacity,
        BookPalette bookPalette,
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

        spriteBatch.Draw(pixel, new Rectangle(cover.X + shadow, cover.Bottom, cover.Width, shadow), bookPalette.Shadow * opacity);
        spriteBatch.Draw(pixel, new Rectangle(cover.Right, cover.Y + shadow, shadow, cover.Height), bookPalette.Shadow * opacity);

        spriteBatch.Draw(pixel, cover, bookPalette.Cover * opacity);

        Color trim = bookPalette.Trim * opacity;

        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Y + inset, cover.Width - inset * 2, 1), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Bottom - farInset, cover.Width - inset * 2, 1), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.X + inset, cover.Y + inset, 1, cover.Height - inset * 2), trim);
        spriteBatch.Draw(pixel, new Rectangle(cover.Right - farInset, cover.Y + inset, 1, cover.Height - inset * 2), trim);

        spriteBatch.Draw(
            pixel,
            new Rectangle((int)origin.X + (int)(pageWidth * scale), cover.Y + inset, (int)(gutter * scale), cover.Height - inset * 2),
            bookPalette.Spine * opacity);
    }

    public const float TAB_WIDTH = 16f;
    public const float TAB_HEIGHT = 22f;
    public const float TAB_GAP = 6f;

    /// <summary>Breathing room either side of a tab's label, before it is elided.</summary>
    private const float LABEL_PADDING = 7.5f;

    /// <summary>Inset of the first tab from the cover's left edge.</summary>
    private const float TAB_INSET = 6f;

    /// <summary>How far a tab tucks under the cover, so it reads as sitting behind the board.</summary>
    private const float TAB_OVERLAP = 5f;

    public static Rectangle TabRect(Vector2 spreadOrigin, int index)
    {
        float x = spreadOrigin.X - BookCover.COVER_MARGIN + TAB_INSET + index * (TAB_WIDTH + TAB_GAP);
        float y = spreadOrigin.Y - BookCover.COVER_MARGIN - TAB_HEIGHT + TAB_OVERLAP;

        return new Rectangle((int)x, (int)y, (int)TAB_WIDTH, (int)TAB_HEIGHT);
    }

    /// <summary>Index of the tab under the cursor, or -1.</summary>
    public static int HitTest(Vector2 spreadOrigin, int count, Point mouse)
    {
        for (int i = 0; i < count; i++)
        {
            // The overlapped strip belongs to the cover, not the tab, or a click meant for the
            // board's edge would register on whatever tab happens to be tucked under it.
            Rectangle rect = TabRect(spreadOrigin, i);
            rect.Height -= (int)TAB_OVERLAP;

            if (rect.Contains(mouse))
                return i;
        }

        return -1;
    }

    public static void DrawBookTabs(
        SpriteBatch spriteBatch,
        Vector2 spreadOrigin,
        IReadOnlyList<BookTab> tabs,
        int hovered,
        float opacity, BookPalette palette)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        DynamicSpriteFont font = FontAssets.MouseText.Value;

        for (int i = 0; i < tabs.Count; i++)
        {
            BookTab tab = tabs[i];
            Rectangle rect = TabRect(spreadOrigin, i);
            bool isHovered = i == hovered;

            Color fill = tab.Filled ? tab.Tint : palette.Shadow;

            if (isHovered)
                fill = Color.Lerp(fill, Color.White, 0.22f);

            spriteBatch.Draw(pixel, rect, fill * opacity);

            // Only the three exposed sides get an edge; the bottom runs under the board.
            Color edge = (tab.Filled ? Color.Lerp(tab.Tint, Color.White, 0.35f) : palette.Cover) * opacity;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge);

            const float scale = 0.68f;

            // Elided to the tab, not merely centred in it. A chapter title is longer than a tab and
            // would otherwise run straight over its neighbours, turning the strip into one smear.
            string label = tab.Filled
                ? BookText.Truncate(font, tab.Label, scale, TAB_WIDTH - LABEL_PADDING * 2f)
                : "+";

            Vector2 size = font.MeasureString(label) * scale;

            // Centred in the exposed part of the tab, not in the whole rect.
            var textPosition = new Vector2(
                (float)Math.Round(rect.X + (rect.Width - size.X) * 0.5f),
                (float)Math.Round(rect.Y + (rect.Height - TAB_OVERLAP - size.Y) * 0.5f));

            Color textColor = (tab.Filled ? Color.White : palette.Cover) * opacity;

            Utils.DrawBorderStringFourWay(
                spriteBatch, font, label, textPosition.X, textPosition.Y, textColor, Color.Black * (opacity * 0.5f), Vector2.Zero, scale);
        }
    }
}
