using ReLogic.Content;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Books;

/// <summary>Page paper, drawn procedurally unless the palette names a <see cref="BookPalette.PageTexture" /> (then mirrored on the left leaf).</summary>
public static class PagePaper
{
    /// <summary>Width of the shading that falls away from the spine.</summary>
    private const int GUTTER_SHADE = 26;

    /// <summary>Width of the shading along the outer edges.</summary>
    private const int EDGE_SHADE = 8;

    public static void Draw(SpriteBatch spriteBatch, int width, int height, BookSide side, BookPalette palette)
    {
        if (palette.PageTexture is { } path)
        {
            spriteBatch.Draw(
                ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value,
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
