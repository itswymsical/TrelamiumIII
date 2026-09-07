using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.IlluminantInk;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.EyeOfCthulhu;


public sealed class EyeOfCthulhuWakeCutscene : Cutscene
{
    // Stage boundaries, in seconds from the opening frame. Each is the moment that stage *ends*.

    /// <summary>Nothing moves. Long enough to register the kill before the scene takes over.</summary>
    private const float HOLD_END = 1.7f;

    /// <summary>The remains lift, and the ink shroud thickens around them.</summary>
    private const float LIFT_END = 3.8f;

    /// <summary>The vortex bleeds into view behind them, faint at first.</summary>
    private const float SUMMON_END = 6.2f;

    /// <summary>The remains wind in and are swallowed.</summary>
    private const float DRAW_END = 10.0f;

    /// <summary>The vortex closes and takes the ink with it.</summary>
    private const float CLOSE_END = 11.6f;

    /// <summary>Opacity the vortex holds while it is still only a suggestion.</summary>
    private const float SUMMON_FADE = 0.42f;

    private Vector2 focus;

    private bool cameraMovedOut;

    public EyeOfCthulhuWakeCutscene()
    {
        EnableLetterbox = true;
        LetterboxHeightPercentage = 0.085f;
        LetterboxAnimationDuration = 75;
        SetMusic(Assets.Music.Resurgence.Slot);
    }

    protected override void OnCutsceneStart()
    {
        focus = EyeOfCthulhuWakeSystem.Pieces.Count > 0
            ? EyeOfCthulhuWakeSystem.Center
            : Main.LocalPlayer.Center - new Vector2(0f, 120f);

        InkVortex.Open(focus);
    }

    protected override void OnCutsceneUpdate(GameTime gameTime)
    {
        float now = ElapsedSeconds;

        InkVortex.Center = focus;

        if (now < HOLD_END)
        {
            SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.65f, Pitch = -0.05f});
            RadialBlurEffect.Set(0.4f, ticks: 10);
            return;
        }

        if (now < LIFT_END)
        {
            Lift(Ramp(now, HOLD_END, LIFT_END));
            return;
        }

        if (now < SUMMON_END)
        {
            Summon(Ramp(now, LIFT_END, SUMMON_END));
            return;
        }

        if (now < DRAW_END)
        {
            Winding(Ramp(now, SUMMON_END, DRAW_END));

            return;
        }

        Closing(Ramp(now, DRAW_END, CLOSE_END));
    }

    private void Lift(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Float;
            piece.GlowEnvelope = progress;
        }

        if (!cameraMovedOut)
        {
            CameraSystem.MoveCameraOut(
                TimeConversion.SetTime(secs: 3),
                InkVortex.Center);

            cameraMovedOut = true;
        }
    }

    private void Summon(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Float;
            piece.GlowEnvelope = 1f;
        }
        InkVortex.Fade = SUMMON_FADE * progress;
        InkVortex.Progress = 0f;
    }

    private void Winding(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Drawn;
            piece.VortexCenter = focus;
            piece.VortexProgress = progress;
            piece.GlowEnvelope = 1f;
        }
        InkVortex.Fade = MathHelper.Lerp(SUMMON_FADE, 1f, MathF.Min(progress * 2.2f, 1f));
        InkVortex.Progress = progress;
        //CameraSystem.ReturnCamera(60);
    }

    private void Closing(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
            piece.GlowEnvelope = 1f - progress;

        InkVortex.Fade = 1f - progress;
        InkVortex.Progress = 1f;
    }

    public override bool IsFinished() => ElapsedSeconds >= CLOSE_END;

    protected override void OnCutsceneEnd()
    {
        CameraSystem.ReturnCamera(
            TimeConversion.SetTime(secs: 1));

        InkVortex.Close();

        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Tumble;
            piece.GlowEnvelope = 0f;
        }

        cameraMovedOut = false;
    }

    private static float Ramp(float now, float from, float to)
    {
        return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((now - from) / MathF.Max(to - from, 0.0001f), 0f, 1f));
    }
}
