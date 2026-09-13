using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Flash animation for one station potency meter, drawn around its filled pips by <see cref="PotencyMeter.Draw" />. A pip flashes
/// when it fills and every <see cref="SWEEP_PERIOD" /> seconds, delayed by screen x. All layers draw white masks built from the
/// star texture's alpha; only the blurred glow extends past the star's outline.
/// </summary>
internal sealed class PotencyFlash
{
    private const float FLASH_SECONDS = 0.6f;
    private const float SWEEP_PERIOD = 4f;

    /// <summary>Screen pixels per second.</summary>
    private const float SWEEP_SPEED = 150f;

    private const float SWEEP_STRENGTH = 0.65f;
    private const float FILL_STAGGER_SECONDS = 0.07f;

    /// <summary>Flash progress at which the white mask peaks, then the progress span it takes to fade back to the texture.</summary>
    private const float WHITE_RISE = 0.08f;
    private const float WHITE_FADE = 0.42f;

    private const float SHEEN_START = 0.3f;

    /// <summary>Texels the sheen band shifts left per texel down.</summary>
    private const float SHEEN_SLOPE = 0.5f;

    private const int GLOW_PADDING = 4;
    private const int GLOW_BLUR_RADIUS = 2;

    private static Texture2D? _mask;
    private static Texture2D? _glowMask;

    private readonly float[] _fillStartTimes = new float[PotencyMeter.PIPS];
    private int _potency = -1;

    public PotencyFlash() => Array.Fill(_fillStartTimes, float.NaN);

    /// <summary>
    /// Starts staggered flashes for the pips above the last observed potency; the first call only records it.
    /// <paramref name="potency" /> must be within 0..<see cref="PotencyMeter.PIPS" />.
    /// </summary>
    public void Observe(int potency)
    {
        if (_potency >= 0)
        {
            float now = Main.GlobalTimeWrappedHourly;

            for (int i = _potency; i < potency; i++)
                _fillStartTimes[i] = now + (i - _potency) * FILL_STAGGER_SECONDS;
        }

        _potency = potency;
    }

    /// <summary>Layers drawn before pip <paramref name="pip" />'s star. <paramref name="position" /> is the star's top-left.</summary>
    public void DrawUnder(SpriteBatch sb, int pip, Vector2 position, float scale, Color color) =>
        DrawActive(sb, pip, position, scale, color, under: true);

    /// <summary>Layers drawn after pip <paramref name="pip" />'s star. <paramref name="position" /> is the star's top-left.</summary>
    public void DrawOver(SpriteBatch sb, int pip, Vector2 position, float scale, Color color) =>
        DrawActive(sb, pip, position, scale, color, under: false);

    /// <summary>Disposes the generated masks on the main thread.</summary>
    public static void Unload()
    {
        Texture2D? mask = _mask;
        Texture2D? glowMask = _glowMask;
        _mask = _glowMask = null;

        if (mask == null && glowMask == null)
            return;

        Main.QueueMainThreadAction(() =>
        {
            mask?.Dispose();
            glowMask?.Dispose();
        });
    }

    private void DrawActive(SpriteBatch sb, int pip, Vector2 position, float scale, Color color, bool under)
    {
        Texture2D star = PotencyMeter.Star(true);
        float fill = FillProgress(pip);
        float sweep = SweepProgress(position.X + star.Width * scale * 0.5f);

        if (fill < 0f && sweep < 0f)
            return;

        var (mask, glowMask) = Masks(star);

        if (under)
        {
            if (fill >= 0f)
                DrawGlow(sb, glowMask, position, scale, color, fill, strength: 1f);

            if (sweep >= 0f)
                DrawGlow(sb, glowMask, position, scale, color, sweep, SWEEP_STRENGTH);
        }
        else
        {
            if (fill >= 0f)
                DrawFlash(sb, mask, position, scale, color, fill, strength: 1f);

            if (sweep >= 0f)
                DrawFlash(sb, mask, position, scale, color, sweep, SWEEP_STRENGTH);
        }
    }

    /// <summary>0..1 progress through pip <paramref name="pip" />'s fill flash; -1 before it starts and after it ends.</summary>
    private float FillProgress(int pip)
    {
        float start = _fillStartTimes[pip];

        if (float.IsNaN(start))
            return -1f;

        float elapsed = Main.GlobalTimeWrappedHourly - start;

        // GlobalTimeWrappedHourly wraps to 0 every 3600 seconds.
        if (elapsed < -1800f)
            elapsed += 3600f;

        if (elapsed > FLASH_SECONDS)
            _fillStartTimes[pip] = float.NaN;

        return elapsed is >= 0f and <= FLASH_SECONDS ? elapsed / FLASH_SECONDS : -1f;
    }

    /// <summary>0..1 progress of the periodic flash at screen x <paramref name="x" />; -1 between flashes.</summary>
    private static float SweepProgress(float x)
    {
        float phase = (Main.GlobalTimeWrappedHourly - x / SWEEP_SPEED) % SWEEP_PERIOD;

        if (phase < 0f)
            phase += SWEEP_PERIOD;

        return phase < FLASH_SECONDS ? phase / FLASH_SECONDS : -1f;
    }

    /// <summary>Blurred mask behind the star, additive, fading out over the flash.</summary>
    private static void DrawGlow(SpriteBatch sb, Texture2D glowMask, Vector2 position, float scale, Color color, float progress, float strength)
    {
        float fade = 1f - progress;

        sb.Draw(glowMask, position - new Vector2(GLOW_PADDING * scale), null, Additive(color, 0.7f * strength * fade * fade),
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// The mask in white fading back to the star texture, a diagonal white sheen crossing it, and an additive copy expanding from
    /// its centre. Zero-alpha colors add in the premultiplied AlphaBlend UI batch.
    /// </summary>
    private static void DrawFlash(SpriteBatch sb, Texture2D mask, Vector2 position, float scale, Color color, float progress, float strength)
    {
        float white = progress < WHITE_RISE
            ? progress / WHITE_RISE
            : EaseFunction.EaseQuadIn.Ease(Math.Max(0f, 1f - (progress - WHITE_RISE) / WHITE_FADE));

        if (white > 0f)
            sb.Draw(mask, position, null, Color.White * (strength * white), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        DrawSheen(sb, mask, position, scale, progress, strength);

        float pulse = EaseFunction.EaseQuadOut.Ease(progress);
        Vector2 origin = mask.Size() * 0.5f;

        sb.Draw(mask, position + origin * scale, null, Additive(color, 0.5f * strength * (1f - pulse)), 0f, origin,
            scale * (1f + 0.6f * pulse), SpriteEffects.None, 0f);
    }

    /// <summary>Slanted band of the mask crossing the star left to right, drawn in row slices at least one screen pixel tall.</summary>
    private static void DrawSheen(SpriteBatch sb, Texture2D mask, Vector2 position, float scale, float progress, float strength)
    {
        float t = (progress - SHEEN_START) / (1f - SHEEN_START);

        if (t is <= 0f or >= 1f)
            return;

        float halfWidth = mask.Width * 0.15f;
        float topCenter = -halfWidth + t * (mask.Width + halfWidth * 2f + mask.Height * SHEEN_SLOPE);
        int slice = Math.Max(1, (int)MathF.Ceiling(1f / scale));
        Color color = Color.White * (0.8f * strength * MathF.Sin(t * MathF.PI));

        for (int row = 0; row < mask.Height; row += slice)
        {
            int height = Math.Min(slice, mask.Height - row);
            float center = topCenter - (row + height * 0.5f) * SHEEN_SLOPE;
            int left = Math.Clamp((int)MathF.Round(center - halfWidth), 0, mask.Width);
            int right = Math.Clamp((int)MathF.Round(center + halfWidth), 0, mask.Width);

            if (right > left)
            {
                sb.Draw(mask, position + new Vector2(left, row) * scale, new Rectangle(left, row, right - left, height), color,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>The star's white mask and its padded, blurred glow mask, built on first use.</summary>
    private static (Texture2D Mask, Texture2D Glow) Masks(Texture2D star)
    {
        if (_mask == null || _glowMask == null)
        {
            var pixels = new Color[star.Width * star.Height];
            star.GetData(pixels);

            _mask = CreateMask(pixels, star.Width, star.Height, padding: 0, blurRadius: 0);
            _glowMask = CreateMask(pixels, star.Width, star.Height, GLOW_PADDING, GLOW_BLUR_RADIUS);
        }

        return (_mask, _glowMask);
    }

    /// <summary>
    /// Premultiplied white texture carrying <paramref name="source" />'s alpha, padded on every side and, when
    /// <paramref name="blurRadius" /> is above 0, box-blurred twice per axis.
    /// </summary>
    private static Texture2D CreateMask(Color[] source, int width, int height, int padding, int blurRadius)
    {
        int maskWidth = width + padding * 2;
        int maskHeight = height + padding * 2;
        var alpha = new float[maskWidth * maskHeight];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                alpha[(y + padding) * maskWidth + x + padding] = source[y * width + x].A / 255f;
        }

        for (int pass = 0; pass < 2 && blurRadius > 0; pass++)
        {
            alpha = BoxBlur(alpha, maskWidth, maskHeight, blurRadius, horizontal: true);
            alpha = BoxBlur(alpha, maskWidth, maskHeight, blurRadius, horizontal: false);
        }

        var pixels = new Color[alpha.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = (byte)(Math.Clamp(alpha[i], 0f, 1f) * 255f);
            pixels[i] = new Color(a, a, a, a);
        }

        var mask = new Texture2D(Main.graphics.GraphicsDevice, maskWidth, maskHeight);
        mask.SetData(pixels);

        return mask;
    }

    /// <summary>One box-blur pass along rows or columns. Samples outside the texture count as 0.</summary>
    private static float[] BoxBlur(float[] values, int width, int height, int radius, bool horizontal)
    {
        var result = new float[values.Length];
        int length = horizontal ? width : height;
        int lines = horizontal ? height : width;

        for (int line = 0; line < lines; line++)
        {
            for (int i = 0; i < length; i++)
            {
                float sum = 0f;

                for (int k = Math.Max(0, i - radius); k <= Math.Min(length - 1, i + radius); k++)
                    sum += values[horizontal ? line * width + k : k * width + line];

                result[horizontal ? line * width + i : i * width + line] = sum / (radius * 2 + 1);
            }
        }

        return result;
    }

    private static Color Additive(Color color, float opacity) => (color * opacity) with { A = 0 };
}
