using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>
///     A <see cref="Filters.Scene" /> pass over the finished frame, with a 0..1 strength that ramps toward a target.
///     The pass is active while <see cref="Active" />; subclasses write their uniforms in <see cref="Push" />.
/// </summary>
/// <remarks>The latest request wins; requests are never queued.</remarks>
internal abstract class ScreenFilterSystem<TSelf, TParameters> : ModSystem
    where TSelf : ScreenFilterSystem<TSelf, TParameters>
    where TParameters : IShaderParameters, new()
{
    /// <summary>Strength below which the pass is switched off.</summary>
    protected const float Cutoff = 0.002f;

    private ScreenFilterData<TParameters>? data;

    private float strength;
    private float target;
    private float rate;

    protected static TSelf Instance => ModContent.GetInstance<TSelf>();

    /// <summary>Current strength, 0 to 1.</summary>
    public static float Intensity => Instance.strength;

    protected float Strength => strength;

    private string Key => $"{nameof(TrelamiumIII)}:{Name}";

    protected abstract Asset<Effect> Shader { get; }

    protected abstract EffectPriority Priority { get; }

    /// <summary>Whether the pass draws this frame.</summary>
    protected virtual bool Active => strength > Cutoff || target > Cutoff;

    /// <summary>Ramps to zero over <paramref name="ticks" /> frames.</summary>
    public static void Stop(int ticks = 40) => Instance.StopRamp(ticks);

    /// <summary>Ramps to <paramref name="value" /> (0..1) over <paramref name="ticks" /> frames; zero ticks jumps there.</summary>
    protected void RampTo(float value, int ticks)
    {
        if (data is null)
            return;

        target = Math.Clamp(value, 0f, 1f);
        rate = Math.Abs(target - strength) / Math.Max(ticks, 1);

        if (rate <= 0f)
            strength = target;
    }

    protected virtual void StopRamp(int ticks) => RampTo(0f, ticks);

    /// <summary>Drops the effect this frame, without a ramp.</summary>
    protected virtual void Cut()
    {
        strength = target = rate = 0f;
        Deactivate();
    }

    /// <summary>Per-frame state, run before the strength advances.</summary>
    protected virtual void Tick() { }

    /// <summary>Writes this frame's uniforms. False skips the frame, e.g. while a texture is still loading.</summary>
    protected abstract bool Push(TParameters parameters);

    public override void Load()
    {
        if (Main.dedServ)
            return;

        data = new ScreenFilterData<TParameters>(Shader, "P0");

        Filters.Scene[Key] = new Filter(data, Priority);
        Filters.Scene[Key].Load();
    }

    public override void Unload() => data = null;

    public override void OnWorldUnload() => Cut();

    public override void PostUpdateEverything()
    {
        if (data is null)
            return;

        Tick();

        strength = strength < target ? Math.Min(strength + rate, target) : Math.Max(strength - rate, target);

        if (!Active || !Push(data.Parameters))
        {
            Deactivate();
            return;
        }

        // ScreenShaderData.Apply writes uIntensity after the pushed parameters, so it must carry the same value.
        data.UseIntensity(strength);

        if (!Filters.Scene[Key].IsActive())
            Filters.Scene.Activate(Key);
    }

    private void Deactivate()
    {
        if (data is not null && Filters.Scene[Key].IsActive())
            Filters.Scene[Key].Deactivate();
    }
}

/// <summary>A <see cref="ScreenShaderData" /> that writes generated shader parameters before the base pass applies.</summary>
internal sealed class ScreenFilterData<TParameters>(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
    where TParameters : IShaderParameters, new()
{
    private readonly string passName = passName;

    public TParameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}
