using ReLogic.Content;
using Terraria.Graphics.Effects;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>Chromatic aberration over the finished frame.</summary>
internal sealed class ChromaticScreenEffect : ScreenFilterSystem<ChromaticScreenEffect, Assets.Shaders.ChromaticAberration.Parameters>
{
    private const float BaseSpread = 3f;

    private float spread = BaseSpread;
    private float spreadTarget = BaseSpread;

    protected override Asset<Effect> Shader => Assets.Shaders.ChromaticAberration.Asset;

    protected override EffectPriority Priority => EffectPriority.VeryHigh;

    /// <summary>Ramps to <paramref name="intensity" /> (0..1) over <paramref name="ticks" /> frames; the spread eases toward its new value.</summary>
    public static void Set(float intensity, float spread, int ticks = 30)
    {
        ChromaticScreenEffect self = Instance;

        self.spreadTarget = Math.Max(spread, 0.1f);
        self.RampTo(intensity, ticks);
    }

    protected override void Cut()
    {
        base.Cut();
        spread = spreadTarget = BaseSpread;
    }

    protected override void Tick() => spread = MathHelper.Lerp(spread, spreadTarget, 0.05f);

    /// <remarks>Leaves the image sampler alone: the filter binds the captured screen to s0.</remarks>
    protected override bool Push(Assets.Shaders.ChromaticAberration.Parameters parameters)
    {
        parameters.uIntensity = Strength;
        parameters.uSpread = spread;

        return true;
    }
}
