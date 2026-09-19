using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Systems;

/// <summary>Counts world days and unlocks jobs as their day milestones are reached.</summary>
/// <remarks>
/// The server owns the count and syncs it in world data. Job state is saved per player on each client, so each
/// client unlocks its own player: on entering the world, and whenever the synced count rises.
/// </remarks>
public class WorldTimeSystem : ModSystem
{
    public static int TotalDaysElapsed;

    /// <summary>Day the world must have reached for these jobs to be offered. Day 0 is offered on entering the world.</summary>
    private static readonly Dictionary<int, int[]> JobMilestones = new()
    {
        { 0, [JobID.HomeForager] },
        { 3, [JobID.GoldMine] },
        // { 7, [ JobID.DryadEaterOfWorldsI, JobID.ArmsDealerGunI ] }
    };

    public override void PreUpdateWorld()
    {
        if (!Main.dayTime || Main.time != 0.0)
            return;

        TotalDaysElapsed++;

        // Clients unlock their own players' jobs when the new count reaches them in NetReceive.
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
        else
            ApplyMilestonesTo(Main.LocalPlayer);
    }

    // Static, so without this a new world starts on the last world's day count.
    public override void ClearWorld() => TotalDaysElapsed = 0;

    public override void SaveWorldData(TagCompound tag) => tag["TotalDaysElapsed"] = TotalDaysElapsed;

    public override void LoadWorldData(TagCompound tag) => TotalDaysElapsed = tag.GetInt("TotalDaysElapsed");

    public override void NetSend(BinaryWriter writer) => writer.Write(TotalDaysElapsed);

    public override void NetReceive(BinaryReader reader)
    {
        int previous = TotalDaysElapsed;
        TotalDaysElapsed = reader.ReadInt32();

        // While joining (gameMenu), JobPlayer.OnEnterWorld catches the player up instead.
        if (TotalDaysElapsed > previous && !Main.gameMenu)
            ApplyMilestonesTo(Main.LocalPlayer);
    }

    /// <summary>Unlocks every job whose day the world has reached, not only today's.</summary>
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
