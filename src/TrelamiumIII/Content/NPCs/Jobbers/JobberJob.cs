using SubworldLibrary;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.Jobbers;

/// <summary>
/// A gathering job bound to one <see cref="Jobber"/> instance rather than the NPC type. Each player has one job per
/// entry in <see cref="Slots"/>; <see cref="JobberPlayer"/> binds and frees them.
/// </summary>
public abstract class JobberJob : Job
{
    public static readonly int[] Slots = [JobID.JobberSlot1, JobID.JobberSlot2];

    private const int GatherTaskIndex = 0;
    private const int HandInTaskIndex = 1;

    /// <summary><see cref="Main.ActiveWorldFileData"/> UniqueId of the world the bound jobber lives in.</summary>
    private string worldId = string.Empty;

    protected JobberJob(int id) : base(
        id, "Jobber.Name", "Jobber.Description",
        taskList: [[new JobberGatherTask(), new JobberHandInTask()]],
        rewards: [],
        providerNPC: ModContent.NPCType<Jobber>())
        => ShowTaskAfterCompletion(0, HandInTaskIndex, GatherTaskIndex);

    /// <summary>The bound jobber's <see cref="Jobber.JobberId"/>; 0 while the slot is free.</summary>
    public int JobberId { get; private set; }

    public string JobberName { get; private set; } = string.Empty;

    public JobberRequest Request { get; private set; }

    public bool IsBound => JobberId != 0;

    /// <summary>True in the main world the bound jobber lives in, where its absence means it is gone.</summary>
    public bool InBoundWorld => IsBound && !SubworldSystem.AnyActive()
        && worldId == Main.ActiveWorldFileData.UniqueId.ToString();

    /// <summary>Requested items in the owner's inventory, capped at the request. Equipped items don't count.</summary>
    public int Held => Math.Min(Context.Player.CountItem(Request.ItemType, Request.Count), Request.Count);

    public int Remaining => Request.Count - Held;

    private JobberGatherTask Gather => (JobberGatherTask)TaskList[0].Task[GatherTaskIndex];

    private JobberHandInTask HandIn => (JobberHandInTask)TaskList[0].Task[HandInTaskIndex];

    public override string Description => IsBound
        ? Text($"Requests.{Request.Kind}").Format(Request.Count, Request.ItemName)
        : base.Description;

    public override string ProviderName => IsBound ? JobberName : base.ProviderName;

    public override bool IsProvidedBy(NPC npc) => IsBound && npc.ModNPC is Jobber jobber && jobber.JobberId == JobberId;

    internal void Bind(Jobber jobber, JobberRequest request)
    {
        JobberId = jobber.JobberId;
        JobberName = jobber.NPC.GivenOrTypeName;
        worldId = Main.ActiveWorldFileData.UniqueId.ToString();

        Apply(request);
    }

    private void Apply(JobberRequest request)
    {
        Request = request;

        Gather.ItemType = request.ItemType;
        Gather.RequiredCount = request.Count;
        HandIn.JobberName = JobberName;

        Rewards.Clear();
        Rewards.AddRange(request.Rewards());
    }

    /// <summary>Unbinds the slot and returns it to <see cref="JobState.Locked"/> with progress cleared.</summary>
    internal void Release()
    {
        Reset();
        State = JobState.Locked;

        JobberId = 0;
        JobberName = string.Empty;
        worldId = string.Empty;
        Request = default;
        Rewards.Clear();
    }

    /// <summary>Drops an active job whose jobber no longer exists.</summary>
    internal void Abandon()
    {
        if (Context.IsLocal)
            Main.NewText(Text("Abandoned").Format(JobberName), new Color(225, 160, 90));

        Release();
    }

    /// <summary>Takes the requested items and completes the job. False if the player is short.</summary>
    public bool TryHandIn()
    {
        if (State != JobState.Active || Remaining > 0)
            return false;

        int count = Request.Count;

        Context.Revoke(Request.ItemType, count);
        UpdateProgress(GatherTaskIndex, count - Gather.Count);
        UpdateProgress(HandInTaskIndex, 1);

        return State == JobState.Completed;
    }

    public override void OnJobStart() => Jobber.Find(JobberId)?.Claim();

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Jobber.Find(JobberId)?.CompleteRequest();
    }

    /// <summary>Mirrors the gather task's count to the inventory, so dropping items re-opens it.</summary>
    public override void Update()
    {
        base.Update();

        if (State != JobState.Active || !Context.IsLocal || !IsBound)
            return;

        int held = Held;

        if (held != Gather.Count)
            UpdateProgress(GatherTaskIndex, held - Gather.Count);
    }

    public override void SaveCustomData(TagCompound tag)
    {
        if (!IsBound)
            return;

        tag["jobber"] = JobberId;
        tag["jobberName"] = JobberName;
        tag["world"] = worldId;
        Request.Save(tag);
    }

    public override void LoadCustomData(TagCompound tag)
    {
        if (!tag.TryGet("jobber", out int jobberId) || JobberRequest.Load(tag) is not { } request)
            return;

        JobberId = jobberId;
        JobberName = tag.GetString("jobberName");
        worldId = tag.GetString("world");

        Apply(request);
    }

    /// <summary>The player's active job from <paramref name="jobber"/>, or null.</summary>
    public static JobberJob? ActiveFor(Player player, Jobber jobber)
    {
        foreach (Job job in player.GetModPlayer<JobPlayer>().ActiveJobs())
        {
            if (job is JobberJob jobberJob && jobberJob.IsProvidedBy(jobber.NPC))
                return jobberJob;
        }

        return null;
    }

    private static LocalizedText Text(string key) => TrelamiumIII.Instance.GetLocalization($"Jobs.Jobber.{key}");
}

public sealed class JobberSlot1Job() : JobberJob(JobID.JobberSlot1) { }

public sealed class JobberSlot2Job() : JobberJob(JobID.JobberSlot2) { }

/// <summary>Hold the requested item. Count is set by <see cref="JobberJob.Update"/> from the inventory, not by events.</summary>
public sealed class JobberGatherTask() : JobTask("Jobber.Tasks.Gather")
{
    public int ItemType { get; set; }

    public override string Description =>
        TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Format(Lang.GetItemNameValue(ItemType));

    public override bool Matches(GameEvent evt) => false;
}

/// <summary>Hand the items over. Credited only by <see cref="JobberJob.TryHandIn"/>.</summary>
public sealed class JobberHandInTask() : JobTask("Jobber.Tasks.HandIn")
{
    public string JobberName { get; set; } = string.Empty;

    public override string Description =>
        TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Format(JobberName);

    public override bool Matches(GameEvent evt) => false;
}
