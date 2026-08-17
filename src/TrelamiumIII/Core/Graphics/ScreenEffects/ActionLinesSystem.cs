using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>
///     Radial speed lines over the finished frame, with a per-angle rumble worked into the warp.
/// </summary>
/// <remarks>
///     <para>
///         Same shape as <see cref="ScreenFlashSystem" />: a <see cref="Filters.Scene" /> pass, because
///         what it wants is the composed frame and nothing earlier. See that type for why a filter
///         beats hand-rolled targets here.
///     </para>
///     <para>
///         Unlike the flash this one is <em>held</em> rather than fired -- an intro keeps it up for
///         several seconds -- so the API is a target and a ramp rather than a duration. Two callers
///         asking for different strengths is resolved by whoever asked last, which is the same
///         "latest intent wins" rule the flash uses.
///     </para>
/// </remarks>
internal sealed class ActionLinesSystem : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:ActionLines";

    /// <summary>Below this the pass is not worth a full-screen sample and the filter comes off.</summary>
    private const float Cutoff = 0.002f;

    /// <summary>
    ///     Angular line count. Whole numbers only: the shader samples the noise at
    ///     <c>angle * uDensity</c>, so a fraction leaves a visible seam where the circle wraps.
    /// </summary>
    private const float DefaultDensity = 48f;

    private static ActionLinesShaderData? shaderData;

    private static float intensity;
    private static float target;

    /// <summary>How much <see cref="intensity" /> may move per frame. Derived from the ramp length.</summary>
    private static float rate;

    /// <summary>Current strength, 0 to 1. Worth reading if something wants to ride the effect.</summary>
    public static float Intensity => intensity;

    public static bool IsRunning => intensity > Cutoff || target > Cutoff;

    /// <summary>
    ///     Tiling noise the streaks and the rumble are both cut from.
    /// </summary>
    /// <remarks>
    ///     Wants a <em>smooth</em> source, not white noise. The warp samples it at two turns across
    ///     the whole circle, where anything high-frequency reads as the frame tearing rather than
    ///     shaking; the lines sample the same texture at <see cref="Density" /> turns, which is what
    ///     makes them fine. One texture, two frequencies.
    /// </remarks>
    public static Asset<Texture2D>? Noise { get; set; }

    public static float Density { get; set; } = DefaultDensity;

    /// <summary>
    ///     Rides the effect to <paramref name="strength" /> over <paramref name="ticks" /> frames.
    /// </summary>
    public static void Play(float strength, int ticks = 24)
    {
        if (Main.dedServ || shaderData is null)
            return;

        target = Math.Clamp(strength, 0f, 1f);
        rate = Math.Abs(target - intensity) / Math.Max(ticks, 1);

        // A ramp of zero length is a request to be there now, and the loop below only ever moves by
        // rate, so it would otherwise never arrive.
        if (rate <= 0f)
            intensity = target;
    }

    public static void Stop(int ticks = 40) => Play(0f, ticks);

    /// <summary>Drops the effect this frame, without a ramp. For teardown, not for pacing.</summary>
    public static void Cut()
    {
        intensity = 0f;
        target = 0f;
        rate = 0f;

        Deactivate();
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Noise = Assets.Textures.Noise.Perlin.Asset;

        shaderData = new ActionLinesShaderData(Assets.Shaders.ActionLines.Asset, "P0");

        // High rather than VeryHigh: a whiteout has to be able to blow straight through the lines,
        // and ScreenFlashSystem sits above this on purpose.
        Filters.Scene[Key] = new Filter(shaderData, EffectPriority.High);
        Filters.Scene[Key].Load();
    }

    public override void Unload()
    {
        shaderData = null;
        Noise = null;

        intensity = 0f;
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

        // The sampler cannot take a null texture, and the asset is still empty on the frames before
        // it finishes loading. Nothing is drawn until it lands rather than a frame of untextured
        // garbage.
        if (Noise is not { Value: { } noise })
        {
            Deactivate();
            return;
        }

        var parameters = shaderData.Parameters;

        parameters.uIntensity = intensity;
        parameters.uDensity = Density;
        parameters.uTime = Main.GlobalTimeWrappedHourly;
        parameters.uNoiseTex = noise;

        // Bound to the register the shader actually declares the sampler at, with the wrap state it
        // asks for. Setting the texture parameter alone leaves the addressing mode to whatever the
        // previous batch left in slot 1, and a clamped sampler turns the streaks into one smeared
        // band at the seam.
        parameters.NoiseSampler = new HlslSampler2D { Texture = noise, Sampler = SamplerState.LinearWrap };

        // Kept in step with the parameter above -- see ActionLinesShaderData for why both exist.
        shaderData.UseIntensity(intensity);

        if (!Filters.Scene[Key].IsActive())
            Filters.Scene.Activate(Key);
    }

    private static void Advance()
    {
        if (intensity < target)
            intensity = Math.Min(intensity + rate, target);
        else if (intensity > target)
            intensity = Math.Max(intensity - rate, target);
    }

    private static void Deactivate()
    {
        if (shaderData is null)
            return;

        if (Filters.Scene[Key].IsActive())
            Filters.Scene[Key].Deactivate();
    }
}

/// <summary>
///     Carries the generated <c>ActionLines</c> parameters on the base type
///     <see cref="Filters.Scene" /> will accept.
/// </summary>
/// <remarks>
///     <para>
///         Parameters go on before <c>base.Apply()</c>, exactly as in
///         <see cref="ScreenFlashShaderData" />, because that call is what ultimately applies the
///         pass -- a uniform written after it has missed this draw entirely.
///     </para>
///     <para>
///         Unlike the flash, two of this shader's uniforms are named the same as ones
///         <see cref="ScreenShaderData" /> writes for itself: <c>uTime</c> and <c>uIntensity</c>. The
///         base re-stamps both in between our write and the pass, so it gets the last word on them
///         either way, and the system simply keeps the two sources pointed at the same numbers --
///         vanilla writes <see cref="Main.GlobalTimeWrappedHourly" /> for the clock, which is what
///         this shader wanted anyway, and <see cref="ScreenShaderData.UseIntensity" /> is fed the
///         same strength as the parameter object.
///     </para>
/// </remarks>
internal sealed class ActionLinesShaderData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    // Shadows the primary constructor parameter to avoid CS9107, matching what the generated
    // WrapperShaderData does for the same reason.
    private readonly string passName = passName;

    public Assets.Shaders.ActionLines.Parameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}
