using ReLogic.Graphics;
using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>How a panel's face is rasterised, and therefore how it reads once magnified.</summary>
internal enum ProjectedInk
{
    /// <summary>
    ///     Hard-edged: coverage is thresholded to ink or nothing, so a magnified face is a bitmap
    ///     rather than a blurred one. What the dialogue bubble and the job board are authored for.
    /// </summary>
    Pixelated,

    /// <summary>
    ///     Antialiased, for a face drawn at or near one texel per screen pixel. For panels standing
    ///     in the scene without claiming to be pixel art -- an item tooltip, whose lines are vanilla
    ///     content set in the vanilla face.
    /// </summary>
    Smooth,
}

/// <summary>
///     Draws into a projected surface, switching between the batch its
///     <see cref="ProjectedInk" /> calls for and a point-sampled batch for sprite art.
/// </summary>
/// <remarks>
///     <para>
///         The ink batch (<see cref="ProjectedSurfaces.BeginInk" />) thresholds coverage and returns
///         the vertex colour, which is exactly right for glyphs and MagicPixel and exactly wrong for
///         an item sprite: it would come out as a flat silhouette in the tint colour. Art therefore
///         always goes through its own batch with no shader, whatever the style.
///     </para>
///     <para>
///         The switch is lazy: nothing is begun until something is drawn, and a run of shapes costs
///         one batch however long it is. Do not hold a <see cref="Canvas" /> across a call to
///         <see cref="Sprite" /> -- the struct is only a view over the batch, and the batch will have
///         changed mode underneath it.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     A string, drawn whole.
    /// </summary>
    /// <remarks>
    ///     Whole strings rather than glyph by glyph. <see cref="DynamicSpriteFont" /> advances the pen
    ///     by each character's own kerning plus its character spacing, and a caller placing glyphs
    ///     from summed <c>MeasureString</c> widths loses that spacing -- which is what spread the
    ///     board's first draft out into l o o s e  l e t t e r s. The dialogue box can afford to place
    ///     its own because it reveals and animates each one; a board cannot and need not.
    /// </remarks>
    public void Text(DynamicSpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Ensure(Mode.Text);

        // Snapped for the reason PixelCanvas.Glyph snaps: a sub-texel offset under point sampling
        // shows up as a row of the glyph jumping a whole chunky pixel on its own.
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

    /// <summary>
    ///     A sprite scaled down until it fits inside <paramref name="box" />, centred in it. Never
    ///     scaled up past <paramref name="maxScale" />: pixel art enlarged by a non-integer factor
    ///     stops being pixel art.
    /// </summary>
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
