using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     One release of chemistry: its source, its modifiers, the releasing player, and the stats all three produce.
///     Shared by every projectile, emitter, gas node and residue the release produces.
/// </summary>
/// <remarks>Built where the projectile spawns, so other clients see null; only the owner's machine applies it anyway.</remarks>
public sealed class ChemicalPayload
{
    public IChemicalSource Source { get; }

    /// <summary>The releasing player's index, or -1.</summary>
    public int Owner { get; }

    public IReadOnlyList<ChemicalModifier> Modifiers { get; }

    /// <summary>The source's numbers after its modifiers and the releasing player's stats, fixed at release.</summary>
    public ChemicalStats Stats { get; }

    /// <summary>The first modifier's cling override, else the source's; null leaves it to the gas.</summary>
    public ClingBehaviour? ClingBehaviour { get; }

    private ChemicalPayload(IChemicalSource source, IReadOnlyList<ChemicalModifier> modifiers, Player? player)
    {
        Source = source;
        Owner = player?.whoAmI ?? -1;
        Modifiers = modifiers;

        ChemicalStats stats = ChemicalStats.Of(source);
        ClingBehaviour? cling = null;

        foreach (ChemicalModifier modifier in modifiers)
        {
            modifier.ModifyStats(ref stats);
            cling ??= modifier.ClingBehaviour;
        }

        if (player is not null)
            stats.Apply(player.GetModPlayer<ChemicalPlayerStats>());

        Stats = stats;
        ClingBehaviour = cling ?? source.ClingBehaviour;
    }

    /// <summary>Null for a source that carries no chemistry.</summary>
    public static ChemicalPayload? Create(IChemicalSource? source, Player? player = null, IReadOnlyList<ChemicalModifier>? modifiers = null)
        => source is null || source.ChemicalType == ChemicalDamageType.None
            ? null
            : new ChemicalPayload(source, modifiers ?? [], player);

    /// <summary>
    ///     The payload of a projectile spawned from <paramref name="spawnSource" />: the ammo's chemistry when the ammo
    ///     has any, else the used item's, else the parent projectile's payload. The ammo's modifiers apply first, then
    ///     the weapon's, then the weapon's prefix.
    /// </summary>
    public static ChemicalPayload? From(IEntitySource spawnSource)
    {
        switch (spawnSource)
        {
            case EntitySource_ItemUse use:
                ChemicalSourceComponent? weapon = ChemicalSourceComponent.Of(use.Item);
                ChemicalSourceComponent? ammo = null;

                if (use is EntitySource_ItemUse_WithAmmo { AmmoItemIdUsed: > 0 } withAmmo
                    && ContentSamples.ItemsByType.TryGetValue(withAmmo.AmmoItemIdUsed, out Item? sample))
                    ammo = ChemicalSourceComponent.Of(sample);

                var modifiers = new List<ChemicalModifier>();

                if (ammo is not null)
                    modifiers.AddRange(ammo.Modifiers);

                if (weapon is not null && weapon != ammo)
                    modifiers.AddRange(weapon.Modifiers);

                if (ChemicalPrefix.Of(use.Item) is { } prefix)
                    modifiers.Add(prefix.Modifier);

                return Create(ammo ?? weapon, use.Player, modifiers);

            case EntitySource_Parent { Entity: Projectile parent }:
                return ChemicalHandler.PayloadOf(parent);

            default:
                return null;
        }
    }

    /// <summary>
    ///     Whether this machine applies chemistry released by <paramref name="owner" />: that player's client, or the
    ///     server for chemistry nobody released. Keeps multiplayer from dealing the same affliction once per client.
    /// </summary>
    public static bool IsAuthority(int owner) => Main.netMode switch
    {
        NetmodeID.SinglePlayer => true,
        NetmodeID.MultiplayerClient => owner == Main.myPlayer,
        _ => owner is < 0 or >= Main.maxPlayers,
    };

    /// <summary>
    ///     A direct hit: the release's chemistry at full concentration, then a reaction roll amplified by the target's
    ///     catalysis. <paramref name="medium" /> is the gas a cling would be made of.
    /// </summary>
    public void Hit(NPC target, ChemicalGasField? medium = null, Projectile? delivery = null)
    {
        if (!IsAuthority(Owner))
            return;

        ChemicalGlobalNPC chemistry = target.GetGlobalNPC<ChemicalGlobalNPC>();

        chemistry.Expose(target, Delivery(ExposureKind.Hit, Owner, medium, 1f));

        float amplify = Stats.Catalysis > 0f ? 1f : chemistry.CatalysisMultiplier;

        if (Main.rand.NextFloat(100f) < Stats.ReactionChance * amplify)
            ChemicalReaction.Trigger(target, Source, this, delivery);
    }

    /// <summary>
    ///     Exposure to gas or residue at <paramref name="strength" />. The <paramref name="payload" /> that put it there
    ///     supplies the chemistry; without one (ambient chemistry) <paramref name="formula" /> does. Never reacts.
    /// </summary>
    public static void Contact(NPC target, IChemicalSource formula, ChemicalPayload? payload, int owner, ChemicalGasField? medium, float strength = 1f)
    {
        if (!IsAuthority(owner))
            return;

        ChemicalDelivery delivery = payload?.Delivery(ExposureKind.Contact, owner, medium, strength)
            ?? new ChemicalDelivery(formula.ChemicalType, ChemicalStats.Of(formula), formula.ClingBehaviour, null, owner, strength, ExposureKind.Contact, medium);

        target.GetGlobalNPC<ChemicalGlobalNPC>().Expose(target, delivery);
    }

    /// <summary>Scales the burst's arm speeds, then lets each modifier adjust it.</summary>
    public void ModifyBurst(ref BurstProfile burst)
    {
        float speed = Stats.BurstSpeedMultiplier;

        if (speed != 1f)
        {
            burst = burst with
            {
                AirSpeed = new FloatRange(burst.AirSpeed.Min * speed, burst.AirSpeed.Max * speed),
                GroundSpeed = new FloatRange(burst.GroundSpeed.Min * speed, burst.GroundSpeed.Max * speed),
            };
        }

        foreach (ChemicalModifier modifier in Modifiers)
            modifier.ModifyBurst(ref burst);
    }

    public void OnReaction(in ChemicalReactionEvent e)
    {
        foreach (ChemicalModifier modifier in Modifiers)
            modifier.OnReaction(in e);
    }

    private ChemicalDelivery Delivery(ExposureKind kind, int owner, ChemicalGasField? medium, float strength)
        => new(Source.ChemicalType, Stats, ClingBehaviour, this, owner, strength, kind, medium);
}
