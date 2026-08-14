using System.Collections.Generic;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Stars held in orbit around the singularity, winding inward until the horizon takes them.
/// </summary>
/// <remarks>
///     Stored in polar coordinates about the singularity rather than as free positions and
///     velocities. Orbits are the whole point of the layer, and integrating them from Cartesian
///     velocity would need a real force step that drifts and eventually flings stars off screen;
///     advancing an angle instead makes the orbit exact and free.
///     <para />
///     The angular rate falls off as <c>r^-1.5</c> -- Kepler's third law. That is what stops the
///     field reading as a rigid turntable: the inner stars visibly wind up while the outer ones
///     are all but still, which is the same shear <c>InkNebula.fx</c> applies to the ink, so the
///     two agree without being coupled.
/// </remarks>
internal sealed class StarfieldLayer : IInkLayer
{
    private struct Star
    {
        public float Angle;
        public float Radius;

        /// <summary>0 = far, dim and slow; 1 = near, bright and quick.</summary>
        public float Depth;

        public float TwinklePhase;
        public float TwinkleRate;

        /// <summary>Per-star jitter on the orbital rate, so shells don't move in lockstep.</summary>
        public float RateScale;
    }

    private readonly List<Star> stars = new();

    public void Reset(in InkFrame frame)
    {
        stars.Clear();

        for (var i = 0; i < InkStyle.StarCount; i++)
        {
            // Seeded across the whole annulus, not at the rim, so the field is already
            // populated on the first frame instead of filling in over the next minute.
            stars.Add(Spawn(Main.rand.NextFloat(InkStyle.Horizon * 1.6f, InkStyle.StarFieldRadius)));
        }
    }

    public void Update(in InkFrame frame)
    {
        if (stars.Count == 0)
        {
            Reset(in frame);
        }

        for (var i = 0; i < stars.Count; i++)
        {
            var star = stars[i];

            var radius = MathF.Max(star.Radius, InkStyle.Horizon);
            star.Angle += InkStyle.StarOrbitRate * star.RateScale / (radius * MathF.Sqrt(radius));
            star.Radius -= InkStyle.StarInfall * (0.35f + star.Depth);
            star.TwinklePhase += star.TwinkleRate;

            // Crossed the horizon: gone. Respawned at the rim rather than removed, so the
            // population is constant and no allocation happens in the steady state.
            if (star.Radius <= InkStyle.Horizon)
            {
                star = Spawn(InkStyle.StarFieldRadius);
            }

            stars[i] = star;
        }
    }

    public void DrawInk(SpriteBatch spriteBatch, in InkFrame frame)
    {
    }

    public void DrawLight(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var starTexture = Assets.Textures.VFX.PixelStar.Asset.Value;
        var glowTexture = Assets.Textures.VFX.Glow.Asset.Value;

        var starOrigin = starTexture.Size() * 0.5f;
        var glowOrigin = glowTexture.Size() * 0.5f;

        foreach (var star in stars)
        {
            var position = frame.Orbit(star.Angle, star.Radius);

            if (!frame.OnScreen(position, 64f))
            {
                continue;
            }

            var twinkle = 0.72f + 0.28f * MathF.Sin(star.TwinklePhase);
            var brightness = (0.30f + 0.70f * star.Depth) * twinkle * frame.Fade;

            // Tangential smear: a star deep in the well is moving fast enough across the
            // frame to streak, so it is stretched along its own velocity -- which in polar
            // terms is simply perpendicular to its radius.
            var stretch = 1f + InkStyle.StarStretch / MathF.Max(star.Radius, InkStyle.Horizon);
            var scale = MathHelper.Lerp(InkStyle.StarScaleMin, InkStyle.StarScaleMax, star.Depth);

            spriteBatch.Draw(
                glowTexture,
                position,
                null,
                Color.White * (brightness * 0.16f),
                0f,
                glowOrigin,
                scale * 0.55f,
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                starTexture,
                position,
                null,
                Color.White * brightness,
                star.Angle + MathHelper.PiOver2,
                starOrigin,
                new Vector2(scale * stretch, scale),
                SpriteEffects.None,
                0f
            );
        }
    }

    private static Star Spawn(float radius)
    {
        // Depth is cubed toward 0 so most stars sit far back and only a few carry real
        // brightness -- an evenly lit field reads as noise rather than as distance.
        var depth = Main.rand.NextFloat();
        depth *= depth * depth;

        return new Star
        {
            Angle = Main.rand.NextFloat(MathHelper.TwoPi),
            Radius = radius,
            Depth = depth,
            TwinklePhase = Main.rand.NextFloat(MathHelper.TwoPi),
            TwinkleRate = Main.rand.NextFloat(0.004f, 0.028f) * (Main.rand.NextBool() ? 1f : -1f),
            RateScale = Main.rand.NextFloat(0.82f, 1.24f),
        };
    }
}
