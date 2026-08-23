using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     An optional label followed by a texture, laid out inline with surrounding text.
/// </summary>
/// <remarks>
///     <para>
///         Terraria measures and draws a line by walking its snippets, so a snippet that reports an
///         honest <c>size</c> is spaced around correctly by the tooltip box, chat wrapping and sign
///         text alike. That is why this hangs off the snippet system rather than a
///         <c>ModifyTooltips</c> pass: a GlobalItem could only rewrite strings, and the tooltip box
///         would still be measured as if the icon were not there.
///     </para>
///     <para>
///         Text is drawn on every pass, icons only on coloured ones. Terraria produces the drop
///         shadow by re-drawing the whole line at offsets in <see cref="Color.Black" />, so letting
///         the label through on those passes gives it the same shadow as ordinary text for free,
///         while an icon drawn on them would stamp four black silhouettes behind itself.
///     </para>
/// </remarks>
public sealed class InlineTextureSnippet : TextSnippet
{
    /// <summary>
    ///     Longest edge an inline icon is scaled to fit, in pixels. Sized against the tooltip
    ///     font's line height so a 32x32 buff icon sits on the line instead of straddling two.
    /// </summary>
    private const float ICON_FIT = 22f;

    /// <summary>Gap between an icon and its label.</summary>
    private const float LABEL_GAP = 4f;

    private readonly Asset<Texture2D>? texture;
    private readonly Rectangle? frame;
    private readonly string? label;
    private readonly Color labelColor;
    private readonly float iconScale;

    public InlineTextureSnippet(
        Asset<Texture2D>? texture,
        string rawTag,
        Color labelColor,
        string? label = null,
        Rectangle? frame = null,
        float iconScale = 1f) : base(rawTag, labelColor)
    {
        this.texture = texture;
        this.frame = frame;
        this.label = label;
        this.labelColor = labelColor;
        this.iconScale = iconScale;

        // Atomic: wrapping or selection should never split the icon away from its label.
        DeleteWhole = true;
    }

    public override void OnHover() { }

    public override bool UniqueDraw(
        bool justCheckingString,
        out Vector2 size,
        SpriteBatch spriteBatch,
        Vector2 position = default,
        Color color = default,
        float scale = 1f)
    {
        var font = FontAssets.MouseText.Value;

        Vector2 iconSize = Vector2.Zero;
        Rectangle source = default;
        float drawScale = 0f;

        if (texture is { Value: { } value })
        {
            source = frame ?? value.Bounds;

            // Fit the longest edge, so a wide sprite cannot balloon the line the way normalising
            // on height alone would.
            float longest = Math.Max(Math.Max(source.Width, source.Height), 1);

            drawScale = ICON_FIT / longest * iconScale * scale;
            iconSize = source.Size() * drawScale;
        }

        Vector2 labelSize = string.IsNullOrEmpty(label)
            ? Vector2.Zero
            : font.MeasureString(label) * scale;

        float gap = iconSize.X > 0f && labelSize.X > 0f ? LABEL_GAP * scale : 0f;

        size = new Vector2(labelSize.X + gap + iconSize.X, Math.Max(labelSize.Y, iconSize.Y));

        if (justCheckingString)
            return true;

        // Shadow passes arrive with their colour channels zeroed, but they still carry the
        // tooltip's own alpha -- so this cannot be an equality test against Color.Black. Getting
        // that wrong sends every shadow pass down the coloured branch, and the label and icon get
        // stamped five times at 2px offsets instead of once.
        bool shadowPass = color.R == 0 && color.G == 0 && color.B == 0;

        if (labelSize.X > 0f)
        {
            Color drawColor = shadowPass ? color : labelColor;
            Vector2 labelPosition = position + new Vector2(0f, (size.Y - labelSize.Y) * 0.5f);

            spriteBatch.DrawString(font, label, labelPosition, drawColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        if (iconSize.X > 0f && !shadowPass)
        {
            // Centred on the line rather than sat on the baseline: the icon is taller than the
            // glyphs, and aligning their tops would leave it hanging below the text.
            Vector2 iconCenter = position + new Vector2(labelSize.X + gap + iconSize.X * 0.5f, size.Y * 0.325f);

            spriteBatch.Draw(texture!.Value, iconCenter, source, Color.White * (color.A / 255f), 0f, source.Size() * 0.5f, drawScale, SpriteEffects.None, 0f);
        }

        return true;
    }
}
