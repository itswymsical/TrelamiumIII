using ReLogic.Content;
using System.Collections.Generic;
namespace TrelamiumIII.Core.Graphics.IlluminantInk;

internal static class FloatingLogo
{
    public static readonly Vector2 FullSize = new(1244f, 222f);

    private static readonly Vector2 CanvasCenter = new(622f, 111f);

    private static readonly Vector2 BodyHalfSize = new(111f, 111f);

    private const float WAVE_SPEED = 1.2f;
    private const float WAVE_PHASE_STEP = 0.62f;
    private const float WAVE_AMPLITUDE = 14f;
    private const float SWAY_AMPLITUDE = 6f;
    private const float TILT_AMPLITUDE = 0.035f;

    private const float SPRING = 0.06f;
    private const float DAMPING = 0.86f;
    private const float ANGULAR_SPRING = 0.05f;
    private const float ANGULAR_DAMPING = 0.85f;

    private const float BUBBLE_LIFT = 0.015f;
    private const float BUBBLE_TORQUE = 0.00025f;
    private const float CURSOR_REACH = 70f;
    private const float CURSOR_PUSH = 0.35f;

    private readonly record struct Glyph(Func<Asset<Texture2D>> Texture, Vector2 Position);

    private static readonly Glyph[] glyphs =
    [
        new(static () => Assets.Textures.Logo.Logo_T.Asset, new Vector2(0f, 22f)),
        new(static () => Assets.Textures.Logo.Logo_R.Asset, new Vector2(198f, 22f)),
        new(static () => Assets.Textures.Logo.Logo_E.Asset, new Vector2(370f, 14f)),
        new(static () => Assets.Textures.Logo.Logo_L.Asset, new Vector2(498f, 22f)),
        new(static () => Assets.Textures.Logo.Logo_A.Asset, new Vector2(634f, 14f)),
        new(static () => Assets.Textures.Logo.Logo_M.Asset, new Vector2(834f, 22f)),
        new(static () => Assets.Textures.Logo.Logo_I.Asset, new Vector2(1012f, 22f)),
        new(static () => Assets.Textures.Logo.Logo_U.Asset, new Vector2(1160f, 14f)),
        new(static () => Assets.Textures.Logo.Logo_M.Asset, new Vector2(1358f, 22f)),
    ];

    private static readonly Vector2[] offsets = new Vector2[glyphs.Length];
    private static readonly Vector2[] velocities = new Vector2[glyphs.Length];
    private static readonly float[] angles = new float[glyphs.Length];
    private static readonly float[] angularVelocities = new float[glyphs.Length];

    private static Vector2 center;
    private static float scale;
    public static void SetLayout(Vector2 logoCenter, float logoScale)
    {
        center = logoCenter;
        scale = logoScale;
    }

    public static void Reset()
    {
        Array.Clear(offsets);
        Array.Clear(velocities);
        Array.Clear(angles);
        Array.Clear(angularVelocities);
    }

    public static void Draw(SpriteBatch spriteBatch, float time, Color tint, Color halo, float fade)
    {
        if (scale <= 0f)
            return;

        var scrim = Assets.Textures.VFX.Glow.Asset.Value;
        var scrimScale = BodyHalfSize * 2.6f * scale / scrim.Width;
        var scrimColor = Color.Black * (0.4f * fade);

        for (var i = 0; i < glyphs.Length; i++)
            spriteBatch.Draw(scrim, Placement(i, time, out _), null, scrimColor, 0f, scrim.Size() * 0.5f, scrimScale, SpriteEffects.None, 0f);

        for (var i = 0; i < glyphs.Length; i++)
        {
            for (var k = 0; k < 8; k++)
                DrawGlyph(spriteBatch, i, time, (MathHelper.TwoPi * k / 8f).ToRotationVector2() * 1.5f, halo);
        }

        for (var i = 0; i < glyphs.Length; i++)
            DrawGlyph(spriteBatch, i, time, Vector2.Zero, tint);
    }

    private static void DrawGlyph(SpriteBatch spriteBatch, int i, float time, Vector2 shift, Color color)
    {
        var position = Placement(i, time, out var rotation) + shift;
        spriteBatch.Draw(glyphs[i].Texture().Value, position, null, color, rotation, CanvasCenter, scale, SpriteEffects.None, 0f);
    }

    private static Vector2 Placement(int i, float time, out float rotation)
    {
        var phase = time * WAVE_SPEED - i * WAVE_PHASE_STEP;
        var wave = new Vector2(MathF.Sin(phase * 0.42f) * SWAY_AMPLITUDE, MathF.Sin(phase) * WAVE_AMPLITUDE) * scale;

        // Tilt follows the wave's slope, the way a floating body leans with the surface under it.
        rotation = MathF.Cos(phase) * TILT_AMPLITUDE + angles[i];

        return RestCenter(i) + wave + offsets[i];
    }

    private static Vector2 RestCenter(int i) => center + (glyphs[i].Position + CanvasCenter - FullSize * 0.5f) * scale;
}