using TrelamiumIII.Core.Graphics.Cinematics;

namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     The Dryad's arrival from the rift opening until she reaches her spot, scored by <c>DryadArrival.mp3</c>:
///     music, skip and the input lock. It ends when she arrives, so the track cross-fades out rather than playing to
///     its end. Everything that moves is driven by <see cref="DryadArrivalSequence" />. The letterbox belongs to the
///     sequence rather than to this class, so it stays up across the hand-off into the conversation.
/// </summary>
/// <remarks>
///     Also starts a forced run by itself when launched with <c>/startc DryadArrivalCutscene</c>.
/// </remarks>
public sealed class DryadArrivalCutscene : Cutscene
{
    private bool skipped;

    public DryadArrivalCutscene()
    {
        SetMusic(Assets.Music.DryadArrival.Slot);
    }

    protected override void OnCutsceneStart()
    {
        ControlsOFF();
        DryadArrivalSystem.EnsureRunning();
    }

    protected override void OnSkipTriggered()
    {
        skipped = true;
        base.OnSkipTriggered();
    }

    public override bool IsFinished() => !DryadArrivalSystem.SceneRunning;

    protected override void OnCutsceneEnd() => DryadArrivalSystem.OnSceneEnded(skipped);
}
