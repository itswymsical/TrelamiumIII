using TrelamiumIII.Core.NPCs.Behaviors;
using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Core.NPCs;

/// <remarks>
/// Threats are found with vanilla's danger test (<see cref="Threats" />). An armed NPC fights them: it closes to
/// <see cref="CombatStyle.PreferredRange" /> and uses <see cref="CombatWeapon" /> on cooldown. An unarmed one that can die
/// runs. Hits and projectiles resolve on the server; clients replay each use from the synced <see cref="TownState.Attack" />.
/// </remarks>
public abstract partial class WorldNPC
{
    /// <summary>Ticks without a threat before a fight or flight ends.</summary>
    private const int CalmTimeout = 60;

    /// <summary>How far ahead of a threat a fleeing NPC aims, in pixels.</summary>
    private const float FleeDistance = 160f;

    private int calmTicks;

    /// <summary>Item used against threats. 0 is unarmed.</summary>
    public virtual int CombatWeapon => 0;

    /// <summary>Whether an unarmed NPC runs from threats. Defaults to NPCs that can die.</summary>
    protected virtual bool FleesThreats => CanDespawn;

    /// <summary>How close a threat must come before an unarmed NPC runs. Vanilla's default danger range.</summary>
    protected virtual float FleeRange => 200f;

    protected virtual float CombatSpeed => BehaviorMovement.DefaultWalkSpeed;

    /// <summary>Ranges and cadence for <see cref="CombatWeapon" />.</summary>
    protected virtual CombatStyle GetCombatStyle() => CombatStyle.For(CombatWeapon);

    /// <summary>
    /// Starts one use of <see cref="CombatWeapon" /> aimed at <paramref name="aimAt" />, on every side. False when this
    /// NPC has no way to attack, which ends the attack.
    /// </summary>
    protected virtual bool BeginAttack(Vector2 aimAt) => false;

    /// <summary>The hostile NPC in <c>ai[2]</c>, or null.</summary>
    private NPC? Target => NPCAt(NPC.ai[2]) is { friendly: false, life: > 0 } target ? target : null;

    private int AttackDuration => Math.Max(1, ContentSamples.ItemsByType[CombatWeapon].useAnimation);

    /// <summary>Vanilla's danger check: starts a fight or a flight when a threat is in range.</summary>
    private void CheckThreats()
    {
        if (State is TownState.Fight or TownState.Attack or TownState.Flee)
            return;

        bool armed = CombatWeapon > 0;

        if (!armed && !FleesThreats)
            return;

        float anchorX = AnchorTile.X * 16f + 8f;
        CombatStyle style = GetCombatStyle();

        NPC? threat = armed
            ? Threats.Nearest(NPC, style.DetectRange, other => InLeash(other, anchorX, style))
            : Threats.Nearest(NPC, FleeRange);

        if (threat == null)
            return;

        EndChat();

        if (armed)
            SetState(TownState.Fight, Main.rand.Next(20), threat.whoAmI, anchorX);
        else
            SetState(TownState.Flee, 0f, threat.whoAmI);
    }

    /// <summary>Within reach of a weapon held at the edge of the leash around <paramref name="originX" />.</summary>
    private static bool InLeash(NPC threat, float originX, CombatStyle style)
        => style.LeashRange <= 0f || Math.Abs(threat.Center.X - originX) <= style.LeashRange + style.StrikeRange;

    private void TickFight()
    {
        CombatStyle style = GetCombatStyle();
        NPC? target = Authority ? Retarget(style) : Target;

        if (target == null)
        {
            BehaviorMovement.Decelerate(NPC);

            if (Authority && ++calmTicks >= CalmTimeout)
                Stand(60 + Main.rand.Next(60));

            return;
        }

        calmTicks = 0;
        FaceToward(target.Center);

        float distance = Vector2.Distance(NPC.Center, target.Center);
        Reposition(target, distance, style);
        NPC.ai[1]--;

        if (Authority && NPC.ai[1] <= 0f && distance <= style.StrikeRange && NPC.velocity.Y == 0f)
            SetState(TownState.Attack, AttackDuration, target.whoAmI, NPC.ai[3]);
    }

    /// <summary>The nearest threat inside the leash, written to <c>ai[2]</c>.</summary>
    private NPC? Retarget(CombatStyle style)
    {
        float originX = NPC.ai[3];
        NPC? nearest = Threats.Nearest(NPC, style.DetectRange, other => InLeash(other, originX, style));

        if (nearest != null && nearest.whoAmI != (int)NPC.ai[2])
        {
            NPC.ai[2] = nearest.whoAmI;
            NPC.netUpdate = true;
        }

        return nearest;
    }

    private void Reposition(NPC target, float distance, CombatStyle style)
    {
        float awayX = NPC.Center.X + Math.Sign(NPC.Center.X - target.Center.X) * style.RetreatRange;

        if (style.RetreatRange > 0f && distance < style.RetreatRange && WithinLeash(awayX, style))
        {
            BehaviorMovement.WalkTowardX(NPC, awayX, CombatSpeed);
            FaceToward(target.Center);
            Hop();
        }
        else if (distance > style.PreferredRange && WithinLeash(target.Center.X, style))
        {
            BehaviorMovement.WalkTowardX(NPC, target.Center.X, CombatSpeed, stopDistance: style.PreferredRange);
            Hop();
        }
        else
        {
            BehaviorMovement.Decelerate(NPC);
        }
    }

    /// <summary>False when walking toward <paramref name="x" /> takes the NPC further past the leash.</summary>
    private bool WithinLeash(float x, CombatStyle style)
    {
        if (style.LeashRange <= 0f)
            return true;

        float offset = NPC.Center.X - NPC.ai[3];
        return Math.Abs(offset) < style.LeashRange || Math.Sign(x - NPC.Center.X) != Math.Sign(offset);
    }

    private void Hop()
    {
        if (!BehaviorMovement.TryJump(NPC))
            BehaviorMovement.TryGapJump(NPC);
    }

    /// <summary>Entry of <see cref="TownState.Attack" />: aims above the target to offset projectile drop.</summary>
    private void StartAttack()
    {
        if (Target is not { } target)
            return;

        CombatStyle style = GetCombatStyle();
        float distance = Vector2.Distance(NPC.Center, target.Center);
        Vector2 aim = target.Center - Vector2.UnitY * style.ArcHeight * MathHelper.Clamp(distance / style.StrikeRange, 0f, 1f);

        FaceToward(target.Center);

        if (!BeginAttack(aim) && Authority)
            NPC.ai[1] = 0f;
    }

    private void TickAttack()
    {
        NPC.velocity.X *= 0.8f;
        NPC.ai[1]--;

        if (!Authority || NPC.ai[1] > 0f)
            return;

        CombatStyle style = GetCombatStyle();
        int cooldown = style.Cooldown + (style.CooldownVariance > 0 ? Main.rand.Next(style.CooldownVariance) : 0);

        SetState(TownState.Fight, cooldown, NPC.ai[2], NPC.ai[3]);
    }

    private void TickFlee()
    {
        NPC? threat = Target;

        if (Authority)
        {
            threat = Threats.Nearest(NPC, FleeRange);

            if (threat == null)
            {
                if (++calmTicks >= CalmTimeout)
                {
                    Stand(60 + Main.rand.Next(60));
                    return;
                }
            }
            else
            {
                calmTicks = 0;

                if (threat.whoAmI != (int)NPC.ai[2])
                {
                    NPC.ai[2] = threat.whoAmI;
                    NPC.netUpdate = true;
                }
            }
        }

        if (threat == null)
        {
            BehaviorMovement.Decelerate(NPC);
            return;
        }

        int away = NPC.Center.X < threat.Center.X ? -1 : 1;
        BehaviorMovement.WalkTowardX(NPC, NPC.Center.X + away * FleeDistance, BehaviorMovement.FleeSpeed, BehaviorMovement.FleeAcceleration);

        if (Authority)
            BehaviorMovement.TryOpenDoor(NPC);

        Hop();
    }
}
