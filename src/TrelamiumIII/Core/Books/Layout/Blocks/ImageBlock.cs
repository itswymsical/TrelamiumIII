using System.Collections.Generic;
using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout.Blocks;

/// <summary>
///     A figure set into the page: plate, diagram, pressed specimen, map fragment.
/// </summary>
/// <remarks>
///     <para>
///         The figure takes a horizontal band to itself and is aligned within it by
///         <see cref="BlockStyle.Align" />; body text does not run around it. Wrapping text around a
///         floated figure would mean teaching both the composer and the line breaker about exclusion
///         rectangles, and the arrangement it buys -- a plate with a column of text beside it -- is
///         better served by pairing the figure with its prose explicitly.
///     </para>
///     <para>
///         A figure is never split, so one taller than the space left simply moves to the next page.
///     </para>
/// </remarks>
public sealed class ImageBlock(Asset<Texture2D> texture, string? caption = null) : BookBlock
{
    /// <summary>Gap between the plate and its caption.</summary>
    private const float CAPTION_GAP = 4f;

    /// <summary>Fraction of the column the figure spans at most. Height follows the aspect ratio.</summary>
    public float Span { get; init; } = 1f;

    /// <summary>Source rectangle, for pulling one frame out of a sheet. Null uses the whole texture.</summary>
    public Rectangle? Frame { get; init; }

    /// <summary>Draws a hairline border and a paper-coloured mat around the plate.</summary>
    public bool Framed { get; init; } = true;

    /// <summary>Padding between the border and the image, when <see cref="Framed" /> is set.</summary>
    public float Matting { get; init; } = 4f;

    public Color Tint { get; init; } = Color.White;

    /// <summary>Style the caption is set in. Independent of the block's own style, which sizes the plate.</summary>
    public BlockStyle CaptionStyle { get; init; } = BlockStyle.Caption;

    private List<BookTextLine>? captionLines;
    private float captionWidth = float.NaN;

    private Rectangle SourceFrame => Frame ?? texture.Value.Bounds;

    protected override float MeasureCore(float width)
    {
        float height = PlateSize(width).Y;

        if (Framed)
            height += Matting * 2f;

        if (!string.IsNullOrEmpty(caption))
        {
            var lines = CaptionLines(width);
            height += CAPTION_GAP + BookText.MeasureHeight(lines, CaptionStyle.Face, CaptionStyle.Scale, CaptionStyle.LineSpacing);
        }

        return height;
    }

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        Vector2 plate = PlateSize(inner.Width);

        float outerW = plate.X + (Framed ? Matting * 2f : 0f);
        float x = inner.X + AlignOffset(inner.Width, outerW);
        float y = inner.Y;

        if (Framed)
        {
            var border = new Rectangle((int)x, (int)y, (int)outerW, (int)(plate.Y + Matting * 2f));
            DrawBorder(spriteBatch, border, Style.Color * 0.35f);
            x += Matting;
            y += Matting;
        }

        // Pixel art has to come off the page's linear sampler or it smears.
        BookBatch.Switch(spriteBatch, SamplerState.PointClamp, context.Transform);

        Rectangle source = SourceFrame;

        spriteBatch.Draw(
            texture.Value,
            new Rectangle((int)x, (int)y, (int)plate.X, (int)plate.Y),
            source,
            Tint);

        BookBatch.Switch(spriteBatch, SamplerState.LinearClamp, context.Transform);

        if (string.IsNullOrEmpty(caption))
            return;

        float captionY = inner.Y + plate.Y + (Framed ? Matting * 2f : 0f) + CAPTION_GAP;
        float advance = BookText.LineHeight(CaptionStyle.Face, CaptionStyle.Scale) + CaptionStyle.LineSpacing;

        foreach (BookTextLine line in CaptionLines(inner.Width))
        {
            BookText.DrawLine(
                spriteBatch,
                line,
                CaptionStyle.Face,
                new Vector2(inner.X, captionY),
                CaptionStyle.Color,
                CaptionStyle.Scale,
                inner.Width,
                CaptionStyle.Align);

            captionY += advance;
        }
    }

    /// <summary>Drawn size of the plate, fitted to the column without ever enlarging the source.</summary>
    private Vector2 PlateSize(float width)
    {
        Rectangle source = SourceFrame;
        float budget = width * MathHelper.Clamp(Span, 0.05f, 1f);

        if (Framed)
            budget -= Matting * 2f;

        float scale = Math.Min(1f, budget / Math.Max(1, source.Width));

        return new Vector2(source.Width * scale, source.Height * scale);
    }

    private float AlignOffset(float columnWidth, float itemWidth) => Style.Align switch
    {
        TextAlign.Center => (columnWidth - itemWidth) * 0.5f,
        TextAlign.Right  => columnWidth - itemWidth,
        _                => 0f,
    };

    private List<BookTextLine> CaptionLines(float width)
    {
        if (captionLines is not null && Math.Abs(captionWidth - width) < 0.5f)
            return captionLines;

        captionLines = BookText.Layout(caption, CaptionStyle.Face, width, CaptionStyle.Scale, CaptionStyle.Color);
        captionWidth = width;

        return captionLines;
    }

    internal static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }
}
