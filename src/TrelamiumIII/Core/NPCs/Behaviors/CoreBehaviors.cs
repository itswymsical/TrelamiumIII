using Terraria.GameContent.UI;
using TrelamiumIII.Utilities.Extensions;

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

        if (target?.IsActive() != true || target is Player { dead: true })
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
