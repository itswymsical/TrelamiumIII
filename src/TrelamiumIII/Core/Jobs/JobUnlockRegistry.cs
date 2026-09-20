using System.Collections.Generic;

namespace TrelamiumIII.Core.Jobs;

/// <summary>What puts a job on offer, and whether the offer can be taken back.</summary>
/// <param name="Revocable">
/// When true the job is withdrawn again the moment <paramref name="Condition"/> stops holding. Only
/// ever takes back a job still sitting on the board -- one the player has accepted is theirs.
/// </param>
public readonly record struct JobUnlockRule(int JobID, JobCondition Condition, bool Revocable);

/// <summary>
/// Every job's unlock rule, read off the prototypes at load.
/// </summary>
/// <remarks>
/// Held apart from the per-player job instances so a gate can be evaluated for a job nobody has
/// unlocked: the alternative is building one instance of every job for every player on the chance that
/// one of them opens.
/// </remarks>
public static class JobUnlockRegistry
{
    private static readonly List<JobUnlockRule> Rules = [];

    /// <summary>Indexed rather than enumerated by the evaluator, which runs every few ticks.</summary>
    public static IReadOnlyList<JobUnlockRule> All => Rules;

    public static void Register(JobUnlockRule rule) => Rules.Add(rule);

    public static void Clear() => Rules.Clear();
}
