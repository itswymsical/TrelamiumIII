using System.Collections.Generic;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Beads of ferrofluid adrift in the frame, which spike toward the cursor as if it were a
///     magnet held under them.
/// </summary>
/// <remarks>
///     This is the layer that sells the material. A nebula can be painted by a shader, but
///     ferrofluid is defined by how it <em>answers</em> a field, and the only field in a menu is
///     the pointer -- so the cursor is the magnet, motes stretch along the line to it, and a
///     click is a pulse that throws them off and lets them creep back.
///     <para />
///     Each mote is drawn twice: a dark body under alpha blend that genuinely occludes the
///     nebula behind it, then a bright meniscus under additive. Ferrofluid is black liquid with
///     a mirror surface; drawing only the highlight would leave it looking like smoke.
/// </remarks>
internal sealed class InkMoteLayer : IInkLayer
{
    private struct Mote
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Rotation;
        public float RotationRate;

        /// <summary>Fade-in weight, so a respawn creeps in instead of appearing.</summary>
        public float Alpha;

        /// <summary>0..1 magnetisation: how far this mote is drawn out toward the cursor.</summary>
        public float Spike;

        /// <summary>Direction the spike points, held between frames so it eases rather than snaps.</summary>
        public float SpikeAngle;
    }

    private readonly List<Mote> motes = new();

    private bool mouseWasDown;

    public void Reset(in InkFrame frame)
    {
        motes.Clear();

        for (var i = 0; i < InkStyle.MoteCount; i++)
        {
            motes.Add(Spawn(in frame, scattered: true));
        }
    }

    public void Update(in InkFrame frame)
    {
        if (motes.Count == 0)
        {
            Reset(in frame);
        }

        // Rising edge only: holding the button down should not fire a pulse every tick.
        var pulsed = Main.mouseLeft && !mouseWasDown;
        mouseWasDown = Main.mouseLeft;

        for (var i = 0; i < motes.Count; i++)
        {
            var mote = motes[i];

            var toCursor = frame.Cursor - mote.Position;
            var distance = toCursor.Length();

            if (distance > 0.001f && distance < InkStyle.MagnetRadius)
            {
                // Linear falloff, not inverse-square: the real thing is nonlinear, but an
                // inverse-square well slams every mote into the pointer the instant it comes
                // in range, and the interesting part is the reaching, not the arrival.
                var pull = 1f - distance / InkStyle.MagnetRadius;
                var direction = toCursor / distance;

                mote.Velocity += direction * (InkStyle.MagnetForce * pull * pull);
                mote.Spike = MathHelper.Lerp(mote.Spike, pull, 0.10f);
                mote.SpikeAngle = direction.ToRotation();

                if (pulsed && distance < InkStyle.PulseRadius)
                {
                    var push = 1f - distance / InkStyle.PulseRadius;
                    mote.Velocity -= direction * (InkStyle.PulseForce * push);
                }
            }
            else
            {
                mote.Spike = MathHelper.Lerp(mote.Spike, 0f, 0.04f);
            }

            // A slow bias toward the singularity, so the field as a whole leans into the
            // hole rather than milling about the middle of the frame.
            var toCore = frame.Singularity - mote.Position;

            if (toCore != Vector2.Zero)
            {
                mote.Velocity += Vector2.Normalize(toCore) * 0.006f;
            }

            mote.Velocity *= 0.955f;
            mote.Position += mote.Velocity;
            mote.Rotation += mote.RotationRate;
            mote.Alpha = MathF.Min(mote.Alpha + 0.012f, 1f);

            if (!frame.OnScreen(mote.Position, InkStyle.MoteRadiusMax * 3f))
            {
                mote = Spawn(in frame, scattered: false);
            }

            motes[i] = mote;
        }
    }

    public void DrawInk(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var bodyTexture = Assets.Textures.VFX.InkMote.Asset.Value;
        var bodyOrigin = bodyTexture.Size() * 0.5f;

        foreach (var mote in motes)
        {
            if (!frame.OnScreen(mote.Position, InkStyle.MoteRadiusMax * 3f))
            {
                continue;
            }

            var scale = Stretch(in mote, mote.Radius * 3.1f / bodyTexture.Width);

            // Glow.png is alpha-shaped, so tinting it black and blending normally punches a
            // soft hole in the nebula rather than laying a grey disc over it.
            spriteBatch.Draw(
                bodyTexture,
                mote.Position,
                null,
                Color.Black * (mote.Alpha * frame.Fade * 0.85f),
                mote.SpikeAngle,
                bodyOrigin,
                scale,
                SpriteEffects.None,
                0f
            );
        }
    }

    public void DrawLight(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var rimTexture = Assets.Textures.VFX.Corona.Asset.Value;
        var glintTexture = Assets.Textures.VFX.Bloom.Asset.Value;

        var rimOrigin = rimTexture.Size() * 0.5f;
        var glintOrigin = glintTexture.Size() * 0.5f;

        foreach (var mote in motes)
        {
            if (!frame.OnScreen(mote.Position, InkStyle.MoteRadiusMax * 3f))
            {
                continue;
            }

            var alpha = mote.Alpha * frame.Fade;

            // Corona is a hollow ring, which is exactly the meniscus: the surface catches
            // light at its silhouette and nowhere else.
            spriteBatch.Draw(
                rimTexture,
                mote.Position,
                null,
                Color.White * (alpha * (0.30f + 0.45f * mote.Spike)),
                mote.SpikeAngle,
                rimOrigin,
                Stretch(in mote, mote.Radius * 2.35f / rimTexture.Width),
                SpriteEffects.None,
                0f
            );

            // Specular glint, offset off-axis and rotating with the mote so the highlight
            // travels over the surface instead of sitting pinned to its centre.
            var glint = mote.Position + new Vector2(mote.Radius * 0.34f, 0f).RotatedBy(mote.Rotation);

            spriteBatch.Draw(
                glintTexture,
                glint,
                null,
                Color.White * (alpha * 0.30f),
                0f,
                glintOrigin,
                mote.Radius * 0.5f / glintTexture.Width,
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>
    ///     Elongates a mote along <see cref="Mote.SpikeAngle" /> in proportion to how
    ///     magnetised it is, conserving apparent area by thinning it across.
    /// </summary>
    private static Vector2 Stretch(in Mote mote, float scale)
    {
        var along = 1f + mote.Spike * InkStyle.MoteSpikeStretch;
        return new Vector2(scale * along, scale / MathF.Sqrt(along));
    }

    private static Mote Spawn(in InkFrame frame, bool scattered)
    {
        // Scattered on entry so the frame is already populated; otherwise from just past the
        // rim, drifting in, which is where a mote that left the frame ought to come back.
        var position = scattered
            ? new Vector2(Main.rand.NextFloat(frame.Width), Main.rand.NextFloat(frame.Height))
            : new Vector2(frame.Width + InkStyle.MoteRadiusMax * 2f, Main.rand.NextFloat(frame.Height));

        return new Mote
        {
            Position = position,
            Velocity = new Vector2(Main.rand.NextFloat(-0.35f, -0.08f), Main.rand.NextFloat(-0.09f, 0.09f)),
            Radius = Main.rand.NextFloat(InkStyle.MoteRadiusMin, InkStyle.MoteRadiusMax),
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
            RotationRate = Main.rand.NextFloat(0.002f, 0.009f) * (Main.rand.NextBool() ? 1f : -1f),
            Alpha = 0f,
            Spike = 0f,
            SpikeAngle = Main.rand.NextFloat(MathHelper.TwoPi),
        };
    }
}
