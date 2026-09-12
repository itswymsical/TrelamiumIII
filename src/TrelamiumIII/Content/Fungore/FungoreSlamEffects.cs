using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A one-shot animation on the ground where a slam landed, drawn bottom-anchored since the frame's bottom edge is
///     the floor. <see cref="FrameHeight" /> is stated because neither sheet divides evenly; the last frame is clamped.
/// </summary>
internal abstract class SlamEffect : Particle
{
    /// <summary>Height of one frame. Stated, not derived: the sheets don't divide evenly.</summary>
    protected abstract int FrameHeight { get; }

    protected abstract int TicksPerFrame { get; }

    public int Lifetime => Math.Max(FrameCount, 1) * TicksPerFrame;

    public override float Gravity => 0f;

    public override float Drag => 1f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override int FadeOutTicks => TicksPerFrame;

    /// <summary>The base spawn picks a random frame; an animation must start at 0.</summary>
    public override void OnSpawn() => Frame = 0;

    public override void Update()
        => Frame = Math.Clamp((int)(Progress * FrameCount), 0, Math.Max(FrameCount - 1, 0));

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        int frames = Math.Max(FrameCount, 1);
        int top = FrameHeight * Math.Clamp(Frame, 0, frames - 1);

        // Clamped: the last frame runs past the end of a sheet that isn't quite tall enough.
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

    /// <summary>Unlit, so the spores read the same in a dark cave as at noon.</summary>
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
