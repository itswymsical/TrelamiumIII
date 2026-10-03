using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     Maps the cursor back onto a projected quad's face, so widgets laid out in texels can be
///     hit-tested in texels.
/// </summary>
internal static class ProjectedHitTest
{
    /// <summary>
    ///     Where <paramref name="screen" /> lands on the face, 0..1 across and down.
    /// </summary>
    /// <returns>False when the point is off the quad, or the quad has no screen footprint.</returns>
    public static bool TryLocate(in QuadPlacement placement, Vector2 screen, out Vector2 uv) =>
        TryProject(placement, screen, out uv) && uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f;

    /// <summary>
    ///     Where <paramref name="screen" /> lands on the face's plane, 0..1 across and down, unclamped.
    /// </summary>
    /// <returns>False when the quad has no screen footprint.</returns>
    public static bool TryProject(in QuadPlacement placement, Vector2 screen, out Vector2 uv)
    {
        uv = default;

        if (placement.Size.X <= 0f || placement.Size.Y <= 0f)
            return false;

        Span<Vector2> s = stackalloc Vector2[4];

        if (!PanelCamera.ScreenCorners(placement, s))
            return false;

        float x0 = s[0].X, y0 = s[0].Y;
        float x1 = s[1].X, y1 = s[1].Y;
        float x2 = s[2].X, y2 = s[2].Y;
        float x3 = s[3].X, y3 = s[3].Y;

        float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;

        float a, b, c, d, e, f, g, h;

        if (MathF.Abs(dx3) < 1e-5f && MathF.Abs(dy3) < 1e-5f)
        {
            // A parallelogram: the map is affine and the perspective terms vanish.
            a = x1 - x0; b = x3 - x0; c = x0;
            d = y1 - y0; e = y3 - y0; f = y0;
            g = 0f; h = 0f;
        }
        else
        {
            float det = dx1 * dy2 - dx2 * dy1;

            if (MathF.Abs(det) < 1e-9f)
                return false;

            g = (dx3 * dy2 - dx2 * dy3) / det;
            h = (dx1 * dy3 - dx3 * dy1) / det;

            a = x1 - x0 + g * x1; b = x3 - x0 + h * x3; c = x0;
            d = y1 - y0 + g * y1; e = y3 - y0 + h * y3; f = y0;
        }

        // Adjugate of the square-to-quad matrix, which is the quad-to-square map up to scale.
        float A = e - f * h, B = c * h - b, C = b * f - c * e;
        float D = f * g - d, E = a - c * g, F = c * d - a * f;
        float G = d * h - e * g, H = b * g - a * h, I = a * e - b * d;

        float w = G * screen.X + H * screen.Y + I;

        if (MathF.Abs(w) < 1e-9f)
            return false;

        uv = new Vector2(
            (A * screen.X + B * screen.Y + C) / w,
            (D * screen.X + E * screen.Y + F) / w);

        return true;
    }

    /// <summary>The cursor as a texel on the sampled <paramref name="region" />, or null if off the quad.</summary>
    public static Point? Texel(in QuadPlacement placement, Rectangle region, Vector2 screen)
    {
        if (!TryLocate(placement, screen, out Vector2 uv))
            return null;

        return new Point(
            region.X + (int)MathF.Floor(uv.X * region.Width),
            region.Y + (int)MathF.Floor(uv.Y * region.Height));
    }
}
