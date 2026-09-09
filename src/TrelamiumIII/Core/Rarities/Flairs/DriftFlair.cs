using ReLogic.Content;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>
///     Sprites drifting across the name and around the item: leaves, embers, motes, ash.
/// </summary>
/// <remarks>
///     <para>
///         This is the generalisation of the hand-written leaf loop that was pasted into the Magnolia
///         items and the Flower Satchel. Those copies had already diverged -- one drew four leaves in
///         pale goldenrod, another eight, a third the same four in forest green -- which is what
///         happens to forty lines of animation maths that live in three places. Here the numbers are
///         the declaration and the motion is written once.
///     </para>
///     <para>
///         Motion is a pure function of time and index, with no stored state. That means the drift is
///         identical on every machine, survives the tooltip closing and reopening without a restart,
///         and costs nothing to keep alive for a rarity nobody is currently hovering.
///     </para>
/// </remarks>
public sealed class DriftFlair : RarityFlair
{
    /// <summary>Path to the sheet. Frames stack vertically, as the mod's other sheets do.</summary>
    public required string TexturePath { get; init; }

    public int FrameCount { get; init; } = 1;

    /// <summary>Ticks each frame is held.</summary>
    public int TicksPerFrame { get; init; } = 6;

    /// <summary>How many drift at once.</summary>
    public int Count { get; init; } = 4;

    public Color? Color { get; init; }

    public float Scale { get; init; } = 1f;

    /// <summary>Horizontal travel in pixels per second, wrapping over the name's width.</summary>
    public float DriftSpeed { get; init; } = 15f;

    /// <summary>Downward travel in pixels per second, wrapping over <see cref="FallRange" />.</summary>
    public float FallSpeed { get; init; } = 8f;

    public float FallRange { get; init; } = 40f;

    public float SwayAmplitude { get; init; } = 15f;

    public float SwaySpeed { get; init; } = 1.5f;

    public float SpinSpeed { get; init; } = 0.8f;

    /// <summary>Depth of the brightness pulse, 0..1.</summary>
    public float Pulse { get; init; } = 0.3f;

    public float PulseSpeed { get; init; } = 1.2f;

    /// <summary>Whether the drift also plays around the item sprite, in slots and in the world.</summary>
    public bool OnSprite { get; init; } = true;

    /// <summary>Orbit radius around the sprite, as a multiple of its half-width.</summary>
    public float SpriteOrbit { get; init; } = 0.8f;

    /// <summary>Seconds between one drifter and the next, so they do not move in lockstep.</summary>
    public float Stagger { get; init; } = 1.5f;

    private Asset<Texture2D>? texture;

    public override BlendState Blend { get; set; } = BlendState.Additive;

    public override void Unload() => texture = null;

    public override void OverName(in RarityNameContext ctx)
    {
        if (Resolve() is not { } sheet)
            return;

        // Wrapping over the name's own width is what keeps the drift anchored to the text rather
        // than to a fixed box: a long name gets a wide, lazy drift and a short one a tight scatter,
        // with no per-item tuning.
        float width = MathF.Max(ctx.Size.X, 1f);

        for (int i = 0; i < Count; i++)
        {
            float t = ctx.Time + i * Stagger;

            var offset = new Vector2(
                MathF.Sin(t * SwaySpeed) * SwayAmplitude + Wrap(t * DriftSpeed, width),
                Wrap(t * FallSpeed, FallRange)
            );

            Draw(ctx.SpriteBatch, sheet, ctx.Position + offset, t, i, ctx.Style, ctx.Time);
        }
    }

    public override void OverSprite(in RaritySpriteContext ctx)
    {
        if (!OnSprite || Resolve() is not { } sheet)
            return;

        float radius = MathF.Max(ctx.SpriteSize.X, ctx.SpriteSize.Y) * 0.5f * SpriteOrbit;

        for (int i = 0; i < Count; i++)
        {
            float t = ctx.Time + i * Stagger;

            // An orbit rather than the name's linear drift: there is no width to wrap over here, and
            // a drifter that wandered off in a straight line would spend most of its life outside
            // the slot it belongs to.
            float angle = t * SwaySpeed + i / (float)Math.Max(Count, 1) * MathHelper.TwoPi;
            float breathe = 1f + MathF.Sin(t * PulseSpeed) * 0.2f;

            Vector2 offset = angle.ToRotationVector2() * radius * breathe;

            Draw(ctx.SpriteBatch, sheet, ctx.Center + offset, t, i, ctx.Style, ctx.Time);
        }
    }

    private void Draw(SpriteBatch spriteBatch, Texture2D sheet, Vector2 position, float t, int index,
        RarityStyle style, float time)
    {
        int frames = Math.Max(FrameCount, 1);
        int frameHeight = sheet.Height / frames;
        int frame = (int)(Main.GameUpdateCount / (uint)Math.Max(TicksPerFrame, 1) + index) % frames;

        var source = new Rectangle(0, frameHeight * frame, sheet.Width, frameHeight);
        var origin = new Vector2(sheet.Width, frameHeight) * 0.5f;

        Color tint = Color ?? style.GlowColor;
        Color drawn = tint * (1f - Pulse * 0.5f + MathF.Sin(time * PulseSpeed) * Pulse);

        spriteBatch.Draw(sheet, position, source, drawn, t * SpinSpeed, origin, Scale,
            SpriteEffects.None, 0f);
    }

    private Texture2D? Resolve()
    {
        if (Main.dedServ)
            return null;

        texture ??= ModContent.Request<Texture2D>(TexturePath);

        return texture.Value;
    }

    /// <summary>Positive modulo. <c>%</c> alone leaves negatives negative once time wraps.</summary>
    private static float Wrap(float value, float range)
    {
        if (range <= 0f)
            return 0f;

        float wrapped = value % range;

        return wrapped < 0f ? wrapped + range : wrapped;
    }
}
