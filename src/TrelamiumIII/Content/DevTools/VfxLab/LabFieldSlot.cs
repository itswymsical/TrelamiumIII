using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Vfx;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     A field being designed: its physics, behaviour and look are all read live from a <see cref="VfxFieldSpec" />, and its shader is
///     generated and replaced while the game runs. It holds no nodes until the lab spawns some, and a field with no nodes costs nothing.
/// </summary>
/// <remarks>
///     Types are discovered at load, so the lab has a fixed number of these. They have to be registered on every machine: a config could
///     only change that on one, and layers are found by the same reflection on all of them.
/// </remarks>
internal abstract class LabFieldSlot : ModularField
{
    private readonly VfxFieldSpec spec = new();

    public LiveEffect Live { get; } = new("VfxLab/FieldSlot");

    /// <summary> The uniforms a hand-written shader shares with <c>GasFbm.fx</c>. Generated shaders take everything from the spec. </summary>
    public GasLook? Material { get; set; }

    /// <summary> Sets a hand-written shader's other uniforms, after <see cref="Material" />. </summary>
    public Action<Effect>? ApplyStages { get; set; }

    public override VfxFieldSpec Spec => spec;

    /// <summary> Above every real layer, so nothing covers what's being edited. </summary>
    public override int LoadOrder => 1000;

    protected override Asset<Effect> Shader => Live.Asset;

    protected override GasLook? Look => Material;

    /// <summary> What gas layers stamp with, so a look rebuilt here sees the same field it does there. </summary>
    protected internal override SamplerState StampSampler => SamplerState.LinearClamp;

    /// <summary> As many ticks as the lab's clock says this one is worth: none while paused, several when sped up. </summary>
    public override void Update()
    {
        for (int i = 0; i < LabClock.StepsThisTick; i++)
            base.Update();
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        base.ApplyShaderParameters(effect);
        ApplyStages?.Invoke(effect);

        if (LabClock.IsManual)
            effect.Parameters["Time"]?.SetValue(LabClock.Seconds);
    }

    public override void Unload()
    {
        base.Unload();
        Live.Release();
    }
}

internal sealed class LabField0 : LabFieldSlot;

internal sealed class LabField1 : LabFieldSlot;

internal sealed class LabField2 : LabFieldSlot;

internal sealed class LabField3 : LabFieldSlot;

/// <summary> Time for what's being designed, which can stop and be stepped without stopping the game. Real fields keep the game's. </summary>
internal static class LabClock
{
    private static float owed;
    private static int stepsRequested;

    public static bool Paused { get; set; }

    public static float Speed { get; set; } = 1f;

    /// <summary> Whether this clock has taken over from the game's. </summary>
    public static bool IsManual => Paused || Speed != 1f;

    public static float Seconds { get; private set; }

    /// <summary> How many ticks a draft field should run this tick. </summary>
    public static int StepsThisTick { get; private set; } = 1;

    public static void Step() => stepsRequested++;

    public static void Tick()
    {
        if (Paused)
        {
            StepsThisTick = stepsRequested;
            stepsRequested = 0;
        }
        else
        {
            owed += Speed;
            StepsThisTick = (int)owed;
            owed -= StepsThisTick;
        }

        Seconds = IsManual ? Seconds + StepsThisTick / 60f : (float)Main.timeForVisualEffects / 60f;
        WorldFieldSpace.Instance.ClockOverride = IsManual ? Seconds : null;
    }
}
