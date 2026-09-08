using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Content.Alchemy.Solutions;

namespace TrelamiumIII.Content.Alchemy.AlchemyStations;

/// <summary>
/// Procedural fluid for the Advanced Alchemy Station chamber. Replaces the flat tinted overlay while
/// keeping its composite-space anchor: the <c>ChamberLiquidOverlay</c> mask (a 48x50 capsule silhouette)
/// is reused as the liquid's <i>shape</i>, and every layer is drawn FROM that mask so it self-clips to the
/// glass. The body is tinted by the loaded vial's average sprite
/// colour; while brewing the hue drifts slowly, turbulence bands rise through the fluid, and the surface
/// sloshes, with intensity scaling by brew progress (see <see cref="AdvancedAlchemyStationEntity.BrewProgress"/>).
/// </summary>
internal static class AdvancedAlchemyStationLiquid
{
    /// <summary>Fraction of the capsule filled at rest, leaving a little headspace like the source art.</summary>
    private const float RestFill = 0.88f;

    /// <summary>How far the fluid's lighting is lifted toward white so it isn't crushed dark (0 = lit normally, 1 = full bright).</summary>
    private const float BrightnessBoost = 0.5f;

    /// <summary>Fallback tint when a vial's sprite can't be sampled (atlas/unreadable) or averages to nothing.</summary>
    private static readonly Color NeutralFluid = new(150, 180, 200);

    private static readonly Dictionary<int, Color> _avgColorCache = new();

    /// <summary>
    /// Per-item fluid colours: listed items render as their actual liquid; everything else falls back to the
    /// item's average sprite colour. Brine and oil are sampled from their liquid sprites; water and honey use
    /// representative vanilla tints. Built lazily because modded item ids resolve only after content loads.
    /// </summary>
    private static Dictionary<int, Color>? _liquidColors;
    private static Dictionary<int, Color> LiquidColors => _liquidColors ??= new()
    {
        [ItemID.BottledWater] = new Color(70, 130, 220),
        [ItemID.BottledHoney] = new Color(255, 160, 30),
        [Solution.Brine] = new Color(70, 150, 190),
        [Solution.BottledOil] = new Color(34, 35, 35),
    };

    private static readonly Color SaltTint = new(220, 225, 235);

    /// <summary>
    /// Draws the chamber fluid. <paramref name="baseTopLeft"/> is the mask's unrocked top-left in screen space
    /// (composite origin + ChamberLiquidOffset); <paramref name="motion"/> rocks/vibrates every layer in step
    /// with the glass. <paramref name="light"/> is the tile's lighting; <paramref name="progress"/> is 0..1
    /// brew progress, only meaningful while <paramref name="brewing"/>.
    /// </summary>
    public static void Draw(SpriteBatch sb, Texture2D mask, Vector2 baseTopLeft, ChamberMotion motion, int vialType, Color light, bool brewing, float progress)
    {
        float t = Main.GlobalTimeWrappedHourly;
        int w = mask.Width;
        int h = mask.Height;
        bool isBrine = vialType > 0 && vialType == Solution.Brine;

        Color content = ContentColor(vialType);

        // Colour: slow hue drift while brewing, so the fluid "reacts" over the brew.
        Color baseColor = content;
        if (brewing)
        {
            Vector3 hsl = Main.rgbToHsl(content);
            float wobble = (float)Math.Sin(t * 0.8f);
            float hue = (hsl.X + 0.05f * wobble + 1f) % 1f;
            Color drifted = Main.hslToRgb(hue, hsl.Y, MathHelper.Clamp(hsl.Z * 1.05f, 0f, 1f));
            baseColor = Color.Lerp(content, drifted, 0.5f + 0.5f * wobble);
        }

        // Fill level: a tiny resting bob, larger while brewing.
        float bobAmp = brewing ? 0.018f + 0.03f * progress : 0.012f;
        float bobSpeed = brewing ? 2.4f : 1.4f;
        float fill = MathHelper.Clamp(RestFill + bobAmp * (float)Math.Sin(t * bobSpeed), 0.05f, 1f);
        int topY = (int)Math.Round((1f - fill) * h);
        int bodyH = h - topY;
        if (bodyH <= 0)
            return;

        // Brighten: multiplying straight by tile light crushes the fluid too dark, so lift the effective
        // lighting toward white. Keeps the per-item hue but stops caves/night from washing it to near-black.
        Color glow = Color.Lerp(light, Color.White, BrightnessBoost);

        // Body: the bottom slice of the mask, self-clipped to the rounded capsule. The mask's own internal
        // shading survives the colour multiply, lending the fluid some volume.
        Color body = Mul(baseColor, glow);
        sb.Draw(mask, motion.Place(baseTopLeft + new Vector2(0f, topY)), new Rectangle(0, topY, w, bodyH),
            body, motion.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // Internal turbulence: a couple of lighter bands rising and swaying through the fluid.
        if (bodyH > 6)
        {
            Color sheen = Mul(Lighten(baseColor, 0.35f), glow);
            float strength = brewing ? 0.5f + 0.5f * MathHelper.Clamp(progress, 0f, 1f) : 0.18f;
            for (int k = 0; k < 2; k++)
            {
                float sway = (brewing ? 1.6f : 0.7f) * (float)Math.Sin(t * (2f + k) + k * 0.5f);
                float scroll = (t * (brewing ? 9f : 4f) + k * 17f) % bodyH;
                int by = topY + (int)(bodyH - scroll);
                if (by < topY) by = topY;
                int bandH = Math.Min(3, h - by);
                if (bandH <= 0) continue;
                sb.Draw(mask, motion.Place(baseTopLeft + new Vector2(sway, by)), new Rectangle(0, by, w, bandH),
                    sheen * (0.22f * strength), motion.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }

        // Surface: a brighter meniscus that sloshes side to side (two phases for a wavy read).
        int surfH = Math.Min(3, bodyH);
        if (surfH > 0)
        {
            Color surf = Mul(Lighten(baseColor, 0.55f), glow);
            float slosh = brewing ? 1.4f : 0.6f;
            var surfSrc = new Rectangle(0, topY, w, surfH);
            sb.Draw(mask, motion.Place(baseTopLeft + new Vector2((float)Math.Sin(t * 2.7f) * slosh, topY)), surfSrc,
                surf * 0.7f, motion.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(mask, motion.Place(baseTopLeft + new Vector2((float)Math.Sin(t * 3.9f + 1f) * slosh, topY)), surfSrc,
                surf * 0.45f, motion.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        // Brine: extra fine "salt" bands on top of the water body. Faster scroll and tighter (1px) bands
        // than the regular turbulence so the read is "suspended particles drifting" rather than "currents".
        // World brine uses an actual scrolling FBM noise overlay; the chamber can't easily clip a noise
        // sample to the capsule mask without a render target, so we approximate the same drift with
        // mask-clipped bands sharing the world render's color palette.
        if (isBrine && bodyH > 4)
        {
            Color salt = Mul(SaltTint, glow);
            int bands = brewing ? 4 : 3;
            for (int k = 0; k < bands; k++)
            {
                float sway = (brewing ? 1.4f : 0.9f) * (float)Math.Sin(t * (5f + k * 1.3f) + k * 0.7f);
                float scroll = (t * (brewing ? 14f : 9f) + k * 11.3f) % bodyH;
                int by = topY + (int)(bodyH - scroll);
                if (by < topY) by = topY;
                int bandH = Math.Min(1, h - by);
                if (bandH <= 0) continue;
                sb.Draw(mask, motion.Place(baseTopLeft + new Vector2(sway, by)), new Rectangle(0, by, w, bandH),
                    salt * 0.28f, motion.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>Fluid colour for a vial: its recorded liquid colour if known, else its average sprite colour.</summary>
    public static Color ContentColor(int itemType)
        => itemType > 0 && LiquidColors.TryGetValue(itemType, out Color liquid)
            ? liquid
            : AverageItemColor(itemType);

    /// <summary>Alpha-weighted average colour of an item's sprite, cached per type. Neutral fallback on failure.</summary>
    public static Color AverageItemColor(int itemType)
    {
        if (itemType <= 0) return NeutralFluid;
        if (_avgColorCache.TryGetValue(itemType, out Color cached)) return cached;

        Color result = NeutralFluid;
        try
        {
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex != null)
            {
                var data = new Color[tex.Width * tex.Height];
                tex.GetData(data);
                long r = 0, g = 0, b = 0, a = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    int alpha = data[i].A;
                    if (alpha <= 16) continue;
                    r += data[i].R * alpha;
                    g += data[i].G * alpha;
                    b += data[i].B * alpha;
                    a += alpha;
                }
                if (a > 0)
                    result = new Color((byte)(r / a), (byte)(g / a), (byte)(b / a));
            }
        }
        catch
        {
            // Unreadable / atlased textures keep the neutral fluid colour.
        }

        _avgColorCache[itemType] = result;
        return result;
    }

    /// <summary>Component-wise multiply of a colour by tile lighting, preserving the source alpha.</summary>
    private static Color Mul(Color c, Color light) => new(
        (byte)(c.R * light.R / 255),
        (byte)(c.G * light.G / 255),
        (byte)(c.B * light.B / 255),
        c.A);

    /// <summary>Lerp a colour toward white by <paramref name="amt"/> (0..1), preserving alpha.</summary>
    private static Color Lighten(Color c, float amt) => new(
        (byte)(c.R + (255 - c.R) * amt),
        (byte)(c.G + (255 - c.G) * amt),
        (byte)(c.B + (255 - c.B) * amt),
        c.A);
}
