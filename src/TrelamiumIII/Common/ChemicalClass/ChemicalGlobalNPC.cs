using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

public enum ExposureKind : byte
{
    /// <summary>A projectile or burst arm landing: the source's full concentration, never rate limited.</summary>
    Hit,

    /// <summary>Gas or residue: a share of the concentration, at most once per contact interval per element.</summary>
    Contact,
}

/// <summary>
///     One application of chemistry to an NPC: the element and the numbers it applies with, what it does if it clings,
///     whose it is, how strong, and the gas a cling would be made of. <see cref="Payload" /> is null for ambient chemistry.
/// </summary>
public readonly record struct ChemicalDelivery(
    ChemicalDamageType Type,
    ChemicalStats Stats,
    ClingBehaviour? ClingBehaviour,
    ChemicalPayload? Payload,
    int Owner,
    float Strength,
    ExposureKind Kind,
    ChemicalGasField? Medium);

/// <summary>One element's affliction and concentration on an NPC.</summary>
public struct AfflictionState
{
    public float DamagePerTick;

    /// <summary>Ticks between damage ticks.</summary>
    public float Rate;

    public int TicksRemaining;

    public int Timer;

    /// <summary>The player credited with the damage, and whose machine deals it. -1 for none.</summary>
    public int Owner;

    /// <summary>The release credited with the affliction, whose modifiers see its ticks. Null for ambient chemistry.</summary>
    public ChemicalPayload? Payload;

    /// <summary>Hidden build-up, 0 to <see cref="ChemicalTuning.MaxConcentration" />. Drives the cling chance.</summary>
    public float Concentration;

    public int ConcentrationHold;

    public int ContactCooldown;

    public readonly bool IsActive => TicksRemaining > 0;
}

/// <summary>
///     Per-NPC chemistry: composition, and per element an affliction, a concentration and a cling, plus catalysis and
///     reaction state. Deliveries are recorded only by the authority for their owner, so each tick is dealt once.
/// </summary>
public class ChemicalGlobalNPC : GlobalNPC
{
    private static readonly int ElementCount = Enum.GetValues<ChemicalDamageType>().Length;

    private static readonly Dictionary<int, EnemyComposition> _registry = [];

    private readonly AfflictionState[] afflictions = new AfflictionState[ElementCount];

    private readonly ClingState[] clings = new ClingState[ElementCount];

    private int catalysisHold;

    private ChemicalGasField? catalystMedium;

    /// <summary>Lives as long as the current catalysis. What it regenerates is derived under it.</summary>
    private ChemicalScope? catalysisScope;

    /// <summary>Per source payload, the payload this catalysis regenerates its gas with.</summary>
    private readonly Dictionary<ChemicalPayload, ChemicalPayload> regenerated = [];

    private int shredDefense;

    private int shredTicks;

    /// <summary>False lets <see cref="PostAI" /> skip an NPC with no chemistry on it.</summary>
    private bool anyActive;

    public override bool InstancePerEntity => true;

    public EnemyComposition Composition { get; private set; }

    /// <summary>0 to 1. Amplifies every delivery that is not itself a catalyst.</summary>
    public float Catalysis { get; private set; }

    public float CatalysisMultiplier => 1f + Catalysis * ChemicalTuning.CatalysisStrength;

    public bool ReactionActive { get; private set; }
    public int ReactionTicksRemaining { get; private set; }
    public int ReactionDefenseShred { get; private set; }

    /// <summary>Reaction chance, in percent, built up on this NPC by <see cref="ReactionPityModifier" />.</summary>
    public float ReactionPity { get; set; }

    /// <summary>Makes the next strike on this NPC ignore defense. <see cref="ChemicalReaction.Trigger" /> clears it after its burst.</summary>
    public bool PierceDefense { get; set; }

    public static void Register(int npcType, EnemyComposition composition)
        => _registry[npcType] = composition;

    public float ConcentrationOf(ChemicalDamageType type) => afflictions[(int)type].Concentration;

    public bool IsAfflicted(ChemicalDamageType type) => afflictions[(int)type].IsActive;

    public bool IsClinging(ChemicalDamageType type) => clings[(int)type].IsActive;

    /// <summary>How many elements other than <paramref name="except" /> are afflicting the NPC.</summary>
    public int AfflictionsBesides(ChemicalDamageType except)
    {
        int count = 0;

        for (int i = 0; i < ElementCount; i++)
        {
            if (i != (int)except && afflictions[i].IsActive)
                count++;
        }

        return count;
    }

    /// <summary>Removes and returns the NPC's concentration of <paramref name="type" />.</summary>
    public float SpendConcentration(ChemicalDamageType type)
    {
        ref AfflictionState state = ref afflictions[(int)type];
        float spent = state.Concentration;

        state.Concentration = 0f;

        return spent;
    }

    /// <summary>Lowers defense by <paramref name="defense" /> for <paramref name="ticks" />. Overlaps keep the larger of each.</summary>
    public void ShredDefense(int defense, int ticks)
    {
        shredDefense = Math.Max(shredDefense, defense);
        shredTicks = Math.Max(shredTicks, ticks);
        anyActive = true;
    }

    /// <summary>Adds <paramref name="defense" /> to the shred, up to <paramref name="cap" />, and holds it at least <paramref name="ticks" />.</summary>
    public void StackShred(int defense, int cap, int ticks)
    {
        shredDefense = Math.Max(shredDefense, Math.Min(cap, shredDefense + defense));
        shredTicks = Math.Max(shredTicks, ticks);
        anyActive = true;
    }

    /// <summary>Keeps the concentration of <paramref name="type" /> from decaying for at least <paramref name="ticks" />.</summary>
    public void HoldConcentration(ChemicalDamageType type, int ticks)
    {
        ref AfflictionState state = ref afflictions[(int)type];
        state.ConcentrationHold = Math.Max(state.ConcentrationHold, ticks);
    }

    /// <summary>Ends every affliction on the NPC and returns the damage their remaining ticks would have dealt.</summary>
    public int EndAfflictions()
    {
        int total = 0;

        for (int i = 0; i < ElementCount; i++)
        {
            ref AfflictionState state = ref afflictions[i];

            if (!state.IsActive)
                continue;

            int ticks = (int)((state.Timer + state.TicksRemaining) / Math.Max(state.Rate, 1f));

            total += ticks * Math.Max(1, (int)state.DamagePerTick);
            state.TicksRemaining = 0;
            state.Timer = 0;
        }

        return total;
    }

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

    /// <summary>
    ///     Applies <paramref name="delivery" />: its catalysis, then its affliction and concentration amplified by the
    ///     target's catalysis, then a cling roll if it is a release. Returns false when nothing applied: chemistry whose
    ///     emitter has ended, a cling's own gas on its host, or a contact inside the element's contact interval.
    /// </summary>
    public bool Expose(NPC npc, in ChemicalDelivery delivery)
    {
        ChemicalDamageType type = delivery.Type;
        ChemicalPayload? payload = delivery.Payload;

        if (type == ChemicalDamageType.None || !npc.active || payload is { IsActive: false } || payload?.Host == npc.whoAmI)
            return false;

        ref AfflictionState state = ref afflictions[(int)type];
        ChemicalStats stats = delivery.Stats;

        if (delivery.Kind == ExposureKind.Contact)
        {
            if (state.ContactCooldown > 0)
                return false;

            state.ContactCooldown = ChemicalTuning.ContactTicks(stats.ContactRateMultiplier);
        }

        anyActive = true;

        float strength = delivery.Strength;
        float share = delivery.Kind == ExposureKind.Contact ? strength * ChemicalTuning.ContactShare : strength;
        float amplify = 1f;

        // A catalyst is never amplified by catalysis, its own included.
        if (stats.Catalysis > 0f)
            Catalyze(stats.Catalysis * share, delivery.Medium);
        else
        {
            amplify = CatalysisMultiplier;

            if (delivery.Kind == ExposureKind.Contact)
                Regenerate(npc, in delivery);
        }

        if (stats.ChemicalDamage > 0)
        {
            float damage = stats.ChemicalDamage * strength * stats.DamageMultiplier * amplify;
            int duration = (int)(stats.AfflictionDuration * stats.DurationMultiplier);

            Afflict(ref state, type, damage, stats.AfflictionRate, duration, delivery.Owner, payload);
        }

        if (stats.Concentration > 0)
            BuildUp(npc, ref state, in delivery, stats.Concentration * share * amplify);

        if (payload is not null)
        {
            foreach (ChemicalModifier modifier in payload.Modifiers)
                modifier.OnExpose(npc, payload, in delivery);
        }

        return true;
    }

    /// <summary>Adds <paramref name="amount" /> concentration, then rolls a cling if the delivery is a release.</summary>
    private void BuildUp(NPC npc, ref AfflictionState state, in ChemicalDelivery delivery, float amount)
    {
        ChemicalDamageType type = delivery.Type;

        state.Concentration = Math.Min(ChemicalTuning.MaxConcentration, state.Concentration + amount);
        state.ConcentrationHold = Math.Max(state.ConcentrationHold, ChemicalTuning.ConcentrationHold);

        // Only a release clings by build-up, so gas a cling or catalysis emitted can't start another cling.
        if (delivery.Payload is { IsDerived: true }
            || clings[(int)type].IsActive
            || Main.rand.NextFloat() >= ChemicalTuning.ClingChance(state.Concentration) * delivery.Stats.ClingChanceMultiplier)
            return;

        ClingBehaviour behaviour = delivery.ClingBehaviour ?? delivery.Medium?.ClingBehaviour ?? ClingBehaviour.Default;
        int ticks = (int)(behaviour.Duration(state.Concentration) * delivery.Stats.ClingDurationMultiplier);

        AttachCling(npc, type, behaviour, delivery.Medium, delivery.Payload, delivery.Owner, ticks);
    }

    /// <summary>
    ///     Catalysis regrows the gas a contact came from: a node of it around the host, at odds equal to the catalysis.
    ///     The node carries the source's chemistry derived under the catalysis, so it stops applying when that ends.
    /// </summary>
    private void Regenerate(NPC npc, in ChemicalDelivery delivery)
    {
        if (catalysisScope is not { } scope || delivery.Medium is not { } medium || delivery.Payload is not { } source || Main.rand.NextFloat() >= Catalysis)
            return;

        // Gas this catalysis already regrew carries on as itself rather than deriving again.
        ChemicalPayload emission = source.Scope == scope ? source : RegeneratedFrom(source, scope);

        Vector2 position = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
        Vector2 velocity = npc.velocity * 0.5f + Main.rand.NextVector2Circular(1f, 1f);

        medium.SpawnNode(
            position,
            velocity,
            ChemicalTuning.RegeneratedRadius.At(Catalysis),
            ChemicalTuning.RegeneratedLife.At(Catalysis),
            owner: delivery.Owner,
            payload: emission);
    }

    private ChemicalPayload RegeneratedFrom(ChemicalPayload source, ChemicalScope scope)
    {
        if (!regenerated.TryGetValue(source, out ChemicalPayload? emission))
            regenerated[source] = emission = source.Derive(scope);

        return emission;
    }

    /// <summary>An affliction outside any delivery, such as a cling's own damage. Matchup and catalysis apply.</summary>
    public void ApplyAffliction(ChemicalDamageType type, float damagePerTick, int duration, float rate, int owner, ChemicalPayload? payload = null)
    {
        if (type == ChemicalDamageType.None)
            return;

        anyActive = true;

        Afflict(ref afflictions[(int)type], type, damagePerTick * CatalysisMultiplier, rate, duration, owner, payload);
    }

    private void Afflict(ref AfflictionState state, ChemicalDamageType type, float damagePerTick, float rate, int duration, int owner, ChemicalPayload? payload)
    {
        float damage = damagePerTick * ChemicalMatchup.GetAfflictionMultiplier(type, Composition);

        // The strongest application by damage over time sets the damage and takes the credit; any application can
        // extend the time.
        if (!state.IsActive || damage / Math.Max(rate, 1f) >= state.DamagePerTick / Math.Max(state.Rate, 1f))
        {
            state.DamagePerTick = damage;
            state.Rate = rate;
            state.Owner = owner;
            state.Payload = payload;
        }

        state.TicksRemaining = Math.Max(state.TicksRemaining, duration);
    }

    /// <summary>
    ///     Attaches <paramref name="behaviour" /> as the cling for <paramref name="type" />, or extends it if it is already
    ///     attached. Any other behaviour is replaced, except that the default never displaces one. Keeps the affliction
    ///     running for the cling's duration. A non-positive <paramref name="ticks" /> takes the behaviour's own duration,
    ///     scaled by the payload's cling duration multiplier.
    /// </summary>
    /// <remarks>The cling emits <paramref name="payload" /> derived under its own scope, so its gas stops with it and never touches the host.</remarks>
    public void AttachCling(NPC npc, ChemicalDamageType type, ClingBehaviour behaviour, ChemicalGasField? medium, ChemicalPayload? payload, int owner, int ticks = 0)
    {
        if (type == ChemicalDamageType.None)
            return;

        ref AfflictionState state = ref afflictions[(int)type];
        ref ClingState cling = ref clings[(int)type];

        if (cling.IsActive && cling.Behaviour != behaviour && behaviour == ClingBehaviour.Default)
            return;

        int duration = ticks > 0
            ? ticks
            : (int)(behaviour.Duration(state.Concentration) * (payload?.Stats.ClingDurationMultiplier ?? 1f));

        if (state.IsActive)
            state.TicksRemaining = Math.Max(state.TicksRemaining, duration);

        anyActive = true;

        if (cling.IsActive)
        {
            if (cling.Behaviour == behaviour)
            {
                cling.TicksRemaining = Math.Max(cling.TicksRemaining, duration);
                cling.MaxTicks = Math.Max(cling.MaxTicks, cling.TicksRemaining);
                return;
            }

            Detach(npc, ref cling, hostKilled: false);
        }

        var scope = new ChemicalScope();

        cling = new ClingState
        {
            Behaviour = behaviour,
            Element = type,
            Medium = medium,
            Payload = (payload ?? ChemicalPayload.Create(medium))?.Derive(scope, npc.whoAmI),
            Scope = scope,
            Owner = owner,
            TicksRemaining = duration,
            MaxTicks = duration,
        };

        behaviour.OnAttach(npc, ref cling);
    }

    /// <summary>Adds catalysis, capped at 1, and restarts its hold. <paramref name="medium" /> is drawn over the host.</summary>
    public void Catalyze(float amount, ChemicalGasField? medium)
    {
        catalysisScope ??= new ChemicalScope();
        Catalysis = Math.Min(1f, Catalysis + amount);
        catalysisHold = ChemicalTuning.CatalysisHold;
        catalystMedium = medium ?? catalystMedium;
        anyActive = true;
    }

    public void ApplyReaction(ChemicalDamageType source)
    {
        ReactionActive = true;
        ReactionTicksRemaining = 180;
        ReactionDefenseShred = ChemicalMatchup.GetReactionDefenseShred(source, Composition);
        anyActive = true;
    }

    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        if (ReactionActive && ReactionDefenseShred > 0)
        {
            modifiers.Defense.Flat -= ReactionDefenseShred;
            modifiers.DamageVariationScale *= 1.5f;
        }

        if (shredTicks > 0)
            modifiers.Defense.Flat -= shredDefense;

        if (PierceDefense)
            modifiers.DefenseEffectiveness *= 0f;
    }

    public override void PostAI(NPC npc)
    {
        if (!anyActive)
            return;

        if (ReactionActive && --ReactionTicksRemaining <= 0)
        {
            ReactionActive = false;
            ReactionDefenseShred = 0;
        }

        if (shredTicks > 0 && --shredTicks <= 0)
            shredDefense = 0;

        bool active = ReactionActive || shredTicks > 0;

        for (int i = 0; i < ElementCount; i++)
        {
            ref AfflictionState state = ref afflictions[i];

            if (state.ContactCooldown > 0)
                state.ContactCooldown--;

            if (state.IsActive)
            {
                if (++state.Timer >= state.Rate)
                {
                    state.Timer = 0;

                    if (DealTick(npc, in state) && npc.active && state.Payload is { IsActive: true } payload)
                    {
                        foreach (ChemicalModifier modifier in payload.Modifiers)
                            modifier.OnAfflictionTick(npc, payload, (ChemicalDamageType)i);
                    }
                }

                state.TicksRemaining--;
            }

            if (state.Concentration > 0f)
            {
                if (state.ConcentrationHold > 0)
                    state.ConcentrationHold--;
                else
                    state.Concentration = Math.Max(0f, state.Concentration - ChemicalTuning.ConcentrationDecayPerTick);
            }

            ref ClingState cling = ref clings[i];

            if (cling.IsActive)
            {
                cling.Age++;
                cling.Behaviour!.Update(npc, ref cling);

                if (--cling.TicksRemaining <= 0)
                    Detach(npc, ref cling, hostKilled: false);
            }

            active |= state.IsActive || state.Concentration > 0f || state.ContactCooldown > 0 || cling.IsActive;
        }

        UpdateCatalysis(npc);

        anyActive = active || Catalysis > 0f;
    }

    /// <summary>Death is read here rather than in OnKill, which multiplayer clients never run.</summary>
    public override void HitEffect(NPC npc, NPC.HitInfo hit)
    {
        if (npc.life > 0)
            return;

        for (int i = 0; i < ElementCount; i++)
        {
            if (afflictions[i] is not { IsActive: true, Payload: { IsActive: true } payload })
                continue;

            foreach (ChemicalModifier modifier in payload.Modifiers)
                modifier.OnAfflictedDeath(npc, payload);
        }

        for (int i = 0; i < ElementCount; i++)
        {
            if (clings[i].IsActive)
                Detach(npc, ref clings[i], hostKilled: true);
        }

        EndCatalysis();
    }

    /// <summary>Ends the cling's scope after its detach hook, so what it emitted stops applying.</summary>
    private static void Detach(NPC npc, ref ClingState cling, bool hostKilled)
    {
        cling.Behaviour?.OnDetach(npc, ref cling, hostKilled);
        cling.Scope?.End();
        cling = default;
    }

    private void EndCatalysis()
    {
        catalysisScope?.End();
        catalysisScope = null;
        regenerated.Clear();
    }

    /// <returns>Whether this machine dealt the tick.</returns>
    private static bool DealTick(NPC npc, in AfflictionState state)
    {
        if (!npc.active || npc.immortal || npc.dontTakeDamage || !ChemicalPayload.IsAuthority(state.Owner))
            return false;

        npc.SimpleStrikeNPC(
            Math.Max(1, (int)state.DamagePerTick),
            hitDirection: 0,
            crit: false,
            knockBack: 0f,
            damageType: ModContent.GetInstance<ChemicalDamageClass>(),
            noPlayerInteraction: state.Owner != Main.myPlayer);

        return true;
    }

    private void UpdateCatalysis(NPC npc)
    {
        if (Catalysis <= 0f)
            return;

        if (catalysisHold > 0)
            catalysisHold--;
        else
            Catalysis = Math.Max(0f, Catalysis - ChemicalTuning.CatalysisDecayPerTick);

        if (Catalysis <= 0f)
        {
            EndCatalysis();
            return;
        }

        if (catalystMedium is { } medium && Main.rand.NextFloat() < Catalysis * 0.5f)
            medium.ShedOnto(npc, Catalysis);
    }
}
