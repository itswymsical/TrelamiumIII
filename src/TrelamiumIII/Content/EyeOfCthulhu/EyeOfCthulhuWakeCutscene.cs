using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.IlluminantInk;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     The Eye of Cthulhu's wake: its remains lifted out of the world, shrouded in ink, and drawn
///     into a singularity that closes behind them.
/// </summary>
/// <remarks>
///     <para>
///         <b>This class only ever sets state.</b> It never integrates a position and never emits a
///         particle. Every stage below resolves to a handful of 0..1 envelopes, which the pieces and
///         <see cref="InkVortex" /> then act on from their own tick hooks.
///     </para>
///     <para>
///         That split is deliberate and load-bearing. <see cref="Cutscene.Update" /> is driven from
///         <c>On_Main.DoDraw</c>, so this runs on the <em>frame</em>, while the particles and the ink
///         field run on the <em>tick</em>. Anything integrated here would advance at frame rate and
///         drift against the gas it is supposed to be moving with -- the same mismatch the menu
///         renderer was rewritten to remove. A slowly varying envelope is immune to that: sampling it
///         twice as often just samples the same curve twice as often.
///     </para>
/// </remarks>
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

    public EyeOfCthulhuWakeCutscene()
    {
        // Set here rather than in OnCutsceneStart: Cutscene.Start reads these to raise the bars
        // before it calls the start hook, so a value assigned there would arrive a stage too late.
        EnableLetterbox = true;
        LetterboxHeightPercentage = 0.085f;
        LetterboxAnimationDuration = 75;
        SetMusic(Assets.Music.Resurgence.Slot);
    }

    protected override void OnCutsceneStart()
    {
        // Falls back to the player when nothing was staged, which is the case when this is started
        // from /startc rather than by a death. That keeps the scene testable without having to go
        // and kill the boss for every tweak.
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
            // Still ordinary debris. The ink billow thrown at the moment of death is already out
            // there doing the work; the scene has not laid a finger on anything yet.
            SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.65f, Pitch = -0.05f});
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

    /// <summary>The remains come off the ground and start to drift.</summary>
    private void Lift(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Float;

            // The bloom comes up with the lift rather than being on from the start, so the pieces
            // read as catching light as they rise instead of having always been glowing.
            piece.GlowEnvelope = progress;
        }
    }

    /// <summary>The vortex arrives, still faint, while the remains go on drifting.</summary>
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

    /// <summary>The remains are taken.</summary>
    private void Winding(float progress)
    {
        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            piece.Drift = GoreDrift.Drawn;
            piece.VortexCenter = focus;
            piece.VortexProgress = progress;
            piece.GlowEnvelope = 1f;
        }
        // Finishes opening as it starts pulling, so the wind-in and the arrival are one gesture
        // rather than the hole appearing and then, separately, doing something.
        InkVortex.Fade = MathHelper.Lerp(SUMMON_FADE, 1f, MathF.Min(progress * 2.2f, 1f));
        InkVortex.Progress = progress;
    }

    /// <summary>Nothing left to swallow. The hole shuts.</summary>
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
        // Closes rather than merely fading, so a skipped cutscene cannot leave a half-open vortex
        // and a field of ink sitting in the world.
        InkVortex.Close();

        foreach (var piece in EyeOfCthulhuWakeSystem.Pieces)
        {
            // Handed back to ordinary physics. Anything the vortex did not reach falls, which is a
            // better ending for a skipped scene than debris frozen in mid-air.
            piece.Drift = GoreDrift.Tumble;
            piece.GlowEnvelope = 0f;
        }
    }

    /// <summary>
    ///     Smooth 0..1 across a stage.
    /// </summary>
    /// <remarks>
    ///     Smoothstepped rather than linear because every consumer is an envelope that reads as
    ///     motion -- a linear ramp gives the lift and the wind-in a visible corner at each boundary.
    /// </remarks>
    private static float Ramp(float now, float from, float to)
    {
        return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((now - from) / MathF.Max(to - from, 0.0001f), 0f, 1f));
    }
}
