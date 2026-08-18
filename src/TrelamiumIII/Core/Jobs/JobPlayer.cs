using Ionic.Zlib;
using TrelamiumIII.Common.Commands;
using TrelamiumIII.Common.UI.Jobs;
using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using Terraria.UI;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Stores Jobs and event handling logic.
/// </summary>
public partial class JobPlayer : ModPlayer
{
    private List<Job> Jobs = new();

    #region Event Handling
    public void OnGameEvent(GameEvent evt)
    {
        foreach (var Job in Jobs)
        {
            if (Job.Progress != JobProgress.Ongoing)
                continue;

            ProcessEventForJob(Job, evt);
        }
    }

    private void ProcessEventForJob(Job Job, GameEvent evt)
    {
        var currentSet = Job.CurrentSet;
        if (currentSet < 0 || currentSet >= Job.TaskList.Count)
            return;

        var tasks = Job.TaskList[currentSet].Task;

        for (var i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].IsCompleted)
                continue;

            if (Job.MatchesEvent(evt, currentSet, i))
            {
                Job.OnMatchedEvent(evt, i);
            }
        }
    }
    #endregion

    #region Job Access
    public Job GetOrCreateJob(int JobId)
    {
        var existing = Jobs.FirstOrDefault(m => m.ID == JobId);
        if (existing != null)
            return existing;

        // Create new Job instance
        var Job = EventSystem.CreateJob(JobId);
        if (Job != null)
        {
            Jobs.Add(Job);
            return Job;
        }

        return null;
    }

    public void StartJob(int JobId)
    {
        var Job = GetOrCreateJob(JobId);
        if (Job?.Status == JobStatus.Unlocked)
        {
            Job.Progress = JobProgress.Ongoing;
            Job.OnJobStart();

            if (Player.whoAmI == Main.myPlayer)
            {
                InGameNotificationsTracker.AddNotification(new JobAcceptNotification(Job));
            }
        }
    }

    public void UnlockJob(int JobId)
    {
        var Job = JobCommandHelper.GetOrCreateJob(JobId, Main.LocalPlayer);
        if (Job == null)
        {
            Main.NewText($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        var currentJob = GetOrCreateJob(JobId);
        if (currentJob?.Status == JobStatus.Locked)
        {
            currentJob.Status = JobStatus.Unlocked;
        }
    }

    public void LockJob(int JobId)
    {
        var Job = GetOrCreateJob(JobId);
        if (Job?.Status == JobStatus.Unlocked)
        {
            Job.Status = JobStatus.Locked;
        }
    }

    public void CompleteJob(int JobId)
    {
        var Job = Jobs.FirstOrDefault(m => m.ID == JobId);
        Job?.Complete();
        InGameNotificationsTracker.AddNotification(new JobCompleteNotification(Job));
    }

    /// <summary>
    /// Returns Jobs that are currently in progress. These can be shown in the Job sidebar or journal.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Job> ActiveJobs() =>
        Jobs.Where(m => m.Progress == JobProgress.Ongoing);

    /// <summary>
    /// Returns Jobs that are unlocked but not yet started. These can be shown in NPC dialogue or Job boards.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Job> UnlockedJobs() =>
        Jobs.Where(m => m.Status == JobStatus.Unlocked);

    public IEnumerable<Job> GetAllJobs() => Jobs;

    /// <summary>
    /// Returns true if the player has any Jobs that are unlocked but not yet started.
    /// </summary>
    /// <returns></returns>
    public bool HasAvailableJobs() => UnlockedJobs().Any();
    #endregion

    #region Persistence
    public override void SaveData(TagCompound tag)
    {
        var JobData = new List<TagCompound>();

        foreach (var Job in Jobs)
        {
            JobData.Add(new TagCompound
            {
                ["ID"] = Job.ID,
                ["Progress"] = (int)Job.Progress,
                ["Status"] = (int)Job.Status,
                ["CurrentList"] = Job.CurrentSet,
                ["Tasks"] = SerializeTasks(Job.TaskList)
            });
        }

        tag["Jobs"] = JobData;
    }

    public override void LoadData(TagCompound tag)
    {
        Jobs.Clear();

        var JobData = tag.GetList<TagCompound>("Jobs");

        foreach (var JobTag in JobData)
        {
            var JobId = JobTag.GetInt("ID");
            var Job = EventSystem.CreateJob(JobId);

            if (Job != null)
            {
                Job.Progress = (JobProgress)JobTag.GetInt("Progress");
                Job.Status = (JobStatus)JobTag.GetInt("Status");
                Job.CurrentSet = JobTag.GetInt("CurrentList");

                DeserializeTasks(Job.TaskList, JobTag.GetList<TagCompound>("Tasks"));

                Jobs.Add(Job);
            }
        }
    }

    private List<TagCompound> SerializeTasks(List<TaskList> taskSets)
    {
        var result = new List<TagCompound>();

        foreach (var set in taskSets)
        {
            var tasks = new List<TagCompound>();

            foreach (var obj in set.Task)
            {
                tasks.Add(new TagCompound
                {
                    ["Desc"] = obj.Description,
                    ["Done"] = obj.IsCompleted,
                    ["Count"] = obj.Count,
                    ["Required"] = obj.RequiredCount
                });
            }

            result.Add(new TagCompound { ["Objs"] = tasks });
        }

        return result;
    }

    private void DeserializeTasks(List<TaskList> taskSets, IList<TagCompound> savedSets)
    {
        for (var i = 0; i < Math.Min(taskSets.Count, savedSets.Count); i++)
        {
            var savedObjs = savedSets[i].GetList<TagCompound>("Objs");
            var currentObjs = taskSets[i].Task;

            foreach (var currentObj in currentObjs)
            {
                var match = savedObjs.FirstOrDefault(s =>
                    s.GetString("Desc") == currentObj.Description);

                if (match != null)
                {
                    currentObj.IsCompleted = match.GetBool("Done");
                    currentObj.Count = match.GetInt("Count");
                }
            }
        }
    }
    #endregion

    #region UI Notifications
    public void NotifyJobUpdate(Job Job) { }
    public bool NPCHasAvailableJob(int npcType) =>
        UnlockedJobs().Any(m => m.ProviderNPC == npcType);

    private bool notificationExists = false;

    public override void OnEnterWorld()
    {
        base.OnEnterWorld();

        notificationExists = false;
    }

    public override void PostUpdate()
    {
        var allJobs = this.ActiveJobs()
            .Concat(this.UnlockedJobs());

        foreach (var Job in allJobs)
        {
            Job.Update();
        }


        bool hasJobs = allJobs.Any();

        if (hasJobs && !notificationExists)
        {
            var currentJob = allJobs.FirstOrDefault();

            if (currentJob != null)
            {
                JobSidebarManager.Instance.SetNotification(new JobSidebar(currentJob));
                notificationExists = true;
            }
        }
        else if (!hasJobs)
        {
            JobSidebarManager.Instance.ClearNotification();
            notificationExists = false;
        }
    }
    #endregion
}