using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// Shared movement utilities for NPC behaviors. Matches vanilla AI_007_TownEntities
/// movement, jumping, and terrain collision patterns.
/// </summary>
public static class BehaviorMovement
{
    // Vanilla defaults from AI_007, ai[0]==1 walking state.
    public const float DefaultWalkSpeed = 2f;
    public const float DefaultAcceleration = 0.07f;

    // speed when hostiles are nearby.
    public const float FleeSpeed = 1.5f;
    public const float FleeAcceleration = 0.1f;

    /// <summary>
    /// Accelerates the NPC horizontally toward <paramref name="targetX"/>.
    /// Decelerates when within <paramref name="stopDistance"/> pixels.
    /// Mirrors vanilla's velocity clamping and grounded friction.
    /// </summary>
    public static void WalkTowardX(NPC npc, float targetX, float speed = DefaultWalkSpeed, float acceleration = DefaultAcceleration, float stopDistance = 0f)
    {
        float diff = targetX - npc.Center.X;

        if (Math.Abs(diff) > stopDistance)
        {
            npc.direction = diff > 0f ? 1 : -1;
            npc.spriteDirection = npc.direction;

            if (npc.velocity.X < speed && npc.direction == 1)
            {
                npc.velocity.X += acceleration;
                if (npc.velocity.X > speed)
                    npc.velocity.X = speed;
            }
            else if (npc.velocity.X > -speed && npc.direction == -1)
            {
                npc.velocity.X -= acceleration;
                if (npc.velocity.X < -speed)
                    npc.velocity.X = -speed;
            }

            // Vanilla friction: bleed off excess speed when grounded.
            if (npc.velocity.Y == 0f && Math.Abs(npc.velocity.X) > speed)
                npc.velocity *= 0.8f;
        }
        else
        {
            Decelerate(npc);
        }
    }

    /// <summary>
    /// Bleeds velocity toward zero, matching vanilla's idle friction curve.
    /// </summary>
    public static void Decelerate(NPC npc)
    {
        if (npc.velocity.X > 0.1f)
            npc.velocity.X -= 0.1f;
        else if (npc.velocity.X < -0.1f)
            npc.velocity.X += 0.1f;
        else
            npc.velocity.X = 0f;
    }

    /// <summary>
    /// Applies slope and step collision matching vanilla's AI_007 pattern.
    ///
    /// StepDown glues the NPC to downward slopes so they don't bounce.
    /// StepUp nudges the NPC over single-tile ledges while walking.
    ///
    /// <paramref name="allowStepUp"/> mirrors vanilla's flag18. Pass true
    /// when the NPC should climb small obstacles (normal walking), false
    /// when above target elevation (descending only).
    /// </summary>
    public static void ApplyTerrainCollision(NPC npc, bool allowStepUp = true)
    {
        // Vanilla: StepDown only when grounded. Keeps NPC glued to
        // downward slopes instead of launching off edges.
        if (npc.velocity.Y == 0f)
            Collision.StepDown(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY);

        // Vanilla: StepUp when falling or grounded. The allowStepUp
        // parameter controls whether the NPC can climb up ledges
        // (true) or only slide down (false).
        if (npc.velocity.Y >= 0f)
            Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY, 1, allowStepUp, 1);
    }

    /// <summary>
    /// Evaluates terrain ahead and applies a jump if needed. Only fires when
    /// the NPC is grounded AND already moving in its facing direction, matching
    /// vanilla's gate: <c>(velocity.X &lt; 0 &amp;&amp; direction == -1) || (velocity.X &gt; 0 &amp;&amp; direction == 1)</c>.
    ///
    /// Jump velocities match vanilla:
    /// -6 for 3-high walls, -5 for 2-high walls, -4.4 for ledge step-ups.
    ///
    /// Returns true if a jump was initiated.
    /// </summary>
    public static bool TryJump(NPC npc)
    {
        if (npc.velocity.Y != 0f)
            return false;

        // NPC must already be moving in its facing direction.
        bool movingForward = (npc.velocity.X < 0f && npc.direction == -1)
                          || (npc.velocity.X > 0f && npc.direction == 1);

        if (!movingForward)
            return false;

        int aheadX = (int)((npc.position.X + npc.width * 0.5f + (npc.width * 0.5f + 1f) * npc.direction) / 16f);
        int feetY = (int)((npc.position.Y + npc.height - 16f) / 16f);

        Tile ahead0 = Framing.GetTileSafely(aheadX, feetY);
        Tile ahead1 = Framing.GetTileSafely(aheadX, feetY - 1);
        Tile ahead2 = Framing.GetTileSafely(aheadX, feetY - 2);
        bool shortNPC = npc.height / 16 < 3;

        // 3-high wall
        if (ahead2.HasUnactuatedTile && Main.tileSolid[ahead2.TileType] && !Main.tileSolidTop[ahead2.TileType]
            && (!shortNPC || (ahead1.HasUnactuatedTile && Main.tileSolid[ahead1.TileType] && !Main.tileSolidTop[ahead1.TileType])))
        {
            if (!Collision.SolidTilesVersatile(aheadX - npc.direction * 2, aheadX - npc.direction, feetY - 5, feetY - 1)
                && !Collision.SolidTiles(aheadX, aheadX, feetY - 5, feetY - 3))
            {
                npc.velocity.Y = -6f;
                npc.netUpdate = true;
                return true;
            }
        }
        // 2-high wall
        else if (ahead1.HasUnactuatedTile && Main.tileSolid[ahead1.TileType] && !Main.tileSolidTop[ahead1.TileType])
        {
            if (!Collision.SolidTilesVersatile(aheadX - npc.direction * 2, aheadX - npc.direction, feetY - 4, feetY - 1)
                && !Collision.SolidTiles(aheadX, aheadX, feetY - 4, feetY - 2))
            {
                npc.velocity.Y = -5f;
                npc.netUpdate = true;
                return true;
            }
        }
        // Ledge step-up: feet tile is solid but NPC is partially above it.
        else if (npc.position.Y + npc.height - feetY * 16 > 20f
                 && ahead0.HasUnactuatedTile && Main.tileSolid[ahead0.TileType] && !ahead0.TopSlope)
        {
            if (!Collision.SolidTilesVersatile(aheadX - npc.direction * 2, aheadX, feetY - 3, feetY - 1))
            {
                npc.velocity.Y = -4.4f;
                npc.netUpdate = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the tile ahead is a gap (no solid ground in front or below).
    /// Used by behaviors that need to decide whether to jump across or turn around.
    /// </summary>
    public static bool IsGapAhead(NPC npc)
    {
        if (npc.velocity.Y != 0f)
            return false;

        int aheadX = (int)((npc.position.X + npc.width * 0.5f + (npc.width * 0.5f + 1f) * npc.direction) / 16f);
        int feetY = (int)((npc.position.Y + npc.height - 16f) / 16f);

        Tile ahead = Framing.GetTileSafely(aheadX, feetY);
        Tile below = Framing.GetTileSafely(aheadX, feetY + 1);
        Tile belowDiag = Framing.GetTileSafely(aheadX + npc.direction, feetY + 1);

        bool groundAhead = ahead.HasUnactuatedTile && Main.tileSolid[ahead.TileType];
        bool groundBelow = below.HasUnactuatedTile && Main.tileSolid[below.TileType];
        bool groundDiag = belowDiag.HasUnactuatedTile && Main.tileSolid[belowDiag.TileType];

        return !groundAhead && !groundBelow && !groundDiag;
    }

    public static bool TryGapJump(NPC npc)
    {
        if (npc.velocity.Y != 0f)
            return false;

        if (!IsGapAhead(npc))
            return false;

        npc.velocity.Y = -7f;
        npc.velocity.X = npc.direction * 3f;
        npc.netUpdate = true;
        return true;
    }

    /// <summary>
    /// True when the NPC is standing on solid ground. Gates on velocity.Y == 0,
    /// which the engine sets when tile collision zeroes vertical movement.
    /// </summary>
    public static bool IsOnGround(NPC npc) => npc.velocity.Y == 0f;
}

/// <summary>
/// Stands in place, decelerating to a stop. Optionally emits random emotes.
/// Completes after <see cref="Duration"/> ticks, or runs indefinitely if duration is -1.
/// </summary>
public class IdleBehavior : NPCBehavior
{
    private int elapsed;

    /// <summary>Ticks before completion. -1 = indefinite (must be cleared externally).</summary>
    public int Duration { get; set; } = -1;

    /// <summary>Ticks between random emote bubbles. 0 = no emotes.</summary>
    public int EmoteInterval { get; set; }

    /// <summary>Emote IDs to pick from at random.</summary>
    public int[] EmotePool { get; set; }

    private int emoteTimer;
    private int nextEmoteTime;

    private static readonly int[] DefaultEmotes =
    [
        EmoteID.EmoteNote,
        EmoteID.EmoteConfused,
        EmoteID.EmoteHappiness,
        EmoteID.WeatherSunny
    ];

    public override void OnStart(NPC npc)
    {
        elapsed = 0;
        emoteTimer = 0;

        if (EmoteInterval > 0)
            nextEmoteTime = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.8f));
    }

    public override void Update(NPC npc)
    {
        // Decelerate
        npc.velocity.X *= 0.8f;
        if (Math.Abs(npc.velocity.X) < 0.1f)
            npc.velocity.X = 0f;

        // Keep in idle AI state for vanilla NPCs
        npc.ai[0] = 0f;
        npc.ai[1] = 300f;

        // Emotes
        if (EmoteInterval > 0)
        {
            emoteTimer++;
            if (emoteTimer >= nextEmoteTime)
            {
                var pool = EmotePool ?? DefaultEmotes;
                int emote = Main.rand.Next(pool);
                EmoteBubble.NewBubble(emote, new WorldUIAnchor(npc), 120);

                emoteTimer = 0;
                nextEmoteTime = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.8f));
            }
        }

        // Duration tracking
        if (Duration >= 0)
        {
            elapsed++;
            if (elapsed >= Duration)
                Complete();
        }
    }

    public IdleBehavior WithDuration(int ticks) { Duration = ticks; return this; }
    public IdleBehavior WithEmotes(int interval, int[] pool = null)
    {
        EmoteInterval = interval;
        EmotePool = pool;
        return this;
    }
}

public class FollowBehavior : NPCBehavior
{
    private readonly int playerIndex;
    private float followDistance = 80f;
    private float teleportThreshold = 2000f;
    private float walkSpeed = 1.5f;
    private float acceleration = 0.1f;
    private bool completeOnArrival;

    // Stuck detection mirrors vanilla's localAI[3] = position.X pattern.
    // If the NPC's X hasn't changed after a jump, it's stuck.
    private float lastJumpX;
    private int stuckTicks;
    private const int StuckThreshold = 60;

    public override bool OverrideVanillaAI => true;

    public FollowBehavior(int playerIndex)
    {
        this.playerIndex = playerIndex;
    }

    public FollowBehavior WithDistance(float distance)
    {
        followDistance = distance;
        return this;
    }

    public FollowBehavior WithTeleport(float threshold)
    {
        teleportThreshold = threshold;
        return this;
    }

    public FollowBehavior WithSpeed(float speed, float accel = 0.1f)
    {
        walkSpeed = speed;
        acceleration = accel;
        return this;
    }

    public FollowBehavior WithCompleteOnArrival(bool complete = true)
    {
        completeOnArrival = complete;
        return this;
    }

    public override void OnStart(NPC npc)
    {
        lastJumpX = npc.position.X;
        stuckTicks = 0;
    }

    public override void Update(NPC npc)
    {
        Player target = Main.player[playerIndex];
        if (!target.active || target.dead)
            return;

        float distX = Math.Abs(target.Center.X - npc.Center.X);
        float distTotal = Vector2.Distance(npc.Center, target.Center);

        // Teleport if too far
        if (distTotal > teleportThreshold)
        {
            Teleport(npc, target);
            return;
        }

        // Within follow distance, idle in place, face the player.
        if (distX <= followDistance)
        {
            BehaviorMovement.Decelerate(npc);
            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;
            stuckTicks = 0;

            BehaviorMovement.ApplyTerrainCollision(npc);

            if (completeOnArrival)
                Complete();

            return;
        }

        // Walk toward player
        BehaviorMovement.WalkTowardX(npc, target.Center.X, walkSpeed, acceleration, followDistance);

        // Try wall jumps (vanilla pattern)
        bool jumped = BehaviorMovement.TryJump(npc);

        // Try gap jumps (non-vanilla, needed for follow)
        if (!jumped && BehaviorMovement.IsOnGround(npc))
            jumped = BehaviorMovement.TryGapJump(npc);

        // Track position after jumps for stuck detection
        if (jumped)
            lastJumpX = npc.position.X;

        // Stuck detection: if X hasn't changed meaningfully, flip direction
        if (npc.velocity.Y != 0f)
        {
            if (Math.Abs(npc.position.X - lastJumpX) < 2f)
            {
                stuckTicks++;
                if (stuckTicks > StuckThreshold)
                {
                    npc.direction *= -1;
                    npc.spriteDirection = npc.direction;
                    npc.velocity.X = npc.direction * walkSpeed;
                    stuckTicks = 0;
                    npc.netUpdate = true;
                }
            }
            else
            {
                stuckTicks = 0;
                lastJumpX = npc.position.X;
            }
        }
        else
        {
            stuckTicks = 0;
        }

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    private void Teleport(NPC npc, Player target)
    {
        // Find ground near the player
        Vector2 destination = target.Bottom + new Vector2(followDistance * -target.direction, 0f);

        int tileX = (int)(destination.X / 16f);
        int tileY = (int)(destination.Y / 16f);

        // Scan downward for solid ground
        for (int y = tileY; y < tileY + 12; y++)
        {
            Tile tile = Framing.GetTileSafely(tileX, y);
            if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            {
                npc.position.X = tileX * 16f + 8f - npc.width * 0.5f;
                npc.position.Y = y * 16f - npc.height;
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;

                stuckTicks = 0;
                lastJumpX = npc.position.X;
                return;
            }
        }

        // Fallback: teleport directly to player feet.
        npc.position = target.Bottom - new Vector2(npc.width * 0.5f, npc.height);
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;

        stuckTicks = 0;
        lastJumpX = npc.position.X;
    }
}

/// <summary>
/// Walks the NPC to a world position, completing when within arrival distance.
/// If the NPC gets stuck for too long, optionally teleports to the destination.
/// </summary>
public class TravelToBehavior : NPCBehavior
{
    private int stuckTimer;

    /// <summary>World-space target position.</summary>
    public Vector2 Target { get; }

    /// <summary>Maximum horizontal speed in pixels per tick.</summary>
    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    /// <summary>Pixel distance considered "arrived".</summary>
    public float ArrivalDistance { get; set; } = 24f;

    /// <summary>
    /// Ticks of no horizontal progress before the NPC teleports to the
    /// destination. 0 = never teleport (NPC can get stuck).
    /// </summary>
    public int StuckTeleportTimeout { get; set; } = 300; // 5 seconds

    public TravelToBehavior(Vector2 worldPosition)
    {
        Target = worldPosition;
    }

    public override void OnStart(NPC npc)
    {
        stuckTimer = 0;
    }

    public override void Update(NPC npc)
    {
        BehaviorMovement.WalkTowardX(npc, Target.X, Speed, stopDistance: ArrivalDistance);

        if (Math.Abs(Target.X - npc.Center.X) > ArrivalDistance)
        {
            if (!BehaviorMovement.TryJump(npc))
                BehaviorMovement.TryGapJump(npc);
        }

        BehaviorMovement.ApplyTerrainCollision(npc);

        bool moving = Vector2.Distance(npc.Center, Target) > ArrivalDistance;

        if (!moving)
        {
            Complete();
            return;
        }

        // Stuck detection
        if (StuckTeleportTimeout > 0)
        {
            if (npc.position.X == npc.oldPosition.X && Math.Abs(npc.velocity.X) < 0.1f)
                stuckTimer++;
            else
                stuckTimer = 0;

            if (stuckTimer >= StuckTeleportTimeout)
            {
                npc.position = Target - new Vector2(npc.width * 0.5f, npc.height);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                Complete();
            }
        }
    }

    public TravelToBehavior WithSpeed(float speed) { Speed = speed; return this; }
    public TravelToBehavior WithArrival(float pixels) { ArrivalDistance = pixels; return this; }
    public TravelToBehavior WithStuckTimeout(int ticks) { StuckTeleportTimeout = ticks; return this; }
}

/// <summary>
/// Anchors the NPC at a position. If pushed away (knockback, terrain), the
/// NPC walks back. Once within tolerance, stands idle. Runs indefinitely.
/// </summary>
public class StayBehavior : NPCBehavior
{
    /// <summary>
    /// Tile coordinates of the anchor point. Defaults to the NPC's position
    /// at the time <see cref="OnStart"/> runs.
    /// </summary>
    public Point? AnchorTile { get; set; }

    /// <summary>Tile tolerance before the NPC starts walking back.</summary>
    public int Tolerance { get; set; } = 2;

    /// <summary>Maximum horizontal speed when returning to anchor.</summary>
    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    public override void OnStart(NPC npc)
    {
        AnchorTile ??= npc.Center.ToTileCoordinates();
    }

    public override void Update(NPC npc)
    {
        var anchor = AnchorTile.Value;
        var current = npc.Center.ToTileCoordinates();

        if (Math.Abs(current.X - anchor.X) > Tolerance)
        {
            npc.direction = Math.Sign(anchor.X - current.X);
            npc.spriteDirection = npc.direction;

            float acceleration = 0.1f;
            if (npc.velocity.X < Speed && npc.direction == 1)
                npc.velocity.X += acceleration;
            else if (npc.velocity.X > -Speed && npc.direction == -1)
                npc.velocity.X -= acceleration;
        }
        else
        {
            npc.velocity.X *= 0.9f;
            if (Math.Abs(npc.velocity.X) < 0.1f)
                npc.velocity.X = 0f;
        }

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    public StayBehavior At(Point tile) { AnchorTile = tile; return this; }
    public StayBehavior At(Vector2 worldPos) { AnchorTile = worldPos.ToTileCoordinates(); return this; }
    public StayBehavior WithTolerance(int tiles) { Tolerance = tiles; return this; }
    public StayBehavior WithSpeed(float speed) { Speed = speed; return this; }
}

public class DialogueFrameConfig
{
    public int[] TalkingFrames { get; set; } = [0, 1];
    public int[] HandOutFrames { get; set; } = [0];
    public int TalkingFrameSpeed { get; set; } = 8;
    public int HandOutFrameSpeed { get; set; } = 6;
}

/// <summary>
/// Freezes the NPC in place, faces the nearest player, and optionally
/// animates talking frames and emits emotes. Completes when the linked
/// dialogue key is no longer active, or when cleared externally.
///
/// Replaces the hardcoded behavior from <c>DialogueNPCBehavior</c>.
/// </summary>
public class DialogueBehavior : NPCBehavior
{
    // Per-NPC-type frame configs. Add entries as needed.
    private static readonly Dictionary<int, DialogueFrameConfig> FrameConfigs = new()
    {
        [NPCID.Guide] = new()
        {
            TalkingFrames = [19, 0],
            HandOutFrames = [17],
            TalkingFrameSpeed = 10,
            HandOutFrameSpeed = 8
        },
    };

    private static readonly DialogueFrameConfig DefaultConfig = new();

    private int frameCounter;
    private int frameIndex;
    private int emoteTimer;
    private int nextEmoteTime;

    /// <summary>
    /// Dialogue key to monitor. When <see cref="DialogueManager.IsDialogueActive"/>
    /// returns false for this key, the behavior completes.
    /// Null = no auto-completion, must be cleared externally.
    /// </summary>
    public string DialogueKey { get; set; }

    /// <summary>Ticks between emote bubbles. 0 = no emotes.</summary>
    public int EmoteInterval { get; set; } = 200;

    /// <summary>Emote IDs to pick from.</summary>
    public int[] EmotePool { get; set; }

    private static readonly int[] DefaultEmotes =
    [
        EmoteID.EmoteNote,
        EmoteID.EmoteConfused,
        EmoteID.EmoteHappiness,
        EmoteID.WeatherSunny
    ];

    public DialogueBehavior(string dialogueKey = null)
    {
        DialogueKey = dialogueKey;
        FaceNearestPlayer = true;
    }

    public override void OnStart(NPC npc)
    {
        npc.dontTakeDamage = true;

        frameCounter = 0;
        frameIndex = 0;
        emoteTimer = 0;
        nextEmoteTime = EmoteInterval > 0
            ? Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f))
            : 0;
    }

    public override void Update(NPC npc)
    {
        // Freeze in place
        npc.velocity.X *= 0.8f;
        if (Math.Abs(npc.velocity.X) < 0.1f)
            npc.velocity.X = 0f;

        npc.ai[0] = 0f;
        npc.ai[1] = 300f;

        // Emotes
        if (EmoteInterval > 0)
        {
            emoteTimer++;
            if (emoteTimer >= nextEmoteTime)
            {
                var pool = EmotePool ?? DefaultEmotes;
                EmoteBubble.NewBubble(Main.rand.Next(pool), new WorldUIAnchor(npc), 120);
                emoteTimer = 0;
                nextEmoteTime = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f));
            }
        }

        AdvanceFrameAnimation(npc);

        //if (DialogueKey != null && !DialogueManager.Instance.IsDialogueActive(DialogueKey))
        //    Complete();
    }

    public override void OnEnd(NPC npc)
    {
        npc.dontTakeDamage = false;

        // Nudge NPC out of locked idle so vanilla AI resumes smoothly.
        npc.localAI[3] = 30f;
    }

    public override bool ModifyFrame(NPC npc, int frameHeight)
    {
        var config = GetConfig(npc.type);
        bool handOut = HeldItem is { ItemType: > 0 };
        int[] frames = handOut ? config.HandOutFrames : config.TalkingFrames;

        int clamped = Math.Clamp(frameIndex, 0, frames.Length - 1);
        npc.frame.Y = frames[clamped] * frameHeight;
        return true;
    }

    private void AdvanceFrameAnimation(NPC npc)
    {
        var config = GetConfig(npc.type);
        bool handOut = HeldItem is { ItemType: > 0 };

        int speed = handOut ? config.HandOutFrameSpeed : config.TalkingFrameSpeed;
        int[] frames = handOut ? config.HandOutFrames : config.TalkingFrames;

        frameCounter++;
        if (frameCounter >= speed)
        {
            frameCounter = 0;
            frameIndex = (frameIndex + 1) % frames.Length;
        }
    }

    private static DialogueFrameConfig GetConfig(int npcType)
    {
        return FrameConfigs.TryGetValue(npcType, out var config) ? config : DefaultConfig;
    }

    /// <summary>
    /// Registers a custom frame config for a vanilla NPC type.
    /// Call during Mod.Load or equivalent.
    /// </summary>
    public static void RegisterFrameConfig(int npcType, DialogueFrameConfig config)
    {
        FrameConfigs[npcType] = config;
    }

    public DialogueBehavior WithEmotes(int interval, int[] pool = null)
    {
        EmoteInterval = interval;
        EmotePool = pool;
        return this;
    }
    public DialogueBehavior WithoutEmotes() { EmoteInterval = 0; return this; }
}

public class UseItemBehavior : NPCBehavior
{
    private readonly int _itemId;
    private readonly Vector2 _targetPosition;
    private WorldNPC _worldNpc;

    /// <summary>
    /// Uses an item dynamically. If it shoots a projectile, it aims at the target position.
    /// </summary>
    public UseItemBehavior(int itemId, Vector2 targetPosition = default)
    {
        _itemId = itemId;
        _targetPosition = targetPosition;
        OverrideVanillaAI = true;
    }

    public override void OnStart(NPC npc)
    {
        _worldNpc = npc.ModNPC as WorldNPC;
        if (_worldNpc != null)
        {
            _worldNpc.StartItemUse(_itemId);
        }
        else
        {
            Complete();
        }
    }

    public override void Update(NPC npc)
    {
        if (_worldNpc == null) return;

        Item item = _worldNpc.DummyPlayer.inventory[_worldNpc.DummyPlayer.selectedItem];

        // Face the target we are aiming at
        if (_targetPosition != default)
        {
            npc.direction = _targetPosition.X < npc.Center.X ? -1 : 1;
            npc.spriteDirection = npc.direction;
        }

        // Trigger the item's effect on the first frame of its use time cycle
        if (_worldNpc.DummyPlayer.itemTime == item.useTime && !item.IsAir)
        {
            FireItemDynamically(npc, item);
        }

        // Advance the animation frames
        _worldNpc.UpdateItemTimers();

        if (_worldNpc.DummyPlayer.itemAnimation <= 0)
        {
            Complete();
        }
    }

    private void FireItemDynamically(NPC npc, Item item)
    {
        // 1. Play the item's native use sound
        if (item.UseSound != null)
        {
            Terraria.Audio.SoundEngine.PlaySound(item.UseSound, npc.Center);
        }

        // 2. Dynamically spawn the projectile if the item has one
        if (item.shoot > 0)
        {
            Vector2 aim = _targetPosition - npc.Center;
            if (aim != Vector2.Zero)
            {
                aim.Normalize();
            }
            else
            {
                aim = new Vector2(npc.direction, 0); // Default forward if no target
            }

            // Apply the item's native shoot speed
            aim *= item.shootSpeed;

            // Spawn the projectile dynamically as friendly to the NPC (hostile to enemies)
            int proj = Terraria.Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                aim,
                item.shoot,
                item.damage,
                item.knockBack,
                Main.myPlayer // Or 255 to unassign it from a specific player
            );

            // Optional: Mark it as friendly so it hurts enemies, not players
            Main.projectile[proj].friendly = true;
            Main.projectile[proj].hostile = false;
        }
    }
}

/// <summary>
/// Delegates to vanilla town NPC AI (aiStyle 7) for combat behavior.
/// Completes when no hostile NPCs remain within range.
///
/// Designed to be used as an interrupt: the <see cref="NPCDirector"/> can
/// push this on top of a follow/idle sequence, and the previous behavior
/// resumes when combat ends.
/// </summary>
public class AttackBehavior : NPCBehavior
{
    /// <summary>Detection radius in pixels.</summary>
    public float Range { get; set; } = 200f;

    public AttackBehavior()
    {
        OverrideVanillaAI = false; // Let vanilla handle combat.
    }

    public override void OnStart(NPC npc)
    {
        npc.aiStyle = 7;
        npc.netUpdate = true;
    }

    public override void Update(NPC npc)
    {
        float rangeSq = Range * Range;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (other.active && !other.friendly && !other.CountsAsACritter
                && other.DistanceSQ(npc.Center) <= rangeSq)
            {
                return;
            }
        }

        Complete();
    }

    public AttackBehavior WithRange(float pixels) { Range = pixels; return this; }
}

/// <summary>
/// Instantly moves the NPC to a world position and completes.
/// Kills velocity so the NPC doesn't slide after arrival.
/// </summary>
public class TeleportBehavior : NPCBehavior
{
    public Vector2 Target { get; }

    /// <summary>When true, spawns a puff of dust at origin and destination.</summary>
    public bool SpawnDust { get; set; } = true;

    public TeleportBehavior(Vector2 worldPosition)
    {
        Target = worldPosition;
    }

    public override void Update(NPC npc)
    {
        if (SpawnDust)
            SpawnTeleportDust(npc);

        npc.position = Target - new Vector2(npc.width * 0.5f, npc.height);
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;

        if (SpawnDust)
            SpawnTeleportDust(npc);

        Complete();
    }

    private static void SpawnTeleportDust(NPC npc)
    {
        for (int i = 0; i < 15; i++)
        {
            var dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.MagicMirror);
            dust.noGravity = true;
            dust.velocity *= 0.5f;
        }
    }

    public TeleportBehavior WithDust(bool dust) { SpawnDust = dust; return this; }
}

/// <summary>
/// Delegates to vanilla town NPC AI (aiStyle 7) for natural wandering.
/// Completes after <see cref="Duration"/> ticks, or runs indefinitely.
/// </summary>
public class WanderBehavior : NPCBehavior
{
    private int elapsed;

    /// <summary>Ticks before completion. -1 = indefinite.</summary>
    public int Duration { get; set; } = -1;

    public WanderBehavior()
    {
        OverrideVanillaAI = false;
    }

    public override void OnStart(NPC npc)
    {
        elapsed = 0;
        npc.aiStyle = 7;
        npc.netUpdate = true;
    }

    public override void Update(NPC npc)
    {
        // Vanilla AI handles everything. We only track duration
        if (Duration >= 0)
        {
            elapsed++;
            if (elapsed >= Duration)
                Complete();
        }
    }

    public WanderBehavior WithDuration(int ticks) { Duration = ticks; return this; }
}