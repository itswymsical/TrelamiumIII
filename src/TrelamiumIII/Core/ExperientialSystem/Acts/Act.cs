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
    Building,

    Collection
}

/// <summary>
/// A tracked, repeatable player action with a ladder of <see cref="Thresholds"/>. Each crossed tier pays once;
/// after <see cref="ExperientialConstants.ActPayoutTiers"/> payouts the act is exhausted. One event may feed
/// several acts (an ore act and its group act).
/// </summary>
public abstract class Act
{
    private const string IdPrefix = "act.";

    private LocalizedText? _name;
    private LocalizedText? _description;

    /// <summary>Stable identifier. Persisted in save data and hashed into an <see cref="ExpSource"/>, so renaming one resets progress.</summary>
    public abstract string ID { get; }

    /// <summary>
    /// Key segment under <c>Mods.TrelamiumIII.ActLang</c>: <see cref="ID"/> without its <c>act.</c> prefix.
    /// Override only to keep an existing key after renaming an id.
    /// </summary>
    protected virtual string LocalizationKey
        => ID.StartsWith(IdPrefix, StringComparison.Ordinal) ? ID[IdPrefix.Length..] : ID;

    public virtual string Name => (_name ??= Localize(nameof(Name))).Value;

    public virtual string Description => (_description ??= Localize(nameof(Description))).Value;

    /// <summary>Registers the key through <see cref="Mod.GetLocalization"/> so missing strings are stubbed into the language file.</summary>
    private LocalizedText Localize(string field)
        => TrelamiumIII.Instance.GetLocalization($"ActLang.{LocalizationKey}.{field}", () => ID);

    public abstract ActCategory Category { get; }

    /// <summary>
    /// Progress counts, in actions (see <see cref="EventsPerAction"/>), at which each tier pays. Must ascend strictly;
    /// <see cref="ActRegistry"/> sorts a malformed ladder. Read through <see cref="Ladder"/>.
    /// </summary>
    public abstract int[] Thresholds { get; }

    /// <summary><see cref="Thresholds"/>, snapshotted on first read.</summary>
    public int[] Ladder => _ladder ??= Thresholds;

    /// <summary>
    /// Raw <see cref="GameEvent"/> amounts per player-recognisable action, e.g. four tile breaks per 2x2 pot.
    /// The tracker carries remainders between events.
    /// </summary>
    public virtual int EventsPerAction => 1;

    /// <summary>Whether this event moves this act's progress.</summary>
    public abstract bool Matches(in GameEvent evt);

    /// <summary>The one event type this act can match, used by the registry to bucket acts. Null tests every event.</summary>
    public virtual GameEventType? ListensFor => null;

    /// <summary>The distinct content ids a group act is made of, for coverage scaling. Empty or one entry for a non-group act.</summary>
    public virtual int[] Members => [];

    /// <summary>Which member this event belongs to, or -1 if the act does not track members.</summary>
    public virtual int MemberFor(in GameEvent evt) => -1;

    #region Tier maths

    private int[]? _ladder;

    public int TierCount => Ladder.Length;

    /// <summary>Tiers that can pay; rungs past <see cref="ExperientialConstants.ActPayoutTiers"/> pay nothing.</summary>
    public int PaidTierCount => Math.Min(TierCount, ExperientialConstants.ActPayoutTiers);

    /// <summary>Number of tiers <paramref name="progress"/> has passed, so tier indices <c>0 .. result-1</c> are done.</summary>
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

    /// <summary>Multiplier on everything this act pays; group acts default to <see cref="ExperientialConstants.GroupActMultiplier"/>.</summary>
    public virtual float ExpMultiplier => IsGroup ? ExperientialConstants.GroupActMultiplier : 1f;

    /// <summary>
    /// Hardmode-only content. Its tiers are floored at <see cref="ExperientialConstants.HardmodeFloorTierIndex"/>
    /// while in hardmode.
    /// </summary>
    public virtual bool IsHardmodeContent => false;

    /// <summary>Fraction of the meter crossing <paramref name="tierIndex"/> pays: base curve, hardmode floor, multiplier and coverage.</summary>
    public float PayoutFraction(int tierIndex, int distinctMembersSeen)
        => ExperientialConstants.ActPayoutFor(EffectiveTierIndex(tierIndex))
         * ExpMultiplier
         * CoverageMultiplier(distinctMembersSeen);

    private int EffectiveTierIndex(int tierIndex)
    {
        // Tiers outside the paying range stay there.
        if (tierIndex < 0 || tierIndex >= ExperientialConstants.ActPayoutTiers)
            return tierIndex;

        return IsHardmodeContent && Main.hardMode
            ? Math.Max(tierIndex, ExperientialConstants.HardmodeFloorTierIndex)
            : tierIndex;
    }

    #endregion

    #region Coverage

    /// <summary>
    /// Scales a group act's payout by the fraction of members seen, from <see cref="ExperientialConstants.GroupCoverageFloor"/>
    /// up to 1. Returns 1 for acts with one member or none.
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

/// <summary>Localized display names for <see cref="ActCategory"/>.</summary>
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
