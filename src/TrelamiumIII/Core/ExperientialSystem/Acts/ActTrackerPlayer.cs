using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.UI;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// One act's state for one character.
///
/// <para>
/// <see cref="TiersPaid"/> is a count, not a set of per-tier flags. Because thresholds ascend, "how many
/// tiers have paid" says everything the old string-keyed <c>CompletedTiers</c> set said, in one integer -
/// and it is self-correcting under retuning. Lower a threshold and the next event pays the tiers that are
/// now reachable; raise one and nothing double-pays. That is what removes the hand-rolled desync repair
/// pass and the <c>isLoadingData</c> flag the previous tracker needed on every load.
/// </para>
/// </summary>
public sealed class ActProgress
{
    /// <summary>Completed actions for this act - pots smashed, trees felled - not raw tile events.</summary>
    public int Count;

    /// <summary>
    /// Raw events banked toward the next whole action, for acts whose <see cref="Act.EventsPerAction"/>
    /// is above 1. Persisted so quitting halfway through a tree does not throw the part away.
    /// </summary>
    public int Partial;

    /// <summary>How many tiers have already paid out. Never decreases.</summary>
    public int TiersPaid;

    /// <summary>Distinct group members engaged with. Left null for acts that are not groups.</summary>
    public HashSet<int>? Members;
}

/// <summary>
/// Tracks act progress for the local character and pays out tiers as they are crossed.
///
/// <para>
/// Acts are personal progression held in the player's save file, so this only ever runs for the local
/// player. Nothing here is computed server-side or synced.
/// </para>
/// </summary>
public sealed class ActTrackerPlayer : ModPlayer
{
    /// <summary>Band fractions toward the next tier that are worth telling the player about.</summary>
    private static readonly float[] Milestones = [0.10f, 0.25f, 0.33f, 0.50f, 0.66f, 0.75f];

    private readonly Dictionary<string, ActProgress> _progress = [];

    /// <summary>Reused across broadcasts so matching a tile break allocates nothing.</summary>
    private readonly List<Act> _matches = [];


    public override void Initialize() => _progress.Clear();

    #region Queries

    public ActProgress? Find(string actId) => _progress.GetValueOrDefault(actId);

    public int ProgressOf(string actId) => Find(actId)?.Count ?? 0;

    public int TiersPaidFor(string actId) => Find(actId)?.TiersPaid ?? 0;

    /// <summary>Distinct group members engaged with, over the act's total membership. 1 for non-group acts.</summary>
    public float CoverageOf(Act act)
    {
        if (act.Members.Length <= 1)
            return 1f;

        int seen = Find(act.ID)?.Members?.Count ?? 0;

        return Math.Clamp(seen / (float)act.Members.Length, 0f, 1f);
    }

    /// <summary>True once every tier has paid and the act can never pay again.</summary>
    public bool IsExhausted(Act act) => TiersPaidFor(act.ID) >= act.TierCount;

    #endregion

    #region Tracking

    /// <summary>Routes a broadcast into every act it advances.</summary>
    public void OnGameEvent(in GameEvent evt)
    {
        if (Player.whoAmI != Main.myPlayer)
            return;

        if (!ShouldCredit(evt))
            return;

        ActRegistry.CollectMatches(evt, _matches);

        foreach (Act act in _matches)
            Advance(act, evt);
    }

    /// <summary>
    /// Refuses credit for breaking a tile the player placed themselves, which would otherwise make every
    /// tile-break act farmable by the tightest possible loop: place a copper ore, break it, repeat.
    ///
    /// <para>
    /// <see cref="PlacedTileTracker"/> owns the bookkeeping. It hooks <c>WorldGen.PlaceTile</c> and block
    /// swap rather than <c>GlobalTile.PlaceInWorld</c>, so nothing here has to record placements - and the
    /// swap path, which places ore without ever touching PlaceInWorld, is covered too.
    /// </para>
    ///
    /// <para>
    /// Reads without forgetting. The Microlith consumes the same mark from <c>GlobalTile.Drop</c>, which
    /// resolves after <c>KillTile</c> has already broadcast this event, so clearing it here would deny
    /// that bonus its own guard.
    /// </para>
    /// </summary>
    private static bool ShouldCredit(in GameEvent evt)
    {
        if (evt.Type != GameEventType.TileBreak)
            return true;

        if (evt.TileX is not int x || evt.TileY is not int y)
            return true;

        return !PlacedTileTracker.WasPlacedByPlayer(x, y);
    }

    private void Advance(Act act, in GameEvent evt)
    {
        ActProgress record = Record(act.ID);

        int before = record.Count;

        // Coverage is only meaningful for groups, so single-member acts allocate no set at all. Recorded
        // before the fold below can bail out: meeting a member counts even on a tile break that only
        // banks a fraction of an action.
        if (act.Members.Length > 1 && act.MemberFor(evt) is int member and >= 0)
            (record.Members ??= []).Add(member);

        // Fold raw events into whole actions, keeping the remainder. A 2x2 pot raises four tile breaks
        // and a tree raises a dozen, so counting events directly would count the wrong thing entirely.
        int banked = record.Partial + Math.Max(evt.Amount, 1);
        int perAction = Math.Max(act.EventsPerAction, 1);

        record.Count += banked / perAction;
        record.Partial = banked % perAction;

        if (EventLog.Enabled)
        {
            // Reads "+1 raw -> 3/4 banked" while an object is mid-break, then "-> action 12" when one
            // lands. Smash a single pot with tracing on and the divisor is either confirmed or disproved.
            EventLog.Trace(record.Count == before
                ? $"[Act] {act.ID} +{evt.Amount} raw -> {record.Partial}/{perAction} banked, no action yet"
                : $"[Act] {act.ID} +{evt.Amount} raw -> action {record.Count} (+{record.Count - before}), {record.Partial}/{perAction} banked");
        }

        // Mid-object: nothing the player would call progress has happened yet.
        if (record.Count == before)
            return;

        int reached = act.TiersReached(record.Count);

        if (record.TiersPaid >= reached)
        {
            ReportMilestone(act, record, before, reached);
            return;
        }

        // One event can cross several rungs at once - a big craft, or an act that was already past its
        // next threshold after a retune. Pay every one of them, but announce them as a single completion
        // for the highest rung reached; a stack of popups from one swing reads as a bug.
        int firstTier = record.TiersPaid;
        int granted = 0;

        while (record.TiersPaid < reached)
        {
            granted += PayTier(act, record, record.TiersPaid);
            record.TiersPaid++;
        }

        int lastTier = reached - 1;

        ActTrackerManager.Instance?.Show(
            ExpMilestoneUI.TierComplete(
                act, lastTier, act.ThresholdOf(lastTier), record.Count,
                act.BandProgress(firstTier, before), granted),
            isComplete: true);
    }

    /// <summary>
    /// Pays one tier straight into the meter and reports what landed.
    ///
    /// <para>
    /// <see cref="Act.PayoutFraction"/> composes the four inputs the economy runs on: where the tier sits
    /// on the distribution curve, the act's own multiplier, the hardmode floor, and group coverage. The
    /// Threshold itself is the fifth, applied by <see cref="ExperientialPlayer.GrantFraction"/> when the
    /// fraction meets the meter it is a fraction of.
    /// </para>
    /// </summary>
    private int PayTier(Act act, ActProgress record, int tierIndex)
    {
        float fraction = act.PayoutFraction(tierIndex, record.Members?.Count ?? 0);

        return fraction > 0f
            ? Player.GetModPlayer<ExperientialPlayer>().GrantFraction(fraction, ExpSource.Act(act.ID))
            : 0;
    }

    private void ReportMilestone(Act act, ActProgress record, int before, int nextTier)
    {
        if (nextTier >= act.TierCount)
            return;

        float previous = act.BandProgress(nextTier, before);
        float current = act.BandProgress(nextTier, record.Count);

        foreach (float milestone in Milestones)
        {
            if (previous >= milestone || current < milestone)
                continue;

            ActTrackerManager.Instance?.Show(
                ExpMilestoneUI.Milestone(act, record.Count, act.ThresholdOf(nextTier), previous, current));

            return;
        }
    }

    private ActProgress Record(string actId)
    {
        if (!_progress.TryGetValue(actId, out ActProgress? record))
            _progress[actId] = record = new ActProgress();

        return record;
    }

    #endregion

    #region Save data

    public override void SaveData(TagCompound tag)
    {
        tag["acts"] = _progress.Select(entry => new TagCompound
        {
            ["id"] = entry.Key,
            ["count"] = entry.Value.Count,
            ["partial"] = entry.Value.Partial,
            ["tiers"] = entry.Value.TiersPaid,
            ["members"] = entry.Value.Members?.ToList() ?? []
        }).ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        _progress.Clear();

        foreach (TagCompound entry in tag.GetList<TagCompound>("acts"))
        {
            string id = entry.GetString("id");
            if (string.IsNullOrEmpty(id))
                continue;

            var record = new ActProgress
            {
                Count = entry.GetInt("count"),
                Partial = entry.GetInt("partial"),
                TiersPaid = entry.GetInt("tiers")
            };

            var members = entry.GetList<int>("members");
            if (members.Count > 0)
                record.Members = [.. members];

            _progress[id] = record;
        }

        // No reconciliation pass. TiersPaid is authoritative on its own: retuning a threshold down lets
        // the next event pay what is newly reachable, and retuning one up simply pays nothing further.
    }

    #endregion
}
