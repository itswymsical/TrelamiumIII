using Terraria.ModLoader;
using TrelamiumIII.Core.Authoring;

namespace TrelamiumIII.Common.Commands;

/// <summary>Opens the authoring desk. Development tool; nothing else surfaces it.</summary>
public class AuthoringCommand : ModCommand
{
    public override string Command => "write";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/write";

    public override string Description => "Open the lore authoring desk. Escape closes it.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.dedServ)
        {
            caller.Reply("The authoring desk is client-side.");

            return;
        }

        AuthoringSystem.Show();
    }
}
