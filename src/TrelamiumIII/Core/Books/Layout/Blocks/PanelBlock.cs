using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout.Blocks;

/// <summary>A bordered container of other blocks. Never splits; one that doesn't fit moves whole to the next page.</summary>
public sealed class PanelBlock : BookBlock
{
    /// <summary>Non-empty children, filtered once so emptiness, height and drawing all agree.</summary>
    private readonly List<BookBlock> visible;

    public PanelBlock(IReadOnlyList<BookBlock> children)
    {
        visible = [];

        foreach (BookBlock child in children)
        {
            if (!child.IsEmpty)
                visible.Add(child);
        }
    }

    public float Padding { get; init; } = 8f;

    public Color Fill { get; init; } = new(0, 0, 0, 18);

    public Color Border { get; init; } = new(120, 96, 68);

    /// <summary>Border alpha. Zero draws a fill with no outline.</summary>
    public float BorderOpacity { get; init; } = 0.5f;

    /// <summary>Empty with no visible children, so a missing aside doesn't leave a bare box.</summary>
    public override bool IsEmpty => visible.Count == 0;

    protected override float MeasureCore(float width)
    {
        float inner = Math.Max(1f, width - Padding * 2f);
        float height = Padding * 2f;

        for (int i = 0; i < visible.Count; i++)
        {
            BookBlock child = visible[i];

            if (i > 0)
                height += child.Style.SpaceBefore;

            height += child.Measure(inner);

            if (i < visible.Count - 1)
                height += child.Style.SpaceAfter;
        }

        return height;
    }

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        var box = new Rectangle((int)inner.X, (int)inner.Y, (int)inner.Width, (int)inner.Height);

        if (Fill.A > 0)
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, Fill);

        if (BorderOpacity > 0f)
            ImageBlock.DrawBorder(spriteBatch, box, Border * BorderOpacity);

        float contentWidth = Math.Max(1f, inner.Width - Padding * 2f);
        float y = inner.Y + Padding;

        for (int i = 0; i < visible.Count; i++)
        {
            BookBlock child = visible[i];

            if (i > 0)
                y += child.Style.SpaceBefore;

            float height = child.Measure(contentWidth);

            child.Draw(spriteBatch, new BlockRect(inner.X + Padding, y, contentWidth, height), context);

            y += height;

            if (i < visible.Count - 1)
                y += child.Style.SpaceAfter;
        }
    }
}

/// <summary>A sticky note: tinted paper, drop shadow, handwriting. Usually attached as a decoration, but can flow inline.</summary>
public sealed class StickyNoteBlock(string? text) : BookBlock
{
    private const float SHADOW_OFFSET = 2f;

    public float Padding { get; init; } = 7f;

    public Color Paper { get; init; } = new(232, 214, 142);

    /// <summary>A darker band along the top, where a real note would still be gummed.</summary>
    public Color GumLine { get; init; } = new(214, 194, 118);

    public float GumHeight { get; init; } = 6f;

    private List<BookTextLine>? lines;
    private float layoutWidth = float.NaN;

    /// <summary>Blank paper stuck to the page is worse than no note.</summary>
    public override bool IsEmpty => string.IsNullOrWhiteSpace(text);

    protected override float MeasureCore(float width)
    {
        var laid = LinesFor(Math.Max(1f, width - Padding * 2f));

        return BookText.MeasureHeight(laid, Style.Face, Style.Scale, Style.LineSpacing) + Padding * 2f + GumHeight;
    }

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        var pixel = TextureAssets.MagicPixel.Value;
        var box = new Rectangle((int)inner.X, (int)inner.Y, (int)inner.Width, (int)inner.Height);

        spriteBatch.Draw(
            pixel,
            new Rectangle(box.X + (int)SHADOW_OFFSET, box.Y + (int)SHADOW_OFFSET, box.Width, box.Height),
            new Color(0, 0, 0, 60));

        spriteBatch.Draw(pixel, box, Paper);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, (int)GumHeight), GumLine);

        float contentWidth = Math.Max(1f, inner.Width - Padding * 2f);
        float y = inner.Y + Padding + GumHeight;
        float advance = BookText.LineHeight(Style.Face, Style.Scale) + Style.LineSpacing;

        foreach (BookTextLine line in LinesFor(contentWidth))
        {
            BookText.DrawLine(
                spriteBatch,
                line,
                Style.Face,
                new Vector2(inner.X + Padding, y),
                Style.DrawColor,
                Style.Scale,
                contentWidth,
                Style.Align);

            y += advance;
        }
    }

    private List<BookTextLine> LinesFor(float width)
    {
        if (lines is not null && Math.Abs(layoutWidth - width) < 0.5f)
            return lines;

        lines = BookText.Layout(text, Style.Face, width, Style.Scale, Style.DrawColor);
        layoutWidth = width;

        return lines;
    }
}
