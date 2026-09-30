using System.IO;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

/// <summary>
/// Grappling hooks latch onto enemies and reel them in. Pressing grapple while reeling drops the enemy
/// where it is; otherwise it is flung at the player. See <see cref="EnemyHookProjectile"/>.
/// </summary>
public sealed class EnemyGrappleSkill : Skill
{
    public const string Id = "skill.grapple";

    public override string ID => Id;

    public override int RequiredThreshold => 12;

    public override SkillGroup Group => SkillGroup.Movement;

    public override float CostFraction => 0.55f;
}

/// <summary>
/// Latch state for any aiStyle 7 hook. The owner detects the latch and the cancel; the state reaches
/// the server and other clients through the projectile sync, and only moves forward
/// (None, Latched, then Launched or Cancelled) so a stale packet cannot re-latch a released hook.
/// </summary>
public sealed class EnemyHookProjectile : GlobalProjectile
{
    private enum HookState : byte
    {
        None,
        Latched,
        Launched,
        Cancelled
    }

    /// <summary>Pixels per tick an NPC with full knockback susceptibility is reeled at.</summary>
    private const float REEL_SPEED = 13f;

    /// <summary>Share of <see cref="REEL_SPEED"/> at knockback resistance 0.</summary>
    private const float HEAVY_SHARE = 0.55f;

    /// <summary>Pixels between the NPC's and the owner's hitboxes at which the NPC is let go.</summary>
    private const int LAUNCH_GAP = 24;

    private const int MAX_LATCH_TICKS = 120;

    /// <summary>Ticks the NPC may fail to close in (blocked by tiles) before the hook lets go.</summary>
    private const int STUCK_TICKS = 20;

    private HookState _state;
    private int _npc = -1;
    private int _latchTicks;
    private int _stuckTicks;
    private float _lastDistance;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        => lateInstantiation && entity.aiStyle == ProjAIStyleID.Hook;

    public override void Load() => On_Player.QuickGrapple += CancelOnGrapple;

    public override void Unload() => On_Player.QuickGrapple -= CancelOnGrapple;

    /// <summary>A grapple press while reeling cancels the reel instead of firing another hook.</summary>
    private static void CancelOnGrapple(On_Player.orig_QuickGrapple orig, Player self)
    {
        bool cancelled = false;

        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != self.whoAmI
                || !projectile.TryGetGlobalProjectile(out EnemyHookProjectile hook)
                || hook._state != HookState.Latched)
                continue;

            hook.Release(projectile, HookState.Cancelled);
            cancelled = true;
        }

        if (!cancelled)
            orig(self);
    }

    public override bool PreAI(Projectile projectile)
    {
        if (_state == HookState.Latched)
        {
            HoldLatch(projectile);

            return _state != HookState.Latched;
        }

        if (_state == HookState.None && projectile.ai[0] == 0f && projectile.owner == Main.myPlayer
            && Main.player[projectile.owner].GetModPlayer<SkillPlayer>().Has<EnemyGrappleSkill>()
            && TryLatch(projectile))
            return false;

        return true;
    }

    /// <summary>Sweeps this tick's travel so a fast hook cannot pass through a small NPC.</summary>
    private bool TryLatch(Projectile projectile)
    {
        Vector2 start = projectile.Center;
        Vector2 end = start + projectile.velocity;
        float collisionPoint = 0f;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!CanReel(npc)
                || !Collision.CheckAABBvLineCollision(npc.position, npc.Size, start, end, projectile.width, ref collisionPoint))
                continue;

            _state = HookState.Latched;
            _npc = npc.whoAmI;
            _latchTicks = _stuckTicks = 0;
            _lastDistance = float.MaxValue;

            projectile.Center = npc.Center;
            projectile.velocity = Vector2.Zero;
            projectile.netUpdate = true;

            return true;
        }

        return false;
    }

    private void HoldLatch(Projectile projectile)
    {
        Player owner = Main.player[projectile.owner];
        NPC? npc = LatchedNPC();

        if (npc is null || !CanReel(npc) || owner.dead || owner.CCed
            || !owner.GetModPlayer<SkillPlayer>().Has<EnemyGrappleSkill>())
        {
            Release(projectile, HookState.Launched);

            return;
        }

        Vector2 toOwner = owner.MountedCenter - npc.Center;
        float distance = toOwner.Length();

        projectile.Center = npc.Center;
        projectile.velocity = Vector2.Zero;
        projectile.rotation = toOwner.ToRotation() - MathHelper.PiOver2;

        _stuckTicks = distance > _lastDistance - 1f ? _stuckTicks + 1 : 0;
        _lastDistance = distance;

        Rectangle reach = npc.Hitbox;
        reach.Inflate(LAUNCH_GAP, LAUNCH_GAP);

        if (reach.Intersects(owner.Hitbox) || _stuckTicks > STUCK_TICKS || ++_latchTicks > MAX_LATCH_TICKS)
        {
            Release(projectile, HookState.Launched);

            return;
        }

        float speed = REEL_SPEED * MathHelper.Lerp(HEAVY_SHARE, 1f, Math.Clamp(npc.knockBackResist, 0f, 1f));

        npc.GetGlobalNPC<ReeledNPC>().Reel(toOwner / MathF.Max(distance, 1f) * speed);

        if (Main.netMode == NetmodeID.Server && _latchTicks % 10 == 0)
            npc.netUpdate = true;
    }

    /// <summary>Hands the hook back to vanilla's retract branch and applies the release to the NPC.</summary>
    private void Release(Projectile projectile, HookState state)
    {
        _state = state;

        projectile.ai[0] = 1f;
        projectile.netUpdate = true;

        if (LatchedNPC() is not { active: true } npc)
            return;

        var reeled = npc.GetGlobalNPC<ReeledNPC>();

        if (state == HookState.Cancelled)
            reeled.Stop(npc);
        else
            reeled.Launch();

        if (Main.netMode == NetmodeID.Server)
            npc.netUpdate = true;
    }

    private NPC? LatchedNPC() => _npc >= 0 && _npc < Main.maxNPCs ? Main.npc[_npc] : null;

    /// <summary>Bosses, worm segments and anything knockback-immune are never reeled.</summary>
    private static bool CanReel(NPC npc)
        => npc.active && npc.CanBeChasedBy() && !npc.boss && !NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
           && npc.knockBackResist > 0f && (npc.realLife == -1 || npc.realLife == npc.whoAmI);

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        binaryWriter.Write((byte)_state);
        binaryWriter.Write((short)_npc);
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        var state = (HookState)binaryReader.ReadByte();
        int npc = binaryReader.ReadInt16();

        if (state == HookState.Latched && _state == HookState.None)
        {
            _state = state;
            _npc = npc;
            _latchTicks = _stuckTicks = 0;
            _lastDistance = float.MaxValue;
        }
        else if (state is HookState.Launched or HookState.Cancelled && _state == HookState.Latched)
        {
            _npc = npc;

            Release(projectile, state);
        }
    }
}

/// <summary>Applies a hook's reel after the NPC's own AI, which would otherwise overwrite the velocity.</summary>
public sealed class ReeledNPC : GlobalNPC
{
    private Vector2 _pull;
    private int _pullTicks;

    public override bool InstancePerEntity => true;

    /// <summary>Holds for two ticks: projectiles update after NPCs, so each reel lands on the next NPC update.</summary>
    public void Reel(Vector2 velocity)
    {
        _pull = velocity;
        _pullTicks = 2;
    }

    public void Launch() => _pullTicks = 0;

    public void Stop(NPC npc)
    {
        _pullTicks = 0;
        npc.velocity = Vector2.Zero;
    }

    public override void PostAI(NPC npc)
    {
        if (_pullTicks <= 0)
            return;

        _pullTicks--;

        npc.velocity = _pull;

        // UpdateNPC adds gravity after AI.
        if (!npc.noGravity)
            npc.velocity.Y -= npc.gravity;
    }
}
