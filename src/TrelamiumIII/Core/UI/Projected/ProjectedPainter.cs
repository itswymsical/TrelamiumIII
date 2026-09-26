using ReLogic.Graphics;
using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>How a panel's face is rasterised, and therefore how it reads once magnified.</summary>
internal enum ProjectedInk
{
    Pixelated,

    Smooth,
}

/// <summary>
///     Draws into a projected surface, switching between the batch its
///     <see cref="ProjectedInk" /> calls for and a point-sampled batch for sprite art.
/// </summary>
internal sealed class ProjectedPainter(SpriteBatch batch, ProjectedInk ink)
{
    private enum Mode
    {
        None,
        Text,
        Art,
    }

    private Mode mode = Mode.None;

    /// <summary>Shapes and text, in texels. Re-read it after any <see cref="Sprite" /> call.</summary>
    public PixelCanvas Canvas
    {
        get
        {
            Ensure(Mode.Text);
            return new PixelCanvas(batch);
        }
    }

    public void Text(DynamicSpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Ensure(Mode.Text);

        batch.DrawString(
            font,
            text,
            new Vector2(MathF.Round(position.X), MathF.Round(position.Y)),
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }

    public void Sprite(Texture2D texture, Vector2 position, Rectangle? source, Color color, float scale = 1f)
    {
        Ensure(Mode.Art);

        batch.Draw(
            texture,
            new Vector2(MathF.Round(position.X), MathF.Round(position.Y)),
            source,
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }

    public void SpriteFitted(Texture2D texture, Rectangle source, Rectangle box, Color color, float maxScale = 1f)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return;

        float fit = MathF.Min(box.Width / (float)source.Width, box.Height / (float)source.Height);
        float scale = MathF.Min(maxScale, fit);

        Vector2 size = new(source.Width * scale, source.Height * scale);
        Vector2 position = new(box.X + (box.Width - size.X) * 0.5f, box.Y + (box.Height - size.Y) * 0.5f);

        Sprite(texture, position, source, color, scale);
    }

    private void Ensure(Mode wanted)
    {
        if (mode == wanted)
            return;

        if (mode != Mode.None)
            batch.End();

        if (wanted == Mode.Art)
            ProjectedSurfaces.BeginArt(batch);
        else if (ink == ProjectedInk.Pixelated)
            ProjectedSurfaces.BeginInk(batch);
        else
            ProjectedSurfaces.BeginSmooth(batch);

        mode = wanted;
    }

    /// <summary>Closes whichever batch is open. The surface calls this; content never needs to.</summary>
    public void Finish()
    {
        if (mode != Mode.None)
            batch.End();

        mode = Mode.None;
    }
}
