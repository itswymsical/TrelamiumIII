using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// A yes/no reading of one player and the world around them, used to decide whether a job is on offer.
/// </summary>
/// <remarks>
///     <para>
///         A condition only reads. Nothing it touches may change as a result of being asked, which is
///         what lets every rule be re-run whenever anything at all happens without the system having to
///         know which rule cared about what.
///     </para>
///     <para>
///         Conditions compose, so a gate reads as the sentence it came from:
///         <code>
///         JobCondition.BloodMoon &amp; JobCondition.MaxLifeAtMost(200) &amp; !JobCondition.AnyBossDowned
///         </code>
///     </para>
/// </remarks>
public sealed class JobCondition
{
    private readonly Func<JobPlayer, bool> predicate;

    private JobCondition(Func<JobPlayer, bool> predicate) => this.predicate = predicate;

    public bool IsMet(JobPlayer jobs) => predicate(jobs);

    #region Sources
    /// <summary>Wraps a test with access to the whole job list. The widest, and the least readable.</summary>
    public static JobCondition From(Func<JobPlayer, bool> predicate) => new(predicate);

    /// <summary>Wraps a test that only reads the player: zones, stats, inventory, buffs.</summary>
    public static JobCondition ForPlayer(Func<Player, bool> predicate) => new(jobs => predicate(jobs.Player));

    /// <summary>Wraps a test that reads nothing but world state: downed flags, events, hardmode.</summary>
    public static JobCondition World(Func<bool> predicate) => new(_ => predicate());

    public static readonly JobCondition Always = World(() => true);

    public static readonly JobCondition Never = World(() => false);
    #endregion

    #region Composition
    public static JobCondition All(params JobCondition[] conditions) => new(jobs =>
    {
        for (var i = 0; i < conditions.Length; i++)
        {
            if (!conditions[i].IsMet(jobs))
                return false;
        }

        return true;
    });

    public static JobCondition Any(params JobCondition[] conditions) => new(jobs =>
    {
        for (var i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].IsMet(jobs))
                return true;
        }

        return false;
    });

    public static JobCondition Not(JobCondition condition) => new(jobs => !condition.IsMet(jobs));

    public static JobCondition operator &(JobCondition left, JobCondition right) => All(left, right);

    public static JobCondition operator |(JobCondition left, JobCondition right) => Any(left, right);

    public static JobCondition operator !(JobCondition condition) => Not(condition);
    #endregion

    #region Progression
    /// <summary>The world has reached <paramref name="day"/>. Day 0 is the day the world is made.</summary>
    public static JobCondition WorldDay(int day) => World(() => WorldTimeSystem.TotalDaysElapsed >= day);

    /// <summary>Another job of this player's is in exactly <paramref name="state"/>.</summary>
    /// <remarks>
    /// A job the player has never touched reads as <see cref="JobState.Locked"/>, so nothing has to be
    /// built to answer this.
    /// </remarks>
    public static JobCondition JobIs(int jobId, JobState state) => new(jobs => jobs.StateOf(jobId) == state);

    /// <summary>The chaining condition: job <paramref name="jobId"/> is done, so this one may be offered.</summary>
    public static JobCondition JobCompleted(int jobId) => JobIs(jobId, JobState.Completed);

    public static JobCondition JobActive(int jobId) => JobIs(jobId, JobState.Active);

    /// <summary>Job <paramref name="jobId"/> has been taken on, whether or not it has been finished.</summary>
    public static JobCondition JobStarted(int jobId) =>
        new(jobs => jobs.StateOf(jobId) is JobState.Active or JobState.Completed);

    /// <summary>
    /// A named story flag this player carries. The escape hatch for anything the game has no reading
    /// of: set it from wherever the moment happens, and the gate opens on the next evaluation.
    /// </summary>
    public static JobCondition Flag(string flag) => new(jobs => jobs.HasFlag(flag));
    #endregion

    #region World state
    public static readonly JobCondition Hardmode = World(() => Main.hardMode);

    public static readonly JobCondition BloodMoon = World(() => Main.bloodMoon);

    public static readonly JobCondition Eclipse = World(() => Main.eclipse);

    public static readonly JobCondition Daytime = World(() => Main.dayTime);

    public static readonly JobCondition Nighttime = World(() => !Main.dayTime);

    /// <summary>Any vanilla boss has gone down, hardmode counting as proof of the Wall of Flesh.</summary>
    public static readonly JobCondition AnyBossDowned = World(() =>
        Main.hardMode || NPC.downedSlimeKing || NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3
        || NPC.downedQueenBee || NPC.downedDeerclops);

    /// <summary>An NPC of this type is alive somewhere in the world.</summary>
    public static JobCondition NPCPresent(int npcType) => World(() => NPC.AnyNPCs(npcType));
    #endregion

    #region Player state
    /// <summary>In a vanilla zone: <c>JobCondition.InZone(player =&gt; player.ZoneJungle)</c>.</summary>
    public static JobCondition InZone(Func<Player, bool> zone) => ForPlayer(zone);

    public static JobCondition InBiome<T>() where T : ModBiome => ForPlayer(player => player.InModBiome<T>());

    public static JobCondition MaxLifeAtLeast(int life) => ForPlayer(player => player.statLifeMax >= life);

    public static JobCondition MaxLifeAtMost(int life) => ForPlayer(player => player.statLifeMax <= life);

    /// <summary>Carrying <paramref name="stack"/> or more of <paramref name="itemType"/>.</summary>
    public static JobCondition HasItem(int itemType, int stack = 1) =>
        ForPlayer(player => player.CountItem(itemType, stack) >= stack);
    #endregion
}
