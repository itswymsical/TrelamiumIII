using Terraria.DataStructures;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
///     One release of chemistry: its source, the releasing player and that player's chemical stats at release.
///     Shared by every projectile, emitter, gas node and residue the release produces.
/// </summary>
/// <remarks>Built where the projectile spawns, so other clients see null; only the owner's machine applies it anyway.</remarks>
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
    ///     A direct hit: the source's own chemistry at full concentration, then a reaction roll of its chance plus the
    ///     player's, amplified by the target's catalysis. <paramref name="medium" /> is the gas a cling would be made of.
    /// </summary>
    public void Hit(NPC target, ChemicalGasField? medium = null, Projectile? delivery = null)
    {
        if (!IsAuthority(Owner))
            return;

        ChemicalGlobalNPC chemistry = target.GetGlobalNPC<ChemicalGlobalNPC>();

        chemistry.Expose(target, Source, this, Owner, 1f, ExposureKind.Hit, medium);

        float amplify = Source.Catalysis > 0f ? 1f : chemistry.CatalysisMultiplier;

        if (Main.rand.NextFloat(100f) < (Source.ReactionChance + ReactionChanceBonus) * amplify)
            ChemicalReaction.Trigger(target, Source, this, delivery);
    }

    /// <summary>
    ///     Exposure to gas or residue at <paramref name="strength" />. The <paramref name="payload" /> that put it there
    ///     supplies the chemistry; without one (ambient chemistry) <paramref name="formula" /> does. Never reacts.
    /// </summary>
    public static void Contact(NPC target, IChemicalSource formula, ChemicalPayload? payload, int owner, ChemicalGasField? medium, float strength = 1f)
    {
        if (IsAuthority(owner))
            target.GetGlobalNPC<ChemicalGlobalNPC>().Expose(target, payload?.Source ?? formula, payload, owner, strength, ExposureKind.Contact, medium);
    }
}
