using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

/// <summary>
/// Keeps the local player's <see cref="JobberJob"/> slots in step with nearby jobbers: offers unclaimed requests on
/// free slots, frees finished or stale offers, and abandons jobs whose jobber is gone.
/// </summary>
public class JobberPlayer : ModPlayer
{
    private const int RefreshInterval = 20;

    /// <summary>Ticks in a world before a missing jobber counts as gone; MP clients receive NPCs after entering.</summary>
    private const int SettleTicks = 60 * 5;

    private const float OfferRange = 1200f;

    /// <summary>Wider than <see cref="OfferRange"/> so an offer doesn't flicker at the edge.</summary>
    private const float KeepRange = 3000f;

    private int ticksInWorld;

    public override void OnEnterWorld() => ticksInWorld = 0;

    public override void PostUpdate()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || ++ticksInWorld % RefreshInterval != 0)
            return;

        var jobs = Player.GetModPlayer<JobPlayer>();
        bool changed = false;

        foreach (int id in JobberJob.Slots)
        {
            if (jobs.GetOrCreateJob(id) is JobberJob job)
                changed |= Settle(job);
        }

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not Jobber { Request: { } request } jobber || !IsOffering(jobber, OfferRange) || IsBound(jobs, npc))
                continue;

            if (FreeSlot(jobs) is not { } slot)
                break;

            slot.Bind(jobber, request);
            jobs.UnlockJob(slot.ID);
            changed = true;
        }

        if (changed)
            EventNPC.InvalidateJobCache();
    }

    /// <summary>Releases or abandons a slot that no longer matches the world. True if the slot changed.</summary>
    private bool Settle(JobberJob job)
    {
        switch (job.State)
        {
            case JobState.Completed:
            case JobState.Available when !IsStillOffered(job):
            case JobState.Active when !job.IsBound:
                job.Release();
                return true;

            case JobState.Active when job.InBoundWorld:
            {
                if (Jobber.Find(job.JobberId) is not { } jobber)
                {
                    if (ticksInWorld < SettleTicks)
                        return false;

                    job.Abandon();
                    return true;
                }

                // Re-claims if the jobber's flag was lost, e.g. to a dropped packet.
                if (!jobber.HasActiveJob)
                    jobber.Claim();

                return false;
            }

            default:
                return false;
        }
    }

    private bool IsOffering(Jobber jobber, float range)
        => jobber.JobberId != 0 && !jobber.HasActiveJob && jobber.Request.HasValue
        && Player.WithinRange(jobber.NPC.Center, range);

    private bool IsStillOffered(JobberJob job)
        => Jobber.Find(job.JobberId) is { } jobber && IsOffering(jobber, KeepRange) && jobber.Request == job.Request;

    private static bool IsBound(JobPlayer jobs, NPC npc)
    {
        foreach (int id in JobberJob.Slots)
        {
            if (jobs.GetOrCreateJob(id) is JobberJob job && job.IsProvidedBy(npc))
                return true;
        }

        return false;
    }

    private static JobberJob? FreeSlot(JobPlayer jobs)
    {
        foreach (int id in JobberJob.Slots)
        {
            if (jobs.GetOrCreateJob(id) is JobberJob { State: JobState.Locked } job)
                return job;
        }

        return null;
    }
}
