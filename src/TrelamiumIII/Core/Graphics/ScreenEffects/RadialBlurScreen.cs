using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>Zoom/spin blur over the finished frame, focused on a world point or on the screen centre.</summary>
internal sealed class RadialBlurEffect : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:RadialBlur";

    private const float Cutoff = 0.002f;

    private const int DefaultRampTicks = 8;

    private static RadialBlurData? shaderData;

    private static float strength;
    private static float target;

    /// <summary>Strength moved per tick toward <see cref="target" />.</summary>
    private static float rate;

    /// <summary>Last non-zero target, so the spin fades out with the strength.</summary>
    private static float peak;

    private static float spin;
    private static float falloff = 1f;
    private static float mix = 1f;

    /// <summary>World-space focus; null centres the blur on the screen.</summary>
    private static Vector2? focus;

    /// <summary>Ticks until the blur stops itself; -1 holds it until <see cref="Stop" />.</summary>
    private static int lifetimeRemaining = -1;

    public static float Intensity => strength;

    public static bool IsRunning => strength > Cutoff || target > Cutoff;

    public static Asset<Texture2D>? Noise { get; set; }

    /// <summary>
    ///     Ramps to <paramref name="intensity" /> (radial reach, 0..1) over <paramref name="rampTicks" /> and holds it for
    ///     <paramref name="ticks" />, or until <see cref="Stop" /> when -1.
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
        if (Main.dedServ || shaderData is null)
            return;

        target = Math.Clamp(intensity, 0f, 1f);
        rate = Math.Abs(target - strength) / Math.Max(rampTicks, 1);

        if (target > Cutoff)
            peak = target;

        lifetimeRemaining = ticks;

        RadialBlurEffect.falloff = Math.Max(falloff, 0f);
        spin = Math.Clamp(rSpin, 0f, 1f);
        mix = Math.Clamp(targetMix, 0f, 1f);
        focus = worldFocus;
    }

    public static void Stop(int ticks = 40)
    {
        target = 0f;
        rate = strength / Math.Max(ticks, 1);
        lifetimeRemaining = -1;
    }

    public static void Cut()
    {
        strength = 0f;
        target = 0f;
        rate = 0f;
        lifetimeRemaining = -1;

        Deactivate();
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Noise = Assets.Textures.Noise.Perlin.Asset;

        shaderData = new RadialBlurData(Assets.Shaders.RadialBlur.Asset, "P0");

        Filters.Scene[Key] = new Filter(shaderData, EffectPriority.High);
        Filters.Scene[Key].Load();
    }

    public override void Unload()
    {
        shaderData = null;
        Noise = null;

        strength = 0f;
        target = 0f;
    }

    public override void OnWorldUnload() => Cut();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || shaderData is null)
            return;

        if (lifetimeRemaining > 0 && --lifetimeRemaining == 0)
            Stop();

        Advance();

        if (!IsRunning)
        {
            Deactivate();
            return;
        }

        var screen = new Vector2(Main.screenWidth, Main.screenHeight);
        var parameters = shaderData.Parameters;

        // The filter samples the zoomed frame, so the world focus goes through the zoom before it becomes uv.
        parameters.uCenter = focus is { } world
            ? Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / screen
            : new Vector2(0.5f);

        parameters.uStrength = strength;
        parameters.uSpin = spin * Math.Clamp(strength / Math.Max(peak, Cutoff), 0f, 1f);
        parameters.uFalloff = falloff;
        parameters.uMix = mix;
        parameters.uAspect = new Vector2(screen.X / Math.Max(screen.Y, 1f), 1f);

        if (!Filters.Scene[Key].IsActive())
            Filters.Scene.Activate(Key);
    }

    private static void Advance()
    {
        if (strength < target)
            strength = Math.Min(strength + rate, target);
        else if (strength > target)
            strength = Math.Max(strength - rate, target);
    }

    private static void Deactivate()
    {
        if (shaderData is null)
            return;

        if (Filters.Scene[Key].IsActive())
            Filters.Scene[Key].Deactivate();
    }
}

internal sealed class RadialBlurData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    private readonly string passName = passName;

    public Assets.Shaders.RadialBlur.Parameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}
