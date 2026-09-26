namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Anything that can apply chemical afflictions or trigger reactions on an NPC: weapons, gas layers, utilities,
/// auras, dispensers. Reaction logic is authored per emitter rather than per type.
/// </summary>
public interface IChemicalSource
{
    ChemicalDamageType ChemicalType { get; }

    /// <summary>Flat affliction damage per tick, like <see cref="Item.damage" />, before matchup scaling.</summary>
    int ChemicalDamage { get; }

    int AfflictionDuration { get; }
    float AfflictionRate { get; }

    /// <summary>Reaction odds in percent, like <see cref="Item.crit" /> (7 = 7%).</summary>
    int ReactionChance { get; }

    /// <summary>Reaction burst damage. 0 disables bursts (e.g. ambient gas where afflictions do the work).</summary>
    int ReactionDamage { get; }

    /// <summary>
    /// Concentration one hit adds to the target, out of <see cref="ChemicalTuning.MaxConcentration" />. Gas and residue
    /// add <see cref="ChemicalTuning.ContactShare" /> of it per exposure. 0 never builds up or clings.
    /// </summary>
    int Concentration { get; }

    /// <summary>Catalysis one hit adds to the target, 0 to 1. Catalysis amplifies every other source on it.</summary>
    float Catalysis { get; }

    /// <summary>What this chemistry does when it clings, or null to use the gas it was delivered through.</summary>
    ClingBehaviour? ClingBehaviour { get; }

    /// <summary>
    /// Called when this source rolls a reaction on a target. World-wide listeners use
    /// <see cref="ChemicalEvents.OnReaction"/> instead.
    /// </summary>
    void OnChemicalReaction(in ChemicalReactionEvent e);
}

public readonly struct ChemicalReactionEvent
{
    public readonly NPC Target;
    public readonly IChemicalSource Source;
    public readonly EnemyComposition Composition;

    /// <summary>The delivering projectile, or null for gas contacts, AOEs and other non-projectile delivery.</summary>
    public readonly Projectile? DeliveryProjectile;

    /// <summary>
    /// Burst damage actually dealt after matchup, player bonus and defense; 0 when the source has no
    /// <see cref="IChemicalSource.ReactionDamage"/>. Scale off this rather than recomputing it.
    /// </summary>
    public readonly int DamageDealt;

    /// <summary>The release that caused the reaction, or null for ambient chemistry.</summary>
    public readonly ChemicalPayload? Payload;

    public ChemicalDamageType Type => Source.ChemicalType;

    /// <summary>The releasing player, or -1. Pass it to anything the reaction spawns.</summary>
    public int Owner => Payload?.Owner ?? -1;

    public ChemicalReactionEvent(
        NPC target,
        IChemicalSource source,
        EnemyComposition composition,
        Projectile? deliveryProjectile = null,
        int damageDealt = 0,
        ChemicalPayload? payload = null)
    {
        Target = target;
        Source = source;
        Composition = composition;
        DeliveryProjectile = deliveryProjectile;
        DamageDealt = damageDealt;
        Payload = payload;
    }
}
/// <summary>
/// Single dispatch point for reactions from projectile hits and gas contacts, so reaction state, burst damage,
/// source callbacks and the global event always run in the same order.
/// </summary>
public static class ChemicalReaction
{
    /// <summary>
    ///     Refuses non-elemental sources (<see cref="ChemicalDamageType.Solute" />) outright, so a solute weapon
    ///     can't gain reactions through careless tuning. The burst credits the local player only when
    ///     <paramref name="payload" /> is theirs.
    /// </summary>
    public static void Trigger(
        NPC target,
        IChemicalSource source,
        ChemicalPayload? payload = null,
        Projectile? deliveryProjectile = null)
    {
        if (!ChemicalMatchup.IsElemental(source.ChemicalType))
            return;

        var npcData = target.GetGlobalNPC<ChemicalGlobalNPC>();
        npcData.ApplyReaction(source.ChemicalType);

        int dealt = 0;
        if (source.ReactionDamage > 0)
        {
            float mult = ChemicalMatchup.GetReactionDamageMultiplier(source.ChemicalType, npcData.Composition);
            int burst = Math.Max(1, (int)(source.ReactionDamage * mult * (payload?.ReactionDamageMultiplier ?? 1f)));

            // SimpleStrikeNPC returns post-defense damage, which is what handlers scale off.
            dealt = target.SimpleStrikeNPC(
                burst,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: ModContent.GetInstance<ChemicalDamageClass>(),
                noPlayerInteraction: payload?.Owner != Main.myPlayer);
        }

        var evt = new ChemicalReactionEvent(target, source, npcData.Composition, deliveryProjectile, dealt, payload);
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