using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Commands;

public class SilvianCommand : ModCommand
{
    public override string Command => "silvian";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/silvian <status | meet | reset | almanac | hints>";

    public override string Description => "Drives meeting Silvian, his almanac, and his Help hints. Singleplayer only, except status.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

        if (verb != "status" && Main.netMode != NetmodeID.SinglePlayer)
        {
            caller.Reply("Only available in singleplayer.", Color.Yellow);

            return;
        }

        var silvian = caller.Player.GetModPlayer<SilvianPlayer>();

        switch (verb)
        {
            case "status":
                caller.Reply(SilvianEncounter.Describe(), Color.Cyan);
                break;

            case "meet":
                caller.Reply(SilvianEncounter.DevMeet(), Color.Green);
                break;

            case "reset":
                ResetFirstJob(caller.Player);
                silvian.Reset();
                caller.Reply("Never met, first job locked, almanac and hints taken back. Walk into sight of him to meet him again.", Color.Green);
                break;

            case "almanac":
                silvian.AlmanacInherited = false;
                bool opened = DialogueManager.Instance.StartDialogue(SilvianAlmanac.HandoffKey, style: DialogueStyle.Projected);

                caller.Reply(opened ? "Handoff started. The book is given when it plays through." : "The handoff could not start.", opened ? Color.Green : Color.Red);
                break;

            case "hints":
                silvian.ClearHints();
                caller.Reply("Hints forgotten. The next Help about an unbrewed potion starts from the first hint.", Color.Green);
                break;

            default:
                caller.Reply($"Usage: {Usage}", Color.Yellow);
                break;
        }
    }

    /// <summary>Back to never offered: <see cref="Job.Reset" /> leaves a job on offer, so each is withdrawn after.</summary>
    private static void ResetFirstJob(Player player)
    {
        var jobs = player.GetModPlayer<JobPlayer>();

        foreach (int jobId in (int[])[JobID.HerbalForager, JobID.BotanyBasics])
        {
            jobs.GetOrCreateJob(jobId)?.Reset();
            jobs.LockJob(jobId);
        }
    }
}
