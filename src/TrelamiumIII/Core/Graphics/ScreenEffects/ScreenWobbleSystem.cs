using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>
///     A slow FBM warp over the whole frame. Something is wrong with your eyes, not with the world.
/// </summary>
/// <remarks>
///     <para>
///         Same shape as <see cref="ActionLinesSystem" />, and held rather than fired for the same
///         reason -- it tracks a condition the player is under, not an event that happened.
///     </para>
///     <para>
///         Two knobs instead of one. Strength is how far the frame is pulled, scale is how fine the
///         pull is, and they do different jobs: strength alone reads as the camera drifting, while
///         raising the frequency alongside it is what makes it read as vision failing. A stacking
///         debuff should climb both.
///     </para>
/// </remarks>
internal sealed class ScreenWobbleSystem : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:ScreenWobble";

    private const float Cutoff = 0.002f;

    /// <summary>Neutral warp frequency, per the shader's own header.</summary>
    private const float BaseScale = 3f;

    private static ScreenWobbleShaderData? shaderData;

    private static float intensity;
    private static float target;
    private static float rate;

    private static float scale = BaseScale;
    private static float scaleTarget = BaseScale;

    public static float Intensity => intensity;

    public static bool IsRunning => intensity > Cutoff || target > Cutoff;

    /// <summary>See <see cref="ActionLinesSystem.Noise" />; the same smooth source suits both.</summary>
    public static Asset<Texture2D>? Noise { get; set; }

    /// <summary>
    ///     Sets where the warp is heading.
    /// </summary>
    /// <remarks>
    ///     Safe to call every frame with the same figures, which is how a stacking debuff should
    ///     drive it -- the ramp means a stack landing or falling off is a slide rather than a step.
    /// </remarks>
    /// <param name="strength">0 to 1.</param>
    /// <param name="warpScale">Noise frequency. Around <see cref="BaseScale" /> is neutral.</param>
    /// <param name="ticks">Frames to get there.</param>
    public static void Set(float strength, float warpScale, int ticks = 30)
    {
        if (Main.dedServ || shaderData is null)
            return;

        target = Math.Clamp(strength, 0f, 1f);
        scaleTarget = Math.Max(warpScale, 0.1f);

        rate = Math.Abs(target - intensity) / Math.Max(ticks, 1);

        if (rate <= 0f)
            intensity = target;
    }

    public static void Stop(int ticks = 40) => Set(0f, scaleTarget, ticks);

    public static void Cut()
    {
        intensity = 0f;
        target = 0f;
        rate = 0f;
        scale = scaleTarget = BaseScale;

        Deactivate();
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Noise = Assets.Textures.Noise.Perlin.Asset;

        shaderData = new ScreenWobbleShaderData(Assets.Shaders.ScreenWobble.Asset, "P0");

        // Low: this is an affliction the player is under, and anything the world does -- a slam's
        // action lines, a whiteout -- should read on top of it rather than underneath.
        Filters.Scene[Key] = new Filter(shaderData, EffectPriority.Medium);
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

        if (Noise is not { Value: { } noise })
        {
            Deactivate();
            return;
        }

        var parameters = shaderData.Parameters;

        parameters.uIntensity = intensity;
        parameters.uScale = scale;
        parameters.uTime = Main.GlobalTimeWrappedHourly;
        parameters.uNoiseTex = noise;
        parameters.NoiseSampler = new HlslSampler2D { Texture = noise, Sampler = SamplerState.LinearWrap };

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

        // Chases on a fixed fraction rather than the strength's rate: frequency has no natural
        // duration to match, and a slide is all it needs.
        scale = MathHelper.Lerp(scale, scaleTarget, 0.05f);
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
///     Carries the generated <c>ScreenWobble</c> parameters on the base type
///     <see cref="Filters.Scene" /> will accept. See <see cref="ActionLinesShaderData" /> for why the
///     parameter push goes before <c>base.Apply()</c> and how the colliding uniform names are kept
///     consistent.
/// </summary>
internal sealed class ScreenWobbleShaderData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    private readonly string passName = passName;

    public Assets.Shaders.ScreenWobble.Parameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}
