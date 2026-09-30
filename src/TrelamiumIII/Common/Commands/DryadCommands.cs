using TrelamiumIII.Content.Dryad;

namespace TrelamiumIII.Common.Commands;

public class DryadCommand : ModCommand
{
    public override string Command => "dryad";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage => "/dryad <status | awaken | briefing | arrive | settle | rift [close] | stage <name> | reset>";

    public override string Description => "Drives the Dryad's arrival after the Eye of Cthulhu, and her rift to Ingress. Singleplayer only, except status.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

        if (verb != "status" && Main.netMode != NetmodeID.SinglePlayer)
        {
            caller.Reply("Only available in singleplayer.", Color.Yellow);

            return;
        }

        switch (verb)
        {
            case "status":
                caller.Reply($"{DryadArrivalSystem.Describe()}, {IngressRift.Describe()}", Color.Cyan);
                break;

            case "awaken":
                DryadArrivalSystem.DevSetStage(DryadArrivalStage.Awakened);
                caller.Reply("Stage Awakened. The Guide's briefing plays once nothing else is on screen.", Color.Green);
                break;

            case "briefing":
                bool briefed = DryadArrivalSystem.DevPlayBriefing();

                caller.Reply(briefed ? "Briefing started." : "The briefing could not start.", briefed ? Color.Green : Color.Red);
                break;

            case "arrive":
                bool started = DryadArrivalSystem.DevBegin();

                caller.Reply(started ? "Arrival started." : "An arrival is already running, or no open ground was found.", started ? Color.Green : Color.Red);
                break;

            case "settle":
                DryadArrivalSystem.DevSettle();
                caller.Reply("Settled. Her job goes on offer within a second; talk to her through the indicator over her head.", Color.Green);
                break;

            case "rift" when args.Length > 1 && args[1].Equals("close", StringComparison.OrdinalIgnoreCase):
                bool closed = IngressRift.DevClose();

                caller.Reply(closed ? "Rift closed. It reopens on its own while you have her job." : "There is no rift here to close.", closed ? Color.Green : Color.Red);
                break;

            case "rift":
                bool opened = IngressRift.DevOpen();

                caller.Reply(opened ? "Rift opened." : "The rift is already open, you are in a realm, or no open ground was found.", opened ? Color.Green : Color.Red);
                break;

            case "stage":
                if (args.Length < 2 || !Enum.TryParse(args[1], true, out DryadArrivalStage stage))
                {
                    caller.Reply($"Usage: /dryad stage <{string.Join("|", Enum.GetNames<DryadArrivalStage>())}>", Color.Yellow);
                    break;
                }

                DryadArrivalSystem.DevSetStage(stage);
                caller.Reply($"Stage set to {stage}.", Color.Green);
                break;

            case "reset":
                DryadArrivalSystem.DevReset();
                caller.Reply("Stage Dormant and the Dryad removed. Kill the Eye of Cthulhu, or use /dryad awaken, to run it again.", Color.Green);
                break;

            default:
                caller.Reply($"Usage: {Usage}", Color.Yellow);
                break;
        }
    }
}
