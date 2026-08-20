using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

internal sealed class RadialBlurEffect : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:RadialBlur";

    private const float Cutoff = 0.002f;

    private static RadialBlurData? shaderData;

    private static float mix;
    private static float spin;

    private static float strength;
    private static float target;

    private static int lifetime;
    private static int lifetimeRemaining;

    private static bool IsInfinite => lifetime < 0;
    private static float rate;

    private static float fallOff;
    private static float falloffTarget;

    public static float Intensity => strength;

    public static bool IsRunning => strength > Cutoff || target > Cutoff;

    public static Asset<Texture2D>? Noise { get; set; }

    /// <summary>
    /// Activates or adjusts the radial blur effect parameters.
    /// </summary>
    /// <param name="intensity">The strength of the blur reach (0.0 to 1.0).</param>
    /// <param name="falloff">The falloff exponent determining how fast the streak ramps out from focus.</param>
    /// <param name="rSpin">The tangential whirl/spin strength (0.0 to 1.0).</param>
    /// <param name="ticks">Duration in game ticks over which to transition strength.</param>
    /// <param name="targetMix">The dry/wet mix factor (0.0 for original frame, 1.0 for full effect).</param>
    public static void Set(
        float intensity,
        float falloff = 1f,
        float rSpin = 0f,
        int ticks = -1,
        float targetMix = 1f)
    {
        target = Math.Clamp(intensity, 0f, 1f);

        // -1 = infinite
        lifetime = ticks;
        lifetimeRemaining = ticks;

        falloffTarget = Math.Max(falloff, 0.1f);
        spin = Math.Clamp(rSpin, 0f, 1f);
        mix = Math.Clamp(targetMix, 0f, 1f);
    }

    public static void Stop(int ticks = 40) => Set(0f, falloffTarget, spin, ticks);

    public static void Cut()
    {
        strength = 0f;
        target = 0f;
        rate = 0f;
        mix = 0f;
        fallOff = falloffTarget = 0f;

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
        mix = 0f;
        target = 0f;
    }

    public override void OnWorldUnload() => Cut();

    public override void PostUpdateEverything()
    {
        Advance();
    }
    private static void Advance()
    {
        // Nothing to do.
        if (!IsRunning)
            return;

        // -1 means infinite.
        if (!IsInfinite)
        {
            lifetimeRemaining--;

            if (lifetimeRemaining <= 0)
            {
                Stop();
                return;
            }
        }

        // Existing parameter interpolation.
        strength = target;

        fallOff = falloffTarget;
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