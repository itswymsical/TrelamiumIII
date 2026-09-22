using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using Terraria.UI;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.UI.Jobs;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// One player's jobs: their state, their progress, and the routing of game events into them.
/// </summary>
public partial class JobPlayer : ModPlayer
{
    private readonly List<Job> jobs = [];

    #region Event handling
    /// <summary>
    /// Offers an event to every active job. Each job decides internally which of its tasks, if any,
    /// the event is for.
    /// </summary>
    public void OnGameEvent(GameEvent evt)
    {
        // Snapshot: a job completing mid-dispatch can add or retire others.
        foreach (var job in jobs.ToArray())
        {
            if (job.State == JobState.Active)
                job.Handle(evt);
        }

        // Anything at all may have moved an unlock condition. Which one is not worth working out here:
        // the rules read live state, so the next evaluation asks them all.
        InvalidateUnlocks();
    }
    #endregion

    #region Access
    /// <summary>
    /// This player's instance of a job, created on first use. Returns <see langword="null"/> for an
    /// id nothing is registered under.
    /// </summary>
    public Job GetOrCreateJob(int jobId)
    {
        var existing = jobs.FirstOrDefault(job => job.ID == jobId);

        if (existing != null)
            return existing;

        var created = EventSystem.CreateJob(jobId);

        if (created is null)
            return null;

        created.Context = new JobContext(Player, this);
        jobs.Add(created);

        return created;
    }

    /// <summary>
    /// Starts a job the player has taken on.
    /// </summary>
    /// <param name="provider">
    /// The NPC that handed it over, if known. The world is searched for one of the right type otherwise.
    /// </param>
    public void StartJob(int jobId, NPC provider = null)
    {
        var job = GetOrCreateJob(jobId);

        if (job?.State != JobState.Available)
            return;

        job.State = JobState.Active;
        job.OnJobStart();

        EventNPC.InvalidateJobCache();
        InvalidateUnlocks();

        if (Player.whoAmI != Main.myPlayer)
            return;

        NotifyProvider(job, provider);
        InGameNotificationsTracker.AddNotification(new JobAcceptNotification(job));
    }

    /// <summary>Makes a job visible on its provider's board.</summary>
    public void UnlockJob(int jobId)
    {
        var job = GetOrCreateJob(jobId);

        if (job?.State != JobState.Locked)
            return;

        job.State = JobState.Available;
        job.OnUnlocked();

        EventNPC.InvalidateJobCache();
        InvalidateUnlocks();
    }

    /// <summary>Withdraws an offered job. Does not touch one already underway.</summary>
    public void LockJob(int jobId)
    {
        var job = jobs.FirstOrDefault(j => j.ID == jobId);

        if (job?.State != JobState.Available)
            return;

        job.State = JobState.Locked;

        EventNPC.InvalidateJobCache();
        InvalidateUnlocks();
    }

    /// <summary>
    /// Finishes a job that has met its tasks.
    /// </summary>
    /// <remarks>
    /// The notification comes from <see cref="Job.OnJobComplete"/>, which is the one path every
    /// completion goes through. Raising a second one here -- as this used to, unconditionally, even
    /// for an id that matched nothing -- double-notified on success and threw on failure.
    /// </remarks>
    public bool CompleteJob(int jobId)
    {
        var job = jobs.FirstOrDefault(j => j.ID == jobId);

        if (job is null)
            return false;

        job.Complete();

        EventNPC.InvalidateJobCache();
        InvalidateUnlocks();

        return job.State == JobState.Completed;
    }

    /// <summary>Jobs currently underway.</summary>
    public IEnumerable<Job> ActiveJobs() => jobs.Where(job => job.State == JobState.Active);

    /// <summary>Jobs offered but not yet taken.</summary>
    public IEnumerable<Job> AvailableJobs() => jobs.Where(job => job.State == JobState.Available);

    public IEnumerable<Job> GetAllJobs() => jobs;

    /// <summary>
    /// Every job the given NPC <em>type</em> is currently offering. The single definition of "this NPC
    /// has work going", used by the indicator to decide whether to appear and by the board to fill its list.
    /// </summary>
    public IEnumerable<Job> AvailableJobsFrom(int npcType) =>
        jobs.Where(job => job.ProviderNPC == npcType && job.State == JobState.Available);

    /// <summary>Every job this NPC instance is currently offering, per <see cref="Job.IsProvidedBy"/>.</summary>
    public IEnumerable<Job> AvailableJobsFrom(NPC npc) =>
        jobs.Where(job => job.State == JobState.Available && job.IsProvidedBy(npc));

    public bool HasAvailableJobs() => AvailableJobs().Any();

    public bool NPCHasAvailableJob(int npcType) => AvailableJobsFrom(npcType).Any();

    /// <summary>Lets the providing NPC know one of its jobs was taken.</summary>
    private static void NotifyProvider(Job job, NPC provider)
    {
        provider ??= FindProvider(job);

        if (provider is { active: true } && provider.ModNPC is WorldNPC worldNpc)
            worldNpc.HasActiveJob = true;
    }

    private static NPC FindProvider(Job job)
    {
        for (var i = 0; i < Main.maxNPCs; i++)
        {
            var npc = Main.npc[i];

            if (npc.active && job.IsProvidedBy(npc))
                return npc;
        }

        return null;
    }
    #endregion

    #region Persistence
    public override void SaveData(TagCompound tag)
    {
        var saved = new List<TagCompound>();

        foreach (var job in jobs)
        {
            var custom = new TagCompound();
            job.SaveCustomData(custom);

            saved.Add(new TagCompound
            {
                ["ID"] = job.ID,
                ["State"] = (int)job.State,
                ["Set"] = job.CurrentTaskSet,
                ["Tasks"] = SerializeTasks(job),
                ["Custom"] = custom
            });
        }

        tag["Jobs"] = saved;
        SaveFlags(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        jobs.Clear();
        LoadFlags(tag);

        foreach (var jobTag in tag.GetList<TagCompound>("Jobs"))
        {
            var job = GetOrCreateJob(jobTag.GetInt("ID"));

            if (job is null)
                continue;

            job.State = ReadState(jobTag);
            job.CurrentTaskSet = Math.Clamp(jobTag.GetInt("Set"), 0, Math.Max(0, job.TaskList.Count - 1));

            DeserializeTasks(job, jobTag);
            job.LoadCustomData(jobTag.GetCompound("Custom") ?? []);
        }
    }

    /// <summary>
    /// Reads the job's state, folding forward the two-enum format that predates <see cref="JobState"/>.
    /// </summary>
    /// <remarks>
    /// Old saves carry <c>Progress {Inactive, Ongoing, Completed}</c> and <c>Status {Locked, Unlocked,
    /// Completed}</c>. Progress wins where they disagree, because it is the one the dispatcher used to
    /// consult -- so it describes what the player was actually experiencing.
    /// </remarks>
    private static JobState ReadState(TagCompound jobTag)
    {
        if (jobTag.TryGet("State", out int state))
            return (JobState)Math.Clamp(state, (int)JobState.Locked, (int)JobState.Completed);

        var progress = jobTag.TryGet("Progress", out int p) ? p : 0;
        var status = jobTag.TryGet("Status", out int s) ? s : 0;

        return progress switch
        {
            1 => JobState.Active,       // Ongoing
            2 => JobState.Completed,    // Completed
            _ => status switch
            {
                1 => JobState.Available,  // Unlocked
                2 => JobState.Completed,  // Completed
                _ => JobState.Locked
            }
        };
    }

    /// <summary>
    /// Tasks are stored per set, keyed by description key rather than by position, so inserting a task
    /// into a set does not silently shift everyone's progress along by one.
    /// </summary>
    private static List<TagCompound> SerializeTasks(Job job)
    {
        var sets = new List<TagCompound>();

        foreach (var set in job.TaskList)
        {
            var tasks = new List<TagCompound>();

            foreach (var task in set.Task)
            {
                var taskTag = new TagCompound { ["key"] = task.DescriptionKey };
                task.SaveData(taskTag);

                tasks.Add(taskTag);
            }

            sets.Add(new TagCompound { ["tasks"] = tasks });
        }

        return sets;
    }

    private static void DeserializeTasks(Job job, TagCompound jobTag)
    {
        var savedSets = jobTag.GetList<TagCompound>("Tasks");

        for (var i = 0; i < Math.Min(job.TaskList.Count, savedSets.Count); i++)
        {
            var savedTasks = savedSets[i].GetList<TagCompound>("tasks");

            // Pre-JobState saves nested tasks under "Objs" with a resolved "Desc"/"Count" pair.
            var legacy = savedTasks.Count == 0 ? savedSets[i].GetList<TagCompound>("Objs") : null;

            foreach (var task in job.TaskList[i].Task)
            {
                if (legacy is not null)
                {
                    var match = legacy.FirstOrDefault(t => t.GetString("Desc") == task.Description);

                    if (match != null)
                        task.LoadData(new TagCompound { ["count"] = match.GetInt("Count") });

                    continue;
                }

                var saved = savedTasks.FirstOrDefault(t => t.GetString("key") == task.DescriptionKey);

                if (saved != null)
                    task.LoadData(saved);
            }
        }
    }
    #endregion

    #region Upkeep
    public override void OnEnterWorld()
    {
        // Conditions are re-read from scratch rather than replayed, so a gate that opened while this
        // player was elsewhere -- a day passing, a boss going down -- is caught on the way in.
        InvalidateUnlocks();

        RefreshSidebar();
    }

    public override void PostUpdate()
    {
        UpdateUnlocks();

        foreach (var job in jobs)
        {
            if (job.State is JobState.Active or JobState.Available)
                job.Update();
        }

        if (Player.whoAmI == Main.myPlayer)
            RefreshSidebar();
    }

    /// <summary>
    /// Puts the sidebar up when there is anything to show and takes it down when there isn't.
    /// </summary>
    /// <remarks>
    /// The sidebar re-reads the job list itself every frame, so it only needs creating once. This used
    /// to be latched behind a <c>notificationExists</c> flag that never reset on world change.
    /// </remarks>
    private void RefreshSidebar()
    {
        if (Main.dedServ || JobSidebarManager.Instance is null)
            return;

        var headline = jobs.FirstOrDefault(job => job.State == JobState.Active)
                    ?? jobs.FirstOrDefault(job => job.State == JobState.Available);

        if (headline is null)
            JobSidebarManager.Instance.ClearNotification();
        else
            JobSidebarManager.Instance.EnsureNotification(headline);
    }
    #endregion
}
