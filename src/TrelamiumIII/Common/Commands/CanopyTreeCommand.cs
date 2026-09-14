using SubworldLibrary;
using TrelamiumIII.Common.Subworlds.Realms;

namespace TrelamiumIII.Common.Commands;

public class CanopyTreeCommand : ModCommand
{
    public override string Command => "canopytree";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/canopytree [seed|number]";

    public override string Description => "Regrows the canopy tree lab's tree from a random or given seed; 'seed' prints the current one.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (!SubworldSystem.IsActive<CanopyTreeLabRealm>())
        {
            caller.Reply("Enter the lab first with /realm treelab.", Color.Red);
            return;
        }

        if (args.Length > 0 && args[0].Equals("seed", StringComparison.OrdinalIgnoreCase))
        {
            caller.Reply($"Current canopy tree seed: {CanopyTreeLabRealm.Seed}");
            return;
        }

        // Regrowing edits tiles locally without syncing them.
        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            caller.Reply("Regrowing only works in single player.", Color.Red);
            return;
        }

        int seed = Main.rand.Next();
        if (args.Length > 0 && !int.TryParse(args[0], out seed))
        {
            caller.Reply($"'{args[0]}' isn't a seed. Usage: {Usage}", Color.Red);
            return;
        }

        CanopyTreeLabRealm.Regrow(seed);
        caller.Reply($"Regrew the canopy tree with seed {seed}.");
    }
}
