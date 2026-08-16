using System;
using ReLogic.Content;
using Terraria.GameContent;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// The five-pip star meter that reads as "potency N", shared by the Almanac's entry pages,
/// its recipe rows, and the alchemy station's solute slots, so one solute reads the same
/// wherever the player meets it.
/// <para>Uses the vanilla bestiary rank stars: bright = filled, dim = empty.</para>
/// </summary>
internal static class PotencyMeter
{
    /// <summary>Pips drawn. Potency is clamped to this.</summary>
    public const int PIPS = 5;

    private static Asset<Texture2D>? _starLight;
    private static Asset<Texture2D>? _starDim;

    /// <summary>One pip's texture. Public so callers laying out their own meter share the assets.</summary>
    public static Texture2D Star(bool filled)
    {
        if (filled)
        {
            _starLight ??= Main.Assets.Request<Texture2D>(
                "Images/UI/Bestiary/Icon_Rank_Light", AssetRequestMode.ImmediateLoad);
            return _starLight.Value;
        }
        _starDim ??= Main.Assets.Request<Texture2D>(
            "Images/UI/Bestiary/Icon_Rank_Dim", AssetRequestMode.ImmediateLoad);
        return _starDim.Value;
    }

    /// <summary>
    /// Draws the meter immediately after a tooltip line's own text, for the "Potency ****" rows on
    /// brewed potions and alchemy ingredients. Left-anchored off the label rather than centred like
    /// <see cref="Draw"/>, and unlike it draws the empty pips at potency 0 so an explicitly-unrated
    /// entry still reads as rated-zero rather than as a missing row.
    /// </summary>
    public static void DrawAfterTooltipLabel(DrawableTooltipLine line, int potency, float pipHeight = 14f)
    {
        var sample = Star(true);
        if (sample == null) return;

        float labelWidth = FontAssets.MouseText.Value.MeasureString(line.Text).X;
        float scale = pipHeight / sample.Height;
        float w = sample.Width * scale;
        float x = line.X + labelWidth + 6f;
        float y = line.Y + 6f;

        for (int i = 0; i < PIPS; i++)
            Main.spriteBatch.Draw(Star(i < potency), new Vector2(x + i * (w + 2f), y), null,
                Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Total width of a meter at the given pip height, for centring against an icon.</summary>
    public static float Width(float pipHeight, float gap = 1f)
    {
        var sample = Star(true);
        if (sample == null) return 0f;

        float w = sample.Width * (pipHeight / sample.Height);
        return PIPS * w + (PIPS - 1) * gap;
    }

    /// <summary>
    /// Draws the meter with <paramref name="potency"/> pips filled, horizontally centred on
    /// <paramref name="centerX"/> with its top edge at <paramref name="y"/>. Draws nothing at
    /// potency 0, so callers can pass an unrated value straight through.
    /// </summary>
    public static void Draw(SpriteBatch sb, int potency, float centerX, float y,
                            float pipHeight = 8f, float gap = 1f, Color? tint = null)
    {
        if (potency <= 0) return;
        potency = Math.Clamp(potency, 0, PIPS);

        var sample = Star(true);
        if (sample == null) return;

        float scale = pipHeight / sample.Height;
        float w = sample.Width * scale;
        float x = centerX - (PIPS * w + (PIPS - 1) * gap) * 0.5f;
        Color color = tint ?? Color.White;

        for (int i = 0; i < PIPS; i++)
            sb.Draw(Star(i < potency), new Vector2(x + i * (w + gap), y), null, color,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
