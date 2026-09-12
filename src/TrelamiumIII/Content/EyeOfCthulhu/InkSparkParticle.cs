using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>A glint struck off a drifting piece of the remains.</summary>
public sealed class InkSparkParticle : Particle
{
    public override string[] Textures => [Assets.Textures.VFX.PixelStar.KEY];

    public override float Gravity => 0f;

    public override float Drag => 0.94f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    /// <summary>Self-lit, so it stays a spark in an unlit arena.</summary>
    public override bool UseLighting => false;

    public override int FadeOutTicks => 16;

    public override bool HasGlow => true;

    /// <summary>Additive only; see <see cref="DrawGlow" />.</summary>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        if (opacity <= 0f)
            return;

        // Flares in quickly and shrinks away, so it never pops at full size.
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

    /// <summary>Pale, near-white: the bloom's violet would flatten the contrast that gives the effect depth.</summary>
    private static readonly Color SparkColor = new(226, 224, 255);
}
