using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     A glint struck off a drifting piece of the remains.
/// </summary>
/// <remarks>
///     <para>
///         The small four-pointed sparks that orbit a fallen star on the ground. They exist to give
///         the bloom something to punctuate it: a bloom on its own reads as a static wash however
///         much it breathes, and it is the sparks appearing and dying at different moments that make
///         the piece look like it is actively shedding light.
///     </para>
///     <para>
///         Drawn only in the additive pass -- <see cref="PreDraw" /> declines the body pass outright.
///         A spark has no substance to occlude with, and drawing it once alpha-blended and once
///         additively would give it a muddy dark core.
///     </para>
/// </remarks>
public sealed class InkSparkParticle : Particle
{
    public override string[] Textures => [Assets.Textures.VFX.PixelStar.KEY];

    /// <summary>Weightless. A glint hangs in the air and goes out; it does not fall.</summary>
    public override float Gravity => 0f;

    public override float Drag => 0.94f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    /// <summary>Self-lit, so it stays a spark in an unlit arena.</summary>
    public override bool UseLighting => false;

    public override int FadeOutTicks => 16;

    public override bool HasGlow => true;

    /// <summary>Additive only. See the class remarks.</summary>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        if (opacity <= 0f)
            return;

        // Flares up over the first stretch of life and shrinks away over the rest, so a spark never
        // pops into or out of existence at full size.
        float life = Progress;
        float flare = MathF.Min(life * 5f, 1f) * (1f - life * life);
        float scale = Scale * MathHelper.Lerp(1.15f, 0.35f, life);

        spriteBatch.Draw(
            texture,
            drawPosition,
            null,
            SparkColor * (flare * opacity),
            Rotation,
            texture.Size() * 0.5f,
            scale,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>
    ///     Pale and barely tinted. The sparks are the brightest thing in the effect, and giving them
    ///     the violet of the bloom would flatten the two into one value -- the contrast between a
    ///     near-white glint and a violet wash is what gives the piece any depth.
    /// </summary>
    private static readonly Color SparkColor = new(226, 224, 255);
}
