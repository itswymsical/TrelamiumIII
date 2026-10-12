using ReLogic.Content;
using Terraria.Graphics.Effects;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>Radial speed lines over the finished frame, with a per-angle rumble in the warp.</summary>
internal sealed class ActionLinesSystem : ScreenFilterSystem<ActionLinesSystem, Assets.Shaders.ActionLines.Parameters>
{
    private const float DefaultDensity = 48f;

    private Asset<Texture2D>? noise;
    private float density = DefaultDensity;

    /// <summary>Angular line count. Whole numbers only, or the circle shows a seam where it wraps.</summary>
    public static float Density
    {
        get => Instance.density;
        set => Instance.density = value;
    }

    protected override Asset<Effect> Shader => Assets.Shaders.ActionLines.Asset;

    /// <summary>Below <see cref="ScreenFlashSystem" />, which must draw over the lines.</summary>
    protected override EffectPriority Priority => EffectPriority.High;

    /// <summary>Ramps to <paramref name="strength" /> (0..1) over <paramref name="ticks" /> frames.</summary>
    public static void Play(float strength, int ticks = 24) => Instance.RampTo(strength, ticks);

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

    protected override bool Push(Assets.Shaders.ActionLines.Parameters parameters)
    {
        if (noise is not { Value: { } texture })
            return false;

        parameters.uIntensity = Strength;
        parameters.uDensity = density;
        parameters.uTime = Main.GlobalTimeWrappedHourly;
        parameters.uNoiseTex = texture;

        // Wrap addressing: a clamped sampler smears the streaks into one band at the seam.
        parameters.NoiseSampler = new HlslSampler2D { Texture = texture, Sampler = SamplerState.LinearWrap };

        return true;
    }
}
