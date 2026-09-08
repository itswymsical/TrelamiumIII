using ReLogic.Graphics;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>
///     Draws into a panel surface in texels -- one unit here is one pixel of the finished, chunky
///     panel, whatever it ends up magnified to on screen.
/// </summary>
/// <remarks>
///     <para>
///         The pixelation is the coordinate system, not a filter over the top. Everything is authored
///         on this grid and the surface is only ever magnified afterwards with point sampling, so an
///         edge is exactly where it was placed and no amount of perspective on the finished quad can
///         put a half-lit pixel along it.
///     </para>
///     <para>
///         Which is also why shapes are rasterised by hand rather than drawn from art. A rectangle
///         that has to grow a texel at a time as text arrives cannot come off a nine-slice without
///         its corners breathing, and the tail has to be able to point anywhere.
///     </para>
/// </remarks>
internal readonly struct PixelCanvas(SpriteBatch batch, int originX = 0, int originY = 0)
{
    private static Texture2D Pixel => TextureAssets.MagicPixel.Value;

    /// <summary>
    ///     The same surface with its origin moved, so a caller can draw one slot of an atlas without
    ///     knowing where in the atlas it landed.
    /// </summary>
    public PixelCanvas At(int x, int y) => new(batch, originX + x, originY + y);

    public void Rect(int x, int y, int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        batch.Draw(Pixel, new Rectangle(originX + x, originY + y, width, height), color);
    }

    /// <summary>
    ///     A rectangle with its corners cut back at 45 degrees, one texel per row.
    /// </summary>
    /// <remarks>
    ///     Cutting the corners is what stops a procedural box reading as a debug rectangle. A stepped
    ///     diagonal rather than a rounded one, because a radius smaller than a handful of texels
    ///     quantises into a shape that changes as the box grows, and this one does not.
    /// </remarks>
    public void ChamferedRect(int x, int y, int width, int height, int chamfer, Color color)
    {
        if (width <= 0 || height <= 0)
            return;

        chamfer = Math.Clamp(chamfer, 0, Math.Min(width, height) / 2);

        for (int row = 0; row < chamfer; row++)
        {
            int inset = chamfer - row;
            Rect(x + inset, y + row, width - inset * 2, 1, color);
            Rect(x + inset, y + height - 1 - row, width - inset * 2, 1, color);
        }

        Rect(x, y + chamfer, width, height - chamfer * 2, color);
    }

    /// <summary>
    ///     A filled triangle, scanline rasterised.
    /// </summary>
    /// <remarks>
    ///     One row rectangle per scanline. At this resolution a tail is a couple of dozen rows, so the
    ///     cost is irrelevant and the payoff is a hard staircase edge that matches everything drawn
    ///     around it -- which a rotated sprite or a primitive triangle would not give.
    /// </remarks>
    public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int top = (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)));
        int bottom = (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));

        for (int y = top; y < bottom; y++)
        {
            // Sampled at the row's centre so a vertex sitting exactly on a boundary lands in one row
            // rather than both, which would fatten the tail by a texel on one side only.
            float scan = y + 0.5f;
            float left = float.MaxValue;
            float right = float.MinValue;

            Cross(a, b, scan, ref left, ref right);
            Cross(b, c, scan, ref left, ref right);
            Cross(c, a, scan, ref left, ref right);

            if (right <= left)
                continue;

            int x0 = (int)MathF.Round(left);
            int x1 = (int)MathF.Round(right);

            Rect(x0, y, x1 - x0, 1, color);
        }
    }

    /// <summary>Widens the span to include this edge's crossing of the scanline, if it has one.</summary>
    private static void Cross(Vector2 p, Vector2 q, float scan, ref float left, ref float right)
    {
        if (p.Y <= scan == q.Y <= scan)
            return;

        float x = p.X + (scan - p.Y) / (q.Y - p.Y) * (q.X - p.X);

        left = MathF.Min(left, x);
        right = MathF.Max(right, x);
    }

    /// <summary>
    ///     One glyph, snapped to the texel grid.
    /// </summary>
    /// <remarks>
    ///     Snapping matters more than it looks. Glyphs land on fractional positions once anything
    ///     animates them, and a sub-texel offset under point sampling shows up as a row of the glyph
    ///     jumping a whole chunky pixel on its own -- text that shimmers rather than sits still.
    /// </remarks>
    public void Glyph(DynamicSpriteFont font, string text, Vector2 position, Color color, float scale)
    {
        Vector2 snapped = new(MathF.Round(originX + position.X), MathF.Round(originY + position.Y));
        batch.DrawString(font, text, snapped, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
