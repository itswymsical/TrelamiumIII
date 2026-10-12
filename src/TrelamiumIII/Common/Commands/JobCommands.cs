using System.Linq;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Commands;

/// <summary>Debug control over the local player's jobs and the story flags their unlock conditions read.</summary>
public class JobCommand : ModCommand
{
    public override string Command => "job";

    public override CommandType Type => CommandType.Chat | CommandType.World;

    public override string Usage =>
        "/job <unlock|lock|start|reset|complete> <id or name> | /job list [unlocked|active|completed|locked] | /job flag <set|clear|list> [name]";

    public override string Description => "Unlocks, locks, starts, resets, completes or lists jobs, and raises or lowers job flags";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        JobPlayer jobs = caller.Player.GetModPlayer<JobPlayer>();

        switch (verb)
        {
            case "list":
                List(caller, jobs, args.Length > 1 ? args[1].ToLowerInvariant() : "all");
                return;

            case "flag":
                Flag(caller, jobs, args);
                return;

            case "unlock" or "lock" or "start" or "reset" or "complete":
                break;

            default:
                caller.Reply("Usage: " + Usage, Color.Red);
                return;
        }

        if (args.Length < 2)
        {
            caller.Reply($"Usage: /job {verb} <id or name>", Color.Red);
            return;
        }

        int id = ParseJobId(args[1]);

        if (id == -1)
        {
            caller.Reply($"Job '{args[1]}' not found", Color.Red);
            return;
        }

        if (!EventSystem.IsJobRegistered(id))
        {
            caller.Reply($"Job ID {id} does not exist", Color.Red);
            return;
        }

        Job job = jobs.GetOrCreateJob(id);

        switch (verb)
        {
            case "unlock":
                jobs.UnlockJob(id);
                caller.Reply($"Unlocked: {job.Name}", Color.Green);
                break;

            case "lock":
                jobs.LockJob(id);
                caller.Reply($"Locked: {job.Name}", Color.Green);
                break;

            case "start":
                if (job.State != JobState.Available)
                {
                    caller.Reply($"Job '{job.Name}' is locked. Use /job unlock first", Color.Yellow);
                    break;
                }

                jobs.StartJob(id);
                caller.Reply($"Started: {job.Name}", Color.Green);
                break;

            case "reset":
                job.Reset();
                caller.Reply($"Reset: {job.Name}", Color.Green);
                break;

            case "complete":
                Complete(caller, jobs, job, id);
                break;
        }
    }

    /// <summary>A numeric id, or a <see cref="JobID" /> field name (case-insensitive); -1 when neither matches.</summary>
    private static int ParseJobId(string identifier)
    {
        if (int.TryParse(identifier, out int id))
            return id;

        var field = typeof(JobID).GetFields()
            .FirstOrDefault(f => string.Equals(f.Name, identifier, StringComparison.OrdinalIgnoreCase));

        return field != null ? (int)field.GetValue(null)! : -1;
    }

    /// <summary>Starts the job if needed, since <see cref="JobPlayer.CompleteJob" /> refuses one that hasn't started.</summary>
    private static void Complete(CommandCaller caller, JobPlayer jobs, Job job, int id)
    {
        if (job.State == JobState.Available)
            jobs.StartJob(id);

        foreach (var set in job.TaskList)
        {
            foreach (var task in set.Task)
                task.ForceComplete();
        }

        job.CurrentTaskSet = job.TaskList.Count - 1;

        if (jobs.CompleteJob(id))
            caller.Reply($"Completed: {job.Name}", Color.Green);
        else
            caller.Reply($"Could not complete '{job.Name}' (state: {job.State})", Color.Yellow);
    }

    private static void List(CommandCaller caller, JobPlayer jobs, string filter)
    {
        caller.Reply("# All Jobs: ", Color.CornflowerBlue);

        // Materialised so a filter like "locked" also matches jobs this player never touched.
        foreach (int registered in EventSystem.RegisteredJobIds.ToList())
            jobs.GetOrCreateJob(registered);

        var all = jobs.GetAllJobs();

        var filtered = filter switch
        {
            "unlocked" => all.Where(j => j.State == JobState.Available),
            "completed" => all.Where(j => j.State == JobState.Completed),
            "locked" => all.Where(j => j.State == JobState.Locked),
            "active" => all.Where(j => j.State == JobState.Active),
            _ => all
        };

        if (!filtered.Any())
        {
            caller.Reply($"  No Jobs found (filter: {filter})", Color.Gray);
            return;
        }

        foreach (var job in filtered)
        {
            string status = job.State switch
            {
                JobState.Locked => " - Locked",
                JobState.Completed => " - Completed",
                JobState.Active => " - Active",
                _ => " - Available"
            };

            caller.Reply($"  {status} {job.ID}: {job.Name}", Color.White);
        }
    }

    /// <summary><c>/job flag set|clear name</c>, or <c>/job flag [list]</c>.</summary>
    private static void Flag(CommandCaller caller, JobPlayer jobs, string[] args)
    {
        string action = args.Length > 1 ? args[1].ToLowerInvariant() : "list";

        if (action == "list")
        {
            if (jobs.Flags.Count == 0)
                caller.Reply("No flags raised", Color.Gray);
            else
                caller.Reply($"Flags: {string.Join(", ", jobs.Flags)}", Color.CornflowerBlue);

            return;
        }

        if (args.Length < 3)
        {
            caller.Reply("Usage: /job flag <set|clear|list> [name]", Color.Red);
            return;
        }

        string flag = args[2];

        switch (action)
        {
            case "set":
                caller.Reply(jobs.SetFlag(flag) ? $"Raised '{flag}'" : $"'{flag}' was already up", Color.Green);
                break;

            case "clear":
                caller.Reply(jobs.ClearFlag(flag) ? $"Lowered '{flag}'" : $"'{flag}' was not up", Color.Green);
                break;

            default:
                caller.Reply("Usage: /job flag <set|clear|list> [name]", Color.Red);
                break;
        }
    }
}
