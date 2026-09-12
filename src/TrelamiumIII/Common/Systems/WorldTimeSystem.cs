using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Systems;

/// <summary>Counts world days and unlocks jobs as their day milestones are reached.</summary>
public class WorldTimeSystem : ModSystem
{
    public static int TotalDaysElapsed;

    /// <summary>Day the world must have reached for these jobs to be offered.</summary>
    private static readonly Dictionary<int, int[]> JobMilestones = new()
    {
        { 1, [JobID.HomeForager] },
        { 3, [JobID.GoldMine] },
        // { 7, [ JobID.DryadEaterOfWorldsI, JobID.ArmsDealerGunI ] }
    };

    public override void PreUpdateWorld()
    {
        if (!Main.dayTime || Main.time != 0.0)
            return;

        TotalDaysElapsed++;
        ApplyMilestones();
    }

    public override void SaveWorldData(TagCompound tag) => tag["TotalDaysElapsed"] = TotalDaysElapsed;

    public override void LoadWorldData(TagCompound tag) => TotalDaysElapsed = tag.GetInt("TotalDaysElapsed");

    /// <summary>Unlocks every job whose day has been reached, not only today's.</summary>
    private static void ApplyMilestones()
    {
        var due = JobMilestones
            .Where(milestone => TotalDaysElapsed >= milestone.Key)
            .SelectMany(milestone => milestone.Value);

        for (var i = 0; i < Main.maxPlayers; i++)
        {
            var player = Main.player[i];

            if (player.active)
                UnlockJobs(player, due);
        }
    }

    /// <summary>Catches a joining player up on every milestone the world has already passed.</summary>
    public static void ApplyMilestonesTo(Player player)
        => UnlockJobs(player, JobMilestones
            .Where(milestone => TotalDaysElapsed >= milestone.Key)
            .SelectMany(milestone => milestone.Value));

    private static void UnlockJobs(Player player, IEnumerable<int> jobIds)
    {
        var jobPlayer = player.GetModPlayer<JobPlayer>();

        foreach (var jobId in jobIds)
        {
            var job = jobPlayer.GetOrCreateJob(jobId);

            // Only locked jobs unlock; taken, finished or withdrawn jobs must not be re-offered.
            if (job?.State != JobState.Locked)
                continue;

            jobPlayer.UnlockJob(jobId);

            if (job.ID == JobID.HomeForager && player.whoAmI == Main.myPlayer)
                DialogueManager.Instance.StartDialogue("Guide.HomeForager");
        }
    }
}
