using System;
using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// Five-pip potency meter drawn with the vanilla bestiary rank stars (light = filled, dim = empty), shared by the almanac,
/// brew tooltips and station slots. Filled pips flash every <see cref="GLINT_PERIOD" /> seconds, delayed by screen x so the
/// flash travels left to right.
/// </summary>
internal static class PotencyMeter
{
    /// <summary>Pips drawn. Potency is clamped to this.</summary>
    public const int PIPS = 5;

    /// <summary>Length of one pip flash, in seconds.</summary>
    public const float FLASH_SECONDS = 0.45f;

    private const float GLINT_PERIOD = 4f;

    /// <summary>Screen pixels per second.</summary>
    private const float GLINT_SPEED = 150f;

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
            DrawPip(Main.spriteBatch, i < potency, new Vector2(x + i * (w + 2f), y), scale, Color.White);
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
    /// Draws the meter with <paramref name="potency"/> pips filled, horizontally centred on <paramref name="centerX"/> with its
    /// top edge at <paramref name="y"/>. Draws nothing at potency 0 unless <paramref name="drawEmpty"/> is set, but
    /// <paramref name="flash"/> observes the potency either way.
    /// </summary>
    public static void Draw(SpriteBatch sb, int potency, float centerX, float y,
                            float pipHeight = 8f, float gap = 1f, Color? tint = null, bool drawEmpty = false,
                            PotencyFlash? flash = null)
    {
        potency = Math.Clamp(potency, 0, PIPS);
        flash?.Observe(potency);

        if (potency <= 0 && !drawEmpty) return;

        var sample = Star(true);
        if (sample == null) return;

        float scale = pipHeight / sample.Height;
        float w = sample.Width * scale;
        float x = centerX - (PIPS * w + (PIPS - 1) * gap) * 0.5f;
        Color color = tint ?? Color.White;

        for (int i = 0; i < PIPS; i++)
            DrawPip(sb, i < potency, new Vector2(x + i * (w + gap), y), scale, color, flash?.Progress(i) ?? -1f);
    }

    /// <summary>
    /// One pip with its top-left at <paramref name="position"/>. A filled pip also flashes while the periodic glint passes it
    /// and while <paramref name="flashProgress"/> is within 0..1.
    /// </summary>
    public static void DrawPip(SpriteBatch sb, bool filled, Vector2 position, float scale, Color color, float flashProgress = -1f)
    {
        Texture2D star = Star(filled);
        sb.Draw(star, position, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        if (!filled)
            return;

        Vector2 center = position + star.Size() * (scale * 0.5f);
        float glint = GlintProgress(center.X);

        if (glint >= 0f)
            DrawFlash(sb, star, center, scale, color, glint, strength: 0.65f);

        if (flashProgress is >= 0f and <= 1f)
            DrawFlash(sb, star, center, scale, color, flashProgress, strength: 1f);
    }

    /// <summary>0..1 progress of the periodic glint at screen x <paramref name="x"/>; -1 while it is elsewhere.</summary>
    private static float GlintProgress(float x)
    {
        float phase = (Main.GlobalTimeWrappedHourly - x / GLINT_SPEED) % GLINT_PERIOD;

        if (phase < 0f)
            phase += GLINT_PERIOD;

        return phase < FLASH_SECONDS ? phase / FLASH_SECONDS : -1f;
    }

    /// <summary>
    /// Bloom, expanding pulse, white flash and a cross glint over one pip. Colors carry zero alpha, which adds in the
    /// premultiplied AlphaBlend batches used by UI layers and the almanac's render targets, so no batch is restarted.
    /// </summary>
    private static void DrawFlash(SpriteBatch sb, Texture2D star, Vector2 center, float scale, Color color, float progress, float strength)
    {
        // Rises over the first 15% of the flash, then decays.
        float intensity = strength * (progress < 0.15f
            ? progress / 0.15f
            : EaseFunction.EaseQuadIn.Ease(1f - (progress - 0.15f) / 0.85f));

        float pipSize = star.Height * scale;
        Vector2 starOrigin = star.Size() * 0.5f;

        Texture2D bloom = Assets.Textures.VFX.Bloom.Asset.Value;
        sb.Draw(bloom, center, null, Additive(color, 0.6f * intensity), 0f, bloom.Size() * 0.5f,
            pipSize * 2.8f / bloom.Height, SpriteEffects.None, 0f);

        float pulse = EaseFunction.EaseQuadOut.Ease(progress);
        sb.Draw(star, center, null, Additive(color, 0.55f * strength * (1f - pulse)), 0f, starOrigin,
            scale * (1f + 0.75f * pulse), SpriteEffects.None, 0f);

        sb.Draw(star, center, null, Additive(Color.White, 0.8f * intensity), 0f, starOrigin, scale, SpriteEffects.None, 0f);

        // Extra_98: the streak vanilla's Main.DrawPrettyStarSparkle crosses into a sparkle.
        Texture2D streak = TextureAssets.Extra[98].Value;
        Vector2 streakScale = new Vector2(0.35f * pipSize / streak.Width, 1.3f * pipSize / streak.Height) * intensity;
        Vector2 glintPosition = center - new Vector2(0.18f, 0.22f) * pipSize;
        Color glintColor = Additive(Color.White, intensity);

        sb.Draw(streak, glintPosition, null, glintColor, 0f, streak.Size() * 0.5f, streakScale, SpriteEffects.None, 0f);
        sb.Draw(streak, glintPosition, null, glintColor, MathHelper.PiOver2, streak.Size() * 0.5f, streakScale, SpriteEffects.None, 0f);
    }

    private static Color Additive(Color color, float opacity) => (color * opacity) with { A = 0 };
}

/// <summary>
/// Flash state for one meter drawn by <see cref="PotencyMeter.Draw" />: pips above the last observed potency flash in
/// sequence. The first observation only records the potency.
/// </summary>
internal sealed class PotencyFlash
{
    private const float STAGGER_SECONDS = 0.07f;

    private readonly float[] _startTimes = new float[PotencyMeter.PIPS];
    private int _potency = -1;

    public PotencyFlash() => Array.Fill(_startTimes, float.NaN);

    /// <summary><paramref name="potency" /> must be within 0..<see cref="PotencyMeter.PIPS" />.</summary>
    public void Observe(int potency)
    {
        if (_potency >= 0)
        {
            float now = Main.GlobalTimeWrappedHourly;

            for (int i = _potency; i < potency; i++)
                _startTimes[i] = now + (i - _potency) * STAGGER_SECONDS;
        }

        _potency = potency;
    }

    /// <summary>0..1 progress through pip <paramref name="pip" />'s flash; -1 before it starts and after it ends.</summary>
    public float Progress(int pip)
    {
        if (float.IsNaN(_startTimes[pip]))
            return -1f;

        float elapsed = Main.GlobalTimeWrappedHourly - _startTimes[pip];

        // GlobalTimeWrappedHourly wraps to 0 every 3600 seconds.
        if (elapsed < -1800f)
            elapsed += 3600f;

        if (elapsed > PotencyMeter.FLASH_SECONDS)
            _startTimes[pip] = float.NaN;

        return elapsed is >= 0f and <= PotencyMeter.FLASH_SECONDS ? elapsed / PotencyMeter.FLASH_SECONDS : -1f;
    }
}
