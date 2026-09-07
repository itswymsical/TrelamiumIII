using ReLogic.Content;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     How a piece of the remains is currently behaving.
/// </summary>
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
public sealed class EyeGoreParticle : Particle
{
    /// <summary>
    ///     Fallback sprites, used only when no vanilla gore was available to adopt.
    /// </summary>
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
    public Rectangle? SourceRect { get; set; }

    private Rectangle Cell(Texture2D texture) => SourceRect ?? texture.Bounds;

    /// <summary>
    ///     Draws the body, so the source rect is honoured.
    /// </summary>
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
