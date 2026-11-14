using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Generation.Utils;
using TrelamiumIII.Content.NPCs.WorldNPCs.Argie;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Housing;

namespace TrelamiumIII.Common.Commands;

public class ArgieCommand : ModCommand
{
    public override string Command => "argie";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/argie <status | reset | lab | goto>";

    public override string Description =>
        "Argie's meeting and jobs. lab builds her lab in a world generated without it; goto takes you to her. Singleplayer only, except status.";

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
                caller.Reply(Describe(caller.Player), Color.Cyan);
                break;

            case "reset":
                Reset(caller.Player);
                caller.Reply("Never met, both jobs locked, letter undelivered, hidden from your map again. Talk to her to replay the meeting.", Color.Green);
                break;

            case "lab":
                caller.Reply(BuildLab(), Color.Green);
                break;

            case "goto":
                caller.Reply(GoTo(caller.Player), Color.Green);
                break;

            default:
                caller.Reply($"Usage: {Usage}", Color.Yellow);
                break;
        }
    }

    private static void Reset(Player player)
    {
        var jobs = player.GetModPlayer<JobPlayer>();

        foreach (int jobId in (int[])[JobID.MycologyHunt, JobID.SporeSurvey])
        {
            jobs.GetOrCreateJob(jobId)?.Reset();
            jobs.LockJob(jobId);
        }

        player.GetModPlayer<ArgiePlayer>().Reset();
        player.GetModPlayer<WorldNPCDiscovery>().Forget(ModContent.NPCType<ArgieNPC>());
    }

    private static string BuildLab()
    {
        if (WorldNPCHousing.TryGetHome(ModContent.NPCType<ArgieNPC>(), out NPCHome? home))
            return $"This world already has her lab, at tile ({home!.Area.X}, {home.Area.Y}).";

        if (!ArgieLabPass.TryPlace(out PlacedStructure placed))
            return "No glowing mushroom biome here has room for the lab.";

        WorldNPCHousing.SpawnMissingResidents();

        return $"Lab built at tile ({placed.Origin.X}, {placed.Origin.Y}), and Argie moved in. /argie goto takes you there.";
    }

    private static string GoTo(Player player)
    {
        int index = NPC.FindFirstNPC(ModContent.NPCType<ArgieNPC>());

        if (index < 0)
            return "Argie isn't in this world. /argie lab builds her lab.";

        player.Teleport(Main.npc[index].Bottom - new Vector2(player.width / 2f + 48f, player.height), TeleportationStyleID.DebugTeleport);

        return "There she is.";
    }

    private static string Describe(Player player)
    {
        var argie = player.GetModPlayer<ArgiePlayer>();
        int index = NPC.FindFirstNPC(ModContent.NPCType<ArgieNPC>());

        string where = index < 0
            ? "not in the world"
            : $"at tile ({(int)(Main.npc[index].Center.X / 16f)}, {(int)(Main.npc[index].Center.Y / 16f)}), "
            + $"{Vector2.Distance(player.Center, Main.npc[index].Center) / 16f:0} tiles away";

        return $"met {(argie.Met ? "yes" : "no")}, letter {(argie.LetterDelivered ? "delivered" : "not delivered")}, "
            + $"fungus hunt {argie.JobStateOf(JobID.MycologyHunt)}, spore survey {argie.JobStateOf(JobID.SporeSurvey)}, Argie {where}";
    }
}
