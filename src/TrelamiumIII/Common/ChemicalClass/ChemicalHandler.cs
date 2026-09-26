using System.Collections.Generic;
using Terraria.DataStructures;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>
/// Carries chemical identity onto projectiles and dispatches afflictions and reactions on hit.
/// The source and a snapshot of the player's stat scalars are captured at spawn.
/// </summary>
public class ChemicalHandler : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public IChemicalSource Source;

    // Firing player's stats, snapshotted at spawn so accessory swaps mid-flight don't change the shot.
    public float DamageBonus = 1f;
    public float StatsAfflictionRate = 1f;
    public float StatsReactionChance;
    public float StatsReactionDamageBonus = 1f;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (projectile.DamageType != ModContent.GetInstance<ChemicalDamageClass>()) return;

        if (source is EntitySource_ItemUse_WithAmmo or EntitySource_ItemUse)
        {
            var stats = Main.player[projectile.owner].GetModPlayer<ChemicalPlayerStats>();
            Source = stats.ActiveChemicalSource;
            DamageBonus = stats.ChemicalDamageBonus;
            StatsAfflictionRate = stats.AfflictionRate;
            StatsReactionChance = stats.ReactionChance;
            StatsReactionDamageBonus = stats.ReactionDamageBonus;
        }
        else if (source is EntitySource_Parent { Entity: Projectile parent })
        {
            var parentData = parent.GetGlobalProjectile<ChemicalHandler>();
            Source = parentData.Source;
            DamageBonus = parentData.DamageBonus;
            StatsAfflictionRate = parentData.StatsAfflictionRate;
            StatsReactionChance = parentData.StatsReactionChance;
            StatsReactionDamageBonus = parentData.StatsReactionDamageBonus;
        }
    }

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Source == null || Source.ChemicalType == ChemicalDamageType.None) return;

        var npcData = target.GetGlobalNPC<ChemicalGlobalNPC>();

        float dmg = Source.ChemicalDamage * DamageBonus;
        int duration = (int)(Source.AfflictionDuration * StatsAfflictionRate);
        float rate = Source.AfflictionRate * StatsAfflictionRate;
        float chance = Source.ReactionChance + StatsReactionChance;

        npcData.ApplyAffliction(dmg, duration, rate, Source.ChemicalType);

        if (Main.rand.NextFloat(100f) < chance)
            ChemicalReaction.Trigger(target, Source, StatsReactionDamageBonus, projectile);
    }
}
/// <summary>
/// Reads ChemicalSourceComponent from the fired item and stamps the active source
/// onto the player's ChemicalPlayerStats so spawned projectiles can inherit it.
/// </summary>
public class ChemicalWeaponGlobalItem : GlobalItem
{
    public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        if (item.TryGetGlobalItem(out ChemicalSourceComponent component))
            player.GetModPlayer<ChemicalPlayerStats>().ActiveChemicalSource = component;

        return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
    }
}
/// <summary>
/// Per-NPC state for the chemical system. Tags each NPC with its composition on spawn,
/// then tracks affliction and reaction state each tick.
/// </summary>
public class ChemicalGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public EnemyComposition Composition { get; private set; }

    public bool IsAfflicted { get; private set; }
    public float AfflictionDamagePerTick { get; private set; }
    public int AfflictionTicksRemaining { get; private set; }
    public ChemicalDamageType AfflictionSource { get; private set; }

    public bool ReactionActive { get; private set; }
    public int ReactionTicksRemaining { get; private set; }
    public int ReactionDefenseShred { get; private set; }

    private int _tickTimer;
    public float AfflictionRate { get; private set; } = 20;

    private static readonly Dictionary<int, EnemyComposition> _registry = [];

    public static void Register(int npcType, EnemyComposition composition)
        => _registry[npcType] = composition;

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        Composition = _registry.TryGetValue(npc.type, out var registered)
            ? registered
            : InferComposition(npc);
    }

    private static EnemyComposition InferComposition(NPC npc)
    {
        var comp = EnemyComposition.None;

        if (npc.aiStyle == NPCAIStyleID.Slime)
            comp |= EnemyComposition.Gel;

        if (npc.type >= NPCID.None && npc.type < NPCID.Sets.Skeletons.Length && NPCID.Sets.Skeletons[npc.type])
            comp |= EnemyComposition.Undead;

        if (npc.HitSound == SoundID.NPCHit4)
            comp |= EnemyComposition.Metal | EnemyComposition.Armored;

        if (comp == EnemyComposition.None)
            comp = EnemyComposition.Flesh;

        return comp;
    }

    public void ApplyAffliction(float damagePerTick, int duration, float rate, ChemicalDamageType source)
    {
        float multiplier = ChemicalMatchup.GetAfflictionMultiplier(source, Composition);
        float scaledDamage = damagePerTick * multiplier;

        if (!IsAfflicted || scaledDamage >= AfflictionDamagePerTick)
        {
            AfflictionDamagePerTick = scaledDamage;
            AfflictionTicksRemaining = duration;
            AfflictionRate = rate;
            AfflictionSource = source;
            IsAfflicted = true;
        }
        else
        {
            AfflictionTicksRemaining = Math.Max(AfflictionTicksRemaining, duration);
        }
    }

    public void ApplyReaction(ChemicalDamageType source)
    {
        ReactionActive = true;
        ReactionTicksRemaining = 180;
        ReactionDefenseShred = ChemicalMatchup.GetReactionDefenseShred(source, Composition);
    }

    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        if (ReactionActive && ReactionDefenseShred > 0)
        {
            modifiers.Defense.Flat -= ReactionDefenseShred;
            modifiers.DamageVariationScale *= 1.5f;
        }
    }

    public override void PostAI(NPC npc)
    {
        if (ReactionActive)
        {
            if (--ReactionTicksRemaining <= 0)
            {
                ReactionActive = false;
                ReactionDefenseShred = 0;
            }
        }

        if (!IsAfflicted) return;

        if (++_tickTimer >= AfflictionRate)
        {
            _tickTimer = 0;
            DealAfflictionTick(npc);
        }

        if (--AfflictionTicksRemaining <= 0)
        {
            IsAfflicted = false;
            AfflictionDamagePerTick = 0f;
        }
    }

    private void DealAfflictionTick(NPC npc)
    {
        if (npc.immortal || npc.dontTakeDamage)
            return;

        int damage = Math.Max(1, (int)AfflictionDamagePerTick);

        npc.SimpleStrikeNPC(
            damage,
            hitDirection: 0,
            crit: false,
            knockBack: 0f,
            damageType: ModContent.GetInstance<ChemicalDamageClass>(),
            noPlayerInteraction: true
        );
    }
}