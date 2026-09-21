using Terraria.GameContent;
using Terraria.Utilities;

namespace TrelamiumIII.Core.Graphics.FbmScreen.Objects;

/// <summary>
/// A square speck that orbits the focus while falling into it, turning faster as it closes in. Alpha follows the iris
/// mask, floored at <c>maskFloor</c>; glowing specks draw additively.
/// </summary>
internal sealed class DriftSpeck : ScreenObject
{
    private const int FadeInSteps = 30;

    /// <summary>Distance from the focus, at 1080p, over which a speck fades out as it arrives.</summary>
    private const float ArrivalFade = 40f;

    private readonly float size;
    private readonly Color color;
    private readonly bool glow;
    private readonly float spin;
    private readonly float fall;
    private readonly int lifetime;
    private readonly float maskFloor;

    private float angle;
    private float distance;
    private Vector2 position;
    private int age;

    /// <param name="distance">Target pixels from the focus.</param>
    /// <param name="size">Edge in pixels at 1080p.</param>
    /// <param name="spin">Radians per step at half the frame height from the focus.</param>
    /// <param name="fall">Pixels per step at 1080p.</param>
    public DriftSpeck(float angle, float distance, float size, Color color, bool glow, float spin, float fall, int lifetime, float maskFloor)
    {
        this.angle = angle;
        this.distance = distance;
        this.size = size;
        this.color = color;
        this.glow = glow;
        this.spin = spin;
        this.fall = fall;
        this.lifetime = Math.Max(lifetime, 1);
        this.maskFloor = maskFloor;
    }

    /// <summary>
    /// A speck somewhere between the focus and the frame's far corner when <paramref name="scattered"/>, otherwise just
    /// past that corner's distance, which is off screen in every direction.
    /// </summary>
    public static DriftSpeck Spiralling(UnifiedRandom random, in ScreenFrame frame, bool scattered, Color color, bool glow, float maskFloor)
    {
        float far = FarthestCorner(in frame);

        return new DriftSpeck(
            random.NextFloat(MathHelper.TwoPi),
            scattered ? far * random.NextFloat(0.15f, 1f) : far * random.NextFloat(1.02f, 1.2f),
            random.NextFloat(3f, 6f),
            color,
            glow,
            random.NextFloat(0.002f, 0.005f),
            random.NextFloat(0.6f, 1.6f),
            random.Next(900, 1500),
            maskFloor);
    }

    public override void Update(FbmScreenRenderer screen, in ScreenFrame frame)
    {
        float half = frame.Height * 0.5f;
        float closeness = MathF.Sqrt(half / MathF.Max(distance, half * 0.1f));

        angle += spin * closeness;
        distance -= fall * frame.Scale * (0.6f + 0.4f * closeness);
        position = frame.Focus + angle.ToRotationVector2() * distance;

        if (++age >= lifetime || distance <= size * frame.Scale)
            Active = false;
    }

    public override void DrawAlpha(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        if (!glow)
            Draw(spriteBatch, in frame);
    }

    public override void DrawAdditive(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        if (glow)
            Draw(spriteBatch, in frame);
    }

    private void Draw(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
        float edge = MathF.Max(size * frame.Scale, 1f);

        if (!frame.OnScreen(position, edge))
            return;

        float alpha = MathF.Min(age / (float)FadeInSteps, 1f)
            * MathF.Min(distance / (ArrivalFade * frame.Scale), 1f)
            * MathHelper.Lerp(maskFloor, 1f, frame.MaskAt(position));

        if (alpha <= 0f)
            return;

        var bounds = new Rectangle((int)(position.X - edge * 0.5f), (int)(position.Y - edge * 0.5f), (int)edge, (int)edge);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, color * alpha);
    }

    private static float FarthestCorner(in ScreenFrame frame)
    {
        Vector2 focus = frame.Focus;

        float x = MathF.Max(focus.X, frame.Width - focus.X);
        float y = MathF.Max(focus.Y, frame.Height - focus.Y);

        return MathF.Sqrt(x * x + y * y);
    }
}
