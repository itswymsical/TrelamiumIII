using TrelamiumIII.Content.DevTools.VfxLab.UI;

namespace TrelamiumIII.Common.Commands;

/// <summary>Opens the VFX Lab. Development tool; nothing else surfaces it.</summary>
public class VfxCommand : ModCommand
{
    public override string Command => "vfx";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/vfx [lab]";

    public override string Description => "Open or close the VFX Lab, to retune any shader while it draws. /vfx lab goes to its rooms.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.dedServ)
        {
            caller.Reply("The VFX Lab is client-side.");

            return;
        }

        VfxLabUI lab = ModContent.GetInstance<VfxLabUI>();

        if (args is ["lab", ..])
            lab.EnterLab();
        else
            lab.Toggle();
    }
}
