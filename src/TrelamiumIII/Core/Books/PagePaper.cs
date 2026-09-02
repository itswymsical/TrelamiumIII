using ReLogic.Content;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     The paper a page is printed on.
/// </summary>
/// <remarks>
///     Drawn procedurally so books work before any paper texture exists. Point
///     <see cref="Texture" /> at an asset and it is used instead, stretched to the page and mirrored
///     on the left leaf so the two halves are not obviously the same image.
/// </remarks>
public static class PagePaper
{
    private static readonly Color Base = new(235, 234, 211);
    private static readonly Color Edge = new(150, 128, 92);

    /// <summary>Width of the shading that falls away from the spine.</summary>
    private const int GUTTER_SHADE = 26;

    /// <summary>Width of the shading along the outer edges.</summary>
    private const int EDGE_SHADE = 8;

    /// <summary>Optional paper texture. Null draws the procedural fallback.</summary>
    public static Asset<Texture2D>? Texture { get; set; }

    public static void Draw(SpriteBatch spriteBatch, int width, int height, BookSide side, BookPalette palette)
    {
        if (Texture is { Value: { } texture })
        {
            spriteBatch.Draw(
                texture,
                new Rectangle(0, 0, width, height),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                side == BookSide.Left ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f);

            return;
        }

        Texture2D pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), palette.PageColor);

        // The spine is on the inner edge, which is the right of a left leaf and the left of a right one.
        bool spineOnRight = side == BookSide.Left;

        for (int i = 0; i < GUTTER_SHADE; i++)
        {
            float depth = 1f - i / (float)GUTTER_SHADE;
            var shade = palette.PageEdgeColor * (depth * depth * 0.35f);
            int x = spineOnRight ? width - 1 - i : i;

            spriteBatch.Draw(pixel, new Rectangle(x, 0, 1, height), shade);
        }

        for (int i = 0; i < EDGE_SHADE; i++)
        {
            float depth = 1f - i / (float)EDGE_SHADE;
            var shade = palette.PageEdgeColor * (depth * depth * 0.22f);
            int x = spineOnRight ? i : width - 1 - i;

            spriteBatch.Draw(pixel, new Rectangle(x, 0, 1, height), shade);
            spriteBatch.Draw(pixel, new Rectangle(0, i, width, 1), shade * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(0, height - 1 - i, width, 1), shade * 0.6f);
        }
    }
}
