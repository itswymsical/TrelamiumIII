using TrelamiumIII.Core.Residues.Bodies;

namespace TrelamiumIII.Common.Commands;

/// <summary>Lights or puts out the test fire on your own lower half. Development tool; nothing else surfaces it.</summary>
public class BodyFireCommand : ModCommand
{
    public override string Command => "bodyfire";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/bodyfire";

    public override string Description => "Light or put out a harmless test fire on your lower half, to see a residue drawn on a body.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.dedServ)
        {
            caller.Reply("Body residues are drawn client-side.");

            return;
        }

        caller.Reply(BodyFireTest.Toggle() ? "Burning. Run it again to put it out." : "Put out.");
    }
}
