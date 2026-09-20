namespace TrelamiumIII.Core.UI;

/// <summary>
/// Builds white premultiplied textures carrying a sprite's alpha, for UI flashes and glows shaped by the
/// sprite itself. Callers own what they build and dispose it on their own unload.
/// </summary>
internal static class SpriteMask
{
    /// <summary>
    /// <paramref name="source"/>'s alpha in white, padded on every side and, when <paramref name="blurRadius"/>
    /// is above 0, box-blurred twice per axis. Must be called on the main thread.
    /// </summary>
    public static Texture2D Build(Texture2D source, int padding = 0, int blurRadius = 0)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);

        int width = source.Width + padding * 2;
        int height = source.Height + padding * 2;
        var alpha = new float[width * height];

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
                alpha[(y + padding) * width + x + padding] = pixels[y * source.Width + x].A / 255f;
        }

        for (int pass = 0; pass < 2 && blurRadius > 0; pass++)
        {
            alpha = BoxBlur(alpha, width, height, blurRadius, horizontal: true);
            alpha = BoxBlur(alpha, width, height, blurRadius, horizontal: false);
        }

        var mask = new Color[alpha.Length];

        for (int i = 0; i < mask.Length; i++)
        {
            byte a = (byte)(Math.Clamp(alpha[i], 0f, 1f) * 255f);
            mask[i] = new Color(a, a, a, a);
        }

        var texture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
        texture.SetData(mask);

        return texture;
    }

    /// <summary>One box-blur pass along rows or columns. Samples outside the texture count as 0.</summary>
    private static float[] BoxBlur(float[] values, int width, int height, int radius, bool horizontal)
    {
        var result = new float[values.Length];
        int length = horizontal ? width : height;
        int lines = horizontal ? height : width;

        for (int line = 0; line < lines; line++)
        {
            for (int i = 0; i < length; i++)
            {
                float sum = 0f;

                for (int k = Math.Max(0, i - radius); k <= Math.Min(length - 1, i + radius); k++)
                    sum += values[horizontal ? line * width + k : k * width + line];

                result[horizontal ? line * width + i : i * width + line] = sum / (radius * 2 + 1);
            }
        }

        return result;
    }
}

/// <summary>Diagonal highlight band swept across a <see cref="SpriteMask"/>, drawn in row slices.</summary>
internal static class SpriteSheen
{
    /// <summary>Texels the band shifts left per texel down.</summary>
    private const float SLOPE = 0.5f;

    /// <summary>Half the band's width, as a fraction of the mask's width.</summary>
    private const float HALF_WIDTH = 0.15f;

    /// <summary>
    /// Draws the band at <paramref name="progress"/> 0..1 across <paramref name="mask"/>, brightest at the
    /// midpoint. <paramref name="position"/> is the mask's top-left.
    /// </summary>
    public static void Draw(SpriteBatch sb, Texture2D mask, Vector2 position, float scale, float progress, Color color)
    {
        if (progress is <= 0f or >= 1f)
            return;

        float halfWidth = mask.Width * HALF_WIDTH;
        float topCenter = -halfWidth + progress * (mask.Width + halfWidth * 2f + mask.Height * SLOPE);
        int slice = Math.Max(1, (int)MathF.Ceiling(1f / scale));
        Color tint = color * MathF.Sin(progress * MathF.PI);

        for (int row = 0; row < mask.Height; row += slice)
        {
            int height = Math.Min(slice, mask.Height - row);
            float center = topCenter - (row + height * 0.5f) * SLOPE;
            int left = Math.Clamp((int)MathF.Round(center - halfWidth), 0, mask.Width);
            int right = Math.Clamp((int)MathF.Round(center + halfWidth), 0, mask.Width);

            if (right > left)
            {
                sb.Draw(mask, position + new Vector2(left, row) * scale, new Rectangle(left, row, right - left, height),
                    tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
