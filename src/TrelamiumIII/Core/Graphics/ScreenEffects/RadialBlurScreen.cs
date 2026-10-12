using ReLogic.Content;
using Terraria.Graphics.Effects;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>Zoom/spin blur over the finished frame, focused on a world point or on the screen centre.</summary>
internal sealed class RadialBlurEffect : ScreenFilterSystem<RadialBlurEffect, Assets.Shaders.RadialBlur.Parameters>
{
    private const int DefaultRampTicks = 8;

    /// <summary>Last target above the cutoff, so the spin fades with the strength.</summary>
    private float peak;

    private float spin;
    private float falloff = 1f;
    private float mix = 1f;

    /// <summary>World-space focus; null centres the blur on the screen.</summary>
    private Vector2? focus;

    /// <summary>Ticks until the blur stops itself; -1 holds it until <see cref="ScreenFilterSystem{TSelf, TParameters}.Stop" />.</summary>
    private int lifetimeRemaining = -1;

    protected override Asset<Effect> Shader => Assets.Shaders.RadialBlur.Asset;

    protected override EffectPriority Priority => EffectPriority.High;

    /// <summary>
    ///     Ramps to <paramref name="intensity" /> (radial reach, 0..1) over <paramref name="rampTicks" /> and holds it for
    ///     <paramref name="ticks" />, or until stopped when -1.
    /// </summary>
    /// <param name="falloff">Exponent of the reach ramp away from the focus; 0 is uniform.</param>
    /// <param name="rSpin">Tangential reach, 0..1.</param>
    /// <param name="targetMix">Dry/wet mix, 0..1.</param>
    /// <param name="worldFocus">World position the blur streaks away from; null for the screen centre.</param>
    public static void Set(
        float intensity,
        float falloff = 1f,
        float rSpin = 0f,
        int ticks = -1,
        float targetMix = 1f,
        Vector2? worldFocus = null,
        int rampTicks = DefaultRampTicks)
    {
        RadialBlurEffect self = Instance;

        self.RampTo(intensity, rampTicks);

        if (intensity > Cutoff)
            self.peak = Math.Min(intensity, 1f);

        self.lifetimeRemaining = ticks;
        self.falloff = Math.Max(falloff, 0f);
        self.spin = Math.Clamp(rSpin, 0f, 1f);
        self.mix = Math.Clamp(targetMix, 0f, 1f);
        self.focus = worldFocus;
    }

    protected override void StopRamp(int ticks)
    {
        lifetimeRemaining = -1;
        base.StopRamp(ticks);
    }

    protected override void Cut()
    {
        lifetimeRemaining = -1;
        base.Cut();
    }

    protected override void Tick()
    {
        if (lifetimeRemaining > 0 && --lifetimeRemaining == 0)
            StopRamp(40);
    }

    protected override bool Push(Assets.Shaders.RadialBlur.Parameters parameters)
    {
        var screen = new Vector2(Main.screenWidth, Main.screenHeight);

        // The filter samples the zoomed frame, so the world focus goes through the zoom before it becomes uv.
        parameters.uCenter = focus is { } world
            ? Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / screen
            : new Vector2(0.5f);

        parameters.uStrength = Strength;
        parameters.uSpin = spin * Math.Clamp(Strength / Math.Max(peak, Cutoff), 0f, 1f);
        parameters.uFalloff = falloff;
        parameters.uMix = mix;
        parameters.uAspect = new Vector2(screen.X / Math.Max(screen.Y, 1f), 1f);

        return true;
    }
}
