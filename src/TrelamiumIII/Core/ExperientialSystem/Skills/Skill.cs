using Terraria.Localization;

namespace TrelamiumIII.Core.ExperientialSystem.Skills;

/// <summary>
/// Broad families a skill can belong to. Used for UI grouping and for respec rules that want to refund
/// or lock a whole branch at once rather than individual picks.
/// </summary>
public enum SkillGroup
{
    Unaligned,
    Locomotion,
    Construction,
    Excavation,
    Ballistics,
    Alchemy,
    Husbandry,
    Communion
}

/// <summary>
/// A permanent, purchasable change to how some piece of content behaves - a pickaxe that mines
/// differently, bullets that interact with tiles, an accessory effect made intrinsic.
///
/// <para>
/// A skill is gated two ways at once, and both matter. <see cref="RequiredThreshold"/> says how far into
/// the world you must have got before the skill exists for you at all; <see cref="Cost"/> is Exp spent
/// out of the live meter, so taking it postpones your next Threshold. Engagement buys the right to buff,
/// and buffing costs progress - that trade is the system.
/// </para>
///
/// <para>
/// Author cost with <see cref="CostFraction"/>, not with a literal. Exp is consumed on every ascend and
/// the meter's capacity spans 50 to 76,000, so a hardcoded price is unaffordable at low Thresholds and
/// free at high ones. A fraction stays worth the same thing everywhere on the curve.
/// </para>
/// </summary>
public abstract class Skill
{
    /// <summary>Prefix every skill id carries, stripped when deriving the localization key.</summary>
    private const string IdPrefix = "skill.";

    private LocalizedText? _name;
    private LocalizedText? _description;

    /// <summary>Stable identifier. Persisted in save data, so renaming one orphans a player's unlock.</summary>
    public abstract string ID { get; }

    /// <summary>
    /// Key segment under <c>Mods.TrelamiumIII.SkillLang</c>, derived from <see cref="ID"/> with its
    /// <c>skill.</c> prefix stripped. Override only to keep an existing key alive after renaming an id.
    /// </summary>
    protected virtual string LocalizationKey
        => ID.StartsWith(IdPrefix, StringComparison.Ordinal) ? ID[IdPrefix.Length..] : ID;

    /// <summary>Display name, from the language files. Cached as a <see cref="LocalizedText"/> so it follows a language change.</summary>
    public virtual string Name => (_name ??= Localize(nameof(Name))).Value;

    public virtual string Description => (_description ??= Localize(nameof(Description))).Value;

    /// <summary>
    /// Registers the key through <see cref="Mod.GetLocalization"/> so a skill nobody has written strings
    /// for is stubbed into the language file rather than silently rendering its own id.
    /// </summary>
    private LocalizedText Localize(string field)
        => TrelamiumIII.Instance.GetLocalization($"SkillLang.{LocalizationKey}.{field}", () => ID);

    /// <summary>
    /// Threshold this skill is authored at. Below <see cref="ExperientialConstants.FirstSkillThreshold"/>
    /// it only orders the skill within the opening band; <see cref="UnlockThreshold"/> is the real gate.
    /// </summary>
    public abstract int RequiredThreshold { get; }

    /// <summary>Threshold the player must have reached to buy this. Never re-checked once unlocked.</summary>
    public int UnlockThreshold => ExperientialConstants.SkillGate(RequiredThreshold);

    public virtual SkillGroup Group => SkillGroup.Unaligned;

    /// <summary>
    /// Position in tree space, origin at the centre of the field and one unit to a node's spacing.
    /// Null places the skill automatically on the ring its <see cref="UnlockThreshold"/> owns.
    /// </summary>
    public virtual Vector2? Space => null;

    /// <summary>
    /// Price as a fraction of the meter at <see cref="UnlockThreshold"/>. 0.4 means "roughly two fifths
    /// of the Threshold you can first buy it on" no matter where on the curve that sits.
    /// </summary>
    public virtual float CostFraction => 0.4f;

    /// <summary>Exp price. Override only for a skill that genuinely needs a flat number.</summary>
    public virtual int Cost => (int)MathF.Round(ExperientialConstants.Capacity(UnlockThreshold) * CostFraction);

    /// <summary>
    /// Fraction of <see cref="Cost"/> returned on respec. Below 1 makes respeccing a real decision rather
    /// than something to do between every fight.
    /// </summary>
    public virtual float RefundRate => 0.75f;

    /// <summary>Exp handed back when this is refunded, before the meter's own capacity clamp.</summary>
    public int RefundValue => (int)MathF.Round(Cost * RefundRate);

    /// <summary>
    /// Extra gate beyond Threshold and Exp - a prerequisite skill, a downed boss, a visited biome.
    /// Return false to leave the skill visible but unbuyable.
    /// </summary>
    public virtual bool IsAvailableTo(Player player) => true;

    /// <summary>Called once when bought. For one-shot changes; ongoing effects belong in <see cref="Apply"/>.</summary>
    public virtual void OnUnlock(Player player) { }

    /// <summary>Called once when refunded. Undo anything <see cref="OnUnlock"/> did.</summary>
    public virtual void OnRefund(Player player) { }

    /// <summary>
    /// Called every tick while the skill is held, from <c>ModPlayer.ResetEffects</c>. This is where stat
    /// changes and behaviour flags belong, so that a respec takes effect immediately with no bookkeeping.
    /// </summary>
    public virtual void Apply(Player player) { }
}

/// <summary>Localized display names for <see cref="SkillGroup"/>, for the branch headings in the skill panel.</summary>
public static class SkillGroupExtensions
{
    private static readonly LocalizedText?[] Names = new LocalizedText?[Enum.GetValues<SkillGroup>().Length];

    public static string DisplayName(this SkillGroup group)
    {
        int index = (int)group;

        if (index < 0 || index >= Names.Length)
            return group.ToString();

        return (Names[index] ??= TrelamiumIII.Instance.GetLocalization(
            $"SkillLang.Group.{group}", group.ToString)).Value;
    }
}
