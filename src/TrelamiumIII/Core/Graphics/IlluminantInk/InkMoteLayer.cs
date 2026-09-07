using System.Collections.Generic;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Beads of ferrofluid adrift in the frame, which spike toward the cursor as if it were a
///     magnet held under them, come apart when clicked, and pool back together when dragged.
/// </summary>
/// <remarks>
///     <para>
///         This is the layer that sells the material. A nebula can be painted by a shader, but
///         ferrofluid is defined by how it <em>answers</em> a field, and the only field in a menu is
///         the pointer -- so the cursor is the magnet, motes stretch along the line to it, and a
///         click is a pulse that throws them off and lets them creep back.
///     </para>
///     <para>
///         <b>Why the motes are a metaball field and not sprites.</b> The other half of what makes
///         a liquid a liquid is that two beads which touch stop being two beads. A sprite per mote
///         can show them overlapping and never merging, which reads as a heap of discs. So no mote
///         is drawn directly: each is stamped as a soft blob into an off-screen coverage field
///         (<see cref="Prepare" />), summed additively, and the whole field is cut at a threshold
///         by <c>InkSilhouette.fx</c> (<see cref="Composite" />). Two motes that merely drift near
///         each other cross that threshold in the gap between them and fuse into one outline. The
///         stamp is the mote texture itself, whose slightly irregular edge gives the field its
///         wobble for free.
///     </para>
///     <para>
///         <b>Clicking and gathering.</b> A click on a mote bursts it into droplets that conserve
///         its area, and the magnet gathers loose droplets under the pointer, where they merge back
///         into larger beads -- again conserving area. The two are inverses, which is what lets the
///         field be driven indefinitely without either draining or filling: what is held steady is
///         mass, not the number of motes. See <see cref="Burst" /> and <see cref="Coalesce" />.
///     </para>
///     <para>
///         The body and its meniscus both come out of the silhouette pass -- mass from the cut,
///         rim from a neighbour test against the same cut -- so the only thing left drawn per mote
///         is the specular glint. Ferrofluid is black liquid with a mirror surface, and the
///         highlight is the half of that a threshold cannot produce.
///     </para>
/// </remarks>
internal sealed class InkMoteLayer : IInkLayer, IDisposable
{
    private struct Mote
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Rotation;
        public float RotationRate;

        /// <summary>Swell-in weight, so a respawn condenses instead of appearing.</summary>
        public float Alpha;

        /// <summary>0..1 magnetisation: how far this mote is drawn out toward the cursor.</summary>
        public float Spike;

        /// <summary>Direction the spike points, held between frames so it eases rather than snaps.</summary>
        public float SpikeAngle;

        /// <summary>Ticks before this mote will merge with a neighbour again.</summary>
        /// <remarks>
        ///     Set on both sides of a split and a join. Without it a burst is invisible -- the
        ///     droplets are born inside each other's reach with the cursor still pulling on them --
        ///     and a merge can cascade down a line of motes within one tick.
        /// </remarks>
        public int Cohesion;

        /// <summary>Area, up to a factor of pi. What burst and merge conserve.</summary>
        public readonly float Mass => Radius * Radius;
    }

    /// <summary>
    ///     Mass a settled field carries, which is what the refill holds steady rather than a count.
    /// </summary>
    /// <remarks>
    ///     <see cref="InkStyle.MoteCount" /> motes at the mean square radius of the spawn range --
    ///     the closed form of E[r^2] for r uniform on [min, max]. Written out rather than measured
    ///     so that widening the range retunes the field automatically instead of silently changing
    ///     how much ink the frame holds.
    /// </remarks>
    private static readonly float MassTarget = InkStyle.MoteCount * (
        InkStyle.MoteRadiusMin * InkStyle.MoteRadiusMin
        + InkStyle.MoteRadiusMin * InkStyle.MoteRadiusMax
        + InkStyle.MoteRadiusMax * InkStyle.MoteRadiusMax) / 3f;

    private readonly List<Mote> motes = new();

    private WrapperShaderData<Assets.Shaders.InkSilhouette.Parameters>? silhouette;
    private RenderTarget2D? field;

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

        // Before the physics, so the mote is hit-tested where the player saw it when they clicked.
        // Run after, the pulse this same click fires would have shoved a small mote clear of its
        // own hit radius first, and clicking one would do nothing more than half the time.
        var droplets = pulsed ? Burst(frame.Cursor) : motes.Count;

        var mass = 0f;

        // Backwards, because a mote that leaves the frame is now removed rather than recycled in
        // place -- the field's population is free to breathe, and it is mass that is held steady.
        for (var i = motes.Count - 1; i >= 0; i--)
        {
            var mote = motes[i];

            if (mote.Cohesion > 0)
            {
                mote.Cohesion--;
            }

            var toCursor = frame.Cursor - mote.Position;
            var distance = toCursor.Length();

            if (distance > 0.001f && distance < InkStyle.MagnetRadius)
            {
                // Linear falloff, not inverse-square: the real thing is nonlinear, but an
                // inverse-square well slams every mote into the pointer the instant it comes
                // in range, and the interesting part is the reaching, not the arrival.
                var pull = 1f - distance / InkStyle.MagnetRadius;
                var direction = toCursor / distance;

                // ...and a second falloff on the way in, so the pull fades back out inside the
                // core. Without it the force is still at full strength at distance zero: the mote
                // is driven through the cursor, the direction to it flips, and it buzzes across
                // the pointer indefinitely. Scaled linearly, the core is a spring rather than a
                // well, and the damping below is enough to leave it overdamped.
                var grip = MathHelper.Clamp(distance / InkStyle.MagnetCore, 0f, 1f);

                mote.Velocity += direction * (InkStyle.MagnetForce * pull * pull * grip);

                if (grip < 1f)
                {
                    // Bleeds off the momentum the mote arrived with. The force fading is what
                    // stops it being pushed through; this is what stops it coasting through.
                    mote.Velocity *= MathHelper.Lerp(InkStyle.MagnetSettle, 1f, grip);
                }

                // The stretch fades out with the pull, for the same reason. A bead sitting on the
                // pole has no lateral direction to reach in, and holding it at full stretch meant
                // holding it along an angle that flipped every few frames -- which was half of
                // what the buzzing looked like.
                mote.Spike = MathHelper.Lerp(mote.Spike, pull * grip, 0.10f);

                // Angle held rather than recomputed inside the core, where a direction taken from
                // a near-zero vector is mostly noise.
                if (grip > 0.5f)
                {
                    mote.SpikeAngle = direction.ToRotation();
                }

                // Droplets sit past the boundary Burst returned and skip this tick's pulse. They
                // are already leaving at their own burst speed, and a point-blank pulse on top of
                // that would triple it -- the click would read as an explosion, not as a split.
                // The test is exact: the loop runs downward, so it reaches every droplet before
                // any removal below it can shift an index.
                if (pulsed && i < droplets && distance < InkStyle.PulseRadius)
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

            if (!frame.OnScreen(mote.Position, StampReach(in mote)))
            {
                motes.RemoveAt(i);
                continue;
            }

            mass += mote.Mass;
            motes[i] = mote;
        }

        Coalesce(frame.Cursor);

        Refill(in frame, mass);
    }

    /// <summary>
    ///     Sums every mote into the off-screen coverage field.
    /// </summary>
    /// <remarks>
    ///     Half resolution, additively, in the same convention every other accumulated field in the
    ///     mod uses: coverage in red, brightness pinned open in green, because a menu is not
    ///     standing anywhere in a world for <see cref="FieldStamp.Lit" /> to sample.
    /// </remarks>
    public void Prepare(SpriteBatch spriteBatch, GraphicsDevice device, in InkFrame frame)
    {
        if (motes.Count == 0 || !EnsureField(device, in frame))
        {
            return;
        }

        device.SetRenderTarget(field);
        device.Clear(Color.Transparent);

        // Motes are already in target pixels, so the transform is only the downscale into the
        // half-resolution surface. Additive is the point: overlapping blobs have to sum for
        // neighbours to fuse.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.CreateScale(1f / InkStyle.MoteFieldScale, 1f / InkStyle.MoteFieldScale, 1f)
        );

        Stamp(spriteBatch, in frame);

        spriteBatch.End();

        // Left unbound, as IInkLayer.Prepare requires: the scene claims its own target next.
        device.SetRenderTarget(null);
    }

    /// <summary>Nothing. The mass is the silhouette, and it lands in <see cref="Composite" />.</summary>
    public void DrawInk(SpriteBatch spriteBatch, in InkFrame frame)
    {
    }

    /// <summary>Cuts the coverage field into the ink mass and its meniscus.</summary>
    public void Composite(SpriteBatch spriteBatch, in InkFrame frame)
    {
        if (motes.Count == 0 || field is not { IsDisposed: false } surface)
        {
            return;
        }

        silhouette ??= Assets.Shaders.InkSilhouette.CreateP0();

        var parameters = silhouette.Parameters;
        var noise = Assets.Textures.Noise.LiquidNoise.Asset.Value;

        // The wobble field tiles, so it is bound through the shader's own wrap sampler rather than
        // through s0 -- SpriteBatch owns s0, and its clamp would fold the far edge into the near
        // one and pin the warp along two sides of the frame.
        parameters.uNoiseTex = noise;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = noise,
            Sampler = SamplerState.LinearWrap,
        };

        parameters.uTime = frame.Time;
        parameters.uAspect = frame.Width / (float)Math.Max(frame.Height, 1);
        parameters.uFieldTexel = new Vector2(
            InkStyle.MoteFieldScale / (float)Math.Max(frame.Width, 1),
            InkStyle.MoteFieldScale / (float)Math.Max(frame.Height, 1)
        );

        // Zero offset and unit zoom: the shader's wobble is written to lock to a world so it boils
        // in place while a camera pans, and there is no camera behind a title screen. Handing it
        // the identity is the correct answer rather than a shortcut -- it makes the warp
        // screen-locked, which is the only frame of reference the menu has.
        parameters.uWorldOffset = Vector2.Zero;
        parameters.uZoom = Vector2.One;

        parameters.uWobble = InkStyle.MoteWobble;
        parameters.uWobbleScale = InkStyle.MoteWobbleScale;
        parameters.uWobbleDrift = InkStyle.MoteWobbleDrift;

        // Smeared toward the singularity, not the cursor. The pointer is what the motes reach for
        // and the hole is what has hold of them, and the trail belongs to the second: coverage
        // trailing toward the drain says the mass is losing its grip, which is the read this scene
        // is after. Aimed at the cursor it would look like the ink was being spilled by the mouse.
        parameters.uCenter = frame.SingularityUv;
        parameters.uSmear = InkStyle.MoteSmear;
        parameters.uSpin = InkStyle.MoteSpin;
        parameters.uFalloff = InkStyle.MoteFalloff;

        parameters.uThreshold = InkStyle.MoteThreshold;
        parameters.uBodyAlpha = InkStyle.MoteBodyAlpha;
        parameters.uBands = InkStyle.MoteBands;

        parameters.uShadowColor = InkStyle.MoteShadowColor;
        parameters.uBodyColor = InkStyle.MoteBodyColor;
        parameters.uCoreColor = InkStyle.MoteCoreColor;
        parameters.uRimColor = InkStyle.MoteRimColor;
        parameters.uRimAlpha = InkStyle.MoteRimAlpha;
        parameters.uOpacity = frame.Fade;

        // Immediate, because the pass has to be bound between Begin and the draw that uses it.
        // PointClamp, because the field is deliberately half resolution and the shader snaps its
        // decisions to that grid; filtering it back up would soften exactly the edge we are after.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        silhouette.Apply();

        // Into a destination rectangle rather than at a scale of MoteFieldScale: the surface is
        // rounded up to whole texels, so on an odd width the scaled draw would overhang the frame
        // by a pixel and shift the grid the shader is quantising to.
        spriteBatch.Draw(surface, new Rectangle(0, 0, frame.Width, frame.Height), Color.White);

        spriteBatch.End();
    }

    /// <summary>
    ///     The specular glint, the one part of a mote a threshold cannot produce.
    /// </summary>
    /// <remarks>
    ///     Offset off-axis and rotating with the mote, so the highlight travels over the surface
    ///     instead of sitting pinned to its centre, and brighter on a magnetised mote because a
    ///     drawn-out bead presents more of its curve to the light.
    /// </remarks>
    public void DrawLight(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var glintTexture = Assets.Textures.VFX.Bloom.Asset.Value;
        var glintOrigin = glintTexture.Size() * 0.5f;

        foreach (var mote in motes)
        {
            if (!frame.OnScreen(mote.Position, StampReach(in mote)))
            {
                continue;
            }

            var glint = mote.Position + new Vector2(mote.Radius * 0.34f, 0f).RotatedBy(mote.Rotation);

            spriteBatch.Draw(
                glintTexture,
                glint,
                null,
                Color.White * (mote.Alpha * frame.Fade * InkStyle.MoteGlint * (0.65f + 0.5f * mote.Spike)),
                0f,
                glintOrigin,
                mote.Radius * 0.5f / glintTexture.Width,
                SpriteEffects.None,
                0f
            );
        }
    }

    public void Dispose()
    {
        field?.Dispose();
        field = null;

        // Dropped rather than reused, so a resolution change or a mod reload cannot leave a stale
        // Effect bound to a dead device -- the same reason NebulaLayer drops its own.
        silhouette = null;
    }

    /// <summary>Stamps every mote into the bound field, as a soft blob carrying its own stretch.</summary>
    private void Stamp(SpriteBatch spriteBatch, in InkFrame frame)
    {
        var texture = Assets.Textures.VFX.InkMote.Asset.Value;
        var origin = texture.Size() * 0.5f;

        foreach (var mote in motes)
        {
            if (!frame.OnScreen(mote.Position, StampReach(in mote)))
            {
                continue;
            }

            // The swell rides on the radius rather than on the coverage. Fading a stamp in by
            // coverage does not fade the silhouette in: the field stays under the threshold and
            // then crosses it all at once, so a respawn pops at full size. Growing the blob
            // instead lets it come up through the cut, which is what a bead condensing looks like.
            var radius = mote.Radius * MathHelper.Lerp(0.15f, 1f, mote.Alpha);

            // Full strength, and not the overdriven coverage the world layers stamp with. That
            // overdrive exists to keep a node fusing as its life runs out and its alpha falls;
            // a mote's swell rides on the radius instead, so its coverage never leaves 1 -- where
            // the vertex colour would clamp it anyway.
            spriteBatch.Draw(
                texture,
                mote.Position,
                null,
                FieldStamp.Flat(1f),
                mote.SpikeAngle,
                origin,
                Stretch(in mote, radius * InkStyle.MoteStampSpread / texture.Width),
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>Allocates the coverage surface, reallocating on a resolution change.</summary>
    private bool EnsureField(GraphicsDevice device, in InkFrame frame)
    {
        // Ceiling: an odd frame width would otherwise drop the last column and leave a seam down
        // the edge of the composite.
        var width = (int)Math.Ceiling(frame.Width / (float)InkStyle.MoteFieldScale);
        var height = (int)Math.Ceiling(frame.Height / (float)InkStyle.MoteFieldScale);

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (field is { IsDisposed: false } current && current.Width == width && current.Height == height)
        {
            return true;
        }

        field?.Dispose();

        field = new RenderTarget2D(
            device,
            width,
            height,
            false,
            device.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        return true;
    }

    /// <summary>
    ///     Breaks the mote under <paramref name="cursor" /> into droplets that carry its area
    ///     between them.
    /// </summary>
    /// <remarks>
    ///     One mote, the one actually clicked, rather than everything in reach: the outward pulse
    ///     already answers for the neighbourhood, and bursting the whole neighbourhood would turn a
    ///     click into a shower with nothing left to click again.
    ///     <para />
    ///     The pieces are sized by normalised random weights, so the areas sum to exactly the
    ///     parent's however uneven they come out. Equal pieces would conserve area too, and read as
    ///     a diagram: real ink comes apart into one large drop and a scatter of small ones.
    /// </remarks>
    /// <returns>
    ///     The index the new droplets start at, so the caller can tell them apart from the motes
    ///     that were already there. Equal to the population when nothing was hit.
    /// </returns>
    private int Burst(Vector2 cursor)
    {
        var index = HitTest(cursor);

        if (index < 0)
        {
            return motes.Count;
        }

        var parent = motes[index];
        var pieces = InkStyle.MoteBurstPieces;

        // A small mote splits into fewer. Dividing its area any further would put every piece
        // under the smallest radius the field can resolve, and the burst would read as the mote
        // having simply vanished.
        while (pieces > 2 && parent.Radius / MathF.Sqrt(pieces) < InkStyle.MoteRadiusMin * 0.55f)
        {
            pieces--;
        }

        pieces = Math.Min(pieces, InkStyle.MoteBudget - motes.Count + 1);

        if (pieces < 2)
        {
            return motes.Count;
        }

        Span<float> weights = stackalloc float[pieces];
        var total = 0f;

        for (var i = 0; i < pieces; i++)
        {
            weights[i] = Main.rand.NextFloat(0.5f, 1.5f);
            total += weights[i];
        }

        motes.RemoveAt(index);

        var start = motes.Count;
        var spin = Main.rand.NextFloat(MathHelper.TwoPi);

        for (var i = 0; i < pieces; i++)
        {
            // Spread around a ring with jitter rather than at free random angles, which clumps and
            // leaves the burst looking one-sided.
            var angle = spin + MathHelper.TwoPi * (i + Main.rand.NextFloat(-0.22f, 0.22f)) / pieces;
            var direction = angle.ToRotationVector2();

            motes.Add(new Mote
            {
                Position = parent.Position + direction * (parent.Radius * 0.45f),

                // Most of the parent's momentum, so the cluster travels together while it spreads.
                Velocity = parent.Velocity * 0.6f
                           + direction * (InkStyle.MoteBurstSpeed * Main.rand.NextFloat(0.7f, 1.3f)),

                Radius = parent.Radius * MathF.Sqrt(weights[i] / total),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationRate = Main.rand.NextFloat(0.002f, 0.009f) * (Main.rand.NextBool() ? 1f : -1f),

                // Already visible: a droplet is not condensing, it is what the mote was made of.
                Alpha = 1f,
                Spike = parent.Spike,
                SpikeAngle = angle,
                Cohesion = InkStyle.MoteBurstCohesion,
            });
        }

        return start;
    }

    /// <summary>The mote under a point, largest first, or -1 if the click landed on the ink.</summary>
    /// <remarks>
    ///     Largest first because motes overlap constantly in a field built to make them overlap,
    ///     and the one the player believes they clicked is the one whose silhouette they can see.
    /// </remarks>
    private int HitTest(Vector2 point)
    {
        var found = -1;
        var best = 0f;

        for (var i = 0; i < motes.Count; i++)
        {
            var mote = motes[i];
            var reach = mote.Radius * InkStyle.MoteHitScale;

            if (Vector2.DistanceSquared(mote.Position, point) > reach * reach)
            {
                continue;
            }

            if (found < 0 || mote.Radius > best)
            {
                found = i;
                best = mote.Radius;
            }
        }

        return found;
    }

    /// <summary>
    ///     Merges motes near the pointer that have run into each other, conserving area and
    ///     momentum.
    /// </summary>
    /// <remarks>
    ///     Gated on being near the cursor, which is what makes gathering something the player does
    ///     rather than something that happens: ungated, the field would slowly collapse into a
    ///     handful of large beads on its own, and the drag would have nothing left to gather.
    ///     <para />
    ///     Quadratic, over a list capped at <see cref="InkStyle.MoteBudget" /> and filtered to the
    ///     few motes actually under the pointer. A grid would be the answer at a scale this will
    ///     never reach.
    /// </remarks>
    private void Coalesce(Vector2 cursor)
    {
        for (var i = motes.Count - 1; i >= 1; i--)
        {
            var a = motes[i];

            if (!CanMerge(in a, cursor))
            {
                continue;
            }

            for (var j = i - 1; j >= 0; j--)
            {
                var b = motes[j];

                if (!CanMerge(in b, cursor))
                {
                    continue;
                }

                var reach = (a.Radius + b.Radius) * InkStyle.MoteMergeOverlap;

                if (Vector2.DistanceSquared(a.Position, b.Position) > reach * reach)
                {
                    continue;
                }

                var radius = MathF.Sqrt(a.Mass + b.Mass);

                // Past the cap the bead would swallow the frame rather than sit in it, so the two
                // are left touching instead. The metaball cut already has them reading as one
                // shape; what is refused is only the bookkeeping.
                if (radius > InkStyle.MoteRadiusMax * InkStyle.MoteMergeCap)
                {
                    continue;
                }

                var mass = a.Mass + b.Mass;

                b.Position = (a.Position * a.Mass + b.Position * b.Mass) / mass;
                b.Velocity = (a.Velocity * a.Mass + b.Velocity * b.Mass) / mass;
                b.Radius = radius;
                b.Alpha = MathF.Max(a.Alpha, b.Alpha);
                b.Rotation = a.Mass > b.Mass ? a.Rotation : b.Rotation;

                // The stronger pull wins the spike outright rather than being averaged: an average
                // of two directions points at neither, and the bead would briefly reach somewhere
                // the cursor is not.
                if (a.Spike > b.Spike)
                {
                    b.Spike = a.Spike;
                    b.SpikeAngle = a.SpikeAngle;
                }

                b.Cohesion = InkStyle.MoteMergeSettle;

                motes[j] = b;
                motes.RemoveAt(i);

                break;
            }
        }
    }

    private static bool CanMerge(in Mote mote, Vector2 cursor)
        => mote.Cohesion <= 0
           && Vector2.DistanceSquared(mote.Position, cursor) <= InkStyle.MoteMergeReach * InkStyle.MoteMergeReach;

    /// <summary>
    ///     Drifts a fresh mote in from the rim while the field is carrying less ink than it should.
    /// </summary>
    /// <remarks>
    ///     Mass rather than population is the thing held steady, because burst and merge both leave
    ///     mass alone while moving the count in opposite directions. Topping up by count would have
    ///     every click permanently add ink to the frame, and every drag permanently remove it.
    /// </remarks>
    private void Refill(in InkFrame frame, float mass)
    {
        if (mass >= MassTarget || motes.Count >= InkStyle.MoteBudget)
        {
            return;
        }

        if (Main.rand.NextBool(InkStyle.MoteRefillChance))
        {
            motes.Add(Spawn(in frame, scattered: false));
        }
    }

    /// <summary>How far past its centre a mote can still put ink, for the off-screen test.</summary>
    private static float StampReach(in Mote mote)
    {
        // The stretch lengthens the stamp along the spike, so the margin has to cover a fully
        // magnetised mote or a bead reaching in from just off-frame would be culled mid-spike.
        return mote.Radius * InkStyle.MoteStampSpread * (1f + InkStyle.MoteSpikeStretch);
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
            Cohesion = 0,
        };
    }
}
