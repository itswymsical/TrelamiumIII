using Terraria.GameContent;

<<<<<<<< HEAD:src/TrelamiumIII/Utilities/Authoring/ColorWheelWidget.cs
namespace TrelamiumIII.Utilities.Authoring;
========
namespace TrelamiumIII.Core.Books.Authoring;
>>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb:src/TrelamiumIII/Core/Books/Authoring/ColorWheelWidget.cs

/// <summary>
///     HSV picker: a saturation/value box plus hue strip, so white and black are one click away. Driven by UI mouse events.
/// </summary>
public sealed class ColorWheelWidget
{
    private float hue;
    private float saturation;
    private float value = 1f;

    private bool draggingBox;
    private bool draggingHue;

    public Color Value => FromHsv(hue, saturation, value);

    public bool Dragging => draggingBox || draggingHue;

    public void SetColor(Color color) => (hue, saturation, value) = ToHsv(color);

    private static Rectangle BoxOf(Rectangle bounds)
    {
        int strip = StripWidthOf(bounds);

        return new Rectangle(bounds.X, bounds.Y, bounds.Width - strip - 8, bounds.Height);
    }

    private static Rectangle StripOf(Rectangle bounds)
    {
        int strip = StripWidthOf(bounds);

        return new Rectangle(bounds.Right - strip, bounds.Y, strip, bounds.Height);
    }

    private static int StripWidthOf(Rectangle bounds) => Math.Max(14, bounds.Width / 9);

    /// <summary>Begins a drag if the press landed on either control. True when it did.</summary>
    public bool Press(Point point, Rectangle bounds)
    {
        if (BoxOf(bounds).Contains(point)) draggingBox = true;
        else if (StripOf(bounds).Contains(point)) draggingHue = true;
        else return false;

        Drag(point, bounds);

        return true;
    }

    public void Release() => draggingBox = draggingHue = false;

    /// <summary>Tracks a drag in progress. Clamped, so the pointer may leave the control.</summary>
    public void Drag(Point point, Rectangle bounds)
    {
        if (draggingBox)
        {
            Rectangle box = BoxOf(bounds);

            saturation = Math.Clamp((point.X - box.X) / (float)Math.Max(1, box.Width), 0f, 1f);
            value = Math.Clamp(1f - (point.Y - box.Y) / (float)Math.Max(1, box.Height), 0f, 1f);
        }

        if (!draggingHue) return;

        Rectangle strip = StripOf(bounds);

        hue = Math.Clamp((point.Y - strip.Y) / (float)Math.Max(1, strip.Height), 0f, 1f);
    }

    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float alpha)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Rectangle box = BoxOf(bounds);
        Rectangle strip = StripOf(bounds);

        // Sampled as a coarse grid rather than a generated texture: a few hundred quads a frame is
        // cheaper than allocating and uploading one, and the picker is only up while it is open.
        const int columns = 28;
        const int rows = 20;

        for (int cx = 0; cx < columns; cx++)
        {
            int x0 = box.X + cx * box.Width / columns;
            int x1 = box.X + (cx + 1) * box.Width / columns;
            float s = (cx + 0.5f) / columns;

            for (int cy = 0; cy < rows; cy++)
            {
                int y0 = box.Y + cy * box.Height / rows;
                int y1 = box.Y + (cy + 1) * box.Height / rows;

                spriteBatch.Draw(pixel, new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1),
                    FromHsv(hue, s, 1f - (cy + 0.5f) / rows) * alpha);
            }
        }

        const int bands = 40;

        for (int i = 0; i < bands; i++)
        {
            spriteBatch.Draw(pixel,
                new Rectangle(strip.X, strip.Y + i * strip.Height / bands, strip.Width, strip.Height / bands + 1),
                FromHsv(i / (float)bands, 1f, 1f) * alpha);
        }

        Marker(spriteBatch, pixel,
            new Point(box.X + (int)(saturation * box.Width), box.Y + (int)((1f - value) * box.Height)), alpha);

        int handleY = strip.Y + (int)(hue * strip.Height);

        spriteBatch.Draw(pixel, new Rectangle(strip.X - 2, handleY - 2, strip.Width + 4, 4), Color.White * alpha);
        spriteBatch.Draw(pixel, new Rectangle(strip.X - 2, handleY - 1, strip.Width + 4, 2), Color.Black * alpha);
    }

    private void Marker(SpriteBatch spriteBatch, Texture2D pixel, Point at, float alpha)
    {
        var outer = new Rectangle(at.X - 5, at.Y - 5, 10, 10);

        spriteBatch.Draw(pixel, outer, Color.Black * alpha);
        spriteBatch.Draw(pixel, new Rectangle(outer.X + 2, outer.Y + 2, 6, 6), Value * alpha);
    }

    public static Color FromHsv(float h, float s, float v)
    {
        h = (h % 1f + 1f) % 1f;

        float chroma = v * s;
        float x = chroma * (1f - Math.Abs(h * 6f % 2f - 1f));
        float m = v - chroma;

        (float r, float g, float b) = ((int)(h * 6f) % 6) switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };

        return new Color(r + m, g + m, b + m);
    }

    public static (float H, float S, float V) ToHsv(Color color)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;
        float h = 0f;

        if (delta > 1e-5f)
        {
            if (max == r) h = (g - b) / delta % 6f;
            else if (max == g) h = (b - r) / delta + 2f;
            else h = (r - g) / delta + 4f;

            h /= 6f;

            if (h < 0f) h += 1f;
        }

        return (h, max <= 1e-5f ? 0f : delta / max, max);
    }
}
