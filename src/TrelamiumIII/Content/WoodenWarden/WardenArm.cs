using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Shockwaves;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     One of the <see cref="WoodenWardenBoss" />'s hands. The boss hands it an <see cref="ArmState" /> through
///     <see cref="Command" /> and this carries it out; it never picks its own attacks.
/// </summary>
[AutoloadBossHead]
public sealed class WardenArm : ModNPC
{
    public enum ArmState
    {
        Idle = 0,

        Punch,
        PalmSlam,
        Clasp,
        Grapple,
        Grabbed,
        Spear,
        Recover,
        Flinch,
        Taunt,
        Tantrum,
    }

    #region sheet

    // 256x84 sheet, four 64x84 columns with the hand at the bottom of each: 0 closed fist, 1 soft open, 2 open, 3 release.

    private const int FRAME_WIDTH = 64;
    private const int FRAME_HEIGHT = 84;

    private const int FRAME_FIST = 0;
    private const int FRAME_SOFT = 1;
    private const int FRAME_OPEN = 2;
    private const int FRAME_RELEASE = 3;

    /// <summary>Distance from the frame centre to the middle of the hand, along the sprite's down axis.</summary>
    private const float HAND_REACH = 26f;

    #endregion

    #region tuning

    /// <summary>Idle station relative to the boss, in world pixels: low beside the body, held out for range.</summary>
    private const float STANCE_X = 150f;

    private const float STANCE_Y = 50f;

    /// <summary>Radians the idle hand is turned out from the body.</summary>
    private const float STANCE_TILT = 0.4f;

    /// <summary>Half-extents of the figure the idle station traces, in world pixels.</summary>
    private const float ORBIT_X = 26f;

    private const float ORBIT_Y = 16f;

    /// <summary>Ticks for one pass of the idle figure. The vertical term runs at twice this rate.</summary>
    private const float ORBIT_PERIOD = 214f;

    /// <summary>Peak fraction of the way toward the target the idle station leans, once per pass.</summary>
    private const float ORBIT_LEAN = 0.16f;

    private const float IDLE_SPEED = 15f;

    /// <summary>Ticks idle before the arm reports itself free for a new order.</summary>
    private const int IDLE_DWELL = 46;

    /// <summary>Extra dwell on the right hand, so the two never come free together.</summary>
    private const int IDLE_STAGGER = 29;

    private const int PUNCH_WIND_TICKS = 34;
    private const float PUNCH_SPEED = 17.5f;
    private const int PUNCH_FLIGHT_TICKS = 52;

    /// <summary>How far behind and above the boss the wind-up carries the fist, in world pixels.</summary>
    private const float SHOULDER_BACK = 86f;

    private const float SHOULDER_SPREAD = 64f;
    private const float SHOULDER_LIFT = 74f;

    /// <summary>Peak tremble over the last third of a wind-up.</summary>
    private const float WIND_SHAKE = 3f;

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

    /// <summary>Distance either side of the clasp point each hand stops at, so the knuckles meet rather than overlap.</summary>
    private const float CLASP_CONTACT = 34f;

    /// <summary>Distance to its stop point at which a hand counts as closed.</summary>
    private const float CLASP_ARRIVE = 22f;

    private const float GRAPPLE_SPEED = 11f;
    private const int GRAPPLE_TICKS = 110;

    /// <summary>Ticks the player is held before the arm commits to the table slam.</summary>
    private const int CRUSH_TICKS = 190;

    /// <summary>Hits needed to break the grab. Zero is inescapable.</summary>
    private const int BREAK_HITS = 3;

    private const int BREAK_HITS_EXPERT = 5;
    private const int BREAK_HITS_MASTER = 0;

    /// <summary>Pixels per tick the elbow rises while holding, and how far it rises in all.</summary>
    private const float GRAB_LIFT = 2.6f;

    private const float GRAB_LIFT_MAX = 120f;

    /// <summary>Distance from the hand to the imaginary elbow the forearm pivots on while holding, in world pixels.</summary>
    private const float ELBOW_REACH = 96f;

    /// <summary>Ticks the hand takes to swing up over the elbow after the catch.</summary>
    private const int GRAB_RAISE_TICKS = 24;

    /// <summary>Radians the held hand leans outward from straight up.</summary>
    private const float GRAB_TILT = 0.25f;

    /// <summary>Damage the master-mode crush deals per interval once the hold is established.</summary>
    private const int CRUSH_TICK_DAMAGE = 12;

    private const int CRUSH_TICK_INTERVAL = 38;

    /// <summary>Squeeze on a held player outside master mode, 0..1.</summary>
    private const float GRIP_SQUEEZE = 0.12f;

    /// <summary>Ticks the master-mode crush takes to reach full squeeze.</summary>
    private const float CRUSH_BUILD_TICKS = 300f;

    private const int TABLE_SLAM_DAMAGE = 90;

    /// <summary>Ticks the hand leans back against the swing, and how far, in radians.</summary>
    private const int TABLE_WIND_TICKS = 12;

    private const float TABLE_WIND_LEAN = 0.45f;

    /// <summary>Ticks of the swing over the elbow, and the angle it sweeps from overhead down to the floor.</summary>
    private const int TABLE_SWING_TICKS = 16;

    private const float TABLE_SWING = 2.6f;

    /// <summary>Height of the hand's centre above the floor when the swing lands.</summary>
    private const float TABLE_CLEARANCE = 18f;

    /// <summary>Tiles searched below the elbow for the floor the swing lands on.</summary>
    private const int TABLE_FLOOR_SEARCH = 40;

    /// <summary>Knockback on the player the swing lands, outward along the swing.</summary>
    private const float TABLE_THROW = 9f;

    private const int FLINCH_TICKS = 50;

    /// <summary>Wrist shake at the start of a flinch, in radians, and its rate in radians of phase per tick.</summary>
    private const float FLINCH_SHAKE = 0.55f;

    private const float FLINCH_RATE = 1.3f;

    /// <summary>Speed the hand jerks away from the player it lost hold of.</summary>
    private const float FLINCH_RECOIL = 10f;

    private const int SPEAR_RAISE_TICKS = 24;
    private const int SPEAR_FORM_TICKS = 40;
    private const int SPEAR_CHARGE_TICKS = 48;

    /// <summary>Ticks before the throw the aim stops tracking; the throw goes where the lock flash pointed.</summary>
    private const int SPEAR_LOCK_TICKS = 14;

    /// <summary>Ticks the white flash takes to fade after the lock.</summary>
    private const int SPEAR_FLASH_TICKS = 8;

    /// <summary>Where the throwing hand holds the pike, relative to the boss, in world pixels.</summary>
    private const float SPEAR_HOLD_X = 170f;

    private const float SPEAR_HOLD_Y = -120f;

    /// <summary>How far the hand draws back across the charge, in world pixels.</summary>
    private const float SPEAR_DRAWBACK = 46f;

    private const float SPEAR_SHAKE = 3.2f;

    /// <summary>Follow-through speed after the release.</summary>
    private const float SPEAR_RECOIL = 9f;

    private const int SPEAR_DAMAGE = 16;
    private const int SPEAR_DAMAGE_EXPERT = 20;
    private const int SPEAR_DAMAGE_MASTER = 24;

    private const int RECOVER_TICKS = 90;
    private const float RECOVER_SHAKE = 3.5f;

    /// <summary>Imaginary shoulder the taunt and the tantrum swing each hand from, relative to the boss's centre.</summary>
    private const float JOINT_X = 78f;

    private const float JOINT_Y = -30f;

    /// <summary>Ticks per knuckle smash in the taunt.</summary>
    public const int TAUNT_PERIOD = 24;

    /// <summary>Where the fists meet, below the boss's centre, and half the gap between their centres there.</summary>
    private const float TAUNT_Y = 72f;

    private const float TAUNT_CONTACT = 16f;

    /// <summary>Radians each hand swings out from the meeting point between smashes.</summary>
    private const float TAUNT_SWING = 1f;

    /// <summary>How far, 0..1, a swung-out fist turns from facing the other toward pointing along its arc.</summary>
    private const float TAUNT_FACING_FOLLOW = 0.35f;

    /// <summary>Ticks the hands take to gather in front of the body before they lock onto the rhythm.</summary>
    private const int TAUNT_GATHER_TICKS = 20;

    /// <summary>Shoulder-to-hand distance of the tantrum swing, in world pixels.</summary>
    private const float TANTRUM_REACH = 150f;

    /// <summary>Right-hand angle of a fist raised over the head, in radians; mirrored for the left.</summary>
    private const float TANTRUM_RAISED = -1.75f;

    /// <summary>Height of the hand's centre above the floor when a pound lands.</summary>
    private const float TANTRUM_CLEARANCE = 16f;

    /// <summary>Depth under the floor a revived arm starts from, in world pixels.</summary>
    public const float EMERGE_DEPTH = 80f;

    /// <summary>Distance from the sprite's centre that bounds it at any rotation, for the under-floor test.</summary>
    private const float SPRITE_BOUND = 50f;

    /// <summary>Ticks per pound at the start and at the end of the tantrum; the period shrinks linearly between.</summary>
    private const float TANTRUM_PERIOD_START = 30f;

    private const float TANTRUM_PERIOD_END = 9f;

    private const float TANTRUM_SHAKE = 4f;

    /// <summary>Side of a splinter cut from the sheet when the arm breaks, in pixels.</summary>
    private const int SPLINTER_CELL = 8;

    private const int IMPACT_DAMAGE = 30;

    /// <summary>Ticks of travel before a strike will accept a terrain hit.</summary>
    private const int IMPACT_GRACE_TICKS = 4;

    /// <summary>Half-extents of the band an arm impact hurts, in world pixels.</summary>
    private const float IMPACT_REACH_X = 95f;

    private const float IMPACT_REACH_Y = 68f;

    /// <summary>255 on the wire for "nobody": the byte <see cref="grabbed" /> rides in has no room for a signed sentinel.</summary>
    private const byte NO_GRAB = 255;

    /// <summary>Squared distance within which the local screen reacts to this arm's impacts.</summary>
    private const float SHAKE_RANGE_SQ = 1400f * 1400f;

    #endregion

    #region audio

    private static readonly SoundStyle SwingSound = SoundID.Item1 with { Volume = 0.75f, Pitch = -0.6f };
    private static readonly SoundStyle ImpactSound = Assets.SFX.NPC.WoodHit2.Asset with { Volume = 1.2f, Pitch = -0.5f };
    private static readonly SoundStyle GrabSound = Assets.SFX.Snap.Asset with { Volume = 1f, Pitch = -0.3f };
    private static readonly SoundStyle SlamSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 0.9f, Pitch = -0.2f };
    private static readonly SoundStyle ClapSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 0.8f, Pitch = 0.2f };
    private static readonly SoundStyle ClackSound = Assets.SFX.NPC.WoodHit2.Asset with { Volume = 1f, Pitch = -0.1f, MaxInstances = 2 };

    // Stand-ins until the pike has its own sounds.
    private static readonly SoundStyle FormSound = SoundID.Item8 with { Volume = 0.9f, Pitch = -0.4f };
    private static readonly SoundStyle LockSound = SoundID.MaxMana with { Volume = 1f, Pitch = -0.2f };
    private static readonly SoundStyle ThrowSound = SoundID.DD2_JavelinThrowersAttack with { Volume = 1.1f, Pitch = -0.3f };

    #endregion

    public ArmState State
    {
        get => (ArmState)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    private ref float BossIndex => ref NPC.ai[3];

    /// <summary>-1 for the left hand, 1 for the right. Set by the boss at spawn and never changes.</summary>
    public int Side { get; set; } = -1;

    /// <summary>True once the arm has finished its order and dwelled. The boss polls this.</summary>
    public bool AwaitingOrder => State == ArmState.Idle && StateTimer >= IDLE_DWELL + (Side > 0 ? IDLE_STAGGER : 0);

    /// <summary>The point an order committed to at the end of its wind-up; while holding, the elbow's base.</summary>
    private Vector2 anchor;

    /// <summary>Index of the held player, or -1.</summary>
    private int grabbed = -1;

    private int hitsTaken;

    /// <summary>Set once a held player has run out of time and the arm is swinging them into the floor.</summary>
    private bool tableSlamming;

    /// <summary>Forearm direction at the catch, in radians; the hold swings up from it.</summary>
    private float catchAngle;

    /// <summary>Direction the pike points, in radians. Synced at the lock so every client telegraphs the real throw.</summary>
    private float spearAim;

    /// <summary>Whether the current state was entered this tick, and so still owes its tick 0.</summary>
    private bool entered;

    /// <summary>Contact damage as tModLoader scaled it, before the AI starts overwriting the field.</summary>
    private int baseDamage;

    // Cosmetic; derived every tick and never synced.
    private int column = FRAME_OPEN;
    private float drawShake;

    /// <summary>Set once this arm's splinters have been thrown by the death burst.</summary>
    private bool burst;

    /// <summary>World Y of the floor the tantrum pounds; see <see cref="DrawBehind" />.</summary>
    private float tantrumFloor;

    /// <summary>Part of the sprite is under <see cref="tantrumFloor" />, so the arm draws behind the tiles.</summary>
    private bool underFloor;

    /// <summary>Set once a revived arm has burst up out of the floor.</summary>
    private bool emerged;

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

    private bool HasTarget => Target is { active: true, dead: false };

    private Vector2 HandPosition => NPC.Center + new Vector2(0f, HAND_REACH).RotatedBy(NPC.rotation);

    private static int BreakHits => Main.masterMode ? BREAK_HITS_MASTER : Main.expertMode ? BREAK_HITS_EXPERT : BREAK_HITS;

    private bool NearLocalPlayer => !Main.dedServ && Main.LocalPlayer.DistanceSQ(NPC.Center) < SHAKE_RANGE_SQ;

    public override void SetStaticDefaults()
    {
        // Frames are columns, so the rectangle is built by hand and vanilla's row arithmetic is never used.
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
    }

    public override void SetDefaults()
    {
        NPC.width = FRAME_WIDTH;
        NPC.height = FRAME_HEIGHT;

        NPC.damage = 18;
        NPC.defense = 12;
        NPC.lifeMax = 440;

        NPC.aiStyle = -1;

        NPC.noTileCollide = true;
        NPC.noGravity = true;
        NPC.lavaImmune = true;

        NPC.knockBackResist = 0f;

        // Hidden from the ordinary NPC pass; DrawBehind puts it in a draw cache instead.
        NPC.hide = true;

        NPC.HitSound = Assets.SFX.NPC.WoodHit2.Asset with
        {
            Volume = 1.4f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };

        NPC.DeathSound = Assets.SFX.NPC.WoodenKilled.Asset with { Volume = 1f };
    }

    /// <summary>Lives and dies with the boss; <see cref="AI" /> removes it once the boss is gone.</summary>
    public override bool CheckActive() => false;

    /// <summary>
    ///     Drawn after players, so the hands sit in front of the body and of a held player. Behind the tiles instead while
    ///     part of it is under the floor, which is how a revived arm comes up out of the ground.
    /// </summary>
    public override void DrawBehind(int index)
    {
        if (underFloor)
            Main.instance.DrawCacheNPCsBehindNonSolidTiles.Add(index);
        else
            Main.instance.DrawCacheNPCsOverPlayers.Add(index);
    }

    /// <summary>Starts <paramref name="order" />, letting go of anyone held. Server-side; the state rides the synced ai block.</summary>
    public void Command(ArmState order)
    {
        grabbed = -1;
        hitsTaken = 0;
        tableSlamming = false;

        SetState(order);
    }

    private void SetState(ArmState state)
    {
        State = state;
        StateTimer = 0f;

        // Claims this tick's increment, so the new state's tick 0 still runs.
        entered = true;

        NPC.netUpdate = true;
    }

    public override void AI()
    {
        if (Boss is not { } boss)
        {
            Despawn();
            return;
        }

        if (baseDamage <= 0)
            baseDamage = NPC.damage;

        entered = false;

        // Inherited rather than retargeted: a clasp needs both hands agreeing on one point.
        NPC.target = boss.target;

        drawShake = 0f;
        underFloor = false;

        NPC.dontTakeDamage = State is ArmState.Taunt or ArmState.Tantrum
                             || boss.ModNPC is WoodenWardenBoss { Performing: true };

        switch (State)
        {
            case ArmState.Punch:
                UpdatePunch(boss);
                break;

            case ArmState.PalmSlam:
                UpdatePalmSlam();
                break;

            case ArmState.Clasp:
                UpdateClasp();
                break;

            case ArmState.Grapple:
                UpdateGrapple();
                break;

            case ArmState.Grabbed:
                UpdateGrabbed();
                break;

            case ArmState.Spear:
                UpdateSpear(boss);
                break;

            case ArmState.Recover:
                UpdateRecover();
                break;

            case ArmState.Flinch:
                UpdateFlinch();
                break;

            case ArmState.Taunt:
                UpdateTaunt(boss);
                break;

            case ArmState.Tantrum:
                UpdateTantrum(boss);
                break;

            default:
                UpdateIdle(boss);
                break;
        }

        NPC.damage = ContactDamage;

        if (!entered)
            StateTimer++;
    }

    /// <summary>Removes the arm without a death. The server syncs it; a client that lost the boss first just follows.</summary>
    private void Despawn()
    {
        NPC.life = 0;
        NPC.active = false;
        NPC.netSkip = -1;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }

    /// <summary>Contact damage for the current pose: only a strike in flight hurts to touch.</summary>
    private int ContactDamage => State switch
    {
        ArmState.Punch when StateTimer >= PUNCH_WIND_TICKS => baseDamage,
        ArmState.PalmSlam when StateTimer >= PALM_RISE_TICKS => baseDamage,
        ArmState.Clasp when StateTimer >= CLASP_WIND_TICKS => baseDamage,
        _ => 0,
    };

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        cooldownSlot = ImmunityCooldownID.Bosses;

        // Tied to ContactDamage so the two can't disagree.
        return ContactDamage > 0;
    }

    #region orders

    private void UpdateIdle(NPC boss)
    {
        column = FRAME_OPEN;

        Vector2 point = IdleAnchor(boss, out float lean);

        Move(point, IDLE_SPEED);

        float stance = -Side * STANCE_TILT;
        float rotation = HasTarget ? stance.AngleLerp(AimRotation(Target.Center), lean) : stance;

        NPC.rotation = NPC.rotation.AngleLerp(rotation, 0.15f);
    }

    /// <summary>
    ///     A small 1:2 figure beside the boss, phase-shifted by side and leaning toward the target once per pass.
    ///     Driven by the boss's synced clock so both hands stay deterministic. <paramref name="lean" /> is 0..1.
    /// </summary>
    private Vector2 IdleAnchor(NPC boss, out float lean)
    {
        float t = boss.ai[WoodenWardenBoss.ARM_CLOCK_SLOT] / ORBIT_PERIOD * MathHelper.TwoPi
                  + (Side > 0 ? MathHelper.Pi : 0f);

        Vector2 point = boss.Center
                        + new Vector2(STANCE_X * Side, STANCE_Y)
                        + new Vector2(MathF.Cos(t) * ORBIT_X, MathF.Sin(t * 2f) * ORBIT_Y);

        lean = HasTarget ? Math.Max(MathF.Sin(t), 0f) : 0f;

        return HasTarget ? Vector2.Lerp(point, Target.Center, lean * ORBIT_LEAN) : point;
    }

    private void UpdatePunch(NPC boss)
    {
        int tick = (int)StateTimer;

        if (tick < PUNCH_WIND_TICKS)
        {
            float progress = tick / (float)PUNCH_WIND_TICKS;

            column = progress < 0.4f ? FRAME_OPEN : progress < 0.75f ? FRAME_SOFT : FRAME_FIST;

            Vector2 shoulder = ShoulderPoint(boss);

            Move(shoulder, MathHelper.Lerp(16f, 4f, progress));
            AimFist(TargetCenter(shoulder + Vector2.UnitY * 200f));

            drawShake = WIND_SHAKE * Math.Clamp((progress - 0.66f) / 0.34f, 0f, 1f);

            return;
        }

        if (tick == PUNCH_WIND_TICKS)
        {
            anchor = TargetCenter(NPC.Center + Vector2.UnitY * 200f);

            NPC.velocity = (anchor - NPC.Center).SafeNormalize(Vector2.UnitY) * PUNCH_SPEED;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound, NPC.Center);
        }

        // Straight, not homing: the wind-up is the tell, and a fist that tracks has no punish window.
        column = FRAME_FIST;

        AimFist(NPC.Center + NPC.velocity);

        int flight = tick - PUNCH_WIND_TICKS;

        if (HitSomething(flight))
        {
            Impact(0.8f);
            SetState(ArmState.Recover);

            return;
        }

        // A punch that ends in open air carries no slam.
        if (flight >= PUNCH_FLIGHT_TICKS)
        {
            NPC.velocity *= 0.5f;
            SetState(ArmState.Recover);
        }
    }

    /// <summary>Over the boss's far shoulder, away from the target.</summary>
    private Vector2 ShoulderPoint(NPC boss)
    {
        Vector2 away = HasTarget ? (boss.Center - Target.Center).SafeNormalize(Vector2.UnitY) : Vector2.UnitY;

        return boss.Center + away * SHOULDER_BACK + new Vector2(SHOULDER_SPREAD * Side, -SHOULDER_LIFT);
    }

    private void UpdatePalmSlam()
    {
        int tick = (int)StateTimer;

        column = FRAME_OPEN;

        if (tick < PALM_RISE_TICKS)
        {
            Vector2 above = TargetCenter(NPC.Center + Vector2.UnitY * PALM_HEIGHT) - Vector2.UnitY * PALM_HEIGHT;

            Move(above, 17f);
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.2f);

            return;
        }

        if (tick == PALM_RISE_TICKS)
        {
            anchor = TargetCenter(NPC.Center + Vector2.UnitY * 200f);

            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.4f }, NPC.Center);
        }

        NPC.velocity.X = Math.Clamp(anchor.X - NPC.Center.X, -5f, 5f);
        NPC.velocity.Y = Math.Min(NPC.velocity.Y + PALM_ACCELERATION, PALM_SPEED);

        NPC.rotation = NPC.rotation.AngleLerp(0f, 0.2f);

        int fall = tick - PALM_RISE_TICKS;

        if (HitSomething(fall))
        {
            Impact(1f);
            SetState(ArmState.Recover);

            return;
        }

        if (fall >= PALM_FALL_TICKS)
            SetState(ArmState.Recover);
    }

    private void UpdateClasp()
    {
        int tick = (int)StateTimer;

        if (tick < CLASP_WIND_TICKS)
        {
            float progress = tick / (float)CLASP_WIND_TICKS;

            column = progress < 0.6f ? FRAME_OPEN : FRAME_SOFT;

            Vector2 flank = TargetCenter(NPC.Center) + new Vector2(CLASP_SPREAD * Side, 0f);

            Move(flank, MathHelper.Lerp(18f, 5f, progress));
            AimFist(TargetCenter(flank - new Vector2(CLASP_SPREAD * Side, 0f)));

            return;
        }

        // Each hand stops short of the shared point by its own knuckle depth, so the two fists meet on it.
        Vector2 stop = anchor + new Vector2(CLASP_CONTACT * Side, 0f);

        if (tick == CLASP_WIND_TICKS)
        {
            anchor = TargetCenter(NPC.Center);
            stop = anchor + new Vector2(CLASP_CONTACT * Side, 0f);

            NPC.velocity = (stop - NPC.Center).SafeNormalize(Vector2.UnitX * -Side) * CLASP_SPEED;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.75f }, NPC.Center);
        }

        column = FRAME_FIST;

        AimFist(anchor);

        int flight = tick - CLASP_WIND_TICKS;

        if (Vector2.DistanceSquared(NPC.Center, stop) < CLASP_ARRIVE * CLASP_ARRIVE)
        {
            NPC.velocity = Vector2.Zero;

            // One clap for the pair; the left hand owns it.
            if (Side < 0)
                Clap(anchor);

            SetState(ArmState.Recover);

            return;
        }

        if (HitSomething(flight))
        {
            Impact(0.75f);
            SetState(ArmState.Recover);

            return;
        }

        if (flight >= CLASP_FLIGHT_TICKS)
            SetState(ArmState.Recover);
    }

    private void UpdateGrapple()
    {
        column = StateTimer / GRAPPLE_TICKS < 0.35f ? FRAME_OPEN : FRAME_SOFT;

        if (!HasTarget)
        {
            SetState(ArmState.Recover);
            return;
        }

        Player target = Target;

        Move(target.Center, GRAPPLE_SPEED);
        AimFist(target.Center);

        if (NPC.Hitbox.Intersects(target.Hitbox) && Main.netMode != NetmodeID.MultiplayerClient)
        {
            grabbed = target.whoAmI;
            hitsTaken = 0;
            tableSlamming = false;

            // The elbow the hold pivots on, behind the hand along the forearm, fixed at the catch so the lift is
            // measured from one point rather than climbing with the arm.
            catchAngle = NPC.rotation + MathHelper.PiOver2;
            anchor = HandPosition - catchAngle.ToRotationVector2() * ELBOW_REACH;

            SoundEngine.PlaySound(GrabSound, NPC.Center);

            SetState(ArmState.Grabbed);

            return;
        }

        if (StateTimer >= GRAPPLE_TICKS)
            SetState(ArmState.Recover);
    }

    private void UpdateGrabbed()
    {
        column = FRAME_FIST;

        if (grabbed < 0 || grabbed >= Main.maxPlayers || Main.player[grabbed] is not { active: true, dead: false } held)
        {
            Release();
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            CountBreakHits(held);

        if (State != ArmState.Grabbed)
            return;

        if (tableSlamming)
        {
            UpdateTableSlam(held);
            return;
        }

        float raise = EaseCubicOut.Ease(Math.Clamp(StateTimer / GRAB_RAISE_TICKS, 0f, 1f));
        Vector2 elbow = anchor - Vector2.UnitY * Math.Min(StateTimer * GRAB_LIFT, GRAB_LIFT_MAX);

        // Swings up over the elbow from the catch, so the player ends up held overhead.
        Vector2 hand = Swing(elbow, catchAngle.AngleLerp(Overhead, raise), ELBOW_REACH);

        HoldPlayer(held, hand, 0.35f);

        if (Main.masterMode)
        {
            // Inescapable by design: the hold runs until the player is dead rather than timing out.
            if (StateTimer > 0 && StateTimer % CRUSH_TICK_INTERVAL == 0)
                CrushDamage(held, CRUSH_TICK_DAMAGE, 0, 0f);

            drawShake = RECOVER_SHAKE;

            return;
        }

        if (StateTimer >= CRUSH_TICKS)
        {
            tableSlamming = true;
            StateTimer = 0;

            NPC.netUpdate = true;

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.9f }, NPC.Center);
        }
    }

    /// <summary>Straight up over the elbow, leaning out to this hand's side.</summary>
    private float Overhead => -MathHelper.PiOver2 + Side * GRAB_TILT;

    /// <summary>Keeps the held player in the hand. Each client only moves its own player.</summary>
    private static void HoldPlayer(Player held, Vector2 hand, float blend)
    {
        if (held.whoAmI != Main.myPlayer)
            return;

        held.Center = Vector2.Lerp(held.Center, hand, blend);
        held.velocity = Vector2.Zero;
        held.jump = 0;
        held.gfxOffY = 0f;
    }

    private void CountBreakHits(Player held)
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

        if (hitsTaken < needed)
            return;

        SoundEngine.PlaySound(SoundID.Item32, NPC.Center);

        Flinch(held.Center);
    }

    /// <summary>Lets go and jerks the hand away from <paramref name="from" />, as if it had been burned.</summary>
    private void Flinch(Vector2 from)
    {
        grabbed = -1;
        hitsTaken = 0;
        tableSlamming = false;

        NPC.velocity = (NPC.Center - from).SafeNormalize(-Vector2.UnitY) * FLINCH_RECOIL - Vector2.UnitY * 3f;

        SetState(ArmState.Flinch);
    }

    /// <summary>
    ///     Leans back, then swings the held player over the elbow and down into the floor on the hand's own side. The
    ///     elbow drops with the swing so the hand lands on the ground; with no floor in reach it swings in place.
    /// </summary>
    private void UpdateTableSlam(Player held)
    {
        int tick = (int)StateTimer;

        Vector2 elbow = anchor - Vector2.UnitY * GRAB_LIFT_MAX;
        float leanedBack = Overhead - Side * TABLE_WIND_LEAN;

        if (tick < TABLE_WIND_TICKS)
        {
            float lean = EaseQuadOut.Ease(tick / (float)TABLE_WIND_TICKS);

            HoldPlayer(held, Swing(elbow, MathHelper.Lerp(Overhead, leanedBack, lean), ELBOW_REACH), 1f);

            drawShake = 2f * lean;

            return;
        }

        float swing = EaseQuadIn.Ease(Math.Clamp((tick - TABLE_WIND_TICKS) / (float)TABLE_SWING_TICKS, 0f, 1f));
        float end = Overhead + Side * TABLE_SWING;
        float floor = SlamFloor(elbow, end);

        if (floor > 0f)
            elbow.Y = MathHelper.Lerp(elbow.Y, floor - TABLE_CLEARANCE - MathF.Sin(end) * ELBOW_REACH, swing);

        Vector2 hand = Swing(elbow, MathHelper.Lerp(leanedBack, end, swing), ELBOW_REACH);

        HoldPlayer(held, hand, 1f);

        if (swing < 1f && !HitSomething(tick - TABLE_WIND_TICKS))
            return;

        CrushDamage(held, TABLE_SLAM_DAMAGE, Side, TABLE_THROW);
        Impact(1f, hand + Vector2.UnitY * TABLE_CLEARANCE);
        Release();
    }

    /// <summary>World Y of the floor under where the swing ends, or -1 with none within <see cref="TABLE_FLOOR_SEARCH" /> tiles.</summary>
    private static float SlamFloor(Vector2 elbow, float end)
    {
        int x = (int)((elbow.X + MathF.Cos(end) * ELBOW_REACH) / 16f);
        int start = (int)(elbow.Y / 16f);

        for (int y = start; y < start + TABLE_FLOOR_SEARCH; y++)
        {
            if (!WorldGen.InWorld(x, y, 10))
                break;

            if (WorldGen.SolidTile(x, y))
                return y * 16f;
        }

        return -1f;
    }

    private void CrushDamage(Player held, int damage, int direction, float knockback)
    {
        if (held.whoAmI != Main.myPlayer || held.dead)
            return;

        held.Hurt(
            PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(held.name + CrushQuotes())),
            Scaled(damage),
            direction,
            knockback: knockback
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

        SetState(ArmState.Recover);
    }

    /// <summary>0..1 squeeze this arm is applying to player <paramref name="player" />; master mode builds to a crush.</summary>
    private float SqueezeFor(int player)
    {
        if (State != ArmState.Grabbed || grabbed != player)
            return 0f;

        if (!Main.masterMode)
            return GRIP_SQUEEZE;

        float build = Math.Min(StateTimer / CRUSH_BUILD_TICKS, 1f);

        // A spike on every crush tick, decaying over the next ten.
        float pulse = 1f - Math.Min(StateTimer % CRUSH_TICK_INTERVAL / 10f, 1f);

        return Math.Clamp(GRIP_SQUEEZE + build * (0.85f - GRIP_SQUEEZE) + pulse * 0.15f * build, 0f, 1f);
    }

    /// <summary>Strongest squeeze any arm is applying to player <paramref name="player" />, for <see cref="WardenGripPlayer" />.</summary>
    public static float SqueezeOn(int player)
    {
        float squeeze = 0f;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is WardenArm arm)
                squeeze = Math.Max(squeeze, arm.SqueezeFor(player));
        }

        return squeeze;
    }

    /// <summary>
    ///     Raises an open palm, materialises the pike in it, draws back while it charges, locks on and throws. The aim
    ///     tracks the target until <see cref="SPEAR_LOCK_TICKS" /> before the throw.
    /// </summary>
    private void UpdateSpear(NPC boss)
    {
        int tick = (int)StateTimer;

        Vector2 hold = boss.Center + new Vector2(SPEAR_HOLD_X * Side, SPEAR_HOLD_Y);
        float toTarget = (TargetCenter(hold + Vector2.UnitY * 300f) - HandPosition).ToRotation();

        if (tick < SPEAR_RAISE_TICKS)
        {
            column = FRAME_OPEN;

            Move(hold, 17f);

            // Palm up to receive the pike.
            NPC.rotation = NPC.rotation.AngleLerp(MathHelper.Pi, 0.2f);
            spearAim = toTarget;

            return;
        }

        int form = tick - SPEAR_RAISE_TICKS;

        if (form < SPEAR_FORM_TICKS)
        {
            float progress = form / (float)SPEAR_FORM_TICKS;

            column = progress < 0.6f ? FRAME_OPEN : progress < 0.85f ? FRAME_SOFT : FRAME_FIST;

            Move(hold, 6f);

            spearAim = spearAim.AngleLerp(toTarget, 0.12f);
            NPC.rotation = NPC.rotation.AngleLerp(spearAim - MathHelper.PiOver2, 0.1f);

            if (form == 0)
                SoundEngine.PlaySound(FormSound, HandPosition);

            if (!Main.dedServ && Main.rand.NextBool(2))
                FormMote();

            return;
        }

        int charge = form - SPEAR_FORM_TICKS;
        int lockTick = SPEAR_CHARGE_TICKS - SPEAR_LOCK_TICKS;

        if (charge < SPEAR_CHARGE_TICKS)
        {
            float progress = charge / (float)SPEAR_CHARGE_TICKS;

            column = FRAME_FIST;

            if (charge < lockTick)
                spearAim = spearAim.AngleLerp(toTarget, 0.18f);

            if (charge == lockTick)
            {
                SoundEngine.PlaySound(LockSound, HandPosition);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
            }

            Vector2 drawnBack = hold - spearAim.ToRotationVector2() * (SPEAR_DRAWBACK * EaseQuadOut.Ease(progress));

            Move(drawnBack, 8f);

            NPC.rotation = NPC.rotation.AngleLerp(spearAim - MathHelper.PiOver2, 0.25f);
            drawShake = SPEAR_SHAKE * EaseQuadIn.Ease(progress);

            return;
        }

        Throw();

        SetState(ArmState.Recover);
    }

    /// <summary>Motes pulled into the palm while the pike forms.</summary>
    private void FormMote()
    {
        Vector2 hand = HandPosition;
        Vector2 offset = Main.rand.NextVector2CircularEdge(52f, 52f);

        Dust mote = Dust.NewDustPerfect(hand + offset, DustID.GreenTorch, -offset * 0.06f, Scale: 1.2f);

        mote.noGravity = true;
    }

    private void Throw()
    {
        SoundEngine.PlaySound(ThrowSound, HandPosition);

        Vector2 direction = spearAim.ToRotationVector2();

        EarthenPike.Throw(NPC, HandPosition, spearAim, NPC.GetAttackDamage_ForProjectiles_MultiLerp(SPEAR_DAMAGE, SPEAR_DAMAGE_EXPERT, SPEAR_DAMAGE_MASTER));

        NPC.velocity = direction * SPEAR_RECOIL;
    }

    private void UpdateRecover()
    {
        column = FRAME_RELEASE;

        drawShake = RECOVER_SHAKE * (1f - StateTimer / RECOVER_TICKS);

        NPC.velocity *= 0.86f;
        NPC.rotation = NPC.rotation.AngleLerp(-Side * STANCE_TILT, 0.08f);

        if (StateTimer >= RECOVER_TICKS)
            SetState(ArmState.Idle);
    }

    /// <summary>Shakes the hand out like it touched a hot stove, then goes back to waiting for orders.</summary>
    private void UpdateFlinch()
    {
        column = (int)StateTimer / 3 % 2 == 0 ? FRAME_OPEN : FRAME_RELEASE;

        NPC.velocity *= 0.88f;

        // Added as a per-tick delta on top of the settle toward the stance, so the shake needs no remembered base angle.
        float shake = FlinchShake(StateTimer) - FlinchShake(Math.Max(StateTimer - 1f, 0f));

        NPC.rotation = NPC.rotation.AngleLerp(-Side * STANCE_TILT, 0.12f) + shake;

        if (!Main.dedServ && StateTimer < 18f && StateTimer % 3f == 0f)
        {
            Dust smoke = Dust.NewDustPerfect(HandPosition + Main.rand.NextVector2Circular(12f, 12f), DustID.Smoke, new Vector2(0f, -1.5f), 120, Color.White, 1.2f);

            smoke.noGravity = true;
        }

        if (StateTimer >= FLINCH_TICKS)
            SetState(ArmState.Idle);
    }

    /// <summary>Wrist angle offset <paramref name="tick" /> ticks into a flinch: a fast shake that dies out.</summary>
    private static float FlinchShake(float tick)
        => MathF.Sin(tick * FLINCH_RATE) * FLINCH_SHAKE * (1f - EaseQuadOut.Ease(Math.Clamp(tick / FLINCH_TICKS, 0f, 1f)));

    /// <summary>
    ///     Knuckles smashed together low in front of the body, each hand swinging about its shoulder. Timed off the boss's
    ///     taunt clock so both hands agree.
    /// </summary>
    private void UpdateTaunt(NPC boss)
    {
        column = FRAME_FIST;

        float bossTick = boss.ai[1];
        float cycle = bossTick % TAUNT_PERIOD / TAUNT_PERIOD;

        // 0 with the fists together, 1 swung out: out over most of the cycle, driven back together as it wraps.
        float open = cycle < 0.7f ? EaseCubicOut.Ease(cycle / 0.7f) : 1f - EaseQuadIn.Ease((cycle - 0.7f) / 0.3f);

        if (bossTick >= WoodenWardenBoss.TAUNT_TICKS)
            open = 1f;

        Vector2 joint = Joint(boss);
        Vector2 meet = boss.Center + new Vector2(TAUNT_CONTACT * Side, TAUNT_Y);
        float angle = (meet - joint).ToRotation() - Side * TAUNT_SWING * open;

        // Knuckles square to each other when they meet, turning partway toward the arc as the hands swing out.
        float facing = (Side * MathHelper.PiOver2).AngleLerp(angle - MathHelper.PiOver2, open * TAUNT_FACING_FOLLOW);

        Swing(joint, angle, Vector2.Distance(meet, joint), StateTimer < TAUNT_GATHER_TICKS ? 0.2f : 1f, facing);

        if (Side < 0 && bossTick > 0f && bossTick < WoodenWardenBoss.TAUNT_TICKS && bossTick % TAUNT_PERIOD == 0f)
            Clack(boss.Center + new Vector2(0f, TAUNT_Y));
    }

    /// <summary>
    ///     The death tantrum, timed off the boss's death clock. Each fist swings about its shoulder between overhead and
    ///     the floor beside the body, alternating ever faster, then hangs raised and trembling until the burst.
    /// </summary>
    private void UpdateTantrum(NPC boss)
    {
        column = FRAME_FIST;

        int deathTick = (int)boss.ai[1];

        if (deathTick >= WoodenWardenBoss.DEATH_BURST_TICK)
        {
            if (!burst)
                Burst();

            NPC.velocity = Vector2.Zero;

            return;
        }

        tantrumFloor = boss.ModNPC is WoodenWardenBoss { DeathFloorY: >= 0f } warden
            ? warden.DeathFloorY
            : boss.Center.Y + WoodenWardenBoss.BODY_HALF_HEIGHT;

        underFloor = NPC.Center.Y + SPRITE_BOUND > tantrumFloor;

        // A revived arm starts buried; the first tick it is seen there, the ground breaks open over it.
        if (!emerged && NPC.Center.Y > tantrumFloor)
        {
            emerged = true;
            EmergeBurst(new Vector2(NPC.Center.X, tantrumFloor));
        }

        Vector2 joint = Joint(boss);
        int since = deathTick - WoodenWardenBoss.DEATH_DROP_TICKS;

        float height = since < 0
            ? TantrumHeight(0, Side)
            : since >= WoodenWardenBoss.DEATH_TANTRUM_TICKS
                ? 1f
                : TantrumHeight(since, Side);

        // Right-hand angles: the one that puts the fist on the floor, swung toward the one raised over the head.
        float drop = Math.Clamp(tantrumFloor - TANTRUM_CLEARANCE - joint.Y, 0f, TANTRUM_REACH);
        float pound = MathF.Asin(drop / TANTRUM_REACH);
        float angle = Mirror(MathHelper.Lerp(pound, TANTRUM_RAISED, height));

        Vector2 hand = Swing(joint, angle, TANTRUM_REACH, since < 0 ? 0.25f : 1f);

        if (since >= WoodenWardenBoss.DEATH_TANTRUM_TICKS)
            drawShake = TANTRUM_SHAKE;

        if (PoundsOn(since, Side))
            Pound(new Vector2(hand.X, tantrumFloor));
    }

    /// <summary>Pounds completed <paramref name="since" /> ticks into the tantrum: the integral of a linearly shrinking period.</summary>
    private static float TantrumPhase(float since)
    {
        float t = Math.Clamp(since, 0f, WoodenWardenBoss.DEATH_TANTRUM_TICKS);
        float slope = (TANTRUM_PERIOD_END - TANTRUM_PERIOD_START) / WoodenWardenBoss.DEATH_TANTRUM_TICKS;

        return MathF.Log((TANTRUM_PERIOD_START + slope * t) / TANTRUM_PERIOD_START) / slope;
    }

    /// <summary>Cycle offset per side, so the hands alternate and the left starts from the top of a raise.</summary>
    private static float TantrumOffset(int side) => side < 0 ? 0.6f : 0.1f;

    /// <summary>0 at the floor, 1 fully raised: raised over the first 60 % of a cycle, slammed down over the rest.</summary>
    private static float TantrumHeight(int since, int side)
    {
        float phase = TantrumPhase(since) + TantrumOffset(side);
        float cycle = phase - MathF.Floor(phase);

        return cycle < 0.6f ? EaseQuadOut.Ease(cycle / 0.6f) : 1f - EaseQuadIn.Ease((cycle - 0.6f) / 0.4f);
    }

    /// <summary>Whether the <paramref name="side" /> hand lands a pound on tick <paramref name="since" /> of the tantrum.</summary>
    public static bool PoundsOn(int since, int side)
    {
        if (since <= 0 || since >= WoodenWardenBoss.DEATH_TANTRUM_TICKS)
            return false;

        float offset = TantrumOffset(side);

        return MathF.Floor(TantrumPhase(since) + offset) > MathF.Floor(TantrumPhase(since - 1) + offset);
    }

    #endregion

    #region impact

    /// <summary>
    ///     Whether the arm is in terrain, once it has been travelling long enough for the answer to mean anything.
    ///     <paramref name="since" /> is ticks spent moving; a wind-up that ends inside a wall would otherwise land the
    ///     strike on the tick it launched.
    /// </summary>
    private bool HitSomething(float since)
        => since >= IMPACT_GRACE_TICKS && Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

    /// <summary>A strike landing on terrain at the leading edge of the arm's travel.</summary>
    private void Impact(float scale) => Impact(scale, NPC.Center + NPC.velocity.SafeNormalize(Vector2.UnitY) * (NPC.height * 0.5f));

    private void Impact(float scale, Vector2 impact)
    {
        NPC.velocity = Vector2.Zero;

        HurtBand(impact, scale);

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(scale >= 1f ? SlamSound : ImpactSound, impact);

        if (NearLocalPlayer)
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

    /// <summary>The two fists meeting on <paramref name="point" />: no ground to heave, so it is sound, dust and a band of damage.</summary>
    private void Clap(Vector2 point)
    {
        HurtBand(point, 0.9f);

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(ClapSound, point);

        if (NearLocalPlayer)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 12);

        for (int i = 0; i < 20; i++)
        {
            Dust dust = Dust.NewDustPerfect(point, DustID.WoodFurniture, Main.rand.NextVector2CircularEdge(6f, 3f), 100, Color.White, Main.rand.NextFloat(1f, 1.7f));

            dust.noGravity = true;
        }
    }

    /// <summary>One knuckle smash of the taunt.</summary>
    private void Clack(Vector2 point)
    {
        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(ClackSound, point);

        if (NearLocalPlayer)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 3);

        for (int i = 0; i < 8; i++)
            Dust.NewDustPerfect(point, DustID.WoodFurniture, Main.rand.NextVector2CircularEdge(3.5f, 2.5f), 100, Color.White, 1.1f);
    }

    /// <summary>One fist of the tantrum hitting the floor at <paramref name="point" />.</summary>
    private void Pound(Vector2 point)
    {
        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(ImpactSound with { Volume = 1f }, point);

        TileShockwaveSystem.Spawn(new BlockWave(point, 120f, lifetime: 22)
        {
            Amplitude = 12f,
            Thickness = 52f,
            Reach = 90f,
            Blend = 0.6f,
        });

        if (NearLocalPlayer)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 8);

        Point tile = (point + Vector2.UnitY * 8f).ToTileCoordinates();

        if (Framing.GetTileSafely(tile) is { HasUnactuatedTile: true } ground && Main.tileSolid[ground.TileType])
        {
            for (int i = 0; i < 3; i++)
                TileClusterShards.Debris(ground.TileType, point - Vector2.UnitY * 8f, new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -2.5f)));
        }

        for (int i = 0; i < 10; i++)
        {
            Dust.NewDustPerfect(
                point + Main.rand.NextVector2Circular(24f, 4f),
                DustID.Smoke,
                new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, -0.5f)),
                120,
                Color.White,
                Main.rand.NextFloat(1f, 1.5f)
            );
        }
    }

    /// <summary>A revived arm breaking up out of the floor at <paramref name="point" />. Client-side.</summary>
    private static void EmergeBurst(Vector2 point)
    {
        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(SoundID.Dig with { Volume = 1.2f, Pitch = -0.6f }, point);
        SoundEngine.PlaySound(ImpactSound, point);

        TileShockwaveSystem.Spawn(new BlockWave(point, 140f, lifetime: 26)
        {
            Amplitude = 14f,
            Thickness = 56f,
            Reach = 96f,
            Blend = 0.6f,
        });

        Point tile = (point + Vector2.UnitY * 8f).ToTileCoordinates();

        if (Framing.GetTileSafely(tile) is { HasUnactuatedTile: true } ground && Main.tileSolid[ground.TileType])
        {
            for (int i = 0; i < 6; i++)
            {
                TileClusterShards.Debris(
                    ground.TileType,
                    point + new Vector2(Main.rand.NextFloat(-20f, 20f), -8f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -4f))
                );
            }
        }

        for (int i = 0; i < 16; i++)
            Dust.NewDustPerfect(point + Main.rand.NextVector2Circular(28f, 4f), DustID.Dirt, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-6f, -2f)), Scale: 1.3f);
    }

    /// <summary>Breaks the arm into splinters of its own sprite. Client-side.</summary>
    private void Burst()
    {
        burst = true;

        if (Main.dedServ)
            return;

        WardenSplinter.Burst(
            TextureAssets.Npc[Type],
            new Rectangle(column * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT),
            NPC.Center,
            NPC.rotation,
            Side < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            SPLINTER_CELL,
            7f
        );
    }

    /// <summary>
    ///     Each client hurts only itself; Player.Hurt is not safe to call for a remote player, and a dedicated server has
    ///     no local player.
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

    /// <summary>Difficulty scaling for damage dealt outside contact, which tModLoader does not scale.</summary>
    private static int Scaled(int damage) => Main.masterMode
        ? (int)(damage * 1.5f)
        : Main.expertMode
            ? (int)(damage * 1.2f)
            : damage;

    #endregion

    #region movement

    private Vector2 TargetCenter(Vector2 fallback) => HasTarget ? Target.Center : fallback;

    private void Move(Vector2 position, float maxSpeed)
    {
        Vector2 toPoint = position - NPC.Center;

        float speed = Math.Min(toPoint.Length() * 0.14f, maxSpeed);

        NPC.velocity = Vector2.SmoothStep(NPC.velocity, toPoint.SafeNormalize(Vector2.Zero) * speed, 0.2f);
    }

    /// <summary>Rotation that points the sprite's down axis, where the hand is, at <paramref name="position" />.</summary>
    private float AimRotation(Vector2 position) => (position - NPC.Center).ToRotation() - MathHelper.PiOver2;

    /// <summary>This hand's imaginary shoulder on the body.</summary>
    private Vector2 Joint(NPC boss) => boss.Center + new Vector2(JOINT_X * Side, JOINT_Y);

    /// <summary>A right-hand angle mirrored across the body for this side.</summary>
    private float Mirror(float angle) => Side > 0 ? angle : MathHelper.Pi - angle;

    /// <summary>
    ///     Poses the arm with its hand <paramref name="reach" /> from <paramref name="joint" /> along <paramref name="angle" />
    ///     and the forearm on that line. <paramref name="follow" /> below 1 eases in from the current pose. Returns the hand.
    /// </summary>
    /// <remarks><paramref name="facing" /> overrides the sprite rotation; the hand still sits on the arc.</remarks>
    private Vector2 Swing(Vector2 joint, float angle, float reach, float follow = 1f, float? facing = null)
    {
        Vector2 hand = joint + angle.ToRotationVector2() * reach;
        float rotation = facing ?? angle - MathHelper.PiOver2;
        Vector2 centre = hand - (rotation + MathHelper.PiOver2).ToRotationVector2() * HAND_REACH;

        NPC.velocity = (centre - NPC.Center) * follow;
        NPC.rotation = follow >= 1f ? rotation : NPC.rotation.AngleLerp(rotation, 0.3f);

        return hand;
    }

    private void AimFist(Vector2 position)
    {
        if (Vector2.DistanceSquared(position, NPC.Center) < 1f)
            return;

        NPC.rotation = NPC.rotation.AngleLerp(AimRotation(position), 0.25f);
    }

    #endregion

    public override void FindFrame(int frameHeight)
    {
        NPC.frame = new Rectangle(Math.Clamp(column, 0, 3) * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (burst)
            return false;

        // Custom, because vanilla's origin is half the whole sheet rather than half a column.
        Texture2D texture = TextureAssets.Npc[Type].Value;

        Vector2 position = NPC.Center + new Vector2(0f, NPC.gfxOffY);

        if (drawShake > 0f)
            position += Main.rand.NextVector2Circular(drawShake, drawShake);

        // Behind the hand, so the fist reads as closed around the shaft.
        if (State == ArmState.Spear)
            DrawSpear(spriteBatch, position);

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

    /// <summary>The pike in the hand: forming, charging, and the white flash on the lock.</summary>
    private void DrawSpear(SpriteBatch spriteBatch, Vector2 position)
    {
        int form = (int)StateTimer - SPEAR_RAISE_TICKS;

        if (form < 0)
            return;

        int charge = form - SPEAR_FORM_TICKS;
        int lockTick = SPEAR_CHARGE_TICKS - SPEAR_LOCK_TICKS;

        float formProgress = Math.Clamp(form / (float)SPEAR_FORM_TICKS, 0f, 1f);
        float chargeProgress = Math.Clamp(charge / (float)SPEAR_CHARGE_TICKS, 0f, 1f);
        float flash = charge >= lockTick ? 1f - Math.Clamp((charge - lockTick) / (float)SPEAR_FLASH_TICKS, 0f, 1f) : 0f;

        Vector2 grip = position + new Vector2(0f, HAND_REACH).RotatedBy(NPC.rotation);

        EarthenPike.DrawHeld(spriteBatch, grip, spearAim, formProgress, chargeProgress, flash, TileCluster.LightAt(grip));
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ)
            return;

        if (NPC.life <= 0)
        {
            Burst();
            return;
        }

        for (int i = 0; i < 3; i++)
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture, hit.HitDirection * 1.4f, -0.8f, Scale: 1.1f);
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
    ///     State outside <see cref="NPC.ai" />. <see cref="Side" /> decides the idle phase; a client that guessed it would
    ///     mirror both hands onto one path.
    /// </summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((sbyte)Side);

        writer.WriteVector2(anchor);

        writer.Write(grabbed is >= 0 and < NO_GRAB ? (byte)grabbed : NO_GRAB);
        writer.Write((byte)Math.Clamp(hitsTaken, 0, byte.MaxValue));
        writer.Write(tableSlamming);
        writer.Write(catchAngle);

        writer.Write(spearAim);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Side = reader.ReadSByte() < 0 ? -1 : 1;

        anchor = reader.ReadVector2();

        byte held = reader.ReadByte();

        grabbed = held == NO_GRAB ? -1 : held;
        hitsTaken = reader.ReadByte();
        tableSlamming = reader.ReadBoolean();
        catchAngle = reader.ReadSingle();

        spearAim = reader.ReadSingle();
    }

    #endregion
}
