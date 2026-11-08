using System.Collections.Generic;
using static Terraria.GameContent.Animations.IL_Actions.Sprites;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The light around the black hole: its halo, its ring flare, and matter falling in.
/// </summary>
/// <remarks>
///     The horizon, the photon ring and the accretion band are drawn by <c>InkNebula.fx</c>, not
///     here, so that they share the ink's warped domain and stay registered with it. What sprites
///     can do that the shader cannot is <em>transience</em> -- this layer only draws the parts
///     that have a lifetime.
/// </remarks>
internal sealed class SingularityLayer : IInkLayer
{
    /// <summary>A clot of ink spiralling in, drawn as a streak along its own motion.</summary>
    private struct Streak
    {
        public float Angle;
        public float Radius;
        public float Rate;
        public float Infall;
        public float Scale;
        public int Age;
    }

    private readonly List<Streak> streaks = new();

    public void Reset(in InkFrame frame)
    {
        streaks.Clear();
    }

    public void Update(in InkFrame frame)
    {
        if (Main.rand.NextBool(InkStyle.StreakChance))
        {
            streaks.Add(new Streak
            {
                Angle = Main.rand.NextFloat(MathHelper.TwoPi),
                Radius = Main.rand.NextFloat(0.34f, 0.72f),
                Rate = Main.rand.NextFloat(0.0022f, 0.0058f) * (Main.rand.NextBool() ? 1f : -1f),
                Infall = Main.rand.NextFloat(0.0012f, 0.0031f),
                Scale = Main.rand.NextFloat(0.30f, 0.85f),
                Age = 0,
            });
        }

        for (var i = streaks.Count - 1; i >= 0; i--)
        {
            var streak = streaks[i];

            var radius = MathF.Max(streak.Radius, InkStyle.Horizon);

            // Same Keplerian falloff the starfield uses, but these are on a decaying orbit
            // steep enough to actually arrive, so they wind up hard on the way in.
            streak.Angle += streak.Rate / (radius * MathF.Sqrt(radius));
            streak.Radius -= streak.Infall;
            streak.Age++;

            if (streak.Radius <= InkStyle.Horizon * 1.05f || streak.Age >= InkStyle.StreakLifetime)
            {
                streaks.RemoveAt(i);
                continue;
            }

            streaks[i] = streak;
        }
    }

    public void Prepare(SpriteBatch spriteBatch, GraphicsDevice device, in InkFrame frame)
    {
    }

    public void DrawInk(SpriteBatch spriteBatch, in InkFrame frame)
    {
    }

    public void Composite(SpriteBatch spriteBatch, in InkFrame frame)
    {
    }

    public void DrawLogo(SpriteBatch spriteBatch)
    {
        var tint = Color.Lerp(Color.White, new Color(237, 231, 237), 0.5f);
        var halo = (new Color(251, 251, 255) * 0.111f) with { A = 100 };

        FloatingLogo.Draw(spriteBatch, (float)(Main.timeForVisualEffects / 33.333333333f), tint, halo, 0f);
    }

    public void DrawLight(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var glowTexture = Assets.Textures.VFX.Glow.Asset.Value;
        var flareTexture = Assets.Textures.VFX.Smoke_02.Asset.Value;
        var trailTexture = Assets.Textures.VFX.BeamTrail_3.Asset.Value;

        var glowOrigin = glowTexture.Size() * 0.5f;
        var flareOrigin = flareTexture.Size() * 0.5f;
        var trailOrigin = trailTexture.Size() * 0.5f;

        var horizonPixels = InkStyle.Horizon * frame.UnitToPixels;
        DrawLogo(spriteBatch);

        // Halo: the glow the ring throws onto the surrounding dust. Wide and weak, mostly
        // there to give the bloom pass something to bite on around the hole.
        spriteBatch.Draw(
            glowTexture,
            frame.Singularity,
            null,
            Color.White * (InkStyle.HaloBrightness * frame.Fade),
            0f,
            glowOrigin,
            horizonPixels * InkStyle.HaloScale / glowTexture.Width,
            SpriteEffects.None,
            0f
        );
        // Ring flare, breathing slowly and counter-rotating against the disc so the two
        // don't beat against each other at a fixed period.
        var pulse = 0.82f + 0.18f * MathF.Sin(frame.Time * 0.7f);

        spriteBatch.Draw(
            flareTexture,
            frame.Singularity,
            null,
            Color.White * (InkStyle.FlareBrightness * pulse * frame.Fade),
            -frame.Time * 0.05f,
            flareOrigin,
            horizonPixels * 2.6f / flareTexture.Width,
            SpriteEffects.None,
            0f
        );

        foreach (var streak in streaks)
        {
            var position = frame.Orbit(streak.Angle, streak.Radius);

            if (!frame.OnScreen(position, 96f))
            {
                continue;
            }

            // Fade in over the first stretch of life and out over the last, so nothing pops
            // into or out of existence mid-frame.
            var life = streak.Age / (float)InkStyle.StreakLifetime;
            var alpha = MathF.Min(life * 6f, 1f) * (1f - life * life) * frame.Fade;

            // Streaks lengthen as they fall: same tangential smear as the starfield, just
            // strong enough here to read as a trail rather than a hint of one.
            var stretch = 1f + 0.22f / MathF.Max(streak.Radius, InkStyle.Horizon);

            spriteBatch.Draw(
                trailTexture,
                position,
                null,
                Color.White * (alpha * 0.55f),
                streak.Angle + MathHelper.PiOver2,
                trailOrigin,
                new Vector2(streak.Scale * stretch * 0.16f, streak.Scale * 0.09f),
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                glowTexture,
                position,
                null,
                Color.White * (alpha * 0.22f),
                0f,
                glowOrigin,
                streak.Scale * 0.10f,
                SpriteEffects.None,
                0f
            );
        }
    }
}
