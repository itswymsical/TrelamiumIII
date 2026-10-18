using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Argie;

/// <summary>
///     Where one player stands with Argie: whether they have met her, and whether Silvian's letter reached her. Per player,
///     like the jobs the meeting unlocks.
/// </summary>
public sealed class ArgiePlayer : ModPlayer
{
    private bool met;

    /// <summary>Starting either meeting conversation counts. A player with her first job unlocked has met her too.</summary>
    public bool Met
    {
        get => met || JobStateOf(JobID.MycologyHunt) != JobState.Locked;
        set => met = value;
    }

    public bool LetterDelivered { get; set; }

    public JobState JobStateOf(int jobId) =>
        Player.GetModPlayer<JobPlayer>().GetAllJobs().FirstOrDefault(job => job.ID == jobId)?.State ?? JobState.Locked;

    /// <summary>Forgets everything above, for the dev command.</summary>
    public void Reset()
    {
        met = false;
        LetterDelivered = false;
    }

    public override void SaveData(TagCompound tag)
    {
        tag["met"] = met;
        tag["letter"] = LetterDelivered;
    }

    public override void LoadData(TagCompound tag)
    {
        met = tag.GetBool("met");
        LetterDelivered = tag.GetBool("letter");
    }
}
