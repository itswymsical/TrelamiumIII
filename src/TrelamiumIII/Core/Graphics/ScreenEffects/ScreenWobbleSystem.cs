using ReLogic.Content;
using Terraria.Graphics.Effects;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>A slow FBM warp over the whole frame, for afflictions the player is under.</summary>
/// <remarks>Strength sets how far the frame is pulled and scale how fine the warp is; a stacking debuff should raise both.</remarks>
internal sealed class ScreenWobbleSystem : ScreenFilterSystem<ScreenWobbleSystem, Assets.Shaders.ScreenWobble.Parameters>
{
    /// <summary>Neutral warp frequency.</summary>
    private const float BaseScale = 3f;

    private Asset<Texture2D>? noise;

    private float scale = BaseScale;
    private float scaleTarget = BaseScale;

    protected override Asset<Effect> Shader => Assets.Shaders.ScreenWobble.Asset;

    /// <summary>Below world events such as action lines and flashes.</summary>
    protected override EffectPriority Priority => EffectPriority.Medium;

    /// <summary>
    ///     Ramps to <paramref name="strength" /> (0..1) over <paramref name="ticks" /> frames. Safe to call every frame with
    ///     the same values.
    /// </summary>
    /// <param name="warpScale">Noise frequency; around 3 is neutral.</param>
    public static void Set(float strength, float warpScale, int ticks = 30)
    {
        ScreenWobbleSystem self = Instance;

        self.scaleTarget = Math.Max(warpScale, 0.1f);
        self.RampTo(strength, ticks);
    }

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noise = Assets.Textures.Noise.Perlin.Asset;
    }

    public override void Unload()
    {
        base.Unload();
        noise = null;
    }

    protected override void Cut()
    {
        base.Cut();
        scale = scaleTarget = BaseScale;
    }

    protected override void Tick() => scale = MathHelper.Lerp(scale, scaleTarget, 0.05f);

    protected override bool Push(Assets.Shaders.ScreenWobble.Parameters parameters)
    {
        if (noise is not { Value: { } texture })
            return false;

        parameters.uIntensity = Strength;
        parameters.uScale = scale;
        parameters.uTime = Main.GlobalTimeWrappedHourly;
        parameters.uNoiseTex = texture;
        parameters.NoiseSampler = new HlslSampler2D { Texture = texture, Sampler = SamplerState.LinearWrap };

        return true;
    }
}
