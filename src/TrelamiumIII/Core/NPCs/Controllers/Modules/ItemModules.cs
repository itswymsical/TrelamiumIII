using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Controllers.Modules;

/// <summary>
/// Puts a weapon in a player-rendered actor's hand and finishes immediately.
/// Pass <see cref="ItemID.None"/> to empty the hand.
/// </summary>
public sealed class Equip : BehaviorModule
{
    private readonly int itemType;

    public Equip(int itemType)
    {
        this.itemType = itemType;
    }

    protected override void Run(BehaviorContext ctx)
    {
        ctx.Puppet?.Hold(itemType);
        Finish();
    }
}

/// <summary>
/// Plays one full use animation of the held item and finishes when it ends.
///
/// <para>
/// The animation itself, the arm pose, the item's position and rotation, the
/// projectile and the swing hitbox all come from the vanilla item pipeline by way
/// of <see cref="PlayerPuppet"/>. This module's only job is to start it, aim it, and
/// know when it's over - which is what makes a sequence of these read like a combo.
/// </para>
/// </summary>
public sealed class UseItem : BehaviorModule
{
    private readonly int itemType;
    private readonly Func<BehaviorContext, Vector2?> aim;

    private bool started;

    /// <summary>Uses whatever is already held, aimed by <paramref name="aim"/>.</summary>
    public UseItem(Func<BehaviorContext, Vector2?> aim = null)
        : this(-1, aim) { }

    /// <summary>Equips <paramref name="itemType"/> and uses it.</summary>
    public UseItem(int itemType, Func<BehaviorContext, Vector2?> aim = null)
    {
        this.itemType = itemType;
        this.aim = aim;
    }

    /// <summary>Uses the held item aimed at a fixed world position.</summary>
    public static UseItem At(Vector2 worldPosition) => new(_ => worldPosition);

    /// <summary>Uses the held item aimed at the nearest player.</summary>
    public static UseItem AtNearestPlayer(float range = 800f)
        => new(ctx => ctx.NearestPlayer(range)?.Center);

    /// <summary>Uses the held item aimed at the nearest hostile NPC.</summary>
    public static UseItem AtNearestHostile(float range = 500f)
        => new(ctx => ctx.NearestHostile(range)?.Center);

    /// <summary>Keep facing the aim target for the whole animation, not just the first frame.</summary>
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
/// A self-contained fight loop: find a target, close to weapon range, swing on
/// cadence, repeat. Finishes when nothing is left to fight.
///
/// <para>
/// This is the module most armed actors want. It handles the whole cycle rather
/// than making every NPC re-derive "approach then attack" out of primitives - but
/// it's still just a module, so a <see cref="Selector"/> above it can yank control
/// away the moment something more important happens.
/// </para>
/// </summary>
public sealed class AttackWith : BehaviorModule
{
    private readonly int weaponType;
    private readonly Func<BehaviorContext, Entity> targeting;

    private int cooldownTimer;

    public AttackWith(int weaponType, Func<BehaviorContext, Entity> targeting = null)
    {
        this.weaponType = weaponType;
        this.targeting = targeting ?? (ctx => ctx.NearestHostile(SearchRange));
    }

    /// <summary>Fights hostile NPCs. The stance for allied actors.</summary>
    public static AttackWith Hostiles(int weaponType, float range = 500f)
        => new AttackWith(weaponType, ctx => ctx.NearestHostile(range)) { SearchRange = range };

    /// <summary>Fights players. The stance for adversarial actors.</summary>
    public static AttackWith Players(int weaponType, float range = 500f)
        => new AttackWith(weaponType, ctx => ctx.NearestPlayer(range)) { SearchRange = range };

    /// <summary>How far to look for a target.</summary>
    public float SearchRange { get; set; } = 500f;

    /// <summary>Distance the NPC tries to hold. Short for melee, long for ranged.</summary>
    public float PreferredRange { get; set; } = 40f;

    /// <summary>
    /// Back away when the target gets closer than this. 0 stands its ground.
    /// What actually makes a ranged stance read as one: an archer that lets you walk
    /// into its face is just a melee fighter with a bad weapon.
    /// </summary>
    public float RetreatRange { get; set; }

    /// <summary>Distance within which the weapon is actually swung.</summary>
    public float StrikeRange { get; set; } = 64f;

    /// <summary>Ticks between swings, on top of the item's own animation.</summary>
    public int Cooldown { get; set; } = 20;

    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    /// <summary>Finish when there is no target left, instead of standing guard.</summary>
    public bool FinishWhenClear { get; set; } = true;

    /// <summary>Ticks to keep fighting after the last target vanished.</summary>
    public int Persistence { get; set; } = 60;

    private int idleTicks;

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

        float distance = Vector2.Distance(npc.Center, target.Center);

        // Hold the swing still while an animation plays; walking mid-swing drags
        // the hitbox around and looks wrong.
        if (puppet is { IsUsingItem: true })
        {
            BehaviorMovement.Decelerate(npc);
            BehaviorMovement.ApplyTerrainCollision(npc);
            return;
        }

        if (RetreatRange > 0f && distance < RetreatRange)
        {
            // Walk away from the target while still facing it, so the weapon keeps
            // pointing the right way as it backs off.
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

        if (distance <= StrikeRange && cooldownTimer <= 0 && puppet != null)
        {
            if (puppet.BeginUse(target.Center))
                cooldownTimer = Cooldown;
        }
    }

    public AttackWith WithRanges(float preferred, float strike)
    {
        PreferredRange = preferred;
        StrikeRange = strike;
        return this;
    }

    public AttackWith WithCooldown(int ticks) { Cooldown = ticks; return this; }
    public AttackWith WithSpeed(float speed) { Speed = speed; return this; }
    public AttackWith Standing() { FinishWhenClear = false; return this; }

    /// <summary>Backs off when the target closes inside <paramref name="pixels"/>.</summary>
    public AttackWith WithRetreat(float pixels) { RetreatRange = pixels; return this; }
}

/// <summary>
/// Holds an item out in front of the NPC, the way the Guide offers you something.
///
/// <para>
/// For player-rendered actors this just equips the item and lets the item pipeline
/// draw it. For sprite-based vanilla NPCs there is no such pipeline, so the module
/// pins the NPC's hand-out frame and draws the item itself.
/// </para>
/// </summary>
public sealed class HoldOut : BehaviorModule, IFramedModule, IDrawnModule
{
    /// <summary>Frame index used as "arm extended" per NPC type.</summary>
    private static readonly Dictionary<int, int> HandOutFrames = new()
    {
        [NPCID.Guide] = 17,
    };

    private const int DefaultHandOutFrame = 0;

    private readonly int itemType;

    public HoldOut(int itemType)
    {
        this.itemType = itemType;
    }

    /// <summary>Ticks to hold the pose. -1 holds it until something else takes over.</summary>
    public int Duration { get; set; } = -1;

    /// <summary>Offset from the NPC's centre, for sprite-based NPCs only.</summary>
    public Vector2 Offset { get; set; } = new(12f, -8f);

    public float Scale { get; set; } = 0.95f;

    public float Rotation { get; set; }

    /// <summary>Registers the hand-out frame for an NPC type. Call during load.</summary>
    public static void RegisterHandOutFrame(int npcType, int frame) => HandOutFrames[npcType] = frame;

    protected override void Begin(BehaviorContext ctx)
    {
        ctx.Puppet?.Hold(itemType);
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        npc.velocity.X *= 0.8f;
        if (Math.Abs(npc.velocity.X) < 0.1f)
            npc.velocity.X = 0f;

        if (Duration >= 0 && Age + 1 >= Duration)
            Finish();
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        ctx.Puppet?.Hold(ItemID.None);
    }

    public bool ModifyFrame(BehaviorContext ctx, int frameHeight)
    {
        if (itemType <= ItemID.None)
            return false;

        int frame = HandOutFrames.GetValueOrDefault(ctx.NPC.type, DefaultHandOutFrame);
        ctx.NPC.frame.Y = frame * frameHeight;
        return true;
    }

    public void Draw(BehaviorContext ctx, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        // Player-rendered actors draw the item through the vanilla HeldItem layer.
        if (ctx.HasPuppet || itemType <= ItemID.None)
            return;

        NPC npc = ctx.NPC;
        Main.instance.LoadItem(itemType);
        Texture2D texture = TextureAssets.Item[itemType].Value;

        Vector2 offset = Offset;
        if (npc.spriteDirection == -1)
            offset.X *= -1f;

        spriteBatch.Draw(
            texture,
            npc.Center - screenPos + offset,
            null,
            drawColor,
            Rotation,
            texture.Size() * 0.5f,
            Scale,
            npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
            0f);
    }

    public HoldOut For(int ticks) { Duration = ticks; return this; }
    public HoldOut WithOffset(Vector2 offset) { Offset = offset; return this; }
    public HoldOut WithScale(float scale) { Scale = scale; return this; }
    public HoldOut WithRotation(float rotation) { Rotation = rotation; return this; }
}
