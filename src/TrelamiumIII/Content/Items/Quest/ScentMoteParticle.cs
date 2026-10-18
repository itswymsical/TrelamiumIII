using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Items.Quest;

/// <summary>One mote of <see cref="LetterScent" />: a soft halo with a bright core, additive and unlit.</summary>
public sealed class ScentMoteParticle : Particle
{
    private static readonly Color HaloColor = new(60, 120, 255);

    private static readonly Color CoreColor = new(190, 230, 255);

    public override string[] Textures => [Assets.Textures.VFX.PixelStar.KEY];

    public override float Gravity => 0f;

    public override float Drag => 0.97f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override bool UseLighting => false;

    public override int FadeOutTicks => 20;

    public override bool HasGlow => true;

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        float life = Progress;
        float strength = MathF.Min(life * 8f, 1f) * (1f - life) * opacity * (0.75f + 0.25f * MathF.Sin(Position.X * 0.09f + life * 18f));

        if (strength <= 0f)
            return;

        Texture2D halo = Assets.Textures.VFX.Glow.Asset.Value;

        spriteBatch.Draw(halo, drawPosition, null, HaloColor * (strength * 0.8f), 0f, halo.Size() * 0.5f, Scale * 0.08f, SpriteEffects.None, 0f);
        spriteBatch.Draw(texture, drawPosition, null, CoreColor * strength, Rotation, texture.Size() * 0.5f, Scale * 0.22f, SpriteEffects.None, 0f);
    }
}
