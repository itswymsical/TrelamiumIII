using TrelamiumIII.Core.Graphics.Cinematics;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

/// <summary>
///     Meeting Silvian, from the camera finding him until he speaks: the input lock, hidden HUD and skip. Everything that
///     moves is driven by <see cref="SilvianEncounterScene" />, which also owns the letterbox so it stays up into the
///     conversation. A skip cuts straight to him greeting the player.
/// </summary>
/// <remarks>
///     Also starts a scene by itself when launched with <c>/startc SilvianEncounterCutscene</c>, the same as
///     <c>/silvian meet</c>.
/// </remarks>
public sealed class SilvianEncounterCutscene : Cutscene
{
    private bool skipped;

    protected override void OnCutsceneStart()
    {
        ControlsOFF();
        SilvianEncounter.EnsureRunning();
    }

    protected override void OnSkipTriggered()
    {
        skipped = true;
        base.OnSkipTriggered();
    }

    public override bool IsFinished() => !SilvianEncounter.SceneRunning;

    protected override void OnCutsceneEnd() => SilvianEncounter.OnSceneEnded(skipped);
}
