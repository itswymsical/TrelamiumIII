using System;
using Terraria;
using Terraria.ID;

namespace TrelamiumIII.Core.NPCs.Behaviors;

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
    /// Opens a closed door in the NPC's way, as vanilla town NPCs do, and records it in <see cref="NPC.closeDoor"/> for
    /// <see cref="TryCloseDoor"/>. Server or singleplayer only; the door state is synced.
    /// </summary>
    public static bool TryOpenDoor(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        int x = (int)((npc.Center.X + 15f * npc.direction) / 16f);
        int y = (int)((npc.position.Y + npc.height - 16f) / 16f) - 2;
        Tile tile = Framing.GetTileSafely(x, y);

        if (!tile.HasUnactuatedTile || !TileLoader.IsClosedDoor(tile))
            return false;

        foreach (int side in new[] { npc.direction, -npc.direction })
        {
            if (!WorldGen.OpenDoor(x, y, side))
                continue;

            npc.closeDoor = true;
            npc.doorX = x;
            npc.doorY = y;
            npc.netUpdate = true;
            NetMessage.SendData(MessageID.ToggleDoorState, number: 0, number2: x, number3: y, number4: side);
            return true;
        }

        return false;
    }

    /// <summary>Shuts the door <see cref="TryOpenDoor"/> opened once the NPC is more than two tiles past it.</summary>
    public static void TryCloseDoor(NPC npc)
    {
        if (!npc.closeDoor || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        float tileX = npc.Center.X / 16f;
        if (Math.Abs(tileX - npc.doorX) <= 2f)
            return;

        if (TileLoader.CloseDoorID(Framing.GetTileSafely(npc.doorX, npc.doorY)) < 0)
        {
            npc.closeDoor = false;
            return;
        }

        if (WorldGen.CloseDoor(npc.doorX, npc.doorY))
        {
            npc.closeDoor = false;
            NetMessage.SendData(MessageID.ToggleDoorState, number: 1, number2: npc.doorX, number3: npc.doorY, number4: npc.direction);
        }
        else if (Math.Abs(tileX - npc.doorX) > 4f || Math.Abs(npc.Center.Y / 16f - npc.doorY) > 4f)
        {
            // Something is standing in the doorway and the NPC has moved on.
            npc.closeDoor = false;
        }
    }

    /// <summary>
    /// True when the NPC is standing on solid ground. Gates on velocity.Y == 0,
    /// which the engine sets when tile collision zeroes vertical movement.
    /// </summary>
    public static bool IsOnGround(NPC npc) => npc.velocity.Y == 0f;

    /// <summary>
    /// Pins vanilla town AI to its "standing around" state.
    /// <para>
    /// A released NPC picks up from whatever is in these slots, so behaviours that
    /// hold an NPC still set them - otherwise it wanders off the instant the director
    /// hands it back, mid-conversation.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Leaves vanilla's listening states (4, and 17 for rock-paper-scissors) alone, since a town NPC starting a chat writes
    /// them into its partner. Leaves <see cref="WorldNPC"/>s alone: their base AI resumes from its own state.
    /// </remarks>
    public static void PinTownIdle(NPC npc)
    {
        if (npc.ModNPC is WorldNPC || npc.ai[0] is 4f or 17f)
            return;

        npc.ai[0] = 0f;
        npc.ai[1] = 300f;
    }

    /// <summary>Rows the floor may rise or fall from one column to the next in <see cref="TraceFloor" />.</summary>
    private const int MaxFloorStep = 1;

    /// <summary>
    /// The leftmost and rightmost columns reachable on foot from <paramref name="anchor" /> (a body row), at most
    /// <paramref name="radius" /> tiles out and inside <paramref name="bounds" />. Walls, drops and closed doors end it,
    /// unless <paramref name="throughDoors" />.
    /// </summary>
    public static (int Left, int Right) TraceFloor(Point anchor, int radius, Rectangle? bounds, bool throughDoors)
    {
        int y = anchor.Y;

        if (!Standable(anchor.X, y, throughDoors))
        {
            if (Standable(anchor.X, y - 1, throughDoors))
                y--;
            else if (Standable(anchor.X, y + 1, throughDoors))
                y++;
            else
                return (anchor.X, anchor.X);
        }

        return (TraceFloor(anchor.X, y, -1, radius, bounds, throughDoors), TraceFloor(anchor.X, y, 1, radius, bounds, throughDoors));
    }

    private static int TraceFloor(int x, int y, int direction, int radius, Rectangle? bounds, bool throughDoors)
    {
        for (int i = 0; i < radius; i++)
        {
            int next = x + direction;

            if (bounds is { } area && (next < area.Left || next >= area.Right))
                break;

            int? row = null;

            for (int step = 0; step <= MaxFloorStep && row == null; step++)
            {
                if (Standable(next, y - step, throughDoors) && Clear(x, y - 2 - step, throughDoors))
                    row = y - step;
                else if (step > 0 && Standable(next, y + step, throughDoors))
                    row = y + step;
            }

            if (row == null)
                break;

            x = next;
            y = row.Value;
        }

        return x;
    }

    /// <summary>Floor under the column and three rows of headroom above it.</summary>
    private static bool Standable(int x, int y, bool throughDoors)
    {
        if (!WorldGen.InWorld(x, y, 10))
            return false;

        Tile floor = Main.tile[x, y + 1];
        bool hasFloor = floor.HasUnactuatedTile && (Main.tileSolid[floor.TileType] || Main.tileSolidTop[floor.TileType]);

        return hasFloor && Clear(x, y, throughDoors) && Clear(x, y - 1, throughDoors) && Clear(x, y - 2, throughDoors);
    }

    private static bool Clear(int x, int y, bool throughDoors)
    {
        Tile tile = Framing.GetTileSafely(x, y);

        if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
            return true;

        return throughDoors && TileLoader.IsClosedDoor(tile);
    }
}
