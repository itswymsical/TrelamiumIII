namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Anything that can apply chemical afflictions or trigger reactions on an NPC.
/// Weapon components, gas layers, utility items, accessory auras, and placed
/// dispensers all implement this. Identity travels with the source so that
/// reaction logic can be authored once per emitter instead of once per type.
/// </summary>
public interface IChemicalSource
{
    ChemicalDamageType ChemicalType { get; }
    float ChemicalDamage { get; }
    int AfflictionDuration { get; }
    float AfflictionRate { get; }
    float ReactionChance { get; }

    /// <summary>
    /// Crit strike damage for when this source triggers a reaction.
    /// Set to 0 to disable bursts (e.g. ambient gas where afflictions carry the work).
    /// </summary>
    float ReactionDamage { get; }

    /// <summary>
    /// Invoked when this specific source rolls a successful reaction on a target.
    /// Owner-scoped behavior lives here; world-observing listeners use
    /// <see cref="ChemicalEvents.OnReaction"/> instead.
    /// </summary>
    void OnChemicalReaction(in ChemicalReactionEvent e);
}

public readonly struct ChemicalReactionEvent
{
    public readonly NPC Target;
    public readonly IChemicalSource Source;
    public readonly EnemyComposition Composition;

    /// <summary>
    /// The projectile that delivered the reaction, when applicable.
    /// Null for gas-layer contacts, utility AOEs, and other non-projectile delivery.
    /// </summary>
    public readonly Projectile DeliveryProjectile;

    /// <summary>
    /// What the reaction burst actually struck the target for, after composition matchup, the
    /// firing player's bonus, and the target's defense. 0 when the source has no
    /// <see cref="IChemicalSource.ReactionDamage"/>. Handlers that scale off the hit (lifesteal,
    /// for one) read this instead of recomputing the formula.
    /// </summary>
    public readonly int DamageDealt;

    public ChemicalDamageType Type => Source.ChemicalType;

    public ChemicalReactionEvent(
        NPC target,
        IChemicalSource source,
        EnemyComposition composition,
        Projectile deliveryProjectile = null,
        int damageDealt = 0)
    {
        Target = target;
        Source = source;
        Composition = composition;
        DeliveryProjectile = deliveryProjectile;
        DamageDealt = damageDealt;
    }
}
/// <summary>
/// dispatch point for reaction effects. Both projectile hits and gas contacts
/// run through here so reaction state, burst damage, source-scoped behavior,
/// and global event broadcast all happen in one consistent order.
/// </summary>
public static class ChemicalReaction
{
    public static void Trigger(
        NPC target,
        IChemicalSource source,
        float playerDamageBonus = 1f,
        Projectile deliveryProjectile = null)
    {
        var npcData = target.GetGlobalNPC<ChemicalGlobalNPC>();
        npcData.ApplyReaction(source.ChemicalType);

        int dealt = 0;
        if (source.ReactionDamage > 0f)
        {
            float mult = ChemicalMatchup.GetReactionDamageMultiplier(source.ChemicalType, npcData.Composition);
            int burst = Math.Max(1, (int)(source.ReactionDamage * mult * playerDamageBonus));

            // SimpleStrikeNPC returns the post-defense damage, which is what handlers want to
            // scale off rather than the requested burst.
            dealt = target.SimpleStrikeNPC(
                burst,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: ModContent.GetInstance<ChemicalDamageClass>(),
                noPlayerInteraction: deliveryProjectile == null);
        }

        var evt = new ChemicalReactionEvent(target, source, npcData.Composition, deliveryProjectile, dealt);
        source.OnChemicalReaction(in evt);
        ChemicalEvents.RaiseReaction(in evt);
    }
}

public static class ChemicalEvents
{
    public static event Action<ChemicalReactionEvent> OnReaction;

    internal static void RaiseReaction(in ChemicalReactionEvent e)
        => OnReaction?.Invoke(e);
}