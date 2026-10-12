using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.ModLoader.IO;
using Terraria.UI;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.UI.Jobs;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Where a job stands for one player.
/// </summary>
public enum JobState
{
    /// <summary>Exists, but the player has no way to see or take it yet.</summary>
    Locked,

    /// <summary>Offered. Shows on the provider's board and puts an indicator over their head.</summary>
    Available,

    /// <summary>Taken and underway.</summary>
    Active,

    /// <summary>Finished and paid out.</summary>
    Completed
}

/// <summary>
/// A quest: an ordered chain of task sets, a payout, and the NPC who hands it out.
/// </summary>
/// <remarks>
///     <para>
///         Every set must finish before the next begins, which is what gives a job stages: gather,
///         then build, then report back. The job's own overrides are the <em>narrative</em> layer:
///         what the provider says as stages turn over, what they say when the player is doing it
///         wrong. Whether an event counts toward a task is the task's business, not the job's.
///     </para>
/// </remarks>
public abstract class Job
{
    #region Definition
    public int ID { get; }

    /// <summary>Localization key, resolved on read so a language switch takes effect immediately.</summary>
    public string NameKey { get; }

    public string DescriptionKey { get; }

    public string Name => TrelamiumIII.Instance.GetLocalization($"Jobs.{NameKey}").Value;

    public virtual string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Value;

    public List<TaskList> TaskList { get; }

    public List<JobReward> Rewards { get; }

    /// <summary>NPC <em>type</em> that offers this job, matching how the board and indicator look it up.</summary>
    public int ProviderNPC { get; }

    /// <summary>Whether this NPC offers the job. Matches by type; override to tie a job to one NPC instance.</summary>
    public virtual bool IsProvidedBy(NPC npc) => npc.type == ProviderNPC;

    /// <summary>Provider name shown in job lists.</summary>
    public virtual string ProviderName => Lang.GetNPCNameValue(ProviderNPC);
    #endregion

    #region Unlocking
    /// <summary>
    /// What puts this job on its provider's board. <see langword="null"/> when nothing does on its own,
    /// and the job waits for an explicit <see cref="JobPlayer.UnlockJob"/> from dialogue or a command.
    /// </summary>
    /// <remarks>
    /// Read once, off the prototype built at load, so it must not close over anything per-player. The
    /// player it is asked about arrives as the argument to <see cref="JobCondition.IsMet"/>.
    /// </remarks>
    public virtual JobCondition UnlockCondition => null;

    /// <summary>
    /// Withdraw the offer again when <see cref="UnlockCondition"/> stops holding. Off by default, since
    /// most gates are milestones and those do not un-happen. A job the player has already accepted is
    /// never taken back either way.
    /// </summary>
    public virtual bool RevokeWhenUnmet => false;

    /// <summary>Called each time the job goes on offer, a returning revoked offer included.</summary>
    public virtual void OnUnlocked() { }
    #endregion

    #region State
    public JobState State { get; set; } = JobState.Locked;

    /// <summary>Index into <see cref="TaskList"/> of the stage currently underway.</summary>
    public int CurrentTaskSet { get; set; }

    /// <summary>
    /// The player this job belongs to, and everything it is allowed to do to them. Assigned by
    /// <see cref="JobPlayer"/> the moment the job is created; never null on a job that reached the
    /// player's list.
    /// </summary>
    public JobContext Context { get; internal set; }

    /// <summary>The stage underway, or <see langword="null"/> if the index has run off the end.</summary>
    public TaskList CurrentSet =>
        CurrentTaskSet >= 0 && CurrentTaskSet < TaskList.Count ? TaskList[CurrentTaskSet] : null;
    #endregion

    /// <summary>Ticks a job must wait between spoken lines, so one big haul can't queue up a monologue.</summary>
    protected virtual int SpeechCooldown => 60 * 20;

    private string pendingSpeech;

    /// <summary>
    /// Earliest tick this job may speak again. A deadline rather than a "last spoke at", so the
    /// initial value is simply zero -- meaning "may speak now" -- and nothing has to encode "never
    /// spoken" as a sentinel far in the past.
    /// </summary>
    private long nextSpeechTick;

    /// <param name="taskList">Stages, in order. Each stage is the set of tasks that must all finish before the next begins.</param>
    protected Job(int id, string nameKey, string descriptionKey, List<List<JobTask>> taskList,
        List<JobReward> rewards, int providerNPC)
    {
        ID = id;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
        TaskList = taskList.Select(set => new TaskList(set)).ToList();
        Rewards = rewards;
        ProviderNPC = providerNPC;
    }

    #region Hooks
    /// <summary>Called once when the player takes the job on.</summary>
    public virtual void OnJobStart() { }

    /// <summary>
    /// Every event this job sees while active, before its tasks get a look at it. For reactions that
    /// aren't progress: commentary, hints, bookkeeping.
    /// </summary>
    /// <remarks>
    /// Running first is the point -- a job can still tell what its state was <em>before</em> this
    /// event credited anything, which is what makes "you already showed me that room" possible.
    /// </remarks>
    public virtual void OnEvent(GameEvent evt) { }

    /// <summary>
    /// Called after <paramref name="task"/> took credit. Override to react to progress; the task has
    /// already recorded it, and its stage has not yet turned over.
    /// </summary>
    /// <param name="evt">
    /// What caused the credit, or <see langword="default"/> when the progress came from the job
    /// itself rather than from an event.
    /// </param>
    public virtual void OnTaskAdvanced(JobTask task, int taskIndex, GameEvent evt) { }

    /// <summary>Called when the last task of a set finishes and the job moves on.</summary>
    public virtual void OnSetAdvanced(int previousSet, int newSet) { }

    public virtual void OnJobComplete(bool giveRewards = true)
    {
        if (giveRewards)
            RewardBag.Award(Context.Player, this);

        if (Context.IsLocal)
            InGameNotificationsTracker.AddNotification(new JobCompleteNotification(this));
    }

    /// <summary>
    /// Dialogue this job wants its <see cref="ProviderNPC"/> to open instead of the vanilla chat
    /// window, asked every time the player talks to one while the job is active.
    /// </summary>
    /// <remarks>
    /// This is the "the job is talking, not the NPC" case: a briefing on the current stage, a nudge
    /// toward what is still open, the hand-in line. Resolved ahead of the NPC's own greeting, so
    /// whichever stage is underway is what gets said.
    /// Defaults to the current set's <see cref="HandInTask.PromptKey"/> while <see cref="CanHandIn"/>.
    /// </remarks>
    public virtual ChatEncounter GetChatEncounter(NPC npc, Player player)
        => CanHandIn(out var set, out var handInIndex) && set.Task[handInIndex] is HandInTask { PromptKey: { } prompt }
            ? ChatEncounter.Play(prompt).Projected()
            : ChatEncounter.None;

    /// <summary>Job-level state to persist. Task progress saves itself; this is for the rest.</summary>
    public virtual void SaveCustomData(TagCompound tag) { }

    /// <summary>
    /// Restores what <see cref="SaveCustomData"/> wrote. The tag is empty for saves made before the
    /// job stored anything, so treat every key as optional.
    /// </summary>
    public virtual void LoadCustomData(TagCompound tag) { }
    #endregion

    #region Progress
    /// <summary>
    /// Feeds one event to the current stage's tasks. Returns true if anything took credit.
    /// </summary>
    /// <remarks>
    /// The job's own <see cref="OnEvent"/> runs first, then each unfinished task in the stage is asked
    /// whether the event is for it. Nothing here knows what any particular task wants -- that is
    /// exactly the routing ladder this replaced.
    /// </remarks>
    internal bool Handle(GameEvent evt)
    {
        if (State != JobState.Active)
            return false;

        OnEvent(evt);

        var set = CurrentSet;

        if (set is null)
            return false;

        var credited = false;

        // Against a snapshot of the stage index: crediting one task can finish the stage and move the
        // job on, and the loop must not carry on crediting the stage it has just left.
        var setIndex = CurrentTaskSet;

        for (var i = 0; i < set.Task.Count && CurrentTaskSet == setIndex; i++)
        {
            var task = set.Task[i];

            if (task.IsCompleted || !task.Matches(evt))
                continue;

            if (UpdateProgress(i, task.CreditFor(evt), evt))
                credited = true;
        }

        return credited;
    }

    /// <summary>
    /// Applies <paramref name="amount"/> to a task of the current stage, advancing the stage or
    /// finishing the job if that was the last of it.
    /// </summary>
    /// <param name="cause">The event responsible, if there was one. Passed on to <see cref="OnTaskAdvanced"/>.</param>
    /// <returns>
    /// True when progress actually moved -- not only when a stage turned over. The old contract
    /// returned false for every ordinary increment, which made the return value unusable.
    /// </returns>
    public bool UpdateProgress(int taskIndex, int amount, GameEvent cause = default)
    {
        if (State != JobState.Active || amount == 0)
            return false;

        var set = CurrentSet;

        if (set is null || taskIndex < 0 || taskIndex >= set.Task.Count)
            return false;

        var task = set.Task[taskIndex];

        // A finished task still accepts a revoke, which is what re-opens it.
        if (task.IsCompleted && amount > 0)
            return false;

        var before = task.Count;

        if (!task.Advance(amount) && task.Count == before)
            return false;

        if (task.IsCompleted && amount > 0 && Context.IsLocal)
        {
            SoundEngine.PlaySound(
                new SoundStyle($"{Assets.SFX.TaskComplete.KEY}") with { Volume = 0.75f },
                Context.Player.position);
        }

        // Before the stage can turn over, so taskIndex still addresses the stage this task is in and
        // the job hears about the task before it hears about the stage.
        OnTaskAdvanced(task, taskIndex, cause);

        if (set.IsCompleted)
            AdvanceSet();

        return true;
    }

    /// <summary>Applies <see cref="JobTask.Poll"/> for each task of the current set.</summary>
    private void PollTasks()
    {
        if (State != JobState.Active || CurrentSet is not { } set)
            return;

        // Same snapshot as Handle: a poll can finish the set and move the job on.
        var setIndex = CurrentTaskSet;

        for (var i = 0; i < set.Task.Count && CurrentTaskSet == setIndex; i++)
        {
            var amount = set.Task[i].Poll(Context);

            if (amount != 0)
                UpdateProgress(i, amount);
        }
    }

    /// <summary>
    /// True when the current set waits only on its <see cref="HandInTask"/> and the inventory still covers every
    /// <see cref="GatherItemTask"/> in it.
    /// </summary>
    public bool CanHandIn() => CanHandIn(out _, out _);

    private bool CanHandIn(out TaskList set, out int handInIndex)
    {
        set = CurrentSet;
        handInIndex = -1;

        if (State != JobState.Active || set is null)
            return false;

        handInIndex = set.Task.FindIndex(task => task is HandInTask);

        if (handInIndex < 0)
            return false;

        for (var i = 0; i < set.Task.Count; i++)
        {
            if (i == handInIndex)
                continue;

            var task = set.Task[i];

            if (!task.IsCompleted || task is GatherItemTask gather && gather.Held(Context.Player) < gather.RequiredCount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Takes the items of the current set's consuming <see cref="GatherItemTask"/>s and credits its
    /// <see cref="HandInTask"/>. False, taking nothing, when <see cref="CanHandIn"/> is false.
    /// </summary>
    public bool TryHandIn()
    {
        // Gather counts can trail the inventory by a tick.
        PollTasks();

        if (!CanHandIn(out var set, out var handInIndex))
            return false;

        foreach (var task in set.Task)
        {
            if (task is GatherItemTask { Consumes: true } gather)
                gather.Take(Context);
        }

        return UpdateProgress(handInIndex, 1);
    }

    /// <summary>
    /// Moves to the next stage, or finishes the job when there isn't one. The single place the stage
    /// index moves forward -- the sidebar used to do it too, from inside its draw pass, which meant
    /// <see cref="OnSetAdvanced"/> was silently skipped whenever the UI got there first.
    /// </summary>
    private void AdvanceSet()
    {
        if (CurrentTaskSet < TaskList.Count - 1)
        {
            var previous = CurrentTaskSet;

            CurrentTaskSet++;
            OnSetAdvanced(previous, CurrentTaskSet);

            return;
        }

        Complete();
    }

    /// <summary>Finishes the job and pays it out, if every stage really is done.</summary>
    public void Complete()
    {
        if (State != JobState.Active || !TaskList.All(set => set.IsCompleted))
            return;

        State = JobState.Completed;
        OnJobComplete();

        // Reached from UpdateProgress without passing through JobPlayer, so a job gated on this one
        // finishing has to be told from here.
        Context?.Jobs.InvalidateUnlocks();
    }

    /// <summary>
    /// Returns the job to its unstarted state, and offers it again. Always call <c>base.Reset()</c>
    /// when overriding.
    /// </summary>
    /// <remarks>
    /// Re-offering is deliberate: the only caller is the <c>resetj</c> debug command, and a reset job
    /// the player cannot then take is of no use to anyone testing one.
    /// </remarks>
    public virtual void Reset()
    {
        State = JobState.Available;
        CurrentTaskSet = 0;
        pendingSpeech = null;
        nextSpeechTick = 0;

        foreach (var set in TaskList)
            set.Reset();
    }
    #endregion

    #region Speech
    /// <summary>
    /// Queues a line from the provider, unless they have just said one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Queued rather than spoken outright, and drained from <see cref="Update"/>. Speaking
    ///         from inside event handling put dialogue on whatever thread and whatever frame the event
    ///         happened to arrive on, including during world load and on the server.
    ///     </para>
    ///     <para>
    ///         Both existing jobs had grown their own private copy of this, with a private cooldown
    ///         field and subtly different semantics.
    ///     </para>
    ///     <para>
    ///         The comparison is against a deadline, in <see langword="long"/>, rather than
    ///         <c>now - lastSpoke &lt; cooldown</c>. That subtraction overflows if the "never spoke"
    ///         sentinel is far enough in the past, wrapping to a negative result that reads as "spoke
    ///         a moment ago" -- which silently muted every job's dialogue for the whole session.
    ///     </para>
    /// </remarks>
    protected void Speak(string dialogueKey)
    {
        if (string.IsNullOrEmpty(dialogueKey))
            return;

        long now = Main.GameUpdateCount;

        if (now < nextSpeechTick)
            return;

        nextSpeechTick = now + SpeechCooldown;
        pendingSpeech = dialogueKey;
    }

    /// <summary>Per-tick upkeep. Override for polling, and call <c>base.Update()</c>.</summary>
    public virtual void Update()
    {
        PollTasks();

        if (pendingSpeech is null)
            return;

        // Only worth saying if the provider is alive to say it.
        if (Context.IsLocal && NPC.AnyNPCs(ProviderNPC))
            Context.Play(pendingSpeech);

        pendingSpeech = null;
    }
    #endregion

    #region Task visibility
    /// <summary>Hides or shows a task outright.</summary>
    public void SetTaskVisibility(int setIndex, int taskIndex, bool isVisible)
    {
        if (TryGetTask(setIndex, taskIndex, out var task))
            task.IsVisible = isVisible;
    }

    /// <summary>Makes a task's visibility depend on the job's state at read time.</summary>
    public void SetTaskVisibilityCondition(int setIndex, int taskIndex, JobTask.VisibilityCondition condition)
    {
        if (TryGetTask(setIndex, taskIndex, out var task))
            task.VisibilityCheck = condition;
    }

    /// <summary>Shows a task only once another task in the same set is done.</summary>
    public void ShowTaskAfterCompletion(int setIndex, int taskToShow, int dependencyTask)
    {
        SetTaskVisibilityCondition(setIndex, taskToShow,
            job => job.TryGetTask(setIndex, dependencyTask, out var dependency) && dependency.IsCompleted);
    }

    private bool TryGetTask(int setIndex, int taskIndex, out JobTask task)
    {
        task = null;

        if (setIndex < 0 || setIndex >= TaskList.Count)
            return false;

        var set = TaskList[setIndex];

        if (taskIndex < 0 || taskIndex >= set.Task.Count)
            return false;

        task = set.Task[taskIndex];
        return true;
    }
    #endregion
}
