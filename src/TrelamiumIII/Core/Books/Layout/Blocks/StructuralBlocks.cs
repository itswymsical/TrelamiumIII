using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Layout.Blocks;

/// <summary>
///     A chapter or section heading, optionally underscored by a rule.
/// </summary>
public sealed class HeadingBlock(string? text, bool rule = true) : BookBlock
{
    /// <summary>Gap between the heading's baseline and its rule.</summary>
    private const float RULE_GAP = 5f;

    private const float RULE_THICKNESS = 1f;

    private List<BookTextLine>? lines;
    private float layoutWidth = float.NaN;

    /// <summary>An unwritten heading would still draw its rule, leaving a stray line on the page.</summary>
    public override bool IsEmpty => string.IsNullOrWhiteSpace(text);

    public override bool KeepWithNext => true;

    protected override float MeasureCore(float width)
    {
        var laid = LinesFor(width);

        if (laid.Count == 0)
            return 0f;

        float height = laid.Count * (BookText.LineHeight(Style.Face, Style.Scale) + Style.LineSpacing) - Style.LineSpacing;

        return rule ? height + RULE_GAP + RULE_THICKNESS : height;
    }

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        var laid = LinesFor(inner.Width);

        float advance = BookText.LineHeight(Style.Face, Style.Scale) + Style.LineSpacing;
        float y = inner.Y;

        foreach (BookTextLine line in laid)
        {
            BookText.DrawLine(spriteBatch, line, Style.Face, new Vector2(inner.X, y), Style.Color, Style.Scale, inner.Width, Style.Align);
            y += advance;
        }

        if (!rule)
            return;

        y += RULE_GAP - Style.LineSpacing;

        spriteBatch.Draw(
            TextureAssets.MagicPixel.Value,
            new Rectangle((int)inner.X, (int)y, (int)inner.Width, (int)RULE_THICKNESS),
            Style.Color * 0.45f);
    }

    private List<BookTextLine> LinesFor(float width)
    {
        if (lines is not null && Math.Abs(layoutWidth - width) < 0.5f)
            return lines;

        lines = BookText.Layout(text, Style.Face, width, Style.Scale, Style.Color);
        layoutWidth = width;

        return lines;
    }
}

/// <summary>A horizontal divider. Reserves only its own thickness plus the style's spacing.</summary>
public sealed class RuleBlock : BookBlock
{
    /// <summary>Fraction of the column the rule spans, centred. 1 draws edge to edge.</summary>
    public float Span { get; init; } = 1f;

    public float Thickness { get; init; } = 1f;

    public float Opacity { get; init; } = 0.45f;

    protected override float MeasureCore(float width) => Thickness;

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context)
    {
        BlockRect inner = Inset(rect);
        float span = inner.Width * MathHelper.Clamp(Span, 0f, 1f);

        spriteBatch.Draw(
            TextureAssets.MagicPixel.Value,
            new Rectangle(
                (int)(inner.X + (inner.Width - span) * 0.5f),
                (int)inner.Y,
                (int)span,
                (int)Math.Max(1f, Thickness)),
            Style.Color * Opacity);
    }
}

/// <summary>Blank vertical space. Useful for pushing a title down a page or parting two figures.</summary>
public sealed class SpacerBlock(float height) : BookBlock
{
    protected override float MeasureCore(float width) => height;

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context) { }
}

/// <summary>
///     Forces everything after it onto a fresh page.
/// </summary>
/// <remarks>
///     The composer treats this as a signal rather than as content, so it occupies no height and can
///     never itself be orphaned at the top of a page.
/// </remarks>
public sealed class PageBreakBlock : BookBlock
{
    protected override float MeasureCore(float width) => 0f;

    public override void Draw(SpriteBatch spriteBatch, in BlockRect rect, BookPageContext context) { }
}
