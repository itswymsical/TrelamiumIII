using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Shockwaves;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     One of the <see cref="WoodenWardenBoss" />'s two hands. It never picks its own attacks: the
///     boss writes an <see cref="ArmOrder" /> into <see cref="Entity.ai" />[0] and this executes it.
/// </summary>
public class WardenArm : ModNPC
{
    public enum ArmOrder
    {
        Hover,
        Punch,
        PalmSlam,
        Clasp,
        Grapple,
        Grabbed,
        Recover,
        Toss,
    }

    #region sheet

    // 256x84 sheet, four 64x84 columns: 0 closed fist, 1 soft open, 2 open hand, 3 release.

    private const int FRAME_WIDTH = 64;
    private const int FRAME_HEIGHT = 84;

    private const int FRAME_FIST = 0;
    private const int FRAME_SOFT = 1;
    private const int FRAME_OPEN = 2;
    private const int FRAME_RELEASE = 3;

    #endregion

    #region tuning

    /// <summary>Half-extents of the idle orbit around the boss, in world pixels.</summary>
    private const float ORBIT_X = 190f;

    private const float ORBIT_Y = 72f;

    /// <summary>
    ///     Centre of the idle orbit, relative to the boss, in world pixels. Held well out from the
    ///     body: the hands are the warden's reach, and they read as ranged only at a distance.
    /// </summary>
    private const float ORBIT_SPREAD = 300f;

    private const float ORBIT_LIFT = -40f;

    /// <summary>Ticks for one pass of the idle orbit. The vertical term runs at twice this rate.</summary>
    private const float ORBIT_PERIOD = 214f;

    /// <summary>Peak fraction of the way toward the target the idle orbit leans, once per pass.</summary>
    private const float ORBIT_LEAN = 0.2f;

    private const float IDLE_SPEED = 15f;

    /// <summary>Ticks of hovering before the arm reports itself free for a new order.</summary>
    private const int HOVER_DWELL = 46;

    /// <summary>Extra dwell on the right hand, so the two never come off cooldown together.</summary>
    private const int HOVER_STAGGER = 29;

    private const int PUNCH_WIND_TICKS = 34;
    private const float PUNCH_SPEED = 21f;
    private const int PUNCH_FLIGHT_TICKS = 52;

    /// <summary>How far behind and above the boss the wind-up carries the fist, in world pixels.</summary>
    private const float SHOULDER_BACK = 86f;

    private const float SHOULDER_SPREAD = 64f;
    private const float SHOULDER_LIFT = 74f;

    private const int PALM_RISE_TICKS = 40;
    private const float PALM_HEIGHT = 230f;
    private const float PALM_ACCELERATION = 1.7f;
    private const float PALM_SPEED = 22f;
    private const int PALM_FALL_TICKS = 70;

    private const int CLASP_WIND_TICKS = 46;

    /// <summary>Half the gap the two fists open to before converging, in world pixels.</summary>
    private const float CLASP_SPREAD = 230f;

    private const float CLASP_SPEED = 19f;
    private const int CLASP_FLIGHT_TICKS = 40;

    /// <summary>Distance to the committed point at which a clasp counts as closed.</summary>
    private const float CLASP_ARRIVE = 26f;

    private const float GRAPPLE_SPEED = 11f;
    private const int GRAPPLE_TICKS = 110;

    /// <summary>Ticks the player is held before the arm commits to the table slam.</summary>
    private const int CRUSH_TICKS = 190;

    /// <summary>Hits needed to break the grab. Zero is inescapable.</summary>
    private const int BREAK_HITS = 3;

    private const int BREAK_HITS_EXPERT = 5;
    private const int BREAK_HITS_MASTER = 0;

    /// <summary>Pixels per tick the grab lifts the player toward the boss.</summary>
    private const float GRAB_LIFT = 2.6f;

    private const float GRAB_LIFT_MAX = 210f;

    /// <summary>Damage the master-mode crush deals per tick once the hold is established.</summary>
    private const int CRUSH_TICK_DAMAGE = 12;

    private const int CRUSH_TICK_INTERVAL = 38;

    private const int TABLE_SLAM_DAMAGE = 90;
    private const float TABLE_SLAM_SPEED = 26f;
    private const int TABLE_SLAM_TICKS = 60;

    private const int RECOVER_TICKS = 96;
    private const float RECOVER_SHAKE = 3.5f;

    private const int IMPACT_DAMAGE = 30;

    /// <summary>Ticks of travel before a strike will accept a terrain hit.</summary>
    private const int IMPACT_GRACE_TICKS = 4;

    /// <summary>
    ///     255 on the wire for "nobody": the byte <see cref="grabbed" /> rides in has no room for a
    ///     signed sentinel, and no player ever occupies that slot.
    /// </summary>
    private const byte NO_GRAB = 255;

    /// <summary>Half-extents of the band an arm impact hurts, in world pixels.</summary>
    private const float IMPACT_REACH_X = 95f;

    private const float IMPACT_REACH_Y = 68f;

    private const int TOSS_SETTLE_TICKS = 34;

    /// <summary>Where the hand stands off to throw from, relative to the target, in world pixels.</summary>
    private const float TOSS_STANDOFF = 330f;

    private const float TOSS_LIFT = 120f;

    private const int TOSS_COUNT = 2;
    private const int TOSS_INTERVAL = 30;

    /// <summary>Ticks the release frame is held after a throw.</summary>
    private const int TOSS_RELEASE_TICKS = 11;

    /// <summary>Flight speed the ballistic lead is solved against, in pixels per tick.</summary>
    private const float TOSS_SPEED = 14f;

    private const int TOSS_DAMAGE = 26;
    private const float TOSS_RECOIL = 6f;

    #endregion

    #region audio

    private static readonly SoundStyle SwingSound = SoundID.Item1 with { Volume = 0.75f, Pitch = -0.6f };
    private static readonly SoundStyle ImpactSound = Assets.SFX.NPC.WoodHit2.Asset with { Volume = 1.2f, Pitch = -0.5f };
    private static readonly SoundStyle GrabSound = Assets.SFX.Snap.Asset with { Volume = 1f, Pitch = -0.3f };
    private static readonly SoundStyle SlamSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 0.9f, Pitch = -0.2f };

    #endregion

    public ArmOrder Order
    {
        get => (ArmOrder)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float OrderTimer => ref NPC.ai[1];

    private ref float BossIndex => ref NPC.ai[3];

    /// <summary>-1 for the left hand, 1 for the right. Set by the boss at spawn and never changes.</summary>
    public int Side { get; set; } = -1;

    /// <summary>True once the arm has finished its order and dwelled. The boss polls this.</summary>
    public bool AwaitingOrder => Order == ArmOrder.Hover && OrderTimer >= HOVER_DWELL + (Side > 0 ? HOVER_STAGGER : 0);

    /// <summary>The point an order committed to at the end of its wind-up.</summary>
    private Vector2 anchor;

    /// <summary>Index of the held player, or -1.</summary>
    private int grabbed = -1;

    private int hitsTaken;

    /// <summary>Set once a held player has run out of time and the arm is driving them into the floor.</summary>
    private bool tableSlamming;

    private int column = FRAME_OPEN;

    private float drawShake;

    /// <summary>Contact damage as tModLoader scaled it, before the AI starts overwriting the field.</summary>
    private int baseDamage;

    private NPC? Boss
    {
        get
        {
            int index = (int)BossIndex;

            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC boss = Main.npc[index];

            return boss.active && boss.type == ModContent.NPCType<WoodenWardenBoss>() ? boss : null;
        }
    }

    private Player Target => Main.player[NPC.target];

    private static int BreakHits => Main.masterMode ? BREAK_HITS_MASTER : Main.expertMode ? BREAK_HITS_EXPERT : BREAK_HITS;

    public override void SetStaticDefaults()
    {
        // Frames are columns, so the rectangle is built by hand in FindFrame and vanilla's row
        // arithmetic is never used.
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.TrailCacheLength[Type] = 5;
        NPCID.Sets.TrailingMode[Type] = 4;
    }

    public override void SetDefaults()
    {
        NPC.width = FRAME_WIDTH;
        NPC.height = FRAME_HEIGHT;

        NPC.damage = 18;
        NPC.defense = 12;
        NPC.lifeMax = 520;

        NPC.aiStyle = -1;

        NPC.noTileCollide = true;
        NPC.noGravity = true;
        NPC.lavaImmune = true;

        NPC.knockBackResist = 0f;

        NPC.HitSound = Assets.SFX.NPC.WoodHit2.Asset with
        {
            Volume = 1.4f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };

        NPC.DeathSound = Assets.SFX.NPC.WoodenKilled.Asset with { Volume = 1f };
    }

    public override bool CheckActive()
    {
        if (Boss is null)
            NPC.active = false;

        return false;
    }

    public override void AI()
    {
        if (Boss is not { } boss)
            return;

        if (baseDamage <= 0)
            baseDamage = NPC.damage;

        // Inherited rather than retargeted: a clasp needs both hands agreeing on one point.
        NPC.target = boss.target;

        drawShake = 0f;

        switch (Order)
        {
            case ArmOrder.Hover:
                UpdateHover(boss);
                break;

            case ArmOrder.Punch:
                UpdatePunch(boss);
                break;

            case ArmOrder.PalmSlam:
                UpdatePalmSlam();
                break;

            case ArmOrder.Clasp:
                UpdateClasp();
                break;

            case ArmOrder.Grapple:
                UpdateGrapple();
                break;

            case ArmOrder.Grabbed:
                UpdateGrabbed();
                break;

            case ArmOrder.Recover:
                UpdateRecover(boss);
                break;

            case ArmOrder.Toss:
                UpdateToss();
                break;
        }

        OrderTimer++;
    }

    private void Finish(ArmOrder order)
    {
        Order = order;
        OrderTimer = -1;

        NPC.netUpdate = true;
    }

    #region orders

    private void UpdateHover(NPC boss)
    {
        NPC.damage = 0;
        column = FRAME_OPEN;

        Vector2 point = IdleAnchor(boss);

        Move(point, IDLE_SPEED);
        AimAt(Target is { active: true } ? Target.Center : point);
    }

    /// <summary>
    ///     A 1:2 figure around the boss, phase-shifted by side, leaning toward the target once per
    ///     pass. Driven by the boss's free-running clock so both hands stay deterministic and synced.
    /// </summary>
    private Vector2 IdleAnchor(NPC boss)
    {
        float t = boss.ai[WoodenWardenBoss.ARM_CLOCK_SLOT] / ORBIT_PERIOD * MathHelper.TwoPi
                  + (Side > 0 ? MathHelper.Pi : 0f);

        Vector2 point = boss.Center
                        + new Vector2(ORBIT_SPREAD * Side, ORBIT_LIFT)
                        + new Vector2(MathF.Cos(t) * ORBIT_X, MathF.Sin(t * 2f) * ORBIT_Y);

        if (Target is not { active: true } target)
            return point;

        return Vector2.Lerp(point, target.Center, Math.Max(MathF.Sin(t), 0f) * ORBIT_LEAN);
    }

    private void UpdatePunch(NPC boss)
    {
        if (OrderTimer < PUNCH_WIND_TICKS)
        {
            NPC.damage = 0;

            float progress = OrderTimer / PUNCH_WIND_TICKS;

            column = progress < 0.4f ? FRAME_OPEN : progress < 0.75f ? FRAME_SOFT : FRAME_FIST;

            Vector2 shoulder = ShoulderPoint(boss);

            Move(shoulder, MathHelper.Lerp(16f, 4f, progress));
            AimAt(Target is { active: true } ? Target.Center : shoulder);

            return;
        }

        if (OrderTimer == PUNCH_WIND_TICKS)
        {
            anchor = Target is { active: true } target ? target.Center : NPC.Center + Vector2.UnitY * 200f;

            NPC.velocity = NPC.DirectionTo(anchor).SafeNormalize(Vector2.UnitY) * PUNCH_SPEED;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound, NPC.Center);
        }

        // Straight, not homing: the wind-up is the tell, and a fist that tracks has no punish window.
        NPC.damage = baseDamage;
        column = FRAME_FIST;

        AimAt(NPC.Center + NPC.velocity);

        if (HitSomething(OrderTimer - PUNCH_WIND_TICKS) || OrderTimer >= PUNCH_WIND_TICKS + PUNCH_FLIGHT_TICKS)
        {
            Impact(0.8f);
            Finish(ArmOrder.Recover);
        }
    }

    /// <summary>Over the boss's far shoulder, away from the target.</summary>
    private Vector2 ShoulderPoint(NPC boss)
    {
        Vector2 away = Target is { active: true } target
            ? (boss.Center - target.Center).SafeNormalize(Vector2.UnitY)
            : Vector2.UnitY;

        return boss.Center + away * SHOULDER_BACK + new Vector2(SHOULDER_SPREAD * Side, -SHOULDER_LIFT);
    }

    private void UpdatePalmSlam()
    {
        column = FRAME_OPEN;

        if (OrderTimer < PALM_RISE_TICKS)
        {
            NPC.damage = 0;

            Vector2 above = Target is { active: true } target
                ? target.Center - Vector2.UnitY * PALM_HEIGHT
                : NPC.Center;

            Move(above, 17f);
            AimAt(NPC.Center + Vector2.UnitY);

            return;
        }

        if (OrderTimer == PALM_RISE_TICKS)
        {
            anchor = Target is { active: true } target ? target.Center : NPC.Center + Vector2.UnitY * 200f;

            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.4f }, NPC.Center);
        }

        NPC.damage = baseDamage;

        NPC.velocity.X = Math.Clamp(anchor.X - NPC.Center.X, -5f, 5f);
        NPC.velocity.Y = Math.Min(NPC.velocity.Y + PALM_ACCELERATION, PALM_SPEED);

        AimAt(NPC.Center + Vector2.UnitY);

        if (HitSomething(OrderTimer - PALM_RISE_TICKS) || OrderTimer >= PALM_RISE_TICKS + PALM_FALL_TICKS)
        {
            Impact(1f);
            Finish(ArmOrder.Recover);
        }
    }

    /// <summary>Stands off to one side of the target and lobs boulders at it.</summary>
    private void UpdateToss()
    {
        NPC.damage = 0;

        Vector2 standoff = Target is { active: true } target
            ? target.Center + new Vector2(TOSS_STANDOFF * Side, -TOSS_LIFT)
            : NPC.Center;

        Move(standoff, OrderTimer < TOSS_SETTLE_TICKS ? 17f : 5f);
        AimAt(Target is { active: true } aim ? aim.Center : standoff);

        if (OrderTimer < TOSS_SETTLE_TICKS)
        {
            column = FRAME_OPEN;
            return;
        }

        float since = OrderTimer - TOSS_SETTLE_TICKS;
        float phase = since % TOSS_INTERVAL;

        column = phase < TOSS_RELEASE_TICKS ? FRAME_RELEASE : FRAME_OPEN;

        if (since / TOSS_INTERVAL >= TOSS_COUNT)
        {
            Finish(ArmOrder.Recover);
            return;
        }

        if (phase == 0f)
            Throw();
    }

    private void Throw()
    {
        if (Target is not { active: true, dead: false } target)
            return;

        SoundEngine.PlaySound(SwingSound with { Pitch = -0.2f }, NPC.Center);

        Vector2 toTarget = target.Center - NPC.Center;

        // Fixed-flight-time lead: the boulder falls under its own gravity, so a straight shot at
        // the target lands short of it by half an acceleration over the flight.
        float flight = Math.Max(toTarget.Length() / TOSS_SPEED, 1f);

        Vector2 velocity = toTarget / flight;

        velocity.Y -= 0.5f * WardenBoulder.GRAVITY * flight;

        WardenBoulder.Throw(NPC, NPC.Center, velocity, Scaled(TOSS_DAMAGE));

        NPC.velocity -= velocity.SafeNormalize(Vector2.Zero) * TOSS_RECOIL;
        NPC.netUpdate = true;
    }

    private void UpdateClasp()
    {
        if (OrderTimer < CLASP_WIND_TICKS)
        {
            NPC.damage = 0;

            float progress = OrderTimer / CLASP_WIND_TICKS;

            column = progress < 0.6f ? FRAME_OPEN : FRAME_SOFT;

            Vector2 flank = Target is { active: true } target
                ? target.Center + new Vector2(CLASP_SPREAD * Side, 0f)
                : NPC.Center;

            Move(flank, MathHelper.Lerp(18f, 5f, progress));
            AimAt(Target is { active: true } aim ? aim.Center : flank);

            return;
        }

        if (OrderTimer == CLASP_WIND_TICKS)
        {
            anchor = Target is { active: true } target ? target.Center : NPC.Center;

            NPC.velocity = NPC.DirectionTo(anchor).SafeNormalize(Vector2.UnitX * Side) * CLASP_SPEED;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.75f }, NPC.Center);
        }

        NPC.damage = baseDamage;
        column = FRAME_FIST;

        AimAt(anchor);

        bool closed = Vector2.DistanceSquared(NPC.Center, anchor) < CLASP_ARRIVE * CLASP_ARRIVE;

        if (closed || HitSomething(OrderTimer - CLASP_WIND_TICKS) || OrderTimer >= CLASP_WIND_TICKS + CLASP_FLIGHT_TICKS)
        {
            // Scaled down rather than suppressed on one hand: both fists land, and the other may
            // be dead. The second hit is absorbed by the player's immunity frames either way.
            Impact(0.75f);
            Finish(ArmOrder.Recover);
        }
    }

    private void UpdateGrapple()
    {
        NPC.damage = 0;

        float progress = OrderTimer / GRAPPLE_TICKS;

        column = progress < 0.35f ? FRAME_OPEN : FRAME_SOFT;

        if (Target is not { active: true, dead: false } target)
        {
            Finish(ArmOrder.Recover);
            return;
        }

        Move(target.Center, GRAPPLE_SPEED);
        AimAt(target.Center);

        if (NPC.Hitbox.Intersects(target.Hitbox) && Main.netMode != NetmodeID.MultiplayerClient)
        {
            grabbed = target.whoAmI;
            hitsTaken = 0;
            tableSlamming = false;

            // Where the lift is measured from. Without it the rest point is re-derived from the
            // arm's own position every tick and the hold climbs without bound.
            anchor = NPC.Center;

            SoundEngine.PlaySound(GrabSound, NPC.Center);

            Finish(ArmOrder.Grabbed);

            return;
        }

        if (OrderTimer >= GRAPPLE_TICKS)
            Finish(ArmOrder.Recover);
    }

    private void UpdateGrabbed()
    {
        NPC.damage = 0;
        column = FRAME_FIST;

        if (grabbed < 0 || grabbed >= Main.maxPlayers || Main.player[grabbed] is not { active: true, dead: false } held)
        {
            Release();
            return;
        }

        HoldPlayer(held);

        if (Main.netMode != NetmodeID.MultiplayerClient)
            CountBreakHits();

        if (tableSlamming)
        {
            UpdateTableSlam();
            return;
        }

        if (Main.masterMode)
        {
            // Inescapable by design: the hold runs until the player is dead rather than timing out.
            if (OrderTimer > 0 && OrderTimer % CRUSH_TICK_INTERVAL == 0)
                CrushDamage(held, CRUSH_TICK_DAMAGE);

            drawShake = RECOVER_SHAKE;

            return;
        }

        if (OrderTimer >= CRUSH_TICKS)
        {
            tableSlamming = true;
            OrderTimer = 0;

            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.9f }, NPC.Center);
        }
    }

    /// <summary>Carries the held player up toward the boss. Each client only moves its own player.</summary>
    private void HoldPlayer(Player held)
    {
        NPC.velocity = Vector2.Zero;

        if (!tableSlamming)
        {
            Vector2 rest = anchor - Vector2.UnitY * Math.Min(OrderTimer * GRAB_LIFT, GRAB_LIFT_MAX);

            NPC.Center = Vector2.Lerp(NPC.Center, rest, 0.12f);
        }

        AimAt(NPC.Center + Vector2.UnitY);

        if (held.whoAmI != Main.myPlayer)
            return;

        held.Center = Vector2.Lerp(held.Center, NPC.Center, 0.35f);
        held.velocity = Vector2.Zero;
        held.jump = 0;
        held.gfxOffY = 0f;
    }

    private void CountBreakHits()
    {
        if (!NPC.justHit)
            return;

        hitsTaken++;

        NPC.netUpdate = true;

        int needed = BreakHits;

        if (needed <= 0)
            return;

        if (!Main.dedServ)
            CombatText.NewText(NPC.Hitbox, Color.Orange, $"{hitsTaken}/{needed}", true);

        if (hitsTaken >= needed)
        {
            SoundEngine.PlaySound(SoundID.Item32, NPC.Center);

            Release();
        }
    }

    /// <summary>Drives the held player into the floor, then lets go.</summary>
    private void UpdateTableSlam()
    {
        NPC.velocity = Vector2.UnitY * TABLE_SLAM_SPEED;
        NPC.Center += NPC.velocity;

        drawShake = RECOVER_SHAKE;

        if (!HitSomething(OrderTimer) && OrderTimer < TABLE_SLAM_TICKS)
            return;

        if (grabbed >= 0 && grabbed < Main.maxPlayers)
            CrushDamage(Main.player[grabbed], TABLE_SLAM_DAMAGE);

        Impact(1f);
        Release();
    }

    private void CrushDamage(Player held, int damage)
    {
        if (held.whoAmI != Main.myPlayer || held.dead)
            return;

        held.Hurt(
            PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(held.name + CrushQuotes())),
            Scaled(damage),
            0
        );

        if (Main.dedServ)
            return;

        for (int i = 0; i < 12; i++)
            Dust.NewDust(held.position, held.width, held.height, DustID.Blood, newColor: Color.DarkRed, Scale: 2f);
    }

    private void Release()
    {
        grabbed = -1;
        hitsTaken = 0;
        tableSlamming = false;

        Finish(ArmOrder.Recover);
    }

    private void UpdateRecover(NPC boss)
    {
        NPC.damage = 0;
        column = FRAME_RELEASE;

        drawShake = RECOVER_SHAKE * (1f - OrderTimer / RECOVER_TICKS);

        NPC.velocity *= 0.86f;

        AimAt(boss.Center);

        if (OrderTimer >= RECOVER_TICKS)
            Finish(ArmOrder.Hover);
    }

    #endregion

    #region impact

    /// <summary>
    ///     Whether the arm is in terrain, once it has been travelling long enough for the answer to
    ///     mean anything. <paramref name="since" /> is ticks spent moving; a wind-up that ends
    ///     inside a cave wall would otherwise land the strike on the frame it launched.
    /// </summary>
    private bool HitSomething(float since)
        => since >= IMPACT_GRACE_TICKS && Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

    private void Impact(float scale)
    {
        Vector2 impact = NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitY) * (NPC.height * 0.5f);

        NPC.velocity = Vector2.Zero;

        HurtBand(impact, scale);

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(scale >= 1f ? SlamSound : ImpactSound, impact);

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)(11f * scale));

        TileShockwaveSystem.Spawn(new BlockWave(impact, 155f * scale, lifetime: 26)
        {
            Amplitude = 16f * scale,
            Thickness = 72f * scale,
            Reach = 118f * scale,
            Blend = 0.6f,
        });

        for (int i = 0; i < 14; i++)
        {
            Dust.NewDustPerfect(
                impact + Main.rand.NextVector2Circular(30f * scale, 10f),
                DustID.WoodFurniture,
                new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, -1f)),
                100,
                Color.White,
                Main.rand.NextFloat(0.9f, 1.6f)
            );
        }
    }

    /// <summary>
    ///     Each client hurts only itself; Player.Hurt is not safe to call for a remote player, and a
    ///     dedicated server has no local player.
    /// </summary>
    private void HurtBand(Vector2 impact, float scale)
    {
        foreach (Player target in Main.ActivePlayers)
        {
            if (target.whoAmI != Main.myPlayer || target.dead || target.immune)
                continue;

            Vector2 offset = target.Center - impact;

            if (Math.Abs(offset.X) > IMPACT_REACH_X * scale || Math.Abs(offset.Y) > IMPACT_REACH_Y * scale)
                continue;

            target.Hurt(
                PlayerDeathReason.ByNPC(NPC.whoAmI),
                (int)(Scaled(IMPACT_DAMAGE) * scale),
                offset.X < 0f ? -1 : 1,
                knockback: 6f
            );
        }
    }

    /// <summary>
    ///     Difficulty scaling for damage the arm deals outside contact. tModLoader only scales
    ///     <see cref="NPC.damage" />, which these numbers deliberately do not go through.
    /// </summary>
    private static int Scaled(int damage) => Main.masterMode
        ? (int)(damage * 1.5f)
        : Main.expertMode
            ? (int)(damage * 1.2f)
            : damage;

    #endregion

    #region movement

    private void Move(Vector2 position, float maxSpeed)
    {
        Vector2 toPoint = position - NPC.Center;

        float speed = Math.Min(toPoint.Length() * 0.14f, maxSpeed);

        NPC.velocity = Vector2.SmoothStep(NPC.velocity, toPoint.SafeNormalize(Vector2.Zero) * speed, 0.2f);
    }

    /// <summary>Turns the hand so the sprite's up axis points at <paramref name="position" />.</summary>
    private void AimAt(Vector2 position)
    {
        Vector2 toPoint = position - NPC.Center;

        if (toPoint.LengthSquared() < 1f)
            return;

        NPC.rotation = NPC.rotation.AngleLerp(toPoint.ToRotation() + MathHelper.PiOver2, 0.25f);
    }

    #endregion

    public override void FindFrame(int frameHeight)
    {
        NPC.frame = new Rectangle(Math.Clamp(column, 0, 3) * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        // Custom, because vanilla's origin is half the whole sheet rather than half a column.
        Texture2D texture = TextureAssets.Npc[Type].Value;

        Vector2 position = NPC.Center + new Vector2(0f, NPC.gfxOffY);

        if (drawShake > 0f)
            position += Main.rand.NextVector2Circular(drawShake, drawShake);

        spriteBatch.Draw(
            texture,
            position - screenPos,
            NPC.frame,
            NPC.GetAlpha(drawColor),
            NPC.rotation,
            new Vector2(FRAME_WIDTH, FRAME_HEIGHT) * 0.5f,
            NPC.scale,
            Side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f
        );

        return false;
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ || NPC.life <= 0)
            return;

        for (int i = 0; i < 3; i++)
        {
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture,
                hit.HitDirection * 1.4f, -0.8f, Scale: 1.1f);
        }
    }

    /// <summary>A dead arm stops running its AI, so dropping the index is all the release needs.</summary>
    public override void OnKill() => grabbed = -1;

    public static string CrushQuotes()
    {
        string[] quotes =
        [
            " was crushed into a pool of blood.",
            " was turned into a ball of bramble.",
            " was brutally squeezed to death.",
            " couldn't break free.",
            " couldn't wiggle their way out of this one.",
        ];

        return quotes[Main.rand.Next(quotes.Length)];
    }

    #region net

    /// <summary>
    ///     State outside <see cref="NPC.ai" />. <see cref="Side" /> is here because it decides the
    ///     idle phase, and a client that guessed at it would mirror both hands onto one path.
    /// </summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((sbyte)Side);

        writer.Write(anchor.X);
        writer.Write(anchor.Y);

        writer.Write(grabbed is >= 0 and < NO_GRAB ? (byte)grabbed : NO_GRAB);
        writer.Write((byte)Math.Clamp(hitsTaken, 0, byte.MaxValue));
        writer.Write(tableSlamming);

        writer.Write((byte)Math.Clamp(column, 0, 3));
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Side = reader.ReadSByte() < 0 ? -1 : 1;

        anchor = new Vector2(reader.ReadSingle(), reader.ReadSingle());

        byte held = reader.ReadByte();

        grabbed = held == NO_GRAB ? -1 : held;
        hitsTaken = reader.ReadByte();
        tableSlamming = reader.ReadBoolean();

        column = reader.ReadByte();
    }

    #endregion
}
