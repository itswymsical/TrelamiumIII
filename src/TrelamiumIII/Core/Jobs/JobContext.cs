using Terraria.DataStructures;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// The world as one <see cref="Job"/> is allowed to touch it: its owning player, and the verbs it
/// needs to act on them.
/// </summary>
/// <remarks>
///     <para>
///         A job used to reach for <see cref="Main.LocalPlayer"/> whenever it wanted to do anything --
///         inside <see cref="Job.UpdateProgress"/>, no less, which is called while iterating a
///         specific player's jobs. That is correct exactly once (single player, local client) and
///         quietly wrong everywhere else. The context makes the owner explicit, so a job physically
///         cannot act on the wrong player.
///     </para>
///     <para>
///         It is also where "author a new event" lives. Granting, revoking, marking and speaking are
///         the things jobs actually do in response to progress, and every one of them was previously
///         either absent or hand-rolled per job.
///     </para>
/// </remarks>
public sealed class JobContext(Player player, JobPlayer jobs)
{
    public Player Player { get; } = player;

    public JobPlayer Jobs { get; } = jobs;

    /// <summary>
    /// True when this player's slots and screen belong to this client. Anything that draws, plays a
    /// sound, opens dialogue, or reads inventory directly has to check it.
    /// </summary>
    public bool IsLocal => !Main.dedServ && Player.whoAmI == Main.myPlayer;

    private IEntitySource Source => new EntitySource_Misc("Job");

    #region Items
    /// <summary>Hands over an item, preferring the inventory and dropping only the overflow.</summary>
    public void Grant(Item item) => RewardBag.GiveOrDrop(Player, item, Source);

    /// <summary>Hands over <paramref name="stack"/> of <paramref name="itemType"/>.</summary>
    public void Grant(int itemType, int stack = 1)
    {
        if (itemType > ItemID.None && stack > 0)
            Grant(new Item(itemType, stack));
    }

    /// <summary>
    /// Takes items back, and returns how many were actually there. A job that revokes fewer than it
    /// asked for should treat the shortfall as the player having spent them.
    /// </summary>
    public int Revoke(int itemType, int amount = 1) => JobUtils.TakePlayerItems(Player, itemType, amount);

    /// <summary>Takes back everything marked for <paramref name="jobId"/>. Returns the count taken.</summary>
    public int RevokeMarked(int jobId) => JobUtils.TakeMarkedJobItems(Player, jobId);

    /// <summary>
    /// Reserves an item for <paramref name="jobId"/>: it can no longer be used, worn, fired, sold,
    /// trashed, or stored until the job releases it.
    /// </summary>
    public void Mark(Item item, int jobId) => MarkedItem.MarkFor(item, jobId);
    #endregion

    #region Presentation
    /// <summary>Opens a dialogue line, if this client is the one that should see it.</summary>
    public bool Play(string dialogueKey, bool zoomIn = false, bool letterbox = false,
        DialogueStyle style = DialogueStyle.Panel)
    {
        if (!IsLocal || string.IsNullOrEmpty(dialogueKey))
            return false;

        return DialogueManager.Instance.StartDialogue(dialogueKey, zoomIn, letterbox, style: style);
    }
    #endregion
}
