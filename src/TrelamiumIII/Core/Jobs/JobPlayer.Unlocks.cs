using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// The half of <see cref="JobPlayer"/> that decides what is on offer: the story flags this player
/// carries, and the pass that re-reads every job's <see cref="Job.UnlockCondition"/> against them.
/// </summary>
public partial class JobPlayer
{
    /// <summary>
    /// Ticks between evaluations when nothing has happened. Conditions read live state -- a blood moon
    /// rising is nobody's event -- so the pass has to run on its own even in a silent world.
    /// </summary>
    private const int UnlockPollInterval = 20;

    private readonly HashSet<string> flags = [];

    /// <summary>Set by anything that could have changed an answer. Starts true so the first tick evaluates.</summary>
    private bool unlocksDirty = true;

    private int nextUnlockPoll;

    #region Flags
    public bool HasFlag(string flag) => flags.Contains(flag);

    public IReadOnlyCollection<string> Flags => flags;

    /// <summary>Raises a story flag. Returns true if it was not already up.</summary>
    public bool SetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag))
            return false;

        InvalidateUnlocks();
        return true;
    }

    /// <summary>Lowers a story flag. Returns true if it was up.</summary>
    public bool ClearFlag(string flag)
    {
        if (!flags.Remove(flag))
            return false;

        InvalidateUnlocks();
        return true;
    }
    #endregion

    #region Evaluation
    /// <summary>
    /// State of one of this player's jobs without building it. A job nobody has unlocked has no
    /// instance, and reads as <see cref="JobState.Locked"/> -- which is what it is.
    /// </summary>
    public JobState StateOf(int jobId) => FindJob(jobId)?.State ?? JobState.Locked;

    /// <summary>Marks the unlock rules as worth re-reading on the next tick.</summary>
    public void InvalidateUnlocks() => unlocksDirty = true;

    /// <summary>
    /// Re-reads every unlock rule, unlocking and -- for a revocable rule -- re-locking as the answers
    /// change.
    /// </summary>
    /// <remarks>
    /// Job state is saved per player on each client, so each client evaluates its own player, the same
    /// division the day milestones used before conditions replaced them.
    /// </remarks>
    internal void UpdateUnlocks()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;

        var now = (int)Main.GameUpdateCount;

        if (!unlocksDirty && now < nextUnlockPoll)
            return;

        // Cleared before the pass, not after: a rule that sets a flag from OnUnlocked has to leave the
        // list dirty for the next tick rather than have its own change wiped off.
        unlocksDirty = false;
        nextUnlockPoll = now + UnlockPollInterval;

        var rules = JobUnlockRegistry.All;

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var state = StateOf(rule.JobID);

            if (rule.Condition.IsMet(this))
            {
                if (state == JobState.Locked)
                    UnlockJob(rule.JobID);
            }
            else if (rule.Revocable && state == JobState.Available)
            {
                LockJob(rule.JobID);
            }
        }
    }

    private Job FindJob(int jobId)
    {
        for (var i = 0; i < jobs.Count; i++)
        {
            if (jobs[i].ID == jobId)
                return jobs[i];
        }

        return null;
    }
    #endregion

    #region Persistence
    private void SaveFlags(TagCompound tag) => tag["Flags"] = new List<string>(flags);

    private void LoadFlags(TagCompound tag)
    {
        flags.Clear();

        foreach (var flag in tag.GetList<string>("Flags"))
            flags.Add(flag);

        InvalidateUnlocks();
    }
    #endregion
}
