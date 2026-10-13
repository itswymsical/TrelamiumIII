using System.Collections.Generic;
using Terraria.GameContent.UI;

namespace TrelamiumIII.Core.NPCs.Behaviors;

/// <summary>Stands still for a fixed number of ticks. The glue between real behaviours.</summary>
public sealed class Wait : Behavior
{
    private readonly int ticks;

    public Wait(int ticks)
    {
        this.ticks = ticks;
    }

    protected override void Run(BehaviorContext ctx)
    {
        BehaviorMovement.Decelerate(ctx.NPC);

        if (Age + 1 >= ticks)
            Finish();
    }
}

/// <summary>Runs a callback once and finishes. For one-shot effects inside a sequence.</summary>
public sealed class Do : Behavior
{
    private readonly Action<BehaviorContext> action;

    public Do(Action<BehaviorContext> action)
    {
        this.action = action;
    }

    protected override void Run(BehaviorContext ctx)
    {
        action?.Invoke(ctx);
        Finish();
    }
}

/// <summary>Ticks a callback until it returns true.</summary>
public sealed class Until : Behavior
{
    private readonly Func<BehaviorContext, bool> tick;

    public Until(Func<BehaviorContext, bool> tick)
    {
        this.tick = tick;
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (tick == null || tick(ctx))
            Finish();
    }
}

/// <summary>
/// Stands around, bleeding off velocity, optionally emoting.
/// Runs forever unless given a duration.
/// </summary>
public sealed class Idle : Behavior
{
    private static readonly int[] DefaultEmotes =
    [
        EmoteID.EmoteNote,
        EmoteID.EmoteConfused,
        EmoteID.EmoteHappiness,
        EmoteID.WeatherSunny,
    ];

    private int emoteTimer;
    private int nextEmote;

    /// <summary>Ticks before finishing. -1 runs until something else takes over.</summary>
    public int Duration { get; set; } = -1;

    /// <summary>Average ticks between emote bubbles. 0 disables them.</summary>
    public int EmoteInterval { get; set; }

    /// <summary>Emotes to draw from. Null uses a small generic set.</summary>
    public int[] EmotePool { get; set; }

    protected override void Begin(BehaviorContext ctx)
    {
        emoteTimer = 0;
        nextEmote = EmoteInterval > 0 ? Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.8f)) : 0;
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);

        TickEmotes(npc);

        if (Duration >= 0 && Age + 1 >= Duration)
            Finish();
    }

    private void TickEmotes(NPC npc)
    {
        if (EmoteInterval <= 0 || ++emoteTimer < nextEmote)
            return;

        EmoteBubble.NewBubble(Main.rand.Next(EmotePool ?? DefaultEmotes), new WorldUIAnchor(npc), 120);
        emoteTimer = 0;
        nextEmote = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.8f));
    }

    public Idle For(int ticks) { Duration = ticks; return this; }

    public Idle WithEmotes(int interval, int[] pool = null)
    {
        EmoteInterval = interval;
        EmotePool = pool;
        return this;
    }
}

/// <summary>
/// Holds a spot. Drift past the tolerance - knockback, a shove, a bad landing - and
/// the NPC walks back, climbing anything in the way. Runs forever.
/// </summary>
public sealed class Stay : Behavior
{
    /// <summary>Anchor in tile coordinates. Defaults to wherever the NPC stood at entry.</summary>
    public Point? AnchorTile { get; set; }

    /// <summary>Tiles of slack before walking back.</summary>
    public int Tolerance { get; set; } = 2;

    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    protected override void Begin(BehaviorContext ctx) => AnchorTile ??= ctx.NPC.Center.ToTileCoordinates();

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        Point anchor = AnchorTile.Value;
        Point current = npc.Center.ToTileCoordinates();

        if (Math.Abs(current.X - anchor.X) > Tolerance)
        {
            BehaviorMovement.WalkTowardX(npc, anchor.ToWorldCoordinates().X, Speed);

            // Without this the NPC grinds against the first ledge between it and home.
            if (!BehaviorMovement.TryJump(npc))
                BehaviorMovement.TryGapJump(npc);
        }
        else
        {
            BehaviorMovement.Decelerate(npc);
        }

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    public Stay At(Point tile) { AnchorTile = tile; return this; }
    public Stay At(Vector2 worldPosition) { AnchorTile = worldPosition.ToTileCoordinates(); return this; }
    public Stay WithTolerance(int tiles) { Tolerance = tiles; return this; }
    public Stay WithSpeed(float speed) { Speed = speed; return this; }
}

/// <summary>
/// Walks to a world position and finishes on arrival, jumping ledges and gaps on the
/// way. Teleports if it has made no progress for long enough, so a script can't stall
/// forever on bad terrain.
/// </summary>
public sealed class WalkTo : Behavior
{
    private int stuckTimer;

    public WalkTo(Vector2 worldPosition)
    {
        Target = worldPosition;
    }

    public Vector2 Target { get; }

    public float Speed { get; set; } = BehaviorMovement.DefaultWalkSpeed;

    /// <summary>How close counts as arrived, in pixels.</summary>
    public float ArrivalDistance { get; set; } = 24f;

    /// <summary>Ticks of no progress before giving up and teleporting. 0 disables.</summary>
    public int StuckTimeout { get; set; } = 300;

    protected override void Begin(BehaviorContext ctx) => stuckTimer = 0;

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.WalkTowardX(npc, Target.X, Speed, stopDistance: ArrivalDistance);

        if (Math.Abs(Target.X - npc.Center.X) > ArrivalDistance && !BehaviorMovement.TryJump(npc))
            BehaviorMovement.TryGapJump(npc);

        BehaviorMovement.ApplyTerrainCollision(npc);

        if (Vector2.Distance(npc.Center, Target) <= ArrivalDistance)
        {
            Finish();
            return;
        }

        if (StuckTimeout <= 0)
            return;

        if (npc.position.X == npc.oldPosition.X && Math.Abs(npc.velocity.X) < 0.1f)
            stuckTimer++;
        else
            stuckTimer = 0;

        if (stuckTimer < StuckTimeout)
            return;

        npc.position = Target - new Vector2(npc.width * 0.5f, npc.height);
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;
        Finish();
    }

    public WalkTo WithSpeed(float speed) { Speed = speed; return this; }
    public WalkTo WithArrival(float pixels) { ArrivalDistance = pixels; return this; }
    public WalkTo WithStuckTimeout(int ticks) { StuckTimeout = ticks; return this; }
}

/// <summary>
/// Trails an entity, keeping roughly <see cref="Distance"/> pixels away. Teleports
/// when left far enough behind that walking would never catch up.
/// </summary>
public sealed class Follow : Behavior
{
    private const int StuckThreshold = 30;

    private readonly Func<BehaviorContext, Entity> resolveTarget;

    private float lastJumpX;
    private int stuckTicks;

    public Follow(Func<BehaviorContext, Entity> target)
    {
        resolveTarget = target;
    }

    /// <summary>How close to get before standing still.</summary>
    public float Distance { get; set; } = 80f;

    /// <summary>Distance at which walking is abandoned in favour of teleporting.</summary>
    public float TeleportThreshold { get; set; } = 2000f;

    public float Speed { get; set; } = 1.5f;

    public float Acceleration { get; set; } = 0.1f;

    /// <summary>Finish once the target is reached, instead of shadowing it forever.</summary>
    public bool FinishOnArrival { get; set; }

    protected override void Begin(BehaviorContext ctx)
    {
        lastJumpX = ctx.NPC.position.X;
        stuckTicks = 0;
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        Entity target = resolveTarget?.Invoke(ctx);

        if (target is not { active: true } || target is Player { dead: true })
        {
            BehaviorMovement.Decelerate(npc);
            return;
        }

        if (Vector2.Distance(npc.Center, target.Center) > TeleportThreshold)
        {
            TeleportNear(npc, target);
            return;
        }

        if (Math.Abs(target.Center.X - npc.Center.X) <= Distance)
        {
            BehaviorMovement.Decelerate(npc);
            ctx.Face(target.Center);
            stuckTicks = 0;

            BehaviorMovement.ApplyTerrainCollision(npc);

            if (FinishOnArrival)
                Finish();

            return;
        }

        BehaviorMovement.WalkTowardX(npc, target.Center.X, Speed, Acceleration, Distance);

        bool jumped = BehaviorMovement.TryJump(npc);
        if (!jumped && BehaviorMovement.IsOnGround(npc))
            jumped = BehaviorMovement.TryGapJump(npc);

        if (jumped)
            lastJumpX = npc.position.X;

        TrackStuck(npc);

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    /// <summary>
    /// Mid-air with no horizontal progress means the jump went into a wall. Turn
    /// around rather than pinballing off it forever.
    /// </summary>
    private void TrackStuck(NPC npc)
    {
        if (npc.velocity.Y == 0f)
        {
            stuckTicks = 0;
            return;
        }

        if (Math.Abs(npc.position.X - lastJumpX) >= 2f)
        {
            stuckTicks = 0;
            lastJumpX = npc.position.X;
            return;
        }

        if (++stuckTicks <= StuckThreshold)
            return;

        npc.direction *= -1;
        npc.spriteDirection = npc.direction;
        npc.velocity.X = npc.direction * Speed;
        stuckTicks = 0;
        npc.netUpdate = true;
    }

    private void TeleportNear(NPC npc, Entity target)
    {
        int direction = target is Player player ? player.direction : 1;
        Vector2 destination = target.Bottom + new Vector2(Distance * -direction, 0f);

        int tileX = (int)(destination.X / 16f);
        int tileY = (int)(destination.Y / 16f);

        for (int y = tileY; y < tileY + 12; y++)
        {
            Tile tile = Framing.GetTileSafely(tileX, y);
            if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
                continue;

            npc.position.X = tileX * 16f + 8f - npc.width * 0.5f;
            npc.position.Y = y * 16f - npc.height;
            Settle(npc);
            return;
        }

        npc.position = target.Bottom - new Vector2(npc.width * 0.5f, npc.height);
        Settle(npc);
    }

    private void Settle(NPC npc)
    {
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;
        stuckTicks = 0;
        lastJumpX = npc.position.X;
    }

    public Follow WithDistance(float pixels) { Distance = pixels; return this; }
    public Follow WithSpeed(float speed, float acceleration = 0.1f) { Speed = speed; Acceleration = acceleration; return this; }
    public Follow WithTeleport(float threshold) { TeleportThreshold = threshold; return this; }
    public Follow Once() { FinishOnArrival = true; return this; }
}

/// <summary>
/// Turns to face the nearest player every tick and nothing else. Usually reached
/// through <see cref="BehaviorExtensions.FacingNearestPlayer"/> instead.
/// </summary>
public sealed class FacePlayer : Behavior
{
    public float Range { get; set; } = 400f;

    protected override void Run(BehaviorContext ctx)
    {
        Player player = ctx.NearestPlayer(Range);

        if (player != null)
            ctx.Face(player.Center);
    }
}

/// <summary>Snaps to a position and finishes.</summary>
public sealed class Teleport : Behavior
{
    public Teleport(Vector2 worldPosition)
    {
        Target = worldPosition;
    }

    public Vector2 Target { get; }

    /// <summary>Puff of dust at both ends of the hop.</summary>
    public bool SpawnDust { get; set; } = true;

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        if (SpawnDust)
            Puff(npc);

        npc.position = Target - new Vector2(npc.width * 0.5f, npc.height);
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;

        if (SpawnDust)
            Puff(npc);

        Finish();
    }

    private static void Puff(NPC npc)
    {
        for (int i = 0; i < 15; i++)
        {
            Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.MagicMirror);
            dust.noGravity = true;
            dust.velocity *= 0.5f;
        }
    }

    public Teleport WithDust(bool dust) { SpawnDust = dust; return this; }
}

/// <summary>
/// Paces a stretch of floor like an idle town NPC: stands a while, now and then turns around or strikes up a chat with
/// an NPC nearby, then strolls to another spot, opening and shutting doors on the way. The floor is traced out from
/// <see cref="AnchorTile"/> before every stroll and ends at walls, drops, <see cref="Bounds"/>, and closed doors while
/// <see cref="ThroughDoors"/> says no. Runs forever.
/// </summary>
/// <remarks>
/// The server picks where to go and stores it in <c>ai[3]</c> (0 while standing), which <see cref="MessageID.SyncNPC"/>
/// carries, so clients walk the same stroll instead of rolling their own. Not <c>ai[2]</c>: a vanilla town NPC starting
/// a chat writes its partner's index there.
/// </remarks>
public sealed class Wander : Behavior
{
    /// <summary>Rows the floor may rise or fall from one column to the next.</summary>
    private const int MaxStep = 1;

    /// <summary>Ticks of no horizontal progress before a stroll is given up.</summary>
    private const int StuckTimeout = 60;

    /// <summary>Furthest away, in pixels, another NPC can be and still get walked over to for a chat.</summary>
    private const float ChatReach = 240f;

    private int standTimer;
    private int turnAt;
    private int stuckTimer;
    private bool doorsOpen;

    /// <summary>Tile the NPC stands on (its lowest body row). Defaults to wherever it stood at entry.</summary>
    public Point? AnchorTile { get; set; }

    /// <summary>Tile area the NPC may not leave, such as its home. Null bounds it by <see cref="Radius"/> alone.</summary>
    public Rectangle? Bounds { get; set; }

    /// <summary>Furthest a stroll may end from the anchor, in tiles.</summary>
    public int Radius { get; set; } = 12;

    public float Speed { get; set; } = 1f;

    public int MinStand { get; set; } = 180;

    public int MaxStand { get; set; } = 600;

    /// <summary>Whether strolls may lead through closed doors, asked before each one. Null never does.</summary>
    public Func<bool>? ThroughDoors { get; set; }

    /// <summary>One in this many standing ticks looks for an NPC nearby to chat with. 0 never does.</summary>
    public int ChatChance { get; set; }

    private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

    protected override void Begin(BehaviorContext ctx)
    {
        AnchorTile ??= ctx.NPC.Bottom.ToTileCoordinates() - new Point(0, 1);

        if (Authority)
            Stand(ctx.NPC);
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        float targetX = npc.ai[3];

        if (targetX > 0f)
            Stroll(npc, targetX);
        else
            Idle(ctx);

        BehaviorMovement.TryCloseDoor(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    protected override void End(BehaviorContext ctx, bool canceled) => ctx.NPC.ai[3] = 0f;

    private void Idle(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);

        if (!Authority)
            return;

        if (ChatChance > 0 && Main.rand.NextBool(ChatChance) && Converse.TryStart(ctx, ChatReach))
            return;

        standTimer--;

        if (standTimer == turnAt)
        {
            npc.direction = -npc.direction;
            npc.spriteDirection = npc.direction;
            npc.netUpdate = true;
        }

        if (standTimer <= 0)
            PickTarget(npc);
    }

    private void Stroll(NPC npc, float targetX)
    {
        BehaviorMovement.WalkTowardX(npc, targetX, Speed);

        if (!Authority)
            return;

        BehaviorMovement.TryOpenDoor(npc);

        stuckTimer = npc.position.X == npc.oldPosition.X ? stuckTimer + 1 : 0;

        if (Math.Abs(targetX - npc.Center.X) <= 8f || stuckTimer >= StuckTimeout)
            Stand(npc);
    }

    private void Stand(NPC npc)
    {
        npc.ai[3] = 0f;
        npc.netUpdate = true;

        standTimer = Main.rand.Next(MinStand, MaxStand + 1);
        turnAt = Main.rand.NextBool(3) ? Main.rand.Next(standTimer / 4, standTimer * 3 / 4) : -1;
        stuckTimer = 0;
    }

    private void PickTarget(NPC npc)
    {
        doorsOpen = ThroughDoors?.Invoke() ?? false;

        (int left, int right) = TraceFloor(AnchorTile.Value);
        int current = (int)(npc.Center.X / 16f);

        // Off the traced floor, say outdoors once the doors are off limits: the stroll is the walk back.
        if (current < left || current > right)
        {
            npc.ai[3] = Main.rand.Next(left, right + 1) * 16f + 8f;
            npc.netUpdate = true;
            return;
        }

        List<int> options = [];

        for (int x = left; x <= right; x++)
        {
            if (Math.Abs(x - current) >= 3)
                options.Add(x);
        }

        if (options.Count == 0)
        {
            Stand(npc);
            return;
        }

        npc.ai[3] = options[Main.rand.Next(options.Count)] * 16f + 8f;
        npc.netUpdate = true;
        stuckTimer = 0;
    }

    /// <summary>The leftmost and rightmost columns reachable on foot from <paramref name="anchor"/>.</summary>
    private (int Left, int Right) TraceFloor(Point anchor)
    {
        int y = anchor.Y;

        if (!Standable(anchor.X, y))
        {
            if (Standable(anchor.X, y - 1))
                y--;
            else if (Standable(anchor.X, y + 1))
                y++;
            else
                return (anchor.X, anchor.X);
        }

        return (Trace(anchor.X, y, -1), Trace(anchor.X, y, 1));
    }

    private int Trace(int x, int y, int direction)
    {
        for (int i = 0; i < Radius; i++)
        {
            int next = x + direction;

            if (Bounds is { } bounds && (next < bounds.Left || next >= bounds.Right))
                break;

            int? row = null;

            for (int step = 0; step <= MaxStep && row == null; step++)
            {
                if (Standable(next, y - step) && Clear(x, y - 2 - step))
                    row = y - step;
                else if (step > 0 && Standable(next, y + step))
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
    private bool Standable(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 10))
            return false;

        Tile floor = Main.tile[x, y + 1];
        bool hasFloor = floor.HasUnactuatedTile && (Main.tileSolid[floor.TileType] || Main.tileSolidTop[floor.TileType]);

        return hasFloor && Clear(x, y) && Clear(x, y - 1) && Clear(x, y - 2);
    }

    private bool Clear(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);

        if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
            return true;

        return doorsOpen && TileLoader.IsClosedDoor(tile);
    }

    public Wander At(Point tile) { AnchorTile = tile; return this; }
    public Wander Within(Rectangle tiles) { Bounds = tiles; return this; }
    public Wander WithRadius(int tiles) { Radius = tiles; return this; }
    public Wander WithSpeed(float speed) { Speed = speed; return this; }
    public Wander WithStand(int min, int max) { MinStand = min; MaxStand = max; return this; }
    public Wander UsingDoors(Func<bool> when) { ThroughDoors = when; return this; }
    public Wander Chatting(int chance = 300) { ChatChance = chance; return this; }
}

/// <summary>
/// Stands facing whoever has the NPC's chat window open, the way vanilla town NPCs turn to the player they're talking
/// to. Finishes once nobody is.
/// </summary>
public sealed class Listen : Behavior
{
    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        Player talker = TownChat.Talker(npc);

        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);

        if (talker == null)
        {
            Finish();
            return;
        }

        ctx.Face(talker.Center);
    }
}

public static class TownChat
{
    /// <summary>The nearest player with this NPC's chat window open, or null.</summary>
    public static Player Talker(NPC npc)
    {
        Player best = null;

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.talkNPC != npc.whoAmI)
                continue;

            if (best == null || npc.DistanceSQ(player.Center) < npc.DistanceSQ(best.Center))
                best = player;
        }

        return best;
    }
}

/// <summary>
/// Stands with another NPC trading emote bubbles, the way vanilla town NPCs chat. The one that starts it walks over
/// first and raises the bubbles for both; the other just faces it. Finishes after <see cref="Duration"/> ticks of talk,
/// or early if the partner leaves or a player opens this NPC's chat window.
/// </summary>
public sealed class Converse : Behavior
{
    /// <summary>Gap, in pixels, the one that starts walks to before talking. Vanilla pairs up at 20-100.</summary>
    private const float TalkDistance = 48f;

    private const float GiveUpDistance = 400f;

    /// <summary>Ticks between rounds of bubbles; the leader's comes early in a round, the partner's later.</summary>
    private const int Round = 300;

    private const int MaxApproach = 600;

    private readonly int partner;
    private readonly bool lead;

    private bool arrived;
    private int talked;

    public Converse(int partner, int duration, bool lead)
    {
        this.partner = partner;
        this.lead = lead;
        Duration = duration;
    }

    /// <summary>Ticks of talk once face to face.</summary>
    public int Duration { get; }

    protected override void Begin(BehaviorContext ctx)
    {
        arrived = !lead;
        talked = 0;
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        NPC other = Main.npc[partner];

        if (!other.active || npc.Distance(other.Center) > GiveUpDistance || TownChat.Talker(npc) != null || talked >= Duration
            || (!arrived && Age >= MaxApproach))
        {
            Finish();
            return;
        }

        if (!arrived && Math.Abs(other.Center.X - npc.Center.X) > TalkDistance)
        {
            BehaviorMovement.WalkTowardX(npc, other.Center.X, 1f, stopDistance: TalkDistance);
            BehaviorMovement.TryOpenDoor(npc);
        }
        else
        {
            arrived = true;
            BehaviorMovement.Decelerate(npc);
            BehaviorMovement.PinTownIdle(npc);
            ctx.Face(other.Center);

            if (lead)
                RaiseBubbles(npc, other);

            talked++;
        }

        BehaviorMovement.TryCloseDoor(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    private void RaiseBubbles(NPC npc, NPC other)
    {
        int beat = talked % Round;

        if (beat == 20)
            EmoteBubble.NewBubbleNPC(new WorldUIAnchor(npc), 100, new WorldUIAnchor(other));
        else if (beat == 150 && other.CanTalk)
            EmoteBubble.NewBubbleNPC(new WorldUIAnchor(other), 90, new WorldUIAnchor(npc));
    }

    /// <summary>
    /// Looks for an NPC within <paramref name="reach"/> pixels on about the same floor that is in sight and free to talk,
    /// and starts a chat with it: this NPC through an interrupt on its director, the partner through vanilla's listening
    /// state or, when its own director is running a script, an interrupt of its own. Server or singleplayer only.
    /// </summary>
    public static bool TryStart(BehaviorContext ctx, float reach)
    {
        NPC npc = ctx.NPC;

        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI == npc.whoAmI || !other.CanBeTalkedTo || other.wet || Math.Abs(other.Bottom.Y - npc.Bottom.Y) > 24f)
                continue;

            float distance = npc.Distance(other.Center);
            if (distance < 20f || distance > reach || TownChat.Talker(other) != null || !Collision.CanHit(npc.Center, 0, 0, other.Center, 0, 0))
                continue;

            int duration = 420 * Main.rand.Next(1, 4);

            if (!TryRecruit(npc, other, duration))
                continue;

            ctx.Director.Interrupt(npc, new Converse(other.whoAmI, duration, lead: true));
            return true;
        }

        return false;
    }

    private static bool TryRecruit(NPC npc, NPC other, int duration)
    {
        NPCDirector director = other.TryGetGlobalNPC(out NPCDirector found) && found.Enabled ? found : null;

        if (director is { IsBusy: true })
        {
            if (director.IsInterrupted || other.ModNPC is not WorldNPC)
                return false;

            director.Interrupt(other, new Converse(npc.whoAmI, duration, lead: false));
            return true;
        }

        // Vanilla town AI. Past walking it is already busy: sitting, talking, partying.
        if (other.aiStyle != NPCAIStyleID.Passive || other.ai[0] > 1f || (other.ai[0] == 1f && other.closeDoor))
            return false;

        other.ai[0] = 4f;
        other.ai[1] = duration + MaxApproach;
        other.ai[2] = npc.whoAmI;
        other.direction = npc.Center.X < other.Center.X ? -1 : 1;
        other.velocity.X = 0f;
        other.netUpdate = true;
        return true;
    }
}
