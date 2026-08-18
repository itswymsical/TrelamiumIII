using TrelamiumIII.Common.UI.Jobs;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.UI;
using TrelamiumIII.Core.Jobs.Events;

namespace TrelamiumIII.Core.Jobs;

public enum JobProgress { Inactive, Ongoing, Completed }

public enum JobStatus { Locked, Unlocked, Completed }

/// <summary>
/// A Job represents a quest or task that the player can undertake, 
/// consisting of one or more sets of tasks (tasks) that must be completed in order. 
/// Each Job has its own unique ID, name, description, list of tasks, rewards, and provider NPC.
/// </summary>
public abstract class Job
{
    #region Properties
    public int ID { get; set; }
    public string NameKey { get; private set; }        // Localization key
    public string DescriptionKey { get; private set; } // Localization key

    // Resolved at runtime
    public string Name => TrelamiumIII.Instance.GetLocalization($"Jobs.{NameKey}").Value;
    public string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Value;

    /// <summary>
    /// List of task sets, where each set contains one or more tasks that must be completed. The player must complete all tasks in the current set to progress to the next set. This allows for Jobs with multiple stages or phases, where each phase has its own set of tasks. For example, a Job might require the player to first gather resources (set 1), then defeat a boss (set 2), and finally return to the quest giver (set 3). The CurrentSet property tracks which set the player is currently working on, and the Job progresses through the sets as tasks are completed. Each Task contains its own description, required count, and completion status, allowing for detailed tracking of Job progress.
    /// </summary>
    public List<TaskList> TaskList { get; protected set; }
    public List<JobReward> Rewards { get; private set; } // Changed type
    public int ProviderNPC { get; set; }

    public JobProgress Progress { get; set; } = JobProgress.Inactive;

    public JobStatus Status { get; set; } = JobStatus.Locked;

    /// <summary>
    /// Index of the current task set the player is working on. Jobs can have multiple sets of tasks that need to be completed in order. This tracks which set is currently active.
    /// </summary>
    public int CurrentSet { get; set; } = 0;
    #endregion

    /// <summary>
    /// Constructor acts as definition for the Job. 
    /// When creating a new Job, you provide all the necessary information about its tasks and rewards through the parameters. 
    /// </summary>
    /// <param name="id">ID number.</param>
    /// <param name="name">Display name.</param>
    /// <param name="description">Description, appears in UI.</param>
    /// <param name="taskList">The list/array for all task sets and their nested tasks.</param>
    /// <param name="rewards">The goods!</param>
    /// <param name="providerNPC">The provider/comJober of this Job. An indicator will appear above their head when this Job becomes available.</param>
    protected Job(int id, string nameKey, string descriptionKey, List<List<(string, int)>> taskList,
           List<JobReward> rewards, int providerNPC)
    {
        ID = id;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
        TaskList = taskList.Select(set =>
            new TaskList(set.Select(o =>
                new Task(o.Item1, o.Item2)).ToList())).ToList();
        Rewards = rewards;
        ProviderNPC = providerNPC;
    }

    /// <summary>
    /// Determines if this Job cares about a specific event for the current task.
    /// Override in child classes to define event matching logic.
    /// </summary>
    /// <param name="evt">The event that occurred</param>
    /// <param name="currentSet">Current task set index</param>
    /// <param name="taskIndex">Task index within the set</param>
    /// <returns>True if this Job should react to the event</returns>
    public virtual bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        return false;
    }

    /// <summary>
    /// Called when a matching event occurs. Override to customize behavior.
    /// Default: just increment progress by event amount.
    /// </summary>
    /// <param name="evt">The event that matched</param>
    /// <param name="taskIndex">Which task to update</param>
    /// <returns>True if progress was updated</returns>
    public virtual bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        return UpdateProgress(taskIndex, evt.Amount);
    }

    public virtual void OnJobStart() { }

    public virtual void OnJobComplete(bool giveRewards = true)
    {
        if (giveRewards)
            GiveRewards();

        InGameNotificationsTracker.AddNotification(new JobCompleteNotification(this));
    }

    public bool UpdateProgress(int task, int amount)
    {
        if (Progress != JobProgress.Ongoing)
            return false;

        var currentSet = TaskList[CurrentSet];
        if (task >= 0 && task < currentSet.Task.Count)
        {
            var obj = currentSet.Task[task];
            if (!obj.IsCompleted || amount < 0)
            {
                var wasCompleted = obj.UpdateProgress(amount);
                Main.LocalPlayer.GetModPlayer<JobPlayer>().NotifyJobUpdate(this);

                if (wasCompleted && amount > 0)
                    SoundEngine.PlaySound(new SoundStyle($"{Assets.SFX.TaskComplete.KEY}") 
                        with { Volume = 0.75f }, Main.LocalPlayer.position);
                

                if (currentSet.IsCompleted)
                {
                    if (CurrentSet < TaskList.Count - 1)
                    {
                        CurrentSet++;
                        return true;
                    }
                    if (TaskList.All(set => set.IsCompleted))
                    {
                        Complete();
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// ALWAYS call base.Reset() if you're gonna use this.
    /// </summary>
    public virtual void Reset()
    {
        Progress = JobProgress.Inactive;
        Status = JobStatus.Unlocked;
        CurrentSet = 0;
        foreach (var set in TaskList)
        {
            set.Reset();
        }
    }

    public void Complete()
    {
        if (Progress == JobProgress.Ongoing && TaskList.All(set => set.IsCompleted))
        {
            Progress = JobProgress.Completed;
            Status = JobStatus.Completed;
            OnJobComplete();
        }
    }

    public virtual void Update()
    {
        if (Progress != JobProgress.Ongoing && Status != JobStatus.Unlocked) return;
    }

    private void GiveRewards()
    {
        foreach (var reward in Rewards)
        {
            var item = reward.GetItem(); // Resolves here if needed
            if (item != null && item.type > ItemID.None)
            {
                Main.LocalPlayer.QuickSpawnItem(new EntitySource_Misc("Job_Reward"), item.type, item.stack);
            }
        }
    }

    /// <summary>
    /// Sets the visibility of a specific task. This can be used to hide tasks until certain conditions are met (e.g. completing a previous task). By default, all tasks are visible.
    /// </summary>
    /// <param name="setIndex"></param>
    /// <param name="taskIndex"></param>
    /// <param name="isVisible"></param>
    public void SetTaskVisibility(int setIndex, int taskIndex, bool isVisible)
    {
        if (setIndex >= 0 && setIndex < TaskList.Count &&
            taskIndex >= 0 && taskIndex < TaskList[setIndex].Task.Count)
        {
            TaskList[setIndex].Task[taskIndex].IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Sets a custom visibility condition for a specific task. The condition is a function that takes the current Job state and returns true if the task should be visible. This allows for dynamic visibility based on other tasks or external factors.
    /// </summary>
    /// <param name="setIndex"></param>
    /// <param name="taskIndex"></param>
    /// <param name="condition"></param>
    public void SetTaskVisibilityCondition(int setIndex, int taskIndex,
        Task.VisibilityCondition condition)
    {
        if (setIndex >= 0 && setIndex < TaskList.Count &&
            taskIndex >= 0 && taskIndex < TaskList[setIndex].Task.Count)
        {
            TaskList[setIndex].Task[taskIndex].VisibilityCheck = condition;
        }
    }

    /// <summary>
    /// Convenience method to show an task only after a specific dependency task is completed. This sets a visibility condition that checks the completion status of the dependency task. If the dependency task is not completed, the target task will be hidden.
    /// </summary>
    /// <param name="setIndex"></param>
    /// <param name="taskToShow"></param>
    /// <param name="dependencyTask"></param>
    public void ShowTaskAfterCompletion(int setIndex, int taskToShow, int dependencyTask)
    {
        SetTaskVisibilityCondition(setIndex, taskToShow, (Job) =>
        {
            var set = Job.TaskList[setIndex];
            return dependencyTask >= 0 &&
                   dependencyTask < set.Task.Count &&
                   set.Task[dependencyTask].IsCompleted;
        });
    }
}