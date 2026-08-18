namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// Ids for brewed effects that do NOT ride a vanilla buff. Negative so they can never collide with
/// real <see cref="BuffID"/> values. They live in the same effect list
/// but are consumed item-side at use time instead of ticking on <see cref="AlchemyEffectPlayer"/>:
/// e.g. Restoration scales the item's heal in <see cref="BrewGlobalItem.GetHealLife"/>.
/// </summary>
public static class VirtualBuff
{
    /// <summary>Instant heal scaling: StrengthPercent scales the item's healLife/healMana.</summary>
    public const int Restoration = -1;
}

/// <summary>
/// Per-effect knobs that don't fit the common (potency/duration/strength) shape. 
/// Kept a small value type so effects copy by value, compare structurally (for
/// stacking) and serialize cheaply.
/// </summary>
public readonly record struct EffectParams(int MaxOreTier = 0)
{
    /// <summary>All-default params (no gate). Same as <c>default</c>.</summary>
    public static readonly EffectParams None = default;
}

/// <summary>
/// One brewed effect instance on a potion. A value type, so a <see cref="PotionBrew"/>'s list of these
/// copies/compares by value. Rides a vanilla <see cref="BaseBuff"/> for its icon + timer; the actual
/// magnitude/behaviour is owned by the matching <see cref="IEffectBehavior"/>.
/// </summary>
public readonly record struct AlchemicalEffect(
    int BaseBuff,          // vanilla BuffID this rides on (icon + timer)
    int Potency,           // 1-5, the "level"
    int DurationTicks,     // brewed duration
    int StrengthPercent,   // magnitude as a percent of the vanilla effect (100 = standard)
    EffectParams Params = default);
