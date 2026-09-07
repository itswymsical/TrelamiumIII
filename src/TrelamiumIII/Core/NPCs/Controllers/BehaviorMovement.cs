using System;
using Terraria;
using Terraria.ID;

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
