using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Dryad;

/// <summary>A twinkling mote drifting off the Dryad's rift.</summary>
public sealed class RiftMoteParticle : Particle
{
    private static readonly Color MoteColor = new(214, 255, 222);

    public override string[] Textures => [Assets.Textures.VFX.PixelStar.KEY];

    public override float Gravity => -0.004f;

    public override float Drag => 0.985f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override bool UseLighting => false;

    public override int FadeOutTicks => 24;

    public override bool HasGlow => true;

    /// <summary>Additive only; see <see cref="DrawGlow" />.</summary>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void Update() => Velocity.X += MathF.Sin(Position.Y * 0.05f + Progress * 9f) * 0.012f;

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        if (opacity <= 0f)
            return;

        float life = Progress;
        float twinkle = 0.65f + 0.35f * MathF.Sin(Position.X * 0.11f + life * 22f);
        float flare = MathF.Min(life * 6f, 1f) * (1f - life * life) * twinkle;

        spriteBatch.Draw(
            texture,
            drawPosition,
            null,
            MoteColor * (flare * opacity),
            Rotation,
            texture.Size() * 0.5f,
            Scale * MathHelper.Lerp(1.1f, 0.4f, life),
            SpriteEffects.None,
            0f
        );
    }
}
