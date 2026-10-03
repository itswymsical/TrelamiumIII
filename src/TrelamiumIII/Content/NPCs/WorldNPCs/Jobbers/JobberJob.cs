using SubworldLibrary;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

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
    { }

    /// <summary>The bound jobber's <see cref="Jobber.JobberId"/>; 0 while the slot is free.</summary>
    public int JobberId { get; private set; }

    public string JobberName { get; private set; } = string.Empty;

    public JobberRequest Request { get; private set; }

    public bool IsBound => JobberId != 0;

    /// <summary>True in the main world the bound jobber lives in, where its absence means it is gone.</summary>
    public bool InBoundWorld => IsBound && !SubworldSystem.AnyActive()
        && worldId == Main.ActiveWorldFileData.UniqueId.ToString();

    public int Held => Gather.Held(Context.Player);

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

    public override void OnJobStart() => Jobber.Find(JobberId)?.Claim();

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Jobber.Find(JobberId)?.CompleteRequest();
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

/// <summary>Item and count are set per request by <see cref="JobberJob.Bind"/>.</summary>
public sealed class JobberGatherTask() : GatherItemTask("Jobber.Tasks.Gather", ItemID.None) { }

/// <summary>Taken through the jobber's chat button (<see cref="Jobber.OnChatButtonClicked"/>), so no prompt dialogue.</summary>
public sealed class JobberHandInTask() : HandInTask("Jobber.Tasks.HandIn")
{
    public string JobberName { get; set; } = string.Empty;

    public override string Description =>
        TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Format(JobberName);
}
