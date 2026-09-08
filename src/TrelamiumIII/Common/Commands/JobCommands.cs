using System.Linq;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Commands;

public static class JobCommandHelper
{
    public static int TryParseJobIdentifier(string identifier)
    {
        // Numeric ID
        if (int.TryParse(identifier, out var JobId))
            return JobId;

        // Match by name in JobID class
        var field = typeof(JobID).GetFields()
            .FirstOrDefault(f => string.Equals(f.Name, identifier, StringComparison.OrdinalIgnoreCase));

        return field != null ? (int)field.GetValue(null) : -1;
    }

    /// <summary>
    /// This player's instance of a job, or <see langword="null"/> if no such job is registered.
    /// </summary>
    /// <remarks>
    /// The registry check used to be a full <see cref="EventSystem.CreateJob"/> whose result was
    /// thrown away -- a whole job constructed, task lists and all, purely to find out whether the id
    /// existed.
    /// </remarks>
    public static Job GetOrCreateJob(int jobId, Player player)
        => EventSystem.IsJobRegistered(jobId) ? player.GetModPlayer<JobPlayer>().GetOrCreateJob(jobId) : null;
}

public class UnlockJobCommand : ModCommand
{
    public override string Command => "unlockj";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/unlockj <JobID or name>";
    public override string Description => "Unlocks the specified Job";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        var JobId = JobCommandHelper.TryParseJobIdentifier(args[0]);
        if (JobId == -1)
        {
            caller.Reply($"Job '{args[0]}' not found", Color.Red);
            return;
        }

        var Job = JobCommandHelper.GetOrCreateJob(JobId, caller.Player);
        if (Job == null)
        {
            caller.Reply($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        // Unlock via appropriate storage
        caller.Player.GetModPlayer<JobPlayer>().UnlockJob(JobId);

        caller.Reply($"Unlocked: {Job.Name}", Color.Green);
    }
}

public class LockJobCommand : ModCommand
{
    public override string Command => "lockj";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/lockj <JobID or name>";
    public override string Description => "Locks the specified Job";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        var JobId = JobCommandHelper.TryParseJobIdentifier(args[0]);
        if (JobId == -1)
        {
            caller.Reply($"Job '{args[0]}' not found", Color.Red);
            return;
        }

        var Job = JobCommandHelper.GetOrCreateJob(JobId, caller.Player);
        if (Job == null)
        {
            caller.Reply($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        // Unlock via appropriate storage
        caller.Player.GetModPlayer<JobPlayer>().LockJob(JobId);

        caller.Reply($"Locked: {Job.Name}", Color.Green);
    }
}

public class StartJobCommand : ModCommand
{
    public override string Command => "startj";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/startj <JobID or name>";
    public override string Description => "Starts the specified Job";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        var JobId = JobCommandHelper.TryParseJobIdentifier(args[0]);
        if (JobId == -1)
        {
            caller.Reply($"Job '{args[0]}' not found", Color.Red);
            return;
        }

        var Job = JobCommandHelper.GetOrCreateJob(JobId, caller.Player);
        if (Job == null)
        {
            caller.Reply($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        if (Job.State != JobState.Available)
        {
            caller.Reply($"Job '{Job.Name}' is locked. Use /unlockj first", Color.Yellow);
            return;
        }

        // Start via appropriate storage
        caller.Player.GetModPlayer<JobPlayer>().StartJob(JobId);

        caller.Reply($"Started: {Job.Name}", Color.Green);
    }
}

public class ResetJobCommand : ModCommand
{
    public override string Command => "resetj";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/resetj <JobID or name>";
    public override string Description => "Resets the specified Job";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        var JobId = JobCommandHelper.TryParseJobIdentifier(args[0]);
        if (JobId == -1)
        {
            caller.Reply($"Job '{args[0]}' not found", Color.Red);
            return;
        }

        var Job = JobCommandHelper.GetOrCreateJob(JobId, caller.Player);
        if (Job == null)
        {
            caller.Reply($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        // Reset the Job
        Job.Reset();
        caller.Reply($"Reset: {Job.Name}", Color.Green);
    }
}

public class CompleteJobCommand : ModCommand
{
    public override string Command => "completej";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/completej <JobID or name>";
    public override string Description => "Completes the specified Job";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length < 1)
        {
            caller.Reply("Usage: " + Usage, Color.Red);
            return;
        }

        var JobId = JobCommandHelper.TryParseJobIdentifier(args[0]);
        if (JobId == -1)
        {
            caller.Reply($"Job '{args[0]}' not found", Color.Red);
            return;
        }

        var Job = JobCommandHelper.GetOrCreateJob(JobId, caller.Player);
        if (Job == null)
        {
            caller.Reply($"Job ID {JobId} does not exist", Color.Red);
            return;
        }

        // Force complete all tasks. The job must also be underway, or Complete() correctly refuses:
        // an unstarted job has nothing to pay out for.
        if (Job.State == JobState.Available)
            caller.Player.GetModPlayer<JobPlayer>().StartJob(JobId);

        foreach (var set in Job.TaskList)
        {
            foreach (var task in set.Task)
                task.ForceComplete();
        }

        Job.CurrentTaskSet = Job.TaskList.Count - 1;

        if (caller.Player.GetModPlayer<JobPlayer>().CompleteJob(JobId))
            caller.Reply($"Completed: {Job.Name}", Color.Green);
        else
            caller.Reply($"Could not complete '{Job.Name}' (state: {Job.State})", Color.Yellow);
    }
}

public class ListJobsCommand : ModCommand
{
    public override string Command => "listj";
    public override CommandType Type => CommandType.Chat | CommandType.World;
    public override string Usage => "/listj";
    public override string Description => "Lists available Jobs";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        var filter = args.Length > 0 ? args[0].ToLower() : "all";

        var jobPlayer = caller.Player.GetModPlayer<JobPlayer>();
        caller.Reply("# All Jobs: ", Color.CornflowerBlue);

        // Materialise every registered job so the listing covers ones this player has never touched,
        // which are exactly the ones a "locked" filter is asking about.
        foreach (var registered in EventSystem.RegisteredJobIds.ToList())
            jobPlayer.GetOrCreateJob(registered);

        // Listing only the active and available ones meant the "locked" and "completed" filters could
        // never match anything.
        var allJobs = jobPlayer.GetAllJobs();

        var filteredJobs = filter switch
        {
            "unlocked" => allJobs.Where(j => j.State == JobState.Available),
            "completed" => allJobs.Where(j => j.State == JobState.Completed),
            "locked" => allJobs.Where(j => j.State == JobState.Locked),
            "active" => allJobs.Where(j => j.State == JobState.Active),
            _ => allJobs
        };

        if (filteredJobs.Any())
        {
            foreach (var job in filteredJobs)
            {
                var status = job.State switch
                {
                    JobState.Locked => " - Locked",
                    JobState.Completed => " - Completed",
                    JobState.Active => " - Active",
                    _ => " - Available"
                };
                caller.Reply($"  {status} {job.ID}: {job.Name}", Color.White);
            }
        }
        else
        {
            caller.Reply($"  No Jobs found (filter: {filter})", Color.Gray);
        }
    }
}