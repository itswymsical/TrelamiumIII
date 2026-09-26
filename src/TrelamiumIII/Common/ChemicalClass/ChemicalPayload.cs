using Terraria.DataStructures;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     One release of chemistry: its source, the releasing player and that player's chemical stats at release.
///     Shared by every projectile, emitter, gas node and residue the release produces.
/// </summary>
/// <remarks>Built where the projectile spawns, so every other client sees null and falls back to ambient chemistry.</remarks>
public sealed class ChemicalPayload
{
    public IChemicalSource Source { get; }

    /// <summary>The releasing player's index, or -1.</summary>
    public int Owner { get; }

    public float DamageMultiplier { get; }

    public float DurationMultiplier { get; }

    /// <summary>Reaction odds in percent that a hit adds to the source's own.</summary>
    public float ReactionChanceBonus { get; }

    public float ReactionDamageMultiplier { get; }

    private ChemicalPayload(IChemicalSource source, Player? player)
    {
        Source = source;
        Owner = player?.whoAmI ?? -1;

        if (player is null)
        {
            DamageMultiplier = DurationMultiplier = ReactionDamageMultiplier = 1f;
            return;
        }

        ChemicalPlayerStats stats = player.GetModPlayer<ChemicalPlayerStats>();

        DamageMultiplier = stats.ChemicalDamageBonus;
        DurationMultiplier = stats.AfflictionRate;
        ReactionChanceBonus = stats.ReactionChance;
        ReactionDamageMultiplier = stats.ReactionDamageBonus;
    }

    /// <summary>Null for a source that carries no chemistry.</summary>
    public static ChemicalPayload? Create(IChemicalSource? source, Player? player = null)
        => source is null || source.ChemicalType == ChemicalDamageType.None ? null : new ChemicalPayload(source, player);

    /// <summary>
    ///     The payload of a projectile spawned from <paramref name="spawnSource" />: the ammo's chemistry when the ammo
    ///     has any, else the used item's, else the parent projectile's payload.
    /// </summary>
    public static ChemicalPayload? From(IEntitySource spawnSource)
    {
        switch (spawnSource)
        {
            case EntitySource_ItemUse use:
                IChemicalSource? source = null;

                if (use is EntitySource_ItemUse_WithAmmo { AmmoItemIdUsed: > 0 } withAmmo
                    && ContentSamples.ItemsByType.TryGetValue(withAmmo.AmmoItemIdUsed, out Item? ammo))
                    source = ChemicalSourceComponent.Of(ammo);

                return Create(source ?? ChemicalSourceComponent.Of(use.Item), use.Player);

            case EntitySource_Parent { Entity: Projectile parent }:
                return ChemicalHandler.PayloadOf(parent);

            default:
                return null;
        }
    }

    /// <summary>A direct hit: the source's own chemistry, rolling its reaction chance plus the player's.</summary>
    public void Hit(NPC target, Projectile? delivery = null)
    {
        Afflict(target, Source, this, 1f);

        if (Main.rand.NextFloat(100f) < Source.ReactionChance + ReactionChanceBonus)
            ChemicalReaction.Trigger(target, Source, this, delivery);
    }

    /// <summary>
    ///     Exposure to <paramref name="formula" /> (a gas layer or residue) at <paramref name="strength" />, scaled and
    ///     credited by the <paramref name="payload" /> that put it there; null is ambient exposure. Rolls only the
    ///     formula's reaction chance.
    /// </summary>
    public static void Contact(NPC target, IChemicalSource formula, ChemicalPayload? payload, float strength = 1f)
    {
        Afflict(target, formula, payload, strength);

        if (Main.rand.NextFloat(100f) < formula.ReactionChance * strength)
            ChemicalReaction.Trigger(target, formula, payload);
    }

    private static void Afflict(NPC target, IChemicalSource formula, ChemicalPayload? payload, float strength)
    {
        float damage = formula.ChemicalDamage * strength * (payload?.DamageMultiplier ?? 1f);
        int duration = (int)(formula.AfflictionDuration * (payload?.DurationMultiplier ?? 1f));

        target.GetGlobalNPC<ChemicalGlobalNPC>()
              .ApplyAffliction(damage, duration, formula.AfflictionRate, formula.ChemicalType);
    }
}
