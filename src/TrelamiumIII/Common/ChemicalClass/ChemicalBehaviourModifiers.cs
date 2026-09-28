using System.Collections.Generic;
using Terraria.Audio;
using TrelamiumIII.Common.DamageMeter;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>A modifier whose key and element come from its constructor, so one behaviour can back several named modifiers.</summary>
public abstract class NamedModifier(string key, ChemicalDamageType element = ChemicalDamageType.None) : ChemicalModifier
{
    public override string Key => key;

    public override ChemicalDamageType Element => element;
}

/// <summary>A modifier that only changes stats.</summary>
public sealed class StatModifier(string key, ChemicalModifier.StatEdit edit, ChemicalDamageType element = ChemicalDamageType.None)
    : NamedModifier(key, element)
{
    public override void ModifyStats(ref ChemicalStats stats)
    {
        base.ModifyStats(ref stats);
        edit(ref stats);
    }
}

#region Hits

/// <summary>
///     Hits also expose the nearest other enemies at reduced strength. The shared exposure is derived, so it never
///     clings, reacts or spreads again.
/// </summary>
public sealed class HitSpreadModifier(string key, int targets, float range, float strength) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [targets, Percent(strength)];

    protected override bool UsesBehaviour(IChemicalSource? source)
        => source is null || source.ChemicalDamage > 0 || source.Concentration > 0 || source.Catalysis > 0f;

    public override void OnHit(NPC target, ChemicalPayload payload, ChemicalGasField? medium)
    {
        foreach (NPC other in ChemicalTargets.Nearest(target.Center, range, targets, npc => npc.whoAmI == target.whoAmI))
        {
            if (payload.Derived.Apply(other, ExposureKind.Hit, strength, medium))
                ChemicalEffects.Link(target.Center, other.Center, payload.Source.ChemicalType);
        }
    }
}

/// <summary>Each hit strips defense, stacking up to the cap, for a while.</summary>
public sealed class HitShredModifier(string key, ChemicalDamageType element, int perHit, int cap, int ticks) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [perHit, cap, Number(ticks / 60f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => true;

    public override void OnHit(NPC target, ChemicalPayload payload, ChemicalGasField? medium)
    {
        if (Matches(payload))
            target.GetGlobalNPC<ChemicalGlobalNPC>().StackShred(perHit, cap, ticks);
    }
}

/// <summary>Hits give the releasing player stacking defense for a while, as <see cref="Gallantry" />.</summary>
public sealed class HitGuardModifier(string key, int perHit, int cap, int ticks) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [perHit, cap, Number(ticks / 60f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => true;

    public override void OnHit(NPC target, ChemicalPayload payload, ChemicalGasField? medium)
    {
        if (payload.Owner >= 0 && payload.Owner == Main.myPlayer)
            Main.player[payload.Owner].GetModPlayer<GallantryPlayer>().Raise(perHit, cap, ticks);
    }
}

#endregion

#region Exposure and afflictions

/// <summary>Everything the release exposes gets <typeparamref name="TBuff" />. A non-positive tick count lasts as long as the affliction.</summary>
public sealed class ExposureBuffModifier<TBuff>(string key, ChemicalDamageType element, int ticks) : NamedModifier(key, element)
    where TBuff : ModBuff
{
    protected override object[] DescriptionArgs => [Number(ticks / 60f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => true;

    public override void OnExpose(NPC target, ChemicalPayload payload, in ChemicalDelivery delivery)
    {
        if (!Matches(payload))
            return;

        int duration = ticks > 0 ? ticks : (int)(delivery.Stats.AfflictionDuration * delivery.Stats.DurationMultiplier);

        if (duration > 0)
            ChemicalEffects.Refresh(target, ModContent.BuffType<TBuff>(), duration);
    }
}

/// <summary>
///     Each affliction tick may pass the affliction to the nearest enemy the element isn't already afflicting. What passes
///     is a generation deeper, so contagion stops at the generation cap.
/// </summary>
public sealed class ContagionModifier(string key, ChemicalDamageType element, float chance, float range, float strength, int maxGeneration)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Percent(chance), Number(range / 16f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => source is null || source.ChemicalDamage > 0;

    public override void OnAfflictionTick(NPC npc, ChemicalPayload payload, ChemicalDamageType type)
    {
        if (!Matches(payload) || payload.Generation >= maxGeneration || Main.rand.NextFloat() >= chance)
            return;

        List<NPC> next = ChemicalTargets.Nearest(npc.Center, range, 1,
            other => other.whoAmI == npc.whoAmI || other.GetGlobalNPC<ChemicalGlobalNPC>().IsAfflicted(type));

        if (next.Count > 0 && payload.Derived.Apply(next[0], ExposureKind.Hit, strength))
            ChemicalEffects.Link(npc.Center, next[0].Center, type);
    }
}

/// <summary>Each affliction tick strips more of the target's defense, up to the cap, held while the ticks keep coming.</summary>
public sealed class TickShredModifier(string key, ChemicalDamageType element, int perTick, int cap) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [perTick, cap];

    protected override bool UsesBehaviour(IChemicalSource? source) => source is null || source.ChemicalDamage > 0;

    public override void OnAfflictionTick(NPC npc, ChemicalPayload payload, ChemicalDamageType type)
    {
        if (Matches(payload))
            npc.GetGlobalNPC<ChemicalGlobalNPC>().StackShred(perTick, cap, (int)payload.Stats.AfflictionRate + 30);
    }
}

/// <summary>While the release afflicts a target, the target's concentration of the element doesn't decay.</summary>
public sealed class ConcentrationHoldModifier(string key, ChemicalDamageType element) : NamedModifier(key, element)
{
    protected override bool UsesBehaviour(IChemicalSource? source)
        => source is null || (source.ChemicalDamage > 0 && source.Concentration > 0);

    public override void OnAfflictionTick(NPC npc, ChemicalPayload payload, ChemicalDamageType type)
    {
        if (Matches(payload))
            npc.GetGlobalNPC<ChemicalGlobalNPC>().HoldConcentration(type, (int)payload.Stats.AfflictionRate + 10);
    }
}

/// <summary>
///     An enemy that dies afflicted by the release bursts, exposing every enemy within the radius. Each burst is a
///     generation deeper, so a chain of deaths stops at the generation cap.
/// </summary>
public sealed class DeathBurstModifier(string key, ChemicalDamageType element, float radius, float strength, int maxGeneration)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Number(radius / 16f), Percent(strength)];

    protected override bool UsesBehaviour(IChemicalSource? source) => source is null || source.ChemicalDamage > 0;

    public override void OnAfflictedDeath(NPC npc, ChemicalPayload payload)
    {
        if (!Matches(payload) || payload.Generation >= maxGeneration)
            return;

        ChemicalEffects.Blast(npc.Center, radius, payload.Source.ChemicalType, SoundID.NPCDeath9);

        foreach (NPC other in ChemicalTargets.Nearest(npc.Center, radius, int.MaxValue, target => target.whoAmI == npc.whoAmI))
            payload.Derived.Apply(other, ExposureKind.Hit, strength);
    }
}

#endregion

#region Reaction odds and damage

/// <summary>Hits react likelier the more concentration of the element the target carries.</summary>
public sealed class ConcentrationReactionChanceModifier(string key, float perConcentration) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [Number(perConcentration * ChemicalTuning.MaxConcentration)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.Concentration > 0);

    public override void ModifyReactionChance(NPC target, ChemicalPayload payload, ref float chance)
        => chance += perConcentration * target.GetGlobalNPC<ChemicalGlobalNPC>().ConcentrationOf(payload.Source.ChemicalType);
}

/// <summary>Each hit on a target that doesn't react makes the next one likelier to, up to the cap, until one does.</summary>
public sealed class ReactionPityModifier(string key, ChemicalDamageType element, float step, float cap) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Number(step), Number(cap)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void ModifyReactionChance(NPC target, ChemicalPayload payload, ref float chance)
    {
        if (!Matches(payload))
            return;

        ChemicalGlobalNPC chemistry = target.GetGlobalNPC<ChemicalGlobalNPC>();

        // Raised ahead of the roll; OnReaction clears it when the roll succeeds.
        chance += chemistry.ReactionPity;
        chemistry.ReactionPity = Math.Min(cap, chemistry.ReactionPity + step);
    }

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is { } payload && Matches(payload))
            e.Target.GetGlobalNPC<ChemicalGlobalNPC>().ReactionPity = 0f;
    }
}

/// <summary>
///     Reactions spend the target's concentration of the element for burst damage, up to the bonus more at full
///     concentration. The build-up goes into the hit instead of towards a cling.
/// </summary>
public sealed class ConcentrationBurstModifier(string key, ChemicalDamageType element, float bonus) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Percent(bonus)];

    protected override bool UsesBehaviour(IChemicalSource? source)
        => Reacts(source) && (source is null || (source.ReactionDamage > 0 && source.Concentration > 0));

    public override void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage)
    {
        if (!Matches(payload))
            return;

        float spent = target.GetGlobalNPC<ChemicalGlobalNPC>().SpendConcentration(payload.Source.ChemicalType) / ChemicalTuning.MaxConcentration;

        damage *= 1f + bonus * spent;

        if (spent > 0f)
            ChemicalEffects.Flare(target.Center, spent, payload.Source.ChemicalType);
    }
}

/// <summary>Reactions deal more damage for each other element afflicting the target.</summary>
public sealed class ReactionPerAfflictionModifier(string key, float perAffliction) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [Percent(perAffliction)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ReactionDamage > 0);

    public override void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage)
        => damage *= 1f + perAffliction * target.GetGlobalNPC<ChemicalGlobalNPC>().AfflictionsBesides(payload.Source.ChemicalType);
}

/// <summary>Reactions deal more damage for each other enemy near the target, up to a count.</summary>
public sealed class ReactionCrowdModifier(string key, float perEnemy, int maxEnemies, float range) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [Percent(perEnemy), Number(range / 16f), Percent(perEnemy * maxEnemies)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ReactionDamage > 0);

    public override void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage)
    {
        int crowd = ChemicalTargets.Nearest(target.Center, range, maxEnemies, npc => npc.whoAmI == target.whoAmI).Count;

        damage *= 1f + perEnemy * crowd;
    }
}

/// <summary>Reactions deal more damage the less life the target has left, up to the bonus at none.</summary>
public sealed class ReactionExecuteModifier(string key, ChemicalDamageType element, float bonus) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Percent(bonus)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ReactionDamage > 0);

    public override void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage)
    {
        if (Matches(payload) && target.lifeMax > 0)
            damage *= 1f + bonus * (1f - (float)target.life / target.lifeMax);
    }
}

/// <summary>Reactions ignore the target's defense.</summary>
public sealed class ReactionPierceModifier(string key, ChemicalDamageType element) : NamedModifier(key, element)
{
    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ReactionDamage > 0);

    public override void ModifyReactionDamage(NPC target, ChemicalPayload payload, ref float damage)
    {
        if (Matches(payload))
            target.GetGlobalNPC<ChemicalGlobalNPC>().PierceDefense = true;
    }
}

#endregion

#region Reaction effects

/// <summary>Reactions heal the releasing player for a share of the damage they deal.</summary>
public sealed class ReactionLifestealModifier(string key, float share) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [Percent(share)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ReactionDamage > 0);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.DamageDealt <= 0 || e.Owner < 0 || e.Owner != Main.myPlayer)
            return;

        Player player = Main.player[e.Owner];
        int amount = Math.Min(Math.Max(1, (int)MathF.Round(e.DamageDealt * share)), player.statLifeMax2 - player.statLife);

        if (!player.active || player.dead || amount <= 0)
            return;

        player.statLife += amount;
        player.HealEffect(amount);
    }
}

/// <summary>Reactions lower the target's defense for a while.</summary>
public sealed class ReactionDefenseShredModifier(string key, int defense, int ticks) : NamedModifier(key)
{
    protected override object[] DescriptionArgs => [defense, Number(ticks / 60f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
        => e.Target.GetGlobalNPC<ChemicalGlobalNPC>().ShredDefense(defense, ticks);
}

/// <summary>Reactions give the target <typeparamref name="TBuff" /> for a while.</summary>
public sealed class ReactionBuffModifier<TBuff>(string key, ChemicalDamageType element, int ticks) : NamedModifier(key, element)
    where TBuff : ModBuff
{
    protected override object[] DescriptionArgs => [Number(ticks / 60f)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is { } payload && Matches(payload))
            ChemicalEffects.Refresh(e.Target, ModContent.BuffType<TBuff>(), ticks);
    }
}

/// <summary>
///     Reactions freeze the target (<see cref="Frozen" />). A reaction on a frozen target shatters it instead: the freeze
///     ends and the target takes the burst again at the shatter share.
/// </summary>
public sealed class ReactionFreezeModifier(string key, ChemicalDamageType element, int ticks, float shatter) : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Number(ticks / 60f), Percent(shatter)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { } payload || !Matches(payload) || !e.Target.active)
            return;

        NPC target = e.Target;
        int frozen = ModContent.BuffType<Frozen>();

        if (target.HasBuff(frozen))
        {
            target.RequestBuffRemoval(frozen);
            ChemicalEffects.Shatter(target);
            ChemicalEffects.Strike(target, e.DamageDealt * shatter, payload.Owner);
        }
        else if (Frozen.CanFreeze(target))
            target.AddBuff(frozen, ticks);
    }
}

/// <summary>Reactions end every affliction on the target and deal the damage they had left at once.</summary>
public sealed class AfflictionDetonateModifier(string key, ChemicalDamageType element) : NamedModifier(key, element)
{
    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source) && (source is null || source.ChemicalDamage > 0);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { } payload || !Matches(payload) || !e.Target.active)
            return;

        int damage = e.Target.GetGlobalNPC<ChemicalGlobalNPC>().EndAfflictions();

        if (damage <= 0)
            return;

        ChemicalEffects.Flare(e.Target.Center, 1f, payload.Source.ChemicalType);
        ChemicalEffects.Strike(e.Target, damage, payload.Owner);
    }
}

/// <summary>
///     Reactions strike every enemy within the radius of the target for a share of the burst, and expose them to the
///     release at reduced strength. Only a release's own reactions blast.
/// </summary>
public sealed class ReactionBlastModifier(string key, ChemicalDamageType element, float radius, float damageShare, float exposure)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Number(radius / 16f), Percent(damageShare), Percent(exposure)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { IsDerived: false } payload || !Matches(payload))
            return;

        NPC target = e.Target;
        ChemicalDamageType type = payload.Source.ChemicalType;
        float damage = payload.Stats.ReactionDamage * payload.Stats.ReactionDamageMultiplier * damageShare;

        ChemicalEffects.Blast(target.Center, radius, type);

        foreach (NPC other in ChemicalTargets.Nearest(target.Center, radius, int.MaxValue, npc => npc.whoAmI == target.whoAmI))
        {
            ChemicalEffects.Strike(other, damage * ChemicalMatchup.GetReactionDamageMultiplier(type, other.GetGlobalNPC<ChemicalGlobalNPC>().Composition), payload.Owner);
            payload.Derived.Apply(other, ExposureKind.Hit, exposure);
        }
    }
}

/// <summary>
///     Reactions burn off the releasing player's gas around the target, every layer of it. The blast grows with the
///     nodes burned, to the full share of the burst at the full count, and strikes and exposes every enemy in range.
/// </summary>
public sealed class GasIgnitionModifier(string key, ChemicalDamageType element, float radius, int fullFuel, float maxShare)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [Number(radius / 16f), Percent(maxShare)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { IsDerived: false } payload || !Matches(payload))
            return;

        NPC target = e.Target;
        var fuel = new List<Vector2>();

        foreach (FieldLayer layer in FieldSystem.Layers)
        {
            if (layer is ChemicalGasField gas)
                gas.Consume(target.Center, radius, payload.Owner, fuel);
        }

        if (fuel.Count == 0)
            return;

        ChemicalDamageType type = payload.Source.ChemicalType;
        float strength = Math.Min(1f, fuel.Count / (float)fullFuel);
        float damage = payload.Stats.ReactionDamage * payload.Stats.ReactionDamageMultiplier * maxShare * strength;

        ChemicalEffects.Ignite(fuel, target.Center);

        foreach (NPC npc in ChemicalTargets.Nearest(target.Center, radius, int.MaxValue))
        {
            ChemicalEffects.Strike(npc, damage * ChemicalMatchup.GetReactionDamageMultiplier(type, npc.GetGlobalNPC<ChemicalGlobalNPC>().Composition), payload.Owner);
            payload.Derived.Apply(npc, ExposureKind.Hit, strength);
        }
    }
}

/// <summary>
///     Reactions leap from enemy to enemy, each leap reacting again at a falloff of the last. Leaps carry a derived
///     payload, so only a release's own reaction starts a chain and no chain starts another.
/// </summary>
public sealed class ReactionChainModifier(string key, ChemicalDamageType element, int leaps, float range, float falloff)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [leaps, Percent(1f - falloff)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { IsDerived: false } payload || !Matches(payload))
            return;

        var visited = new HashSet<int> { e.Target.whoAmI };
        NPC from = e.Target;
        float scale = 1f;

        for (int leap = 0; leap < leaps; leap++)
        {
            List<NPC> next = ChemicalTargets.Nearest(from.Center, range, 1, npc => visited.Contains(npc.whoAmI));

            if (next.Count == 0)
                break;

            NPC to = next[0];
            visited.Add(to.whoAmI);
            scale *= falloff;

            ChemicalEffects.Leap(from.Center, to.Center, payload.Source.ChemicalType);
            payload.Derived.Apply(to, ExposureKind.Hit, scale);
            ChemicalReaction.Trigger(to, payload.Source, payload.Derived, damageScale: scale);

            from = to;
        }
    }
}

/// <summary>Reactions arc to the nearest other enemies at once, each reacting again at a share of the burst. Arcs never arc again.</summary>
public sealed class ReactionArcModifier(string key, ChemicalDamageType element, int arcs, float range, float scale)
    : NamedModifier(key, element)
{
    protected override object[] DescriptionArgs => [arcs, Number(range / 16f), Percent(scale)];

    protected override bool UsesBehaviour(IChemicalSource? source) => Reacts(source);

    public override void OnReaction(in ChemicalReactionEvent e)
    {
        if (e.Payload is not { IsDerived: false } payload || !Matches(payload))
            return;

        NPC target = e.Target;

        foreach (NPC to in ChemicalTargets.Nearest(target.Center, range, arcs, npc => npc.whoAmI == target.whoAmI))
        {
            ChemicalEffects.Arc(target.Center, to.Center, payload.Source.ChemicalType);
            payload.Derived.Apply(to, ExposureKind.Hit, scale);
            ChemicalReaction.Trigger(to, payload.Source, payload.Derived, damageScale: scale);
        }
    }
}

#endregion

/// <summary>Enemies around a point, for behaviours that spread to them.</summary>
public static class ChemicalTargets
{
    /// <summary>Up to <paramref name="count" /> chaseable enemies within <paramref name="range" />, nearest first, leaving out <paramref name="exclude" />.</summary>
    public static List<NPC> Nearest(Vector2 center, float range, int count, Predicate<NPC>? exclude = null)
    {
        var found = new List<(NPC Npc, float DistanceSquared)>();
        float rangeSquared = range * range;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc is not { active: true, life: > 0 } || npc.friendly || !npc.CanBeChasedBy() || exclude?.Invoke(npc) == true)
                continue;

            float distanceSquared = Vector2.DistanceSquared(center, npc.Center);

            if (distanceSquared <= rangeSquared)
                found.Add((npc, distanceSquared));
        }

        found.Sort(static (a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));

        var nearest = new List<NPC>(Math.Min(count, found.Count));

        for (int i = 0; i < found.Count && i < count; i++)
            nearest.Add(found[i].Npc);

        return nearest;
    }
}

/// <summary>Strikes, buffs, sound and dust for the behaviours. Dust is skipped on a dedicated server.</summary>
internal static class ChemicalEffects
{
    /// <summary>A controller-damage strike credited to <paramref name="owner" />. Nothing for non-positive damage.</summary>
    public static void Strike(NPC npc, float damage, int owner)
    {
        if (damage <= 0f || !npc.active)
            return;

        int dealt = npc.SimpleStrikeNPC(
            RoundDamage(damage),
            hitDirection: 0,
            crit: false,
            knockBack: 0f,
            damageType: ModContent.GetInstance<ChemicalDamageClass>(),
            noPlayerInteraction: owner != Main.myPlayer);

        DamageMeterSystem.Credit(owner, npc, dealt, DamageMeterSource.Hit);
    }

    /// <summary>
    ///     Rounds up with odds equal to the fraction, so a small bonus on a small number keeps its average instead of
    ///     truncating away. At least 1.
    /// </summary>
    public static int RoundDamage(float damage)
    {
        int whole = (int)damage;

        return Math.Max(1, Main.rand.NextFloat() < damage - whole ? whole + 1 : whole);
    }

    /// <summary>Adds the buff, or tops it up once under half of <paramref name="ticks" />, to spare a sync every exposure.</summary>
    public static void Refresh(NPC npc, int buffType, int ticks)
    {
        int index = npc.FindBuffIndex(buffType);

        if (index < 0 || npc.buffTime[index] < ticks / 2)
            npc.AddBuff(buffType, ticks);
    }

    /// <summary>A faint line of dust between two enemies an exposure passed between.</summary>
    public static void Link(Vector2 from, Vector2 to, ChemicalDamageType type)
        => Line(from, to, type, spacing: 14f, scale: 0.8f, jitter: 2f);

    /// <summary>A bright line of dust and a sound for a reaction leaping between enemies.</summary>
    public static void Leap(Vector2 from, Vector2 to, ChemicalDamageType type)
    {
        SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, PitchVariance = 0.25f, MaxInstances = 4 }, to);
        Line(from, to, type, spacing: 8f, scale: 1.3f, jitter: 2f);
    }

    /// <summary>A jagged line of dust and a crackle for a reaction arcing between enemies.</summary>
    public static void Arc(Vector2 from, Vector2 to, ChemicalDamageType type)
    {
        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, PitchVariance = 0.3f, MaxInstances = 4 }, to);
        Line(from, to, type, spacing: 6f, scale: 1.1f, jitter: 7f);
    }

    /// <summary>A sound, a light and a ring of dust as wide as <paramref name="radius" />. An explosion unless <paramref name="sound" /> says otherwise.</summary>
    public static void Blast(Vector2 center, float radius, ChemicalDamageType type, SoundStyle? sound = null)
    {
        SoundEngine.PlaySound((sound ?? SoundID.Item14) with { Volume = 0.8f, PitchVariance = 0.2f, MaxInstances = 4 }, center);

        if (Main.dedServ)
            return;

        (int dustType, Color color) = DustFor(type);
        Lighting.AddLight(center, color.ToVector3() * 1.5f);

        for (int i = 0; i < 48; i++)
        {
            Vector2 direction = Main.rand.NextVector2Unit();
            Vector2 velocity = direction * Main.rand.NextFloat(0.2f, 1f) * radius / 12f;

            Dust dust = Dust.NewDustPerfect(center + direction * 6f, dustType, velocity, 0, color, Main.rand.NextFloat(1.1f, 1.8f));
            dust.noGravity = true;
        }
    }

    /// <summary>A burst of dust on the target that grows with <paramref name="intensity" />, 0 to 1.</summary>
    public static void Flare(Vector2 center, float intensity, ChemicalDamageType type)
    {
        if (Main.dedServ)
            return;

        (int dustType, Color color) = DustFor(type);
        int count = 6 + (int)(24 * intensity);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2f + 4f * intensity);

            Dust dust = Dust.NewDustPerfect(center, dustType, velocity, 0, color, 1f + intensity);
            dust.noGravity = true;
        }
    }

    /// <summary>Flames where each burned node was, and a roar at the centre.</summary>
    public static void Ignite(List<Vector2> fuel, Vector2 center)
    {
        SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.9f, MaxInstances = 3 }, center);

        if (Main.dedServ)
            return;

        foreach (Vector2 position in fuel)
        {
            Lighting.AddLight(position, 0.9f, 0.45f, 0.1f);

            for (int i = 0; i < 3; i++)
            {
                Dust dust = Dust.NewDustPerfect(position, DustID.Torch, Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY, 0, default, 1.7f);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>Ice breaking off a frozen target.</summary>
    public static void Shatter(NPC npc)
    {
        SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, PitchVariance = 0.2f, MaxInstances = 4 }, npc.Center);

        if (Main.dedServ)
            return;

        for (int i = 0; i < 24; i++)
        {
            Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Ice, 0f, 0f, 0, default, 1.3f);
            dust.velocity = Main.rand.NextVector2Circular(5f, 5f);
        }
    }

    private static void Line(Vector2 from, Vector2 to, ChemicalDamageType type, float spacing, float scale, float jitter)
    {
        if (Main.dedServ)
            return;

        (int dustType, Color color) = DustFor(type);
        int count = Math.Clamp((int)(Vector2.Distance(from, to) / spacing), 2, 40);

        for (int i = 0; i <= count; i++)
        {
            Vector2 position = Vector2.Lerp(from, to, i / (float)count) + Main.rand.NextVector2Circular(jitter, jitter);

            Dust dust = Dust.NewDustPerfect(position, dustType, Main.rand.NextVector2Circular(0.5f, 0.5f), 0, color, scale);
            dust.noGravity = true;
        }
    }

    private static (int DustType, Color Color) DustFor(ChemicalDamageType type)
        => type == ChemicalDamageType.Thermal ? (DustID.Torch, Color.White) : (DustID.RainbowMk2, ElementDisplay.LabelColor(type));
}
