using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

/// <summary>
///     Where one player stands with Silvian: whether they have met him, whether his uncle's almanac is theirs yet, and how
///     far he has gone hinting at each brew they have asked him about.
/// </summary>
/// <remarks>
///     Kept per player rather than per world, like the jobs the meeting unlocks. A player who spoke to him before the
///     encounter existed has his first job past <see cref="JobState.Locked" />, and counts as having met him.
/// </remarks>
public sealed class SilvianPlayer : ModPlayer
{
    private bool met;

    /// <summary>Hint tier reached per brew, keyed by <see cref="ItemKey" /> so modded types survive a reload.</summary>
    private readonly Dictionary<string, int> hintTiers = [];

    public bool Met
    {
        get => met || JobStateOf(JobID.HerbalForager) != JobState.Locked;
        set => met = value;
    }

    public bool AlmanacInherited { get; set; }

    /// <summary>The general lesson the Help button gives next. Not saved: each session starts from the top.</summary>
    public int NextLesson { get; set; }

    public JobState JobStateOf(int jobId) =>
        Player.GetModPlayer<JobPlayer>().GetAllJobs().FirstOrDefault(job => job.ID == jobId)?.State ?? JobState.Locked;

    /// <summary>How many hints Silvian has given for <paramref name="itemType" />; 0 before the first.</summary>
    public int HintTier(int itemType) => hintTiers.TryGetValue(ItemKey(itemType), out int tier) ? tier : 0;

    public void SetHintTier(int itemType, int tier) => hintTiers[ItemKey(itemType)] = tier;

    public void ClearHints() => hintTiers.Clear();

    /// <summary>Forgets everything above, for the dev command.</summary>
    public void Reset()
    {
        met = false;
        AlmanacInherited = false;
        NextLesson = 0;
        hintTiers.Clear();
    }

    private static string ItemKey(int itemType) =>
        itemType < ItemID.Count ? ItemID.Search.GetName(itemType) : ItemLoader.GetItem(itemType)?.FullName ?? itemType.ToString();

    public override void SaveData(TagCompound tag)
    {
        tag["met"] = met;
        tag["almanac"] = AlmanacInherited;

        if (hintTiers.Count == 0)
            return;

        tag["hintItems"] = hintTiers.Keys.ToList();
        tag["hintTiers"] = hintTiers.Values.ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        met = tag.GetBool("met");
        AlmanacInherited = tag.GetBool("almanac");
        hintTiers.Clear();

        IList<string> items = tag.GetList<string>("hintItems");
        IList<int> tiers = tag.GetList<int>("hintTiers");

        for (int i = 0; i < Math.Min(items.Count, tiers.Count); i++)
            hintTiers[items[i]] = tiers[i];
    }
}
