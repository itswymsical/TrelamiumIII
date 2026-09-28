using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     The lifetime of something that keeps emitting chemistry for a release, such as a cling or a catalysis. Payloads
///     derived under it apply nothing once it ends.
/// </summary>
public sealed class ChemicalScope
{
    public bool Ended { get; private set; }

    public void End() => Ended = true;
}

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

    /// <summary>0 for a release. Each cling, reaction, residue or catalysis that re-emits it adds one.</summary>
    public int Generation { get; }

    /// <summary>The emitter this payload lives by, or null. Once that ends, the payload applies nothing.</summary>
    public ChemicalScope? Scope { get; }

    /// <summary>The NPC whose cling emits it, which it never exposes. -1 for none.</summary>
    public int Host { get; } = -1;

    /// <summary>Re-emitted chemistry. It never clings by build-up and never reacts, so it can't sustain itself.</summary>
    public bool IsDerived => Generation > 0;

    public bool IsActive => Scope is not { Ended: true };

    private ChemicalPayload? derived;

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
            stats.Apply(player.GetModPlayer<ChemicalPlayerStats>(), source.ChemicalType);

        Stats = stats;
        ClingBehaviour = cling ?? source.ClingBehaviour;
    }

    private ChemicalPayload(ChemicalPayload parent, ChemicalScope? scope, int host)
    {
        Source = parent.Source;
        Owner = parent.Owner;
        Modifiers = parent.Modifiers;
        Stats = parent.Stats;
        ClingBehaviour = parent.ClingBehaviour;
        Generation = parent.Generation + 1;
        Scope = scope;
        Host = host;
    }

    /// <summary>A one-off re-emission, such as a reaction's burst: derived, keeping this payload's scope and host.</summary>
    public ChemicalPayload Derived => derived ??= new ChemicalPayload(this, Scope, Host);

    /// <summary>What an emitter re-emits: derived, applying only while <paramref name="scope" /> lasts, and never to <paramref name="host" />.</summary>
    public ChemicalPayload Derive(ChemicalScope? scope, int host = -1) => new(this, scope, host);

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
    ///     A direct hit: the release's chemistry at full concentration, then its modifiers' hit behaviour and a reaction
    ///     roll amplified by the target's catalysis. A derived payload only exposes. <paramref name="medium" /> is the
    ///     gas a cling would be made of.
    /// </summary>
    public void Hit(NPC target, ChemicalGasField? medium = null, Projectile? delivery = null)
    {
        if (!Apply(target, ExposureKind.Hit, 1f, medium) || IsDerived)
            return;

        foreach (ChemicalModifier modifier in Modifiers)
            modifier.OnHit(target, this, medium);

        ChemicalGlobalNPC chemistry = target.GetGlobalNPC<ChemicalGlobalNPC>();
        float chance = Stats.ReactionChance * (Stats.Catalysis > 0f ? 1f : chemistry.CatalysisMultiplier);

        foreach (ChemicalModifier modifier in Modifiers)
            modifier.ModifyReactionChance(target, this, ref chance);

        if (Main.rand.NextFloat(100f) < chance)
            ChemicalReaction.Trigger(target, Source, this, delivery);
    }

    /// <summary>Exposes <paramref name="target" /> to this payload at <paramref name="strength" />, from the owner's machine. False when nothing applied.</summary>
    public bool Apply(NPC target, ExposureKind kind, float strength = 1f, ChemicalGasField? medium = null)
        => IsAuthority(Owner) && target.GetGlobalNPC<ChemicalGlobalNPC>().Expose(target, Delivery(kind, Owner, medium, strength));

    /// <summary>
    ///     Exposure to gas or residue at <paramref name="strength" />. The <paramref name="payload" /> that put it there
    ///     supplies the chemistry; without one (ambient chemistry) <paramref name="formula" /> does. Never reacts.
    /// </summary>
    /// <returns>Whether it applied, so effects that ride on a contact can follow it.</returns>
    public static bool Contact(NPC target, IChemicalSource formula, ChemicalPayload? payload, int owner, ChemicalGasField? medium, float strength = 1f)
    {
        if (!IsAuthority(owner))
            return false;

        ChemicalDelivery delivery = payload?.Delivery(ExposureKind.Contact, owner, medium, strength)
            ?? new ChemicalDelivery(formula.ChemicalType, ChemicalStats.Of(formula), formula.ClingBehaviour, null, owner, strength, ExposureKind.Contact, medium);

        return target.GetGlobalNPC<ChemicalGlobalNPC>().Expose(target, delivery);
    }

    /// <summary>Scales the burst's arm speeds and counts, then lets each modifier adjust it.</summary>
    public void ModifyBurst(ref BurstProfile burst)
    {
        float speed = Stats.BurstSpeedMultiplier;
        float count = Stats.BurstCountMultiplier;

        if (speed != 1f || count != 1f)
        {
            burst = burst with
            {
                AirSpeed = Scale(burst.AirSpeed, speed),
                GroundSpeed = Scale(burst.GroundSpeed, speed),
                AirCount = Scale(burst.AirCount, count),
                GroundCount = Scale(burst.GroundCount, count),
            };
        }

        foreach (ChemicalModifier modifier in Modifiers)
            modifier.ModifyBurst(ref burst);
    }

    private static FloatRange Scale(FloatRange range, float by) => new(range.Min * by, range.Max * by);

    public void OnReaction(in ChemicalReactionEvent e)
    {
        foreach (ChemicalModifier modifier in Modifiers)
            modifier.OnReaction(in e);
    }

    private ChemicalDelivery Delivery(ExposureKind kind, int owner, ChemicalGasField? medium, float strength)
        => new(Source.ChemicalType, Stats, ClingBehaviour, this, owner, strength, kind, medium);
}
