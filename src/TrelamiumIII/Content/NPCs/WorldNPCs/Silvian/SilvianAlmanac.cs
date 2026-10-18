using Terraria.DataStructures;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

/// <summary>
///     John Substrate's almanac passing from his nephew to the player, which is the only way to get it: the book has no
///     recipe. Silvian hands it over once his botany lessons are done.
/// </summary>
/// <remarks>
///     The book changes hands when the handoff conversation plays through. Dismissed early, it plays again the next time
///     the player talks to him (<see cref="Silvian.GetEncounter" />), so a stray right-click can't lose it.
/// </remarks>
public static class SilvianAlmanac
{
    /// <summary>
    ///     The job whose completion ends with the handoff: his botany lessons. That job's <see cref="Job.OnJobComplete" />
    ///     calls <see cref="OfferIfDue" />.
    /// </summary>
    public const int HandoffJob = JobID.BotanyBasics;

    public const string HandoffKey = "Silvian.Almanac_Handoff";

    /// <summary>His last job is done and the book is still his.</summary>
    public static bool IsDue(Player player)
    {
        var silvian = player.GetModPlayer<SilvianPlayer>();

        return !silvian.AlmanacInherited && silvian.JobStateOf(HandoffJob) == JobState.Completed;
    }

    /// <summary>Queues the handoff behind whatever the job is already saying, if it is due.</summary>
    public static void OfferIfDue(JobContext context)
    {
        if (IsDue(context.Player))
            context.Play(HandoffKey);
    }

    /// <summary>Gives <paramref name="player" /> the book, once. Run when the handoff conversation completes.</summary>
    public static void Give(Player player)
    {
        var silvian = player.GetModPlayer<SilvianPlayer>();

        if (silvian.AlmanacInherited)
            return;

        silvian.AlmanacInherited = true;
        RewardBag.GiveOrDrop(player, new Item(ModContent.ItemType<AlchemistsAlmanacItem>()), new EntitySource_Misc("SilvianAlmanac"));
    }
}
