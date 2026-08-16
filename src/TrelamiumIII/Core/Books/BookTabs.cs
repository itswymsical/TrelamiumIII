using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books;

/// <summary>One tab along the book's top edge.</summary>
/// <param name="Label">Text on the tab. Empty draws an unused slot.</param>
/// <param name="Filled">False draws the slot as available rather than as a destination.</param>
/// <param name="Tint">Ribbon colour, so tabs can be told apart at a glance.</param>
/// <param name="Tooltip">Raised on hover.</param>
public readonly record struct BookTab(string Label, bool Filled, Color Tint, string? Tooltip = null);

/// <summary>
///     Draws and hit-tests the tabs that stick up out of the top of a book.
/// </summary>
/// <remarks>
///     Tabs live in screen space, drawn under the cover boards rather than into a page target. That is
///     deliberate: a tab is attached to the book, not to a leaf, so it must not curl away when a page
///     turns, and it has to stay clickable while the pages beneath it are mid-animation.
/// </remarks>
public static class BookTabs
{
    public const float TAB_WIDTH = 92f;
    public const float TAB_HEIGHT = 22f;
    public const float TAB_GAP = 3f;

    /// <summary>Breathing room either side of a tab's label, before it is elided.</summary>
    private const float LABEL_PADDING = 6f;

    /// <summary>Inset of the first tab from the cover's left edge.</summary>
    private const float TAB_INSET = 16f;

    /// <summary>How far a tab tucks under the cover, so it reads as sitting behind the board.</summary>
    private const float TAB_OVERLAP = 6f;

    private static readonly Color EmptyFill = new(48, 38, 28);
    private static readonly Color EmptyEdge = new(96, 78, 56);

    public static Rectangle TabRect(Vector2 spreadOrigin, int index)
    {
        float x = spreadOrigin.X - BookCoverFrame.COVER_MARGIN + TAB_INSET + index * (TAB_WIDTH + TAB_GAP);
        float y = spreadOrigin.Y - BookCoverFrame.COVER_MARGIN - TAB_HEIGHT + TAB_OVERLAP;

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

    public static void Draw(
        SpriteBatch spriteBatch,
        Vector2 spreadOrigin,
        IReadOnlyList<BookTab> tabs,
        int hovered,
        float opacity)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        DynamicSpriteFont font = FontAssets.MouseText.Value;

        for (int i = 0; i < tabs.Count; i++)
        {
            BookTab tab = tabs[i];
            Rectangle rect = TabRect(spreadOrigin, i);
            bool isHovered = i == hovered;

            Color fill = tab.Filled ? tab.Tint : EmptyFill;

            if (isHovered)
                fill = Color.Lerp(fill, Color.White, 0.22f);

            spriteBatch.Draw(pixel, rect, fill * opacity);

            // Only the three exposed sides get an edge; the bottom runs under the board.
            Color edge = (tab.Filled ? Color.Lerp(tab.Tint, Color.White, 0.35f) : EmptyEdge) * opacity;

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

            Color textColor = (tab.Filled ? Color.White : EmptyEdge) * opacity;

            Utils.DrawBorderStringFourWay(
                spriteBatch, font, label, textPosition.X, textPosition.Y, textColor, Color.Black * (opacity * 0.5f), Vector2.Zero, scale);
        }
    }
}
