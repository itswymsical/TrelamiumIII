using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A one-shot animation played on the ground where a slam landed. Decoration, nothing else.
/// </summary>
/// <remarks>
///     <para>
///         <b>Bottom-anchored, not centred.</b> Both sheets draw their effect standing on the bottom
///         edge of the frame, because that edge is the floor. The base class centres its origin,
///         which would bury half of every burst in the ground, so drawing is taken over here.
///     </para>
///     <para>
///         <b>The frame height is stated rather than divided.</b> Neither sheet is an exact multiple
///         of its frame height -- both are two pixels short of one -- so dividing the texture would
///         shorten every frame by a pixel or two and walk the animation progressively out of
///         alignment. The source rectangle is clamped to whatever is actually left in the texture,
///         which costs the last frame its final two rows and nothing else.
///     </para>
/// </remarks>
internal abstract class SlamEffect : Particle
{
    /// <summary>Height of one frame. Stated, not derived -- see the class remarks.</summary>
    protected abstract int FrameHeight { get; }

    /// <summary>Frames held per animation frame.</summary>
    protected abstract int TicksPerFrame { get; }

    /// <summary>Total lifetime, so callers do not have to know the frame timing.</summary>
    public int Lifetime => Math.Max(FrameCount, 1) * TicksPerFrame;

    // Pinned to the spot it landed on. It is an impression left on the ground, not an object.
    public override float Gravity => 0f;

    public override float Drag => 1f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    /// <summary>One frame of fade, so the tail does not simply blink off.</summary>
    public override int FadeOutTicks => TicksPerFrame;

    /// <summary>
    ///     Spawn assigns a random frame, which is right for debris and wrong for an animation.
    /// </summary>
    public override void OnSpawn() => Frame = 0;

    public override void Update()
        => Frame = Math.Clamp((int)(Progress * FrameCount), 0, Math.Max(FrameCount - 1, 0));

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        int frames = Math.Max(FrameCount, 1);
        int top = FrameHeight * Math.Clamp(Frame, 0, frames - 1);

        // Clamped, because the last frame runs off the end of a sheet that is not quite tall enough
        // for it. Reading past the texture is undefined at best.
        int height = Math.Min(FrameHeight, texture.Height - top);

        if (height <= 0)
            return false;

        var source = new Rectangle(0, top, texture.Width, height);
        var origin = new Vector2(texture.Width * 0.5f, FrameHeight);

        spriteBatch.Draw(texture, drawPosition, source, lightColor, Rotation, origin, Scale, SpriteEffects.None, 0f);

        return false;
    }
}

/// <summary>Dust thrown up by an ordinary slam.</summary>
internal sealed class FungoreSlamSmoke : SlamEffect
{
    public override string[] Textures => [AssetReferences.Content.Fungore.FungoreSmoke.KEY];

    public override int FrameCount => 4;

    protected override int FrameHeight => 104;

    protected override int TicksPerFrame => 6;

    /// <summary>Lit, so a slam underground kicks up dark dust rather than glowing at you.</summary>
    public override bool UseLighting => true;

    /// <summary>Read-only, and only ever asked for its timing. Never simulated.</summary>
    private static readonly FungoreSlamSmoke Timing = new();

    public static void Spawn(Vector2 impact, float scale)
    {
        if (Main.dedServ)
            return;

        ParticleSystem.Spawn<FungoreSlamSmoke>(impact, Vector2.Zero, Timing.Lifetime, scale: scale);
    }
}

/// <summary>The burst a super slam leaves standing on the ground.</summary>
internal sealed class FungoreSuperSlamBurst : SlamEffect
{
    public override string[] Textures => [AssetReferences.Content.Fungore.FungoreSuperSlam.KEY];

    public override int FrameCount => 6;

    protected override int FrameHeight => 208;

    protected override int TicksPerFrame => 6;

    /// <summary>
    ///     Unlit. This one is the spores themselves going up, and it should read the same in a cave
    ///     at midnight as it does at noon.
    /// </summary>
    public override bool UseLighting => false;

    /// <summary>Read-only, and only ever asked for its timing. Never simulated.</summary>
    private static readonly FungoreSuperSlamBurst Timing = new();

    public static void Spawn(Vector2 impact, float scale)
    {
        if (Main.dedServ)
            return;

        ParticleSystem.Spawn<FungoreSuperSlamBurst>(impact, Vector2.Zero, Timing.Lifetime, scale: scale);
    }
}
