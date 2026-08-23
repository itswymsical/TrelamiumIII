using ReLogic.Content;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     How a piece of the remains is currently behaving.
/// </summary>
/// <remarks>
///     The cutscene owns this. A piece spawns in <see cref="Tumble" /> and stays there through the
///     ordinary death -- so a player who has already seen the cutscene, or who skips it, gets debris
///     that simply falls and beds into the ground. Everything past that is the cutscene taking hold.
/// </remarks>
public enum GoreDrift
{
    /// <summary>Ordinary debris: gravity, tile collision, settles and fades.</summary>
    Tumble,

    /// <summary>Lifted out of the world. Gravity off, drifting up and bobbing.</summary>
    Float,

    /// <summary>Wound inward on the vortex.</summary>
    Drawn,
}

/// <summary>
///     One piece of the Eye of Cthulhu, wearing the gore sprite the base game threw for it.
/// </summary>
/// <remarks>
///     <para>
///         A <see cref="Particle" /> rather than a <see cref="ModGore" /> for the reason the base
///         class gives: a player who turns gore off is asking not to see viscera, not asking the
///         cutscene to lose its cast. The pieces are re-staged debris, so they survive that setting.
///     </para>
///     <para>
///         The sprite normally arrives through <see cref="Particle.TextureOverride" />, set by
///         whatever spawned the piece from the gore vanilla had already created -- which is why there
///         are no gore ids in the interesting path here. <see cref="Textures" /> is only the fallback
///         for when there was no vanilla gore to adopt, which happens whenever the player has gore
///         disabled.
///     </para>
/// </remarks>
public sealed class EyeGoreParticle : Particle
{
    /// <summary>
    ///     Fallback sprites, used only when no vanilla gore was available to adopt.
    /// </summary>
    /// <remarks>
    ///     Vanilla gore keys, so they resolve through <see cref="Main.Assets" /> rather than the mod.
    ///     These are a generic dark chunk rather than the Eye's own pieces -- worth eyeballing once
    ///     in game with gore disabled and swapping for whichever indices read best, since this path
    ///     cannot borrow the real thing.
    /// </remarks>
    public override string[] Textures => ["Images/Gore_6", "Images/Gore_7", "Images/Gore_8"];

    /// <summary>Tighter than the sprite, so a settling chunk beds into a corner instead of hovering.</summary>
    public override Vector2 Size => new(10f);

    public override float Gravity => Drift == GoreDrift.Tumble ? 0.2f : 0f;

    public override bool TileCollide => Drift == GoreDrift.Tumble;

    public override float Drag => Drift == GoreDrift.Tumble ? 0.995f : 0.98f;

    public override float Bounce => 0.34f;

    public override int FadeOutTicks => 30;

    /// <summary>
    ///     Off. The pieces are lit by the bloom they carry, and the cutscene plays at night in an
    ///     arena where world light would drag them toward black just as they become the subject.
    /// </summary>
    public override bool UseLighting => false;

    public override bool HasGlow => true;

    public GoreDrift Drift { get; set; } = GoreDrift.Tumble;

    /// <summary>Point this piece is wound onto while <see cref="GoreDrift.Drawn" />.</summary>
    public Vector2 VortexCenter { get; set; }

    /// <summary>0..1, how far the vortex has taken hold. Drives radius and spin together.</summary>
    public float VortexProgress { get; set; }

    /// <summary>0..1 master envelope, so the cutscene can bring the bloom up and take it away.</summary>
    public float GlowEnvelope { get; set; }

    /// <summary>Phase offset, so pieces bob and shimmer out of step with each other.</summary>
    private float phase;

    /// <summary>Angle and radius on the vortex, seeded on the frame the wind-in starts.</summary>
    private float vortexAngle;
    private float vortexRadius;
    private bool vortexSeeded;

    private Asset<Texture2D>? haloTexture;

    /// <summary>
    ///     The sub-rectangle of the sprite this piece wears, or null for the whole texture.
    /// </summary>
    /// <remarks>
    ///     <see cref="Particle" /> models frames as a vertical strip -- <c>Height / FrameCount</c> --
    ///     which cannot describe a vanilla gore sheet laid out as a GRID. A sheet like that drawn
    ///     without a source rect renders in full, as a lattice of every sprite on it, so anything
    ///     wearing a sheeted gore has to be told which cell it is.
    ///     <para>
    ///         Read by all three draw paths -- body, bloom and coverage stamp -- so a piece cannot end
    ///         up glowing or casting a silhouette in a shape its body does not have.
    ///     </para>
    /// </remarks>
    public Rectangle? SourceRect { get; set; }

    private Rectangle Cell(Texture2D texture) => SourceRect ?? texture.Bounds;

    /// <summary>
    ///     Draws the body, so the source rect is honoured.
    /// </summary>
    /// <remarks>
    ///     Takes the draw over rather than letting the base class do it, because the base resolves its
    ///     own source the vertical-strip way. Returning false is the documented way to do that.
    /// </remarks>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        Rectangle source = Cell(texture);

        spriteBatch.Draw(
            texture,
            drawPosition,
            source,
            lightColor,
            Rotation,
            new Vector2(source.Width, source.Height) * 0.5f,
            Scale,
            SpriteEffects.None,
            0f
        );

        return false;
    }
    public override void OnSpawn()
    {
        phase = Main.rand.NextFloat(MathHelper.TwoPi);
        haloTexture = Assets.Textures.VFX.Bloom.Asset;
    }

    public override void Update()
    {
        switch (Drift)
        {
            case GoreDrift.Float:
                UpdateFloat();
                break;

            case GoreDrift.Drawn:
                UpdateDrawn();
                break;
        }

        Bleed();
    }

    /// <summary>
    ///     Lifted and bobbing. The piece keeps a little of whatever momentum it had so the takeover
    ///     eases in rather than snapping it to a new motion.
    /// </summary>
    private void UpdateFloat()
    {
        phase += 0.045f;

        // Rises slowly and sways on its own phase. Two incommensurate rates so a cluster never
        // settles into one synchronised pulse.
        Velocity.Y += MathF.Sin(phase) * 0.012f - 0.014f;
        Velocity.X += MathF.Cos(phase * 0.73f) * 0.010f;

        // Turns lazily rather than holding the spin it landed with.
        RotationVelocity = MathHelper.Lerp(RotationVelocity, MathF.Sin(phase * 0.5f) * 0.012f, 0.05f);
    }

    /// <summary>
    ///     Wound inward. Angle and radius are integrated rather than lerped toward a target, so the
    ///     piece traces a spiral instead of sliding along a straight line to the centre.
    /// </summary>
    private void UpdateDrawn()
    {
        Vector2 offset = Position - VortexCenter;

        if (!vortexSeeded)
        {
            vortexAngle = offset.ToRotation();
            vortexRadius = offset.Length();
            vortexSeeded = true;
        }

        float progress = MathHelper.Clamp(VortexProgress, 0f, 1f);

        // Same Keplerian shape the menu's infalling streaks use: the closer in, the faster round,
        // so the piece winds up hard at the throat instead of coasting in at a constant rate.
        float safeRadius = MathF.Max(vortexRadius, 12f);
        vortexAngle += MathHelper.Lerp(0.9f, 2.6f, progress) * 24f / (safeRadius * MathF.Sqrt(safeRadius));
        vortexRadius = MathF.Max(vortexRadius - MathHelper.Lerp(0.7f, 3.4f, progress), 0f);

        Vector2 target = VortexCenter + vortexAngle.ToRotationVector2() * vortexRadius;

        // Velocity is derived from where it needs to be, not added to, so the spiral is exact and
        // cannot be knocked off course by leftover drag.
        Velocity = target - Position;

        // Spin tracks the wind-in, and tightens with it.
        RotationVelocity = MathHelper.Lerp(0.05f, 0.3f, progress);

        // Swallowed once it reaches the throat.
        if (vortexRadius <= 10f)
            Kill();
    }

    /// <summary>Ink shed by the piece, plus the odd spark struck off it.</summary>
    private void Bleed()
    {
        float envelope = MathHelper.Clamp(GlowEnvelope, 0f, 1f);

        if (envelope <= 0f)
            return;

        // Heavier while the vortex has it: a piece being torn away should visibly leak more than one
        // hanging still.
        float intensity = Drift == GoreDrift.Drawn ? 0.95f : 0.55f;

        InkLayer.Shroud(Position, Velocity, intensity * envelope);

        if (Drift == GoreDrift.Drawn && Velocity.LengthSquared() > 9f)
            InkLayer.Trail(Position, Velocity, 11f);

        // Sparks are occasional on purpose. The bloom carries the Fallen Star read continuously; the
        // sparks are punctuation, and at every-tick they stop being legible as individual glints.
        if (Main.rand.NextFloat() < 0.16f * envelope)
        {
            Vector2 offset = Main.rand.NextVector2Circular(14f, 14f);

            ParticleSystem.Spawn<InkSparkParticle>(
                Position + offset,
                Velocity * 0.4f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                timeLeft: Main.rand.Next(22, 40),
                scale: Main.rand.NextFloat(0.5f, 1.05f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.05f, 0.05f)
            );
        }
    }

    /// <summary>
    ///     The Fallen Star read: the sprite sampled into its own bloom, with a soft halo under it.
    /// </summary>
    /// <remarks>
    ///     Two parts, and the order matters because both are additive. The halo goes down first as a
    ///     wide, weak indigo wash -- it is what the eye reads as light in the air around the piece,
    ///     and it is mostly there so the silhouette has something to sit against. Then the sprite
    ///     itself is stamped <see cref="BLOOM_COPIES" /> times around a small circle whose angle
    ///     advances with time, which is the trick the base game uses on a fallen star: no authored
    ///     glow texture, just the sprite smeared around its own centre until the shape reads as
    ///     emitting rather than as lit.
    ///     <para>
    ///         The copies are scaled slightly past the body so the bloom pokes out beyond the crisp
    ///         edge. Kept small -- past about 1.25 the piece stops reading as a solid object with a
    ///         glow and starts reading as a blurry sprite.
    ///     </para>
    /// </remarks>
    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        float envelope = MathHelper.Clamp(GlowEnvelope, 0f, 1f) * opacity;

        if (envelope <= 0f)
            return;

        // Breathes, and on the piece's own phase so a cluster shimmers unevenly.
        float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + phase);

        if (haloTexture is { Value: { } halo })
        {
            float spread = texture.Width * HALO_SPREAD * Scale / halo.Width;

            spriteBatch.Draw(
                halo,
                drawPosition,
                null,
                HaloColor * (HALO_STRENGTH * pulse * envelope),
                0f,
                halo.Size() * 0.5f,
                spread,
                SpriteEffects.None,
                0f
            );
        }

        Rectangle source = Cell(texture);
        var origin = new Vector2(source.Width, source.Height) * 0.5f;

        float spin = Main.GlobalTimeWrappedHourly * BLOOM_SPIN + phase;
        Color copyColor = BloomColor * (BLOOM_STRENGTH * pulse * envelope / BLOOM_COPIES);

        for (int i = 0; i < BLOOM_COPIES; i++)
        {
            float angle = spin + MathHelper.TwoPi * i / BLOOM_COPIES;
            Vector2 offset = angle.ToRotationVector2() * BLOOM_RADIUS * Scale;

            spriteBatch.Draw(
                texture,
                drawPosition + offset,
                source,
                copyColor,
                Rotation,
                origin,
                Scale * BLOOM_SCALE,
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>
    ///     Stamps this piece into a coverage field as flat white.
    /// </summary>
    /// <remarks>
    ///     Lives here rather than in the renderer that calls it because the sprite a piece is
    ///     wearing is resolved privately -- it may be a vanilla gore adopted at spawn, and nothing
    ///     outside the particle should have to know which.
    ///     <para>
    ///         Drawn larger than the body and at partial strength. The extra spread gives the
    ///         silhouette room to carry a rim outside the sprite, and the partial strength is what
    ///         lets two pieces SUM into one shape where they overlap instead of one of them simply
    ///         saturating the field on its own.
    ///     </para>
    /// </remarks>
    public void StampCoverage(SpriteBatch spriteBatch)
    {
        if (ResolveTexture() is not { } texture)
            return;

        Rectangle source = Cell(texture);
        var origin = new Vector2(source.Width, source.Height) * 0.5f;

        spriteBatch.Draw(
            texture,
            Position - Main.screenPosition,
            source,
            Color.White * COVERAGE_STRENGTH,
            Rotation,
            origin,
            Scale * COVERAGE_SPREAD,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>How much of the field one piece contributes. See StampCoverage.</summary>
    private const float COVERAGE_STRENGTH = 0.7f;

    private const float COVERAGE_SPREAD = 1.45f;
    /// <summary>
    ///     Copies stamped around the sprite. Six reads as a continuous bloom; three reads as three
    ///     ghosts, because at that count the eye still resolves each one.
    /// </summary>
    private const int BLOOM_COPIES = 6;

    /// <summary>Offset of each copy from the sprite centre, in pixels before <see cref="Particle.Scale" />.</summary>
    private const float BLOOM_RADIUS = 2.6f;

    private const float BLOOM_SCALE = 1.16f;

    private const float BLOOM_STRENGTH = 2.1f;

    /// <summary>Radians per second the copy ring turns. Slow: this is a shimmer, not a spin.</summary>
    private const float BLOOM_SPIN = 0.9f;

    private const float HALO_SPREAD = 3.4f;

    private const float HALO_STRENGTH = 0.5f;

    /// <summary>
    ///     Cold violet, the same end of the palette <see cref="InkLayer" /> rims its silhouette with,
    ///     so the bloom and the gas read as one material rather than two effects layered up.
    /// </summary>
    private static readonly Color BloomColor = new(150, 142, 224);

    private static readonly Color HaloColor = new(74, 62, 150);
}
