using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

internal sealed class RadialBlurEffect : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:RadialBlur";

    private const float Cutoff = 0.002f;

    private static RadialBlurData? shaderData;

    private static float strength;
    private static float mix;
    private static float spin;

    private static float target;
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
    /// <param name="spread">The falloff exponent determining how fast the streak ramps out from focus.</param>
    /// <param name="rSpin">The tangential whirl/spin strength (0.0 to 1.0).</param>
    /// <param name="ticks">Duration in game ticks over which to transition strength.</param>
    /// <param name="targetMix">The dry/wet mix factor (0.0 for original frame, 1.0 for full effect).</param>
    public static void Set(float intensity, float spread, float rSpin = 0f, int ticks = 30, float targetMix = 1f)
    {
        if (Main.dedServ || shaderData is null)
            return;

        target = Math.Clamp(intensity, 0f, 1f);
        spin = Math.Clamp(rSpin, 0f, 1f);
        mix = Math.Clamp(targetMix, 0f, 1f);

        // Falloff controls how sharp the clear center region remains
        falloffTarget = Math.Max(spread, 0.1f);

        // Calculate step rate per tick based on the requested duration
        rate = Math.Abs(target - strength) / Math.Max(ticks, 1);

        // Instant swap if rate is zero or duration is instantaneous
        if (rate <= 0f)
            strength = target;
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
        if (Main.dedServ || shaderData is null)
            return;

        Advance();

        if (!IsRunning)
        {
            Deactivate();
            return;
        }

        var parameters = shaderData.Parameters;

        parameters.uMix = mix;
        parameters.uFalloff = fallOff;
        parameters.uSpin = spin;
        parameters.uStrength = strength;

        parameters.uCenter = new Vector2(0.5f, 0.5f);

        parameters.uAspect = new Vector2((float)Main.screenWidth / Main.screenHeight, 1f);

        shaderData.UseIntensity(strength);

        if (!Filters.Scene[Key].IsActive())
            Filters.Scene.Activate(Key);
    }

    private static void Advance()
    {
        if (strength < target)
            strength = Math.Min(strength + rate, target);
        else if (strength > target)
            strength = Math.Max(strength - rate, target);

        // Chases on a fixed fraction rather than the strength's rate: frequency has no natural
        // duration to match, and a slide is all it needs.
        fallOff = MathHelper.Lerp(fallOff, falloffTarget, 0.05f);
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