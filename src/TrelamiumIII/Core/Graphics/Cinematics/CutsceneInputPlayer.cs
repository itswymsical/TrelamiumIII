namespace TrelamiumIII.Core.Graphics.Cinematics;

/// <summary>Clears the local player's controls while <see cref="Cutscene.DisableInputs" /> is set.</summary>
public sealed class CutsceneInputPlayer : ModPlayer
{
    public override void SetControls()
    {
        if (!Cutscene.DisableInputs || Player.whoAmI != Main.myPlayer)
            return;

        Player.controlLeft = false;
        Player.controlRight = false;
        Player.controlUp = false;
        Player.controlDown = false;
        Player.controlJump = false;
        Player.controlUseItem = false;
        Player.controlUseTile = false;
        Player.controlThrow = false;
        Player.controlHook = false;
        Player.controlMount = false;
        Player.controlQuickHeal = false;
        Player.controlQuickMana = false;
    }
}
