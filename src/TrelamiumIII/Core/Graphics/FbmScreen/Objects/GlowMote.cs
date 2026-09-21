using Terraria.Utilities;

namespace TrelamiumIII.Core.Graphics.FbmScreen.Objects;

/// <summary>
/// An additive orb that rises, weaves sideways and pulses. Brightness follows its lifetime and the iris mask, which is
/// floored at <c>maskFloor</c> so the mote can show over an uncovered area.
/// </summary>
internal sealed class GlowMote : ScreenObject
{
    private const float Rise = 0.02f;
    private const float Drag = 0.98f;
    private const float Weave = 0.03f;
    private const float WeaveFrequency = 0.035f;
    private const float PulseFrequency = 0.07f;
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.3f;
    private const float CoreScale = 0.35f;

    private static readonly Color CoreColor = new(220, 245, 255);

    private readonly float radius;
    private readonly Color color;
    private readonly int lifetime;
    private readonly float phase;
    private readonly float maskFloor;

    private Vector2 position;
    private Vector2 velocity;
    private int age;

    /// <param name="position">Target pixels.</param>
    /// <param name="velocity">Pixels per step at 1080p.</param>
    /// <param name="radius">Pixels at 1080p.</param>
    public GlowMote(Vector2 position, Vector2 velocity, float radius, Color color, int lifetime, float phase, float maskFloor)
    {
        this.position = position;
        this.velocity = velocity;
        this.radius = radius;
        this.color = color;
        this.lifetime = Math.Max(lifetime, 1);
        this.phase = phase;
        this.maskFloor = maskFloor;
    }

    /// <summary>A mote anywhere on the frame, coloured between <paramref name="low"/> and <paramref name="high"/>.</summary>
    public static GlowMote Anywhere(UnifiedRandom random, in ScreenFrame frame, Color low, Color high, float maskFloor)
    {
        return new GlowMote(
            new Vector2(random.NextFloat(frame.Width), random.NextFloat(frame.Height)),
            new Vector2(random.NextFloat(-0.4f, 0.4f), -random.NextFloat(0.1f, 0.6f)),
            random.NextFloat(5f, 11f),
            Color.Lerp(low, high, random.NextFloat()),
            random.Next(240, 480),
            random.NextFloat(MathHelper.TwoPi),
            maskFloor);
    }

    public override void Update(FbmScreenRenderer screen, in ScreenFrame frame)
    {
        if (++age >= lifetime)
        {
            Active = false;

            return;
        }

        velocity.Y -= Rise;
        velocity.X += MathF.Sin(age * WeaveFrequency + phase) * Weave;
        velocity *= Drag;

        position += velocity * frame.Scale;

        if (!frame.OnScreen(position, radius * 2f * frame.Scale))
            Active = false;
    }

    public override void DrawAdditive(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        float life = age / (float)lifetime;

        float alpha = MathF.Min(life / FadeIn, 1f)
            * MathF.Min((1f - life) / FadeOut, 1f)
            * (0.75f + 0.25f * MathF.Sin(age * PulseFrequency + phase))
            * MathHelper.Lerp(maskFloor, 1f, frame.MaskAt(position));

        if (alpha <= 0f)
            return;

        Texture2D texture = Assets.Textures.VFX.RadialGradient.Asset.Value;
        Vector2 origin = texture.Size() * 0.5f;
        float scale = radius * 2f * frame.Scale / texture.Width;

        spriteBatch.Draw(texture, position, null, color * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        spriteBatch.Draw(texture, position, null, CoreColor * alpha, 0f, origin, scale * CoreScale, SpriteEffects.None, 0f);
    }
}
