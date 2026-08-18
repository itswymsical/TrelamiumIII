using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

internal sealed class ChromaticScreenEffect : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:ChromaticAberration";

    private const float Cutoff = 0.002f;

    private const float BaseScale = 3f;

    private static ChromaticShaderData? shaderData;

    private static float intensity;
    private static float target;
    private static float rate;

    private static float spread = BaseScale;
    private static float scaleTarget = BaseScale;

    public static float Intensity => intensity;

    public static bool IsRunning => intensity > Cutoff || target > Cutoff;

    public static Asset<Texture2D>? Noise { get; set; }

    public static void Set(float intensity, float spread, int ticks = 30)
    {
        if (Main.dedServ || shaderData is null)
            return;

        target = Math.Clamp(intensity, 0f, 1f);
        scaleTarget = Math.Max(spread, 0.1f);

        rate = Math.Abs(target - ChromaticScreenEffect.intensity) / Math.Max(ticks, 1);

        if (rate <= 0f)
            ChromaticScreenEffect.intensity = target;
    }

    public static void Stop(int ticks = 40) => Set(0f, scaleTarget, ticks);

    public static void Cut()
    {
        intensity = 0f;
        target = 0f;
        rate = 0f;
        spread = scaleTarget = BaseScale;

        Deactivate();
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Noise = Assets.Textures.Noise.Perlin.Asset;

        shaderData = new ChromaticShaderData(Assets.Shaders.ChromaticAberration.Asset, "P0");

        // Low: this is an affliction the player is under, and anything the world does -- a slam's
        // action lines, a whiteout -- should read on top of it rather than underneath.
        Filters.Scene[Key] = new Filter(shaderData, EffectPriority.VeryHigh);
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
        parameters.uSpread = spread;

        parameters.ImageSampler = new HlslSampler2D { Texture = noise, Sampler = SamplerState.LinearWrap };

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
        spread = MathHelper.Lerp(spread, scaleTarget, 0.05f);
    }

    private static void Deactivate()
    {
        if (shaderData is null)
            return;

        if (Filters.Scene[Key].IsActive())
            Filters.Scene[Key].Deactivate();
    }
}

internal sealed class ChromaticShaderData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    private readonly string passName = passName;

    public Assets.Shaders.ChromaticAberration.Parameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}