namespace TrelamiumIII.Core.ExperientialSystem.Skills;

/// <summary>
/// Broad families a skill can belong to. Used for UI grouping and for respec rules that want to refund
/// or lock a whole branch at once rather than individual picks.
/// </summary>
public enum SkillGroup
{
    Unaligned,
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
    /// <summary>Stable identifier. Persisted in save data, so renaming one orphans a player's unlock.</summary>
    public abstract string ID { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    /// <summary>Threshold the player must have reached before this can be bought. Never re-checked once unlocked.</summary>
    public abstract int RequiredThreshold { get; }

    public virtual SkillGroup Group => SkillGroup.Unaligned;

    /// <summary>
    /// Price as a fraction of the meter at <see cref="RequiredThreshold"/>. 0.4 means "roughly two fifths
    /// of the Threshold you unlocked it on" no matter where on the curve that sits.
    /// </summary>
    public virtual float CostFraction => 0.4f;

    /// <summary>Exp price. Override only for a skill that genuinely needs a flat number.</summary>
    public virtual int Cost => (int)MathF.Round(ExperientialConstants.Capacity(RequiredThreshold) * CostFraction);

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
