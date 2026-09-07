using System.Collections.Generic;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     A block made of rows, each a run of icons and text laid out left to right on one text line.
/// </summary>
/// <remarks>
///     Icons are the same <see cref="InlineTextureSnippet" /> the <c>[buff]</c> and <c>[chem]</c>
///     tags draw, so an icon in a block sits exactly where the same icon would sit in a line of
///     text beside it. Text is drawn at the row's top, as vanilla draws a line, in the line's
///     colour unless a cell asks for its own.
/// </remarks>
public sealed class TooltipRowsBlock : TooltipBlock
{
    private readonly List<TooltipRow> rows = [];

    public TooltipRowsBlock(TooltipAnchor anchor, string fallbackText)
    {
        Anchor = anchor;
        FallbackText = fallbackText;
    }

    public override TooltipAnchor Anchor { get; }

    public override string FallbackText { get; }

    public override int LineCount => Math.Max(1, rows.Count);

    /// <summary>Starts a new row beneath the last.</summary>
    public TooltipRow Row()
    {
        var row = new TooltipRow();
        rows.Add(row);
        return row;
    }

    public override float MeasureWidth(DynamicSpriteFont font, float scale)
    {
        float width = 0f;

        foreach (TooltipRow row in rows)
            width = Math.Max(width, row.Measure(font, scale));

        return width;
    }

    public override void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale)
    {
        float rowHeight = font.LineSpacing * scale;

        for (int i = 0; i < rows.Count; i++)
            rows[i].Draw(spriteBatch, font, position + new Vector2(0f, i * rowHeight), color, scale);
    }
}

/// <summary>
///     One line of a <see cref="TooltipRowsBlock" />. Calls chain:
///     <c>block.Row().Icon(medallion).Text("12/sec", tint)</c>.
/// </summary>
public sealed class TooltipRow
{
    /// <summary>Space between neighbouring cells.</summary>
    private const float CELL_GAP = 4f;

    private readonly List<Cell> cells = [];

    /// <summary>An icon fitted to the line height. Nothing when <paramref name="texture" /> is null.</summary>
    public TooltipRow Icon(Asset<Texture2D>? texture, float iconScale = 1f)
    {
        if (texture is not null)
            cells.Add(new IconCell(texture, iconScale));

        return this;
    }

    /// <summary>An icon by asset path; nothing when no such asset exists.</summary>
    public TooltipRow Icon(string assetPath, float iconScale = 1f)
        => Icon(ModContent.HasAsset(assetPath) ? ModContent.Request<Texture2D>(assetPath) : null, iconScale);

    /// <summary>Text in <paramref name="color" />, or in the line's own colour when null.</summary>
    public TooltipRow Text(string text, Color? color = null)
    {
        if (!string.IsNullOrEmpty(text))
            cells.Add(new TextCell(text, color));

        return this;
    }

    /// <summary>Extra horizontal space, on top of the gap every cell already gets.</summary>
    public TooltipRow Gap(float pixels)
    {
        if (pixels > 0f)
            cells.Add(new GapCell(pixels));

        return this;
    }

    internal float Measure(DynamicSpriteFont font, float scale)
    {
        float width = 0f;

        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                width += CELL_GAP * scale;

            width += cells[i].Measure(font, scale);
        }

        return width;
    }

    internal void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale)
    {
        float x = 0f;

        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                x += CELL_GAP * scale;

            cells[i].Draw(spriteBatch, font, position + new Vector2(x, 0f), color, scale);
            x += cells[i].Measure(font, scale);
        }
    }

    private abstract class Cell
    {
        public abstract float Measure(DynamicSpriteFont font, float scale);

        public abstract void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale);
    }

    private sealed class IconCell : Cell
    {
        private readonly InlineTextureSnippet snippet;

        public IconCell(Asset<Texture2D> texture, float iconScale)
            => snippet = new InlineTextureSnippet(texture, string.Empty, Color.White, iconScale: iconScale);

        public override float Measure(DynamicSpriteFont font, float scale)
        {
            snippet.UniqueDraw(true, out Vector2 size, null!, default, default, scale);
            return size.X;
        }

        public override void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale)
        {
            // The snippet centres its icon inside the height it reports. Beside a label that height
            // is the font's line height; alone it is just the icon, which would ride ~3px higher
            // than the same icon does inline. Giving back the difference lines the two up.
            snippet.UniqueDraw(true, out Vector2 size, null!, default, default, scale);

            float toLineHeight = (font.LineSpacing * scale - size.Y) * 0.325f;

            // The snippet already sits out the shadow passes on its own.
            snippet.UniqueDraw(false, out _, spriteBatch, position + new Vector2(0f, toLineHeight), color, scale);
        }
    }

    private sealed class TextCell(string text, Color? color) : Cell
    {
        public override float Measure(DynamicSpriteFont font, float scale)
            => font.MeasureString(text).X * scale;

        public override void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color lineColor, float scale)
        {
            // A shadow pass keeps its colour so the text is silhouetted like every other line's. A
            // colour of our own is pulsed here, because Terraria only pulsed the line's.
            Color drawColor = color is { } own && !TooltipBlock.IsShadowPass(lineColor)
                ? ChatManager.WaveColor(own)
                : lineColor;

            spriteBatch.DrawString(font, text, position, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private sealed class GapCell(float pixels) : Cell
    {
        public override float Measure(DynamicSpriteFont font, float scale) => pixels * scale;

        public override void Draw(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 position, Color color, float scale) { }
    }
}
