using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.NPCs.Combat;

/// <summary>
/// Behaviours for NPCs that use weapons.
///
/// <para>
/// Kept apart from the core vocabulary on <see cref="Behavior"/> deliberately. Most
/// world NPCs never fight, so fighting is something you reach for rather than
/// something the framework assumes. Everything here needs a
/// <see cref="TerrarianNPC"/> with a <see cref="PuppetCombat"/> attached; without one
/// the behaviours finish immediately instead of misbehaving.
/// </para>
///
/// <example>
/// <code>
/// Puppet.ArmWith(PuppetAllegiance.Ally);
///
/// protected override IBehavior BuildScript() => Behavior.Select()
///     .When(ctx =&gt; ctx.NearestHostile(320f) != null, () =&gt; Fight.With(ItemID.BladeofGrass))
///     .Otherwise(() =&gt; Behavior.Stay(AnchorTile));
/// </code>
/// </example>
/// </summary>
public static class Fight
{
    /// <summary>Puts a weapon in the actor's hand and finishes.</summary>
    public static IBehavior Equip(int itemType) => Behavior.Do(ctx => ctx.Puppet?.Hold(itemType));

    /// <summary>Plays one full use of the held weapon, aimed by <paramref name="aim"/>.</summary>
    public static UseItem Use(Func<BehaviorContext, Vector2?> aim = null) => new(-1, aim);

    /// <summary>Equips a weapon and uses it once.</summary>
    public static UseItem Use(int itemType, Func<BehaviorContext, Vector2?> aim = null) => new(itemType, aim);

    /// <summary>Uses the held weapon aimed at the nearest player.</summary>
    public static UseItem UseAtNearestPlayer(float range = 800f)
        => new(-1, ctx => ctx.NearestPlayer(range)?.Center);

    /// <summary>Closes on hostile NPCs and swings until nothing is left.</summary>
    public static AttackTarget With(int weaponType, float range = 500f)
        => new(weaponType, ctx => ctx.NearestHostile(range)) { SearchRange = range };

    /// <summary>Closes on players and swings. For NPCs that have turned on you.</summary>
    public static AttackTarget WithAgainstPlayers(int weaponType, float range = 500f)
        => new(weaponType, ctx => ctx.NearestPlayer(range)) { SearchRange = range };
}

/// <summary>
/// Plays one full use animation of the held weapon and finishes when it ends.
///
/// <para>
/// The animation, the arm pose, the item's position and rotation, the projectile and
/// the swing hitbox all come from the vanilla item pipeline. This only starts it, aims
/// it, and knows when it's over - which is what makes a sequence of these read like a
/// combo.
/// </para>
/// </summary>
public sealed class UseItem : Behavior
{
    private readonly int itemType;
    private readonly Func<BehaviorContext, Vector2?> aim;

    private bool started;

    public UseItem(int itemType = -1, Func<BehaviorContext, Vector2?> aim = null)
    {
        this.itemType = itemType;
        this.aim = aim;
    }

    /// <summary>Keep facing the aim target for the whole animation, not just frame one.</summary>
    public bool TrackTarget { get; set; } = true;

    /// <summary>Finish without swinging when the aim callback finds nothing.</summary>
    public bool RequireTarget { get; set; }

    protected override void Begin(BehaviorContext ctx)
    {
        PlayerPuppet puppet = ctx.Puppet;
        if (puppet == null)
            return;

        if (itemType >= 0)
            puppet.Hold(itemType);

        Vector2? target = aim?.Invoke(ctx);

        if (RequireTarget && aim != null && !target.HasValue)
            return;

        started = puppet.BeginUse(target);
    }

    protected override void Run(BehaviorContext ctx)
    {
        PlayerPuppet puppet = ctx.Puppet;

        if (!started || puppet == null)
        {
            Finish();
            return;
        }

        if (TrackTarget && aim?.Invoke(ctx) is { } target)
            ctx.Face(target);

        if (!puppet.IsUsingItem)
            Finish();
    }

    public UseItem Tracking(bool track) { TrackTarget = track; return this; }
    public UseItem Requiring(bool require = true) { RequireTarget = require; return this; }
}

/// <summary>
/// A fight loop: find a target, close to weapon range, swing on cadence, repeat.
/// Finishes when there is nothing left to fight, unless told to stand guard.
/// </summary>
public sealed class AttackTarget : Behavior
{
    private readonly int weaponType;
    private readonly Func<BehaviorContext, Entity> targeting;

    private int cooldownTimer;
    private int idleTicks;

    public AttackTarget(int weaponType, Func<BehaviorContext, Entity> targeting)
    {
        this.weaponType = weaponType;
        this.targeting = targeting;
    }

    /// <summary>How far to look for a target.</summary>
    public float SearchRange { get; set; } = 500f;

    /// <summary>Distance the NPC tries to hold. Short for melee, long for ranged.</summary>
    public float PreferredRange { get; set; } = 40f;

    /// <summary>
    /// Backs away when the target closes inside this. 0 stands its ground - which is
    /// what separates an archer from a melee fighter with a bad weapon.
    /// </summary>
    public float RetreatRange { get; set; }

    /// <summary>Distance within which the weapon is actually used.</summary>
    public float StrikeRange { get; set; } = 64f;

    /// <summary>Ticks between swings, on top of the item's own animation.</summary>
    public int Cooldown { get; set; } = 20;

    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    /// <summary>Finish when there is no target left, instead of standing guard.</summary>
    public bool FinishWhenClear { get; set; } = true;

    /// <summary>Ticks to keep fighting after the last target vanished.</summary>
    public int Persistence { get; set; } = 60;

    protected override void Begin(BehaviorContext ctx)
    {
        cooldownTimer = 0;
        idleTicks = 0;
        ctx.Puppet?.Hold(weaponType);
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        PlayerPuppet puppet = ctx.Puppet;

        if (puppet == null)
        {
            Finish();
            return;
        }

        if (cooldownTimer > 0)
            cooldownTimer--;

        Entity target = targeting(ctx);

        if (target is not { active: true })
        {
            BehaviorMovement.Decelerate(npc);
            BehaviorMovement.ApplyTerrainCollision(npc);

            if (FinishWhenClear && ++idleTicks >= Persistence)
                Finish();

            return;
        }

        idleTicks = 0;
        ctx.Face(target.Center);

        // Hold still mid-swing; walking drags the hitbox around and looks wrong.
        if (puppet.IsUsingItem)
        {
            BehaviorMovement.Decelerate(npc);
            BehaviorMovement.ApplyTerrainCollision(npc);
            return;
        }

        float distance = Vector2.Distance(npc.Center, target.Center);
        Reposition(ctx, npc, target, distance);

        if (distance <= StrikeRange && cooldownTimer <= 0 && puppet.BeginUse(target.Center))
            cooldownTimer = Cooldown;
    }

    private void Reposition(BehaviorContext ctx, NPC npc, Entity target, float distance)
    {
        if (RetreatRange > 0f && distance < RetreatRange)
        {
            // Walk away while still facing the target, so the weapon keeps pointing
            // the right way as it backs off.
            float awayX = npc.Center.X + Math.Sign(npc.Center.X - target.Center.X) * RetreatRange;
            BehaviorMovement.WalkTowardX(npc, awayX, Speed);
            ctx.Face(target.Center);

            if (!BehaviorMovement.TryJump(npc))
                BehaviorMovement.TryGapJump(npc);
        }
        else if (distance > PreferredRange)
        {
            BehaviorMovement.WalkTowardX(npc, target.Center.X, Speed, stopDistance: PreferredRange);

            if (!BehaviorMovement.TryJump(npc))
                BehaviorMovement.TryGapJump(npc);
        }
        else
        {
            BehaviorMovement.Decelerate(npc);
        }

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    public AttackTarget WithRanges(float preferred, float strike)
    {
        PreferredRange = preferred;
        StrikeRange = strike;
        return this;
    }

    public AttackTarget WithRetreat(float pixels) { RetreatRange = pixels; return this; }
    public AttackTarget WithCooldown(int ticks) { Cooldown = ticks; return this; }
    public AttackTarget WithSpeed(float speed) { Speed = speed; return this; }
    public AttackTarget Standing() { FinishWhenClear = false; return this; }
}
