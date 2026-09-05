using System.Collections.Generic;
using Terraria.Localization;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>Broad families an act belongs to. Drives UI grouping, not any mechanic.</summary>
public enum ActCategory
{
    Obstruction,
    Combat,
    Exploration,
    Crafting,
    Fishing,
    Building
}

/// <summary>
/// A tracked, repeatable thing a player does - break pots, mine copper, smelt bars, slay slimes.
///
/// <para>
/// An act is a ladder of <see cref="Thresholds"/>. Crossing one pays a slice of the meter and is then
/// spent forever; after <see cref="ExperientialConstants.ActPayoutTiers"/> payouts the act is exhausted
/// for the rest of the character's life. That permanence is what makes acts the authored, finite layer
/// of the economy rather than a grind.
/// </para>
///
/// <para>
/// Acts overlap on purpose. Breaking a copper ore feeds both the copper act and the pre-hardmode metals
/// group, so a player who mines broadly earns from both ladders at once. See <see cref="Members"/> for
/// how the group half stays about breadth rather than volume.
/// </para>
/// </summary>
public abstract class Act
{
    /// <summary>Prefix every act id carries, stripped when deriving the localization key.</summary>
    private const string IdPrefix = "act.";

    private LocalizedText? _name;
    private LocalizedText? _description;

    /// <summary>Stable identifier. Persisted in save data and hashed into an <see cref="ExpSource"/>, so renaming one resets progress.</summary>
    public abstract string ID { get; }

    /// <summary>
    /// Key segment under <c>Mods.TrelamiumIII.ActLang</c>. Derived from <see cref="ID"/> with its
    /// <c>act.</c> prefix stripped, so <c>act.ore.copper</c> localizes at <c>ActLang.ore.copper</c>.
    ///
    /// <para>
    /// Deriving it rather than hand-writing it per act is the point: an id and a localization key that
    /// are typed separately drift, and a drifted key fails by rendering itself into the UI rather than
    /// by throwing. Override only to keep an existing key alive after renaming an id.
    /// </para>
    /// </summary>
    protected virtual string LocalizationKey
        => ID.StartsWith(IdPrefix, StringComparison.Ordinal) ? ID[IdPrefix.Length..] : ID;

    /// <summary>Display name, from the language files. Cached as a <see cref="LocalizedText"/> so it still follows a language change.</summary>
    public virtual string Name => (_name ??= Localize(nameof(Name))).Value;

    public virtual string Description => (_description ??= Localize(nameof(Description))).Value;

    /// <summary>
    /// Goes through <see cref="Mod.GetLocalization"/> rather than <c>Language.GetTextValue</c> so the key
    /// is registered: an act nobody has written strings for yet gets stubbed into the language file
    /// automatically instead of silently displaying its own key forever.
    /// </summary>
    private LocalizedText Localize(string field)
        => TrelamiumIII.Instance.GetLocalization($"ActLang.{LocalizationKey}.{field}", () => ID);

    public abstract ActCategory Category { get; }

    /// <summary>
    /// Progress counts at which each tier pays out, counted in <i>actions</i> rather than raw events -
    /// see <see cref="EventsPerAction"/>. Must be strictly ascending; <see cref="ActRegistry"/> checks
    /// this at load and sorts anything that is not.
    ///
    /// <para>
    /// Either share one of the <see cref="ActCurves"/> ladders or write the array inline. Read through
    /// <see cref="Ladder"/>, never directly - an inline collection expression allocates a fresh array on
    /// every access, and this is read on every matching event.
    /// </para>
    /// </summary>
    public abstract int[] Thresholds { get; }

    /// <summary>
    /// The validated ladder, snapshotted on first read. Everything internal goes through this so an act
    /// is free to build its <see cref="Thresholds"/> inline without allocating per event, and so the
    /// registry sorting a malformed ladder actually sticks.
    /// </summary>
    public int[] Ladder => _ladder ??= Thresholds;

    /// <summary>
    /// Raw <see cref="GameEvent"/> amounts that make up one action the player would recognise. 1 for
    /// anything broken one-to-one.
    ///
    /// <para>
    /// This exists because tile events do not map onto actions. A pot is a 2x2 multitile, so smashing one
    /// raises four tile breaks; a tree is killed a trunk tile at a time, so felling one raises a dozen or
    /// more. Left at 1, a "break 2000 pots" ladder is really 500 pots and a tree ladder is off by an order
    /// of magnitude - which is exactly why the old tree challenge needed thresholds in the hundreds.
    /// </para>
    ///
    /// <para>
    /// The tracker carries the remainder between events, so a divisor never loses a fraction of progress.
    /// </para>
    /// </summary>
    public virtual int EventsPerAction => 1;

    /// <summary>Whether this event moves this act's progress at all.</summary>
    public abstract bool Matches(in GameEvent evt);

    /// <summary>
    /// The one event type this act could ever match, when it has one. Lets the registry skip acts that
    /// cannot possibly care about a broadcast, which matters because tile breaks fire constantly.
    /// Null means "test me against everything".
    /// </summary>
    public virtual GameEventType? ListensFor => null;

    /// <summary>
    /// The distinct things this act is made of - the eight pre-hardmode ore tiles, the bar types,
    /// the slime species. Empty, or a single entry, for an act that is not a group.
    ///
    /// <para>
    /// This is what makes a group act reward completionism rather than volume. Without it, mining five
    /// thousand copper would max out "pre-hardmode metals" without the player ever having touched tin.
    /// </para>
    /// </summary>
    public virtual int[] Members => [];

    /// <summary>Which member this event belongs to, or -1 if the act does not track members.</summary>
    public virtual int MemberFor(in GameEvent evt) => -1;

    #region Tier maths

    private int[]? _ladder;

    public int TierCount => Ladder.Length;

    /// <summary>
    /// How many tiers can ever pay. Authoring more thresholds than
    /// <see cref="ExperientialConstants.ActPayoutTiers"/> gives more rungs to climb but no more Exp.
    /// </summary>
    public int PaidTierCount => Math.Min(TierCount, ExperientialConstants.ActPayoutTiers);

    /// <summary>
    /// Number of tiers <paramref name="progress"/> has passed, so tier indices <c>0 .. result-1</c> are done.
    /// A count rather than a set of flags: thresholds ascend, so one integer says everything, which is
    /// what lets the tracker's save data hold no per-tier keys and need no repair pass on load.
    /// </summary>
    public int TiersReached(int progress)
    {
        int reached = 0;

        while (reached < Ladder.Length && progress >= Ladder[reached])
            reached++;

        return reached;
    }

    /// <summary>Progress needed for <paramref name="tierIndex"/>, or <see cref="int.MaxValue"/> past the last tier.</summary>
    public int ThresholdOf(int tierIndex)
        => tierIndex >= 0 && tierIndex < Ladder.Length ? Ladder[tierIndex] : int.MaxValue;

    /// <summary>Progress the band leading up to <paramref name="tierIndex"/> starts from.</summary>
    public int BandStart(int tierIndex) => tierIndex <= 0 ? 0 : ThresholdOf(tierIndex - 1);

    /// <summary>How far <paramref name="progress"/> is through the band ending at <paramref name="tierIndex"/>, as 0-1.</summary>
    public float BandProgress(int tierIndex, int progress)
    {
        if (tierIndex < 0 || tierIndex >= Ladder.Length)
            return 1f;

        int start = BandStart(tierIndex);
        int span = ThresholdOf(tierIndex) - start;

        if (span <= 0)
            return 1f;

        return Math.Clamp((progress - start) / (float)span, 0f, 1f);
    }

    #endregion

    #region Payout

    /// <summary>True when this act spans a family of content rather than a single piece of it.</summary>
    public bool IsGroup => Members.Length > 1;

    /// <summary>
    /// Flat multiplier on everything this act pays. Universal acts take
    /// <see cref="ExperientialConstants.GroupActMultiplier"/> by default, because covering a whole family
    /// of content is a broader commitment than working one member of it. Override for an act that should
    /// weigh more or less than its shape suggests.
    /// </summary>
    public virtual float ExpMultiplier => IsGroup ? ExperientialConstants.GroupActMultiplier : 1f;

    /// <summary>
    /// True for acts whose content only exists once the wall has fallen. Such an act opens at whatever
    /// Threshold the player happens to be at by then, which is a long way up the curve, so it is floored
    /// rather than started from the bottom rung. See <see cref="ExperientialConstants.HardmodeFloorTierIndex"/>.
    /// </summary>
    public virtual bool IsHardmodeContent => false;

    /// <summary>
    /// What crossing <paramref name="tierIndex"/> pays right now, as a fraction of the meter: the base
    /// curve, floored for hardmode content, scaled by this act's multiplier and by how much of its group
    /// the player has actually covered. The single place all four inputs meet.
    /// </summary>
    public float PayoutFraction(int tierIndex, int distinctMembersSeen)
        => ExperientialConstants.ActPayoutFor(EffectiveTierIndex(tierIndex))
         * ExpMultiplier
         * CoverageMultiplier(distinctMembersSeen);

    private int EffectiveTierIndex(int tierIndex)
    {
        // A tier outside the paying range pays nothing, and the floor must never drag one back into it.
        if (tierIndex < 0 || tierIndex >= ExperientialConstants.ActPayoutTiers)
            return tierIndex;

        return IsHardmodeContent && Main.hardMode
            ? Math.Max(tierIndex, ExperientialConstants.HardmodeFloorTierIndex)
            : tierIndex;
    }

    #endregion

    #region Coverage

    /// <summary>
    /// Scales a group act's payout by how much of the group the player has actually engaged with.
    /// A single-member or memberless act always returns 1, so individual acts need no special case.
    ///
    /// <para>
    /// Set <see cref="ExperientialConstants.GroupCoverageFloor"/> to 1 to switch this off entirely and
    /// make group acts pay purely on volume.
    /// </para>
    /// </summary>
    public float CoverageMultiplier(int distinctMembersSeen)
    {
        int total = Members.Length;
        if (total <= 1)
            return 1f;

        float coverage = Math.Clamp(distinctMembersSeen / (float)total, 0f, 1f);

        return ExperientialConstants.GroupCoverageFloor
             + (1f - ExperientialConstants.GroupCoverageFloor) * coverage;
    }

    #endregion
}

/// <summary>
/// An act driven by one game event type and a set of content ids. Covers nearly every act worth writing,
/// and gives group coverage for free because the id set is also the member list.
/// </summary>
public abstract class SetAct : Act
{
    private HashSet<int>? _lookup;

    /// <summary>The event this act listens for.</summary>
    protected abstract GameEventType Listens { get; }

    /// <summary>Content ids that count, and the members coverage is measured against.</summary>
    protected abstract int[] Ids { get; }

    /// <summary>Pulls the relevant content id out of an event of type <see cref="Listens"/>.</summary>
    protected abstract int IdOf(in GameEvent evt);

    /// <summary>Built once on first use - acts are matched against every broadcast, so this is a hot path.</summary>
    private HashSet<int> Lookup => _lookup ??= [.. Ids];

    public sealed override GameEventType? ListensFor => Listens;

    public sealed override int[] Members => Ids;

    public sealed override int MemberFor(in GameEvent evt) => IdOf(evt);

    public override bool Matches(in GameEvent evt) => evt.Type == Listens && Lookup.Contains(IdOf(evt));
}

/// <summary>
/// An act matched by arbitrary logic rather than an id set - "any tile a shovel digs", "any tree trunk".
/// Carries no group coverage, because there is no bounded member list to measure breadth against.
/// </summary>
public abstract class PredicateAct : Act
{
    protected abstract GameEventType Listens { get; }

    protected abstract bool Test(in GameEvent evt);

    public sealed override GameEventType? ListensFor => Listens;

    public sealed override bool Matches(in GameEvent evt) => evt.Type == Listens && Test(evt);
}

/// <summary>A <see cref="PredicateAct"/> driven by which tile was broken.</summary>
public abstract class TileBreakPredicateAct : PredicateAct
{
    protected sealed override GameEventType Listens => GameEventType.TileBreak;

    protected abstract bool TestTile(int tileType);

    protected sealed override bool Test(in GameEvent evt) => evt.TileType is int type && type >= 0 && TestTile(type);
}

public abstract class TileBreakAct : SetAct
{
    protected sealed override GameEventType Listens => GameEventType.TileBreak;
    protected sealed override int IdOf(in GameEvent evt) => evt.TileType ?? -1;
}

public abstract class TilePlaceAct : SetAct
{
    protected sealed override GameEventType Listens => GameEventType.TilePlace;
    protected sealed override int IdOf(in GameEvent evt) => evt.TileType ?? -1;
}

public abstract class NpcKillAct : SetAct
{
    protected sealed override GameEventType Listens => GameEventType.NPCKill;
    protected sealed override int IdOf(in GameEvent evt) => evt.NPCType ?? -1;
}

public abstract class ItemCraftAct : SetAct
{
    protected sealed override GameEventType Listens => GameEventType.ItemCraft;
    protected sealed override int IdOf(in GameEvent evt) => evt.ItemType ?? -1;
}

/// <summary>
/// Localized display names for <see cref="ActCategory"/>. The category is shown next to the act name in
/// the milestone tracker, so the bare enum name would have been the one untranslated string on that panel.
/// </summary>
public static class ActCategoryExtensions
{
    private static readonly LocalizedText?[] Names = new LocalizedText?[Enum.GetValues<ActCategory>().Length];

    public static string DisplayName(this ActCategory category)
    {
        int index = (int)category;

        if (index < 0 || index >= Names.Length)
            return category.ToString();

        return (Names[index] ??= TrelamiumIII.Instance.GetLocalization(
            $"ActLang.Category.{category}", category.ToString)).Value;
    }
}
