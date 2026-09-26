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

    public static void Register(int npcType, EnemyComposition composition)
        => _registry[npcType] = composition;

    public float ConcentrationOf(ChemicalDamageType type) => afflictions[(int)type].Concentration;

    public bool IsClinging(ChemicalDamageType type) => clings[(int)type].IsActive;

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
    ///     One delivery of <paramref name="source" />: its catalysis, then its affliction and concentration amplified by
    ///     the target's catalysis, then a cling roll. A contact inside the contact interval of the element is ignored.
    /// </summary>
    public void Expose(NPC npc, IChemicalSource source, ChemicalPayload? payload, int owner, float strength, ExposureKind kind, ChemicalGasField? medium)
    {
        ChemicalDamageType type = source.ChemicalType;

        if (type == ChemicalDamageType.None)
            return;

        ref AfflictionState state = ref afflictions[(int)type];

        if (kind == ExposureKind.Contact)
        {
            if (state.ContactCooldown > 0)
                return;

            state.ContactCooldown = ChemicalTuning.ContactInterval;
        }

        anyActive = true;

        float share = kind == ExposureKind.Contact ? strength * ChemicalTuning.ContactShare : strength;
        float amplify = 1f;

        // A catalyst is never amplified by catalysis, its own included.
        if (source.Catalysis > 0f)
            Catalyze(source.Catalysis * share, medium);
        else
            amplify = CatalysisMultiplier;

        if (source.ChemicalDamage > 0)
        {
            float damage = source.ChemicalDamage * strength * (payload?.DamageMultiplier ?? 1f) * amplify;
            int duration = (int)(source.AfflictionDuration * (payload?.DurationMultiplier ?? 1f));

            Afflict(ref state, type, damage, source.AfflictionRate, duration, owner);
        }

        if (source.Concentration <= 0)
            return;

        state.Concentration = Math.Min(ChemicalTuning.MaxConcentration, state.Concentration + source.Concentration * share * amplify);
        state.ConcentrationHold = ChemicalTuning.ConcentrationHold;

        if (!clings[(int)type].IsActive && Main.rand.NextFloat() < ChemicalTuning.ClingChance(state.Concentration))
            AttachCling(npc, type, source.ClingBehaviour ?? medium?.ClingBehaviour ?? ClingBehaviour.Default, medium, payload, owner);
    }

    /// <summary>An affliction outside any delivery, such as a cling's own damage. Matchup and catalysis apply.</summary>
    public void ApplyAffliction(ChemicalDamageType type, float damagePerTick, int duration, float rate, int owner)
    {
        if (type == ChemicalDamageType.None)
            return;

        anyActive = true;

        Afflict(ref afflictions[(int)type], type, damagePerTick * CatalysisMultiplier, rate, duration, owner);
    }

    private void Afflict(ref AfflictionState state, ChemicalDamageType type, float damagePerTick, float rate, int duration, int owner)
    {
        float damage = damagePerTick * ChemicalMatchup.GetAfflictionMultiplier(type, Composition);

        // The strongest application by damage over time sets the damage and takes the credit; any application can
        // extend the time.
        if (!state.IsActive || damage / Math.Max(rate, 1f) >= state.DamagePerTick / Math.Max(state.Rate, 1f))
        {
            state.DamagePerTick = damage;
            state.Rate = rate;
            state.Owner = owner;
        }

        state.TicksRemaining = Math.Max(state.TicksRemaining, duration);
    }

    /// <summary>
    ///     Attaches <paramref name="behaviour" /> as the cling for <paramref name="type" />, or extends it if it is already
    ///     attached. Any other behaviour is replaced, except that the default never displaces one. Keeps the affliction
    ///     running for the cling's duration. A non-positive <paramref name="ticks" /> takes the behaviour's own duration.
    /// </summary>
    public void AttachCling(NPC npc, ChemicalDamageType type, ClingBehaviour behaviour, ChemicalGasField? medium, ChemicalPayload? payload, int owner, int ticks = 0)
    {
        if (type == ChemicalDamageType.None)
            return;

        ref AfflictionState state = ref afflictions[(int)type];
        ref ClingState cling = ref clings[(int)type];

        if (cling.IsActive && cling.Behaviour != behaviour && behaviour == ClingBehaviour.Default)
            return;

        int duration = ticks > 0 ? ticks : behaviour.Duration(state.Concentration);

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

        cling = new ClingState
        {
            Behaviour = behaviour,
            Element = type,
            Medium = medium,
            Payload = payload,
            Owner = owner,
            TicksRemaining = duration,
            MaxTicks = duration,
        };

        behaviour.OnAttach(npc, ref cling);
    }

    /// <summary>Adds catalysis, capped at 1, and restarts its hold. <paramref name="medium" /> is drawn over the host.</summary>
    public void Catalyze(float amount, ChemicalGasField? medium)
    {
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

        bool active = ReactionActive;

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
                    DealTick(npc, in state);
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
            if (clings[i].IsActive)
                Detach(npc, ref clings[i], hostKilled: true);
        }
    }

    private static void Detach(NPC npc, ref ClingState cling, bool hostKilled)
    {
        ClingBehaviour? behaviour = cling.Behaviour;

        behaviour?.OnDetach(npc, ref cling, hostKilled);
        cling = default;
    }

    private static void DealTick(NPC npc, in AfflictionState state)
    {
        if (!npc.active || npc.immortal || npc.dontTakeDamage || !ChemicalPayload.IsAuthority(state.Owner))
            return;

        npc.SimpleStrikeNPC(
            Math.Max(1, (int)state.DamagePerTick),
            hitDirection: 0,
            crit: false,
            knockBack: 0f,
            damageType: ModContent.GetInstance<ChemicalDamageClass>(),
            noPlayerInteraction: state.Owner != Main.myPlayer);
    }

    private void UpdateCatalysis(NPC npc)
    {
        if (Catalysis <= 0f)
            return;

        if (catalysisHold > 0)
            catalysisHold--;
        else
            Catalysis = Math.Max(0f, Catalysis - ChemicalTuning.CatalysisDecayPerTick);

        if (catalystMedium is { } medium && Main.rand.NextFloat() < Catalysis * 0.5f)
            medium.ShedOnto(npc, Catalysis);
    }
}
