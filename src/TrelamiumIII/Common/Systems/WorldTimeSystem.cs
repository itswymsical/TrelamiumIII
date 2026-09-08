using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Counts days in the world, and opens jobs up as the player reaches the day each is gated behind.
/// </summary>
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

    /// <summary>
    /// Unlocks everything the world has reached the day for, not only what it reached it for today.
    /// </summary>
    /// <remarks>
    /// This was keyed on an exact day match, evaluated only at the moment of sunrise. Anyone who
    /// joined a world already past day 1 -- or who was simply not in it when the day turned -- never
    /// got HomeForager at all, and had no way to make it appear.
    /// </remarks>
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

    /// <summary>
    /// Catches one player up on every milestone the world has already passed. Called as they enter,
    /// which is the only moment a player who missed a sunrise can be brought level.
    /// </summary>
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

            // Only a job still locked is news; one already taken, finished, or deliberately withdrawn
            // must not be re-offered by a later day rolling over.
            if (job?.State != JobState.Locked)
                continue;

            jobPlayer.UnlockJob(jobId);

            if (job.ID == JobID.HomeForager && player.whoAmI == Main.myPlayer)
                DialogueManager.Instance.StartDialogue("Guide.HomeForager");
        }
    }
}
