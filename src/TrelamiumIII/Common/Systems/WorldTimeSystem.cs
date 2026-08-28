using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Systems;

public class WorldTimeSystem : ModSystem
{
    public static int TotalDaysElapsed;

    private readonly Dictionary<int, int[]> _jobMilestones = new()
    {
        { 3, [ JobID.GuideKingSlimeI, JobID.MerchantEyeOfCthulhuI ] },
        // { 7, [ JobID.DryadEaterOfWorldsI, JobID.ArmsDealerGunI ] }
    };

    public override void PreUpdateWorld()
    {
        if (Main.dayTime && Main.time == 0.0)
        {
            TotalDaysElapsed++;
            CheckDayMilestones();
        }
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["TotalDaysElapsed"] = TotalDaysElapsed;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        TotalDaysElapsed = tag.GetInt("TotalDaysElapsed");
    }

    private void CheckDayMilestones()
    {
        if (_jobMilestones.TryGetValue(TotalDaysElapsed, out int[] jobsToUnlock))
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active)
                {
                    UnlockJobs(p, jobsToUnlock);
                }
            }
        }
    }

    private void UnlockJobs(Player player, int[] jobs)
    {
        var mp = player.GetModPlayer<JobPlayer>();

        foreach (var jobId in jobs)
        {
            var job = mp.GetOrCreateJob(jobId);

            if (job != null && job.Status == JobStatus.Locked)
            {
                mp.UnlockJob(job.ID);
                string npcName = Lang.GetNPCNameValue(job.ProviderNPC);
                Main.NewText($"{npcName} {Mod.GetLocalizationKey("JobInfo.NewJob")}", Color.LightBlue);
            }
        }
    }
}
