using ReLogic.Graphics;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// Text that reads as lit rather than printed: a dark outline for legibility, a second outline blown out
/// additively around it, and a core that lerps between two colours.
///
/// <para>
/// Exp is meant to feel like something the world hands you, not a number in a ledger, so every surface
/// that reports it - the floating popup and the act tracker alike - draws through here rather than each
/// inventing its own shine.
/// </para>
/// </summary>
public static class GlowText
{
    /// <summary>Copies making up the additive halo. Eight reads as a ring; fewer shows its corners.</summary>
    private const int GlowSamples = 8;

    /// <summary>Offsets of the solid outline, in text-space pixels before scale.</summary>
    private static readonly Vector2[] OutlineOffsets =
    [
        new(-1f, 0f), new(1f, 0f), new(0f, -1f), new(0f, 1f),
        new(-1f, -1f), new(1f, -1f), new(-1f, 1f), new(1f, 1f)
    ];

    /// <summary>
    /// Draws one line of glowing text centred on <paramref name="position"/>.
    /// </summary>
    /// <param name="pulse">
    /// 0 at rest, 1 at the peak of a flourish. Drives how far the halo spreads and how far the core is
    /// pushed toward <paramref name="glow"/>, so one number animates the whole effect.
    /// </param>
    public static void Draw(
        SpriteBatch spriteBatch,
        DynamicSpriteFont font,
        string text,
        Vector2 position,
        Color core,
        Color glow,
        float scale,
        float alpha,
        float pulse = 0f)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0f || scale <= 0f)
            return;

        Vector2 origin = font.MeasureString(text) * 0.5f;

        // Halo first so the solid text sits on top of it rather than being washed out by it.
        if (pulse > 0f)
            DrawHalo(spriteBatch, font, text, position, origin, glow, scale, alpha, pulse);

        foreach (Vector2 offset in OutlineOffsets)
        {
            spriteBatch.DrawString(font, text, position + offset * scale, Color.Black * alpha,
                0f, origin, scale, SpriteEffects.None, 0f);
        }

        // The core rides the pulse toward the glow colour, so a flourish reads as the text heating up
        // rather than as a separate layer switching on behind it.
        Color lit = Color.Lerp(core, glow, pulse);

        spriteBatch.DrawString(font, text, position, lit * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// The additive ring. Runs in its own blend pass, which means ending the caller's batch and starting
    /// it again afterwards - interface layers all open with the same parameters, so restoring them here
    /// is safe and saves every caller from threading its batch state through.
    /// </summary>
    private static void DrawHalo(
        SpriteBatch spriteBatch, DynamicSpriteFont font, string text,
        Vector2 position, Vector2 origin, Color glow, float scale, float alpha, float pulse)
    {
        Restart(spriteBatch, BlendState.Additive);

        float spread = MathHelper.Lerp(1f, 3.5f, pulse) * scale;
        Color halo = glow * (0.36f * pulse * alpha);

        for (int i = 0; i < GlowSamples; i++)
        {
            float angle = i / (float)GlowSamples * MathHelper.TwoPi;
            Vector2 offset = angle.ToRotationVector2() * spread;

            spriteBatch.DrawString(font, text, position + offset, halo, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        Restart(spriteBatch, BlendState.AlphaBlend);
    }

    /// <summary>
    /// Swaps the blend state of an in-flight interface batch. Internal so other UI can borrow it rather
    /// than each re-deriving the Begin parameters an interface layer opens with.
    /// </summary>
    internal static void Restart(SpriteBatch spriteBatch, BlendState blend)
    {
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, blend, Main.SamplerStateForCursor,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }
}
