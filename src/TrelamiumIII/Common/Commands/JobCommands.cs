using System.Linq;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;

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

    public static Job GetOrCreateJob(int JobId, Player player)
    {
        // Try to create Job
        var Job = EventSystem.CreateJob(JobId);
        if (Job == null)
            return null;

        // Get or create from appropriate storage
        return player.GetModPlayer<JobPlayer>().GetOrCreateJob(JobId);
    }
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

        if (Job.Status != JobStatus.Unlocked)
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

        // Force complete all tasks
        foreach (var set in Job.TaskList)
        {
            foreach (var obj in set.Task)
            {
                obj.Count = obj.RequiredCount;
                obj.IsCompleted = true;
            }
        }

        // Complete via appropriate storage
        caller.Player.GetModPlayer<JobPlayer>().CompleteJob(JobId);

        caller.Reply($"Completed: {Job.Name}", Color.Green);
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
        
        var allJobs = jobPlayer.ActiveJobs()
            .Concat(jobPlayer.UnlockedJobs())
            .Distinct();

        var filteredJobs = filter switch
        {
            "unlocked" => allJobs.Where(j => j.Status == JobStatus.Unlocked),
            "completed" => allJobs.Where(j => j.Status == JobStatus.Completed),
            "locked" => allJobs.Where(j => j.Status == JobStatus.Locked),
            _ => allJobs
        };

        if (filteredJobs.Any())
        {
            foreach (var job in filteredJobs)
            {
                var status = job.Status switch
                {
                    JobStatus.Locked => " - Locked",
                    JobStatus.Completed => " - Completed",
                    _ => " - Unlocked"
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