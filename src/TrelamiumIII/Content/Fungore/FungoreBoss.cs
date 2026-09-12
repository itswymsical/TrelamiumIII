using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Audio;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Core.Graphics.Shockwaves;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.Fungore;

[AutoloadBossHead]
public sealed class FungoreBoss : ModNPC
{
    public enum FungoreState
    {
        Idle = 0,

        Emerge,
        Walk,
        Punch,
        Jump,
        SuperSlam,
        BunnyHop,
        Burrow,
        Death,
    }

    #region sheet

    // 552x2144 sheet, a 4x16 grid of 138x134 cells. Columns are attacks, rows their frames:
    // 0 walk (rows 0-7), 1 punch (0-11), 2 jump/slam (0-11), 3 super slam (0-15).

    private const int FRAME_WIDTH = 138;
    private const int FRAME_HEIGHT = 134;

    private const int SHEET_ROWS = 16;

    private const int COLUMN_WALK = 0;
    private const int COLUMN_PUNCH = 1;
    private const int COLUMN_JUMP = 2;
    private const int COLUMN_SUPER = 3;

    private const int WALK_LAST = 7;

    private const int JUMP_STAND = 0;
    private const int JUMP_CROUCH_FIRST = 1;
    private const int JUMP_CROUCH_LAST = 2;
    private const int JUMP_LAUNCH_FIRST = 3;
    private const int JUMP_LAUNCH_LAST = 4;
    private const int JUMP_RISE = 5;
    private const int JUMP_APEX = 6;
    private const int JUMP_FALL = 7;
    private const int JUMP_LAND = 8;
    private const int JUMP_RECOVER_FIRST = 9;
    private const int JUMP_RECOVER_LAST = 11;

    private const int PUNCH_STANCE = 0;
    private const int PUNCH_WIND_FIRST = 2;
    private const int PUNCH_WIND_LAST = 3;
    private const int PUNCH_STRIKE_FIRST = 4;
    private const int PUNCH_STRIKE_LAST = 5;
    private const int PUNCH_RECOIL_FIRST = 6;
    private const int PUNCH_RECOIL_LAST = 7;
    private const int PUNCH_SETTLE_FIRST = 8;
    private const int PUNCH_SETTLE_LAST = 11;

    private const int SUPER_CROUCH_FIRST = 0;
    private const int SUPER_CROUCH_LAST = 2;

    /// <summary>Kneeling. Held for the back half of the charge, shaking, while he loads up.</summary>
    private const int SUPER_LOAD = 11;

    /// <summary>Held from the launch all the way to the top of the arc.</summary>
    private const int SUPER_AIRBORNE = 6;

    /// <summary>The turn and the fall, run once as he accelerates downward.</summary>
    private const int SUPER_DROP_FIRST = 7;

    private const int SUPER_DROP_LAST = 9;

    private const int SUPER_IMPACT = 10;

    private const int SUPER_RECOVER_FIRST = 11;
    private const int SUPER_RECOVER_LAST = 15;

    /// <summary>The pose he hangs on between clearing the ground and starting to fall.</summary>
    private const int EMERGE_HOLD_COLUMN = COLUMN_JUMP;

    private const int EMERGE_HOLD_ROW = JUMP_RISE;

    #endregion

    #region tuning

    private const float GRAVITY = 0.42f;
    private const float MAX_FALL = 15f;

    private const float WALK_ACCELERATION = 0.24f;
    private const float GROUND_FRICTION = 0.8f;

    /// <summary>Ground covered per step of the walk cycle, in pixels.</summary>
    private const float WALK_FRAME_PIXELS = 15f;

    private const int WALK_FRAME_MIN = 3;
    private const int WALK_FRAME_MAX = 8;

    /// <summary>Close enough to throw a jab without stepping in first.</summary>
    private const float MELEE_RANGE = 118f;

    /// <summary>Furthest a dash punch will commit to closing.</summary>
    private const float DASH_RANGE = 380f;

    /// <summary>Past this, walking is not going to get there and he jumps instead.</summary>
    private const float WALK_GIVE_UP = 440f;

    /// <summary>Height difference past which the floor between them is not the same floor.</summary>
    private const float SAME_FLOOR = 110f;

    private const int PUNCH_WIND_TICKS = 13;
    private const int PUNCH_STRIKE_TICKS = 9;
    private const int DASH_STRIKE_TICKS = 18;
    private const int PUNCH_RECOVER_TICKS = 15;

    private const float PUNCH_LUNGE = 5f;
    private const float DASH_LUNGE = 12.5f;

    /// <summary>1-in-N chance of a dash punch rather than a step-in, per phase.</summary>
    private static readonly int[] DashOdds = [4, 2, 2];

    /// <summary>Frames of suspended tile collision when dropping through a platform.</summary>
    private const int PLATFORM_DROP_TICKS = 16;

    /// <summary>How far below him the target must be before a platform stops being a floor.</summary>
    private const float PLATFORM_DROP_MARGIN = 48f;

    /// <summary>Afterimages kept behind a dash.</summary>
    private const int TRAIL_LENGTH = 10;

    /// <summary>Opacity of the nearest afterimage. The rest fall away on a square curve.</summary>
    private const float TRAIL_OPACITY = 0.42f;

    private const int JUMP_CROUCH_TICKS = 17;
    private const float JUMP_SPEED = 13f;

    /// <summary>Horizontal steering while airborne. Enough to correct, not enough to home.</summary>
    private const float JUMP_DRIVE = 0.24f;

    private const float JUMP_MAX_DRIFT = 6f;

    /// <summary>The punish window. Everything else about the slam is set against this number.</summary>
    private const int SLAM_STAGGER_TICKS = 48;

    private const int SUPER_CHARGE_TICKS = 48;

    /// <summary>Frames of the crouch before he settles onto one knee for the rest of the charge.</summary>
    private const int SUPER_CROUCH_TICKS = 16;

    /// <summary>The climb, the float and the fall are all driven by curves rather than by gravity.</summary>
    private const int SUPER_RISE_TICKS = 22;

    /// <summary>Apex height above the launch point, in world pixels.</summary>
    private const float SUPER_HEIGHT = 340f;

    private const int SUPER_HANG_TICKS = 14;

    /// <summary>How much further he creeps upward across the hang, so the top is never a dead stop.</summary>
    private const float SUPER_HANG_DRIFT = 16f;

    /// <summary>Frames spent accelerating from the float to <see cref="SUPER_DROP_SPEED" />.</summary>
    private const int SUPER_DROP_TICKS = 14;

    private const float SUPER_DROP_SPEED = 27f;

    /// <summary>Rough whole-flight length, used only to lead the launch onto the target.</summary>
    private const float SUPER_FLIGHT_TICKS = 54f;

    /// <summary>Frames the impact pose holds before he starts getting up.</summary>
    private const int SUPER_IMPACT_TICKS = 12;

    private const int SUPER_RECOVER_TICKS = 62;

    /// <summary>Ordinary slams landed before the big one comes out. Falls with the phase.</summary>
    private const int SLAMS_PER_SUPER = 3;

    /// <summary>Failed approaches before he stops trying to walk it and goes under.</summary>
    private const int FRUSTRATION_LIMIT = 2;

    /// <summary>Ordinary slams between mushrooms.</summary>
    private const int SLAMS_PER_SAC = 2;

    /// <summary>Mushrooms a super slam plants, spread along the wave.</summary>
    private const int SAC_COUNT_SUPER = 3;

    /// <summary>Half-width of the ground a super slam scatters mushrooms across, in world pixels.</summary>
    private const float SAC_SPREAD = 270f;

    private const int HOP_CROUCH_TICKS = 9;
    private const float HOP_SPEED = 9f;
    private const float HOP_DRIVE = 4.2f;
    private const int HOP_RECOVER_TICKS = 14;

    /// <summary>Frames of struggling before the cloud goes up.</summary>
    private const int DEATH_TICKS = 199;

    /// <summary>Fraction of maximum life at which he stops fighting properly and starts hopping.</summary>
    private const float CRITICAL_LIFE = 0.22f;

    private const float PHASE_TWO_LIFE = 0.66f;
    private const float PHASE_THREE_LIFE = 0.33f;

    #endregion

    #region burrow

    /// <summary>Frames spent folding down into the ground.</summary>
    private const int BURROW_DIVE_TICKS = 24;

    /// <summary>How far below the surface the tunnel runs.</summary>
    private const float BURROW_DEPTH = 190f;

    private const float BURROW_SPEED = 15f;

    /// <summary>Cap on the tunnel, so a burrow under a bottomless pit still comes back up.</summary>
    private const int BURROW_TRAVEL_TIMEOUT = 240;

    /// <summary>The reaction window: ground breaking up before anything comes out of it.</summary>
    private const int BURROW_TELL_TICKS = 35;

    private const int BURROW_TELL_TICKS_MASTER = 20;

    /// <summary>Frames of the eased shot up out of the floor.</summary>
    private const int BURROW_RISE_TICKS = 18;

    /// <summary>How high he clears the ground before gravity has him back.</summary>
    private const float BURROW_APEX = 300f;

    /// <summary>Horizontal distance from the player that counts as being underneath them.</summary>
    private const float BURROW_ARRIVAL = 24f;

    /// <summary>Shortest the tunnel may be, even if he dives straight onto them.</summary>
    private const int BURROW_MIN_TICKS = 26;

    /// <summary>Distance at which he stops trying to walk back and simply tunnels.</summary>
    private const float BURROW_RANGE = 900f;

    private const int BURROW_RECOVER_TICKS = 40;

    /// <summary>Margin kept from the world edge when committing to a spot, in world pixels.</summary>
    private const float WORLD_EDGE = 64f;

    /// <summary>Flattened into the floor on the way down.</summary>
    private static readonly Vector2 BurrowSquish = new(1.5f, 0.32f);

    #endregion

    #region entrance

    /// <summary>Frames of buried rumbling before he breaks the surface.</summary>
    private const int EMERGE_RUMBLE_TICKS = 60;

    /// <summary>Frames spent climbing out of the ground to the top of the arc.</summary>
    private const int EMERGE_RISE_TICKS = 26;

    /// <summary>Frames held at the top, where the title card is readable.</summary>
    private const int EMERGE_HANG_TICKS = 36;

    /// <summary>Frames on the landing pose before the fight is handed over.</summary>
    private const int EMERGE_LAND_TICKS = 22;

    /// <summary>How far above the ground he hangs.</summary>
    private const float EMERGE_APEX = 210f;

    /// <summary>How much further he drifts over the hang. This is the tail of the lerp.</summary>
    private const float EMERGE_DRIFT = 28f;

    /// <summary>Kept off the player, so he does not come up inside whoever summoned him.</summary>
    private const float EMERGE_CLEARANCE = 170f;

    /// <summary>Tiles searched downward for the floor to come up through.</summary>
    private const int GROUND_SEARCH = 60;

    /// <summary>Compression he unfolds out of on the way up.</summary>
    private static readonly Vector2 RiseSquish = new(0.72f, 1.34f);

    /// <summary>Splat on the landing, sprung back out over the recovery.</summary>
    private static readonly Vector2 LandSquish = new(1.3f, 0.74f);

    #endregion

    #region audio

    /// <summary>5.5 seconds long, so it's fired once under the whole entrance rather than retriggered.</summary>
    private static readonly SoundStyle RumbleSound = Assets.SFX.FungoreRumble.Asset with { Volume = 0.95f };

    // TODO: the rest are stand-ins until the real kit exists.
    private static readonly SoundStyle EruptSound = SoundID.Item14 with { Volume = 1f, Pitch = -0.8f };
    private static readonly SoundStyle RoarSound = SoundID.Roar with { Volume = 1.1f, Pitch = -0.55f };
    private static readonly SoundStyle SwingSound = SoundID.Item1 with { Volume = 0.7f, Pitch = -0.6f };
    private static readonly SoundStyle SlamSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 1f, Pitch = -0.5f };
    private static readonly SoundStyle SuperSound = Assets.SFX.Sporeclot_Boom.Asset with { Volume = 1.1f, Pitch = -0.5f };

    #endregion

    public override string Texture => AssetReferences.Content.Fungore.Fungore_Content.KEY;

    public FungoreState State
    {
        get => (FungoreState)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    /// <summary>World Y of the floor he came up through; zero if none, which skips ground clipping in the draw.</summary>
    private ref float GroundY => ref NPC.ai[2];

    private Player Target => Main.player[NPC.target];

    private float LifeFraction => NPC.lifeMax <= 0 ? 1f : NPC.life / (float)NPC.lifeMax;

    private int Phase => LifeFraction switch
    {
        > PHASE_TWO_LIFE => 1,
        > PHASE_THREE_LIFE => 2,
        _ => 3,
    };

    private bool Critical => LifeFraction <= CRITICAL_LIFE;

    /// <summary>Under the floor: nothing to draw, nothing to hit, nothing that can hit him.</summary>
    private bool Underground => State switch
    {
        FungoreState.Emerge => StateTimer < EMERGE_RUMBLE_TICKS,

        // From the moment the dive finishes until he starts coming back out.
        FungoreState.Burrow => StateTimer >= BURROW_DIVE_TICKS && BreachTick < 0,

        _ => false,
    };

    private int facing = 1;

    private bool grounded;

    /// <summary>Whether the current state was entered this tick, and so still owes its frame zero.</summary>
    private bool entered;

    /// <summary>Damage the NPC was given once expert scaling has had its go. Everything scales off it.</summary>
    private int baseDamage;

    private int column;
    private int row;

    private int walkFrameTimer;

    /// <summary>Jabs still owed in the current flurry.</summary>
    private int punchesLeft;

    private bool dashing;

    private int slamsSinceSuper;

    /// <summary><see cref="StateTimer" /> the current state's fall ended on, or -1 while still airborne.</summary>
    private int landTick = -1;

    /// <summary>Frames already spent hanging at the top of a super slam. Reset by every leap.</summary>
    private int hangTicks;

    /// <summary>Frames already spent accelerating downward out of a super slam.</summary>
    private int dropTicks;

    /// <summary>World Y his feet left the ground at. The super slam's curves are all measured from it.</summary>
    private float launchY;

    /// <summary><see cref="StateTimer" /> the tunnel finished on, or -1 while still travelling.</summary>
    private int travelEnd = -1;

    /// <summary>Where he committed to coming up. Meaningless until the tunnel ends and fixes it.</summary>
    private float burrowX;

    /// <summary>Approaches that went nowhere. Enough of them and he stops using the floor.</summary>
    private int frustration;

    private int slamsSinceSac;

    /// <summary>Which of <see cref="CRITICAL_PATTERNS" /> is being played out, and how far through.</summary>
    private int patternIndex;

    private int patternStep;

    /// <summary>Frames of tile collision still suspended to clear a platform.</summary>
    private int dropThrough;

    /// <summary>Pixels of positional jitter on the sprite. Cosmetic, so it is never synced.</summary>
    private float drawShake;

    /// <summary>Client-side; the entrance is cosmetic and is never synced.</summary>
    private bool introStarted;

    /// <summary>Client-side, for the same reason: each client schedules the fill against its own mix.</summary>
    private bool deathFillFired;

    private Vector2 squish = Vector2.One;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = SHEET_ROWS;

        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);

        // Vanilla keeps the history for us. Mode 1 records position and rotation, which is
        // everything the dash trail draws; the facing is locked for the length of a dash anyway.
        NPCID.Sets.TrailCacheLength[Type] = TRAIL_LENGTH;
        NPCID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        NPC.width = 76;
        NPC.height = 96;

        NPC.damage = 14;
        NPC.defense = 8;
        NPC.lifeMax = 932;

        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;

        // Gravity is applied by hand so the entrance can hold him in the air without fighting a
        // second source of acceleration. Tile collision is still vanilla's.
        NPC.noGravity = true;
        NPC.noTileCollide = false;

        NPC.boss = true;
        NPC.npcSlots = 10f;
        NPC.lavaImmune = true;

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;

        NPC.value = Item.buyPrice(gold: 4);

        // ModNPC.Music is unset on purpose: MusicDirector must own the track from its first sample when he surfaces,
        // or the death fill lands off the beat.
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([
            BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            new FlavorTextBestiaryInfoElement($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(FungoreBoss)}.Bestiary"),
        ]);
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<FungoreBag>()));

        npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<FungoreTrophy>(), 10));

        // Outside expert these drop directly, since the bag has its own copies. The mask is only here, or expert
        // players would get two.
        LeadingConditionRule notExpert = new(new Conditions.NotExpert());

        notExpert.OnSuccess(ItemDropRule.Common(ModContent.ItemType<FungoreMask>(), 7));

        notExpert.OnSuccess(ItemDropRule.OneFromOptions(
            1,
            ModContent.ItemType<Girthquake>(),
            ModContent.ItemType<SporeclotSac>(),
            ModContent.ItemType<OrphanSporeStaff>()
        ));

        npcLoot.Add(notExpert);
    }

    public override void OnKill()
    {
        NPC.SetEventFlagCleared(ref DownedBossSystem.DownedFungore, -1);
    }

    public override void BossLoot(ref int potionType) => potionType = ItemID.LesserHealingPotion;

    /// <summary>Places him under the floor near whoever summoned him, and starts the entrance.</summary>
    public override void OnSpawn(IEntitySource source)
    {
        NPC.TargetClosest(false);

        Player target = Target;

        float emergeX = NPC.Center.X;
        float from = NPC.Bottom.Y;

        if (target is { active: true })
        {
            from = target.Bottom.Y;

            // Nudged clear of anyone standing on the spot, so he does not surface through them.
            if (Math.Abs(emergeX - target.Center.X) < EMERGE_CLEARANCE)
                emergeX = target.Center.X + EMERGE_CLEARANCE * (emergeX < target.Center.X ? -1f : 1f);

            facing = target.Center.X < emergeX ? -1 : 1;
        }

        GroundY = FindGround(emergeX, from);

        // Starts fully under the surface; the rise interpolates up and the draw clips whatever is below the floor.
        NPC.Bottom = new Vector2(emergeX, GroundY + NPC.height);
        NPC.velocity = Vector2.Zero;

        // Invulnerability and phasing are derived from the state every tick in UpdateEmerge rather
        // than latched here, so a client -- which never runs this hook -- agrees without being told.
        SetState(FungoreState.Emerge);
    }

    public override void AI()
    {
        if (baseDamage <= 0)
            baseDamage = NPC.damage;

        grounded = NPC.velocity.Y == 0f;

        // Cleared before the switch and re-set by any SetState inside it, so OnSpawn's SetState still gets a real tick 0.
        entered = false;

        StartIntro();

        if (State is not (FungoreState.Emerge or FungoreState.Death))
            Retarget();

        switch (State)
        {
            case FungoreState.Emerge:
                UpdateEmerge();
                break;

            case FungoreState.Walk:
                UpdateWalk();
                break;

            case FungoreState.Punch:
                UpdatePunch();
                break;

            case FungoreState.Jump:
                UpdateJump();
                break;

            case FungoreState.SuperSlam:
                UpdateSuperSlam();
                break;

            case FungoreState.BunnyHop:
                UpdateBunnyHop();
                break;

            case FungoreState.Burrow:
                UpdateBurrow();
                break;

            case FungoreState.Death:
                UpdateDeath();
                break;

            default:
                UpdateIdle();
                break;
        }

        UpdatePlatformDrop();

        if (!DrivesVertical)
            NPC.velocity.Y = Math.Min(NPC.velocity.Y + GRAVITY, MAX_FALL);

        NPC.damage = ContactDamage;
        NPC.spriteDirection = facing;

        if (!Main.dedServ)
            Lighting.AddLight(NPC.Center, 0.35f, 0.18f, 0.05f);

        // A state entered this tick keeps tick 0; incrementing unconditionally would skip every state's first frame.
        if (!entered)
            StateTimer++;
    }

    /// <summary>True while a state is driving his vertical motion itself.</summary>
    private bool DrivesVertical => State switch
    {
        FungoreState.Emerge => StateTimer < EmergeFallTick,

        // The climb, the float and the fall. Everything after the landing is gravity's again.
        FungoreState.SuperSlam => StateTimer > SUPER_CHARGE_TICKS && landTick < 0,

        // The dive, the tunnel and the shot back out. The fall afterwards is gravity's.
        FungoreState.Burrow => Underground || Breaching,

        _ => false,
    };

    /// <summary>First tick of the entrance on which he is allowed to fall.</summary>
    private static int EmergeFallTick => EMERGE_RUMBLE_TICKS + EMERGE_RISE_TICKS + EMERGE_HANG_TICKS;

    /// <summary>Contact damage for the current pose.</summary>
    private int ContactDamage => State switch
    {
        FungoreState.Death => 0,

        // Harmless until he lands, just as he's invulnerable until then.
        FungoreState.Emerge when landTick < 0 => 0,

        // Same trade underground: untouchable, and untouching.
        FungoreState.Burrow when Underground => 0,

        // Coming out of the floor at speed. This is the part that is meant to catch you.
        FungoreState.Burrow when landTick < 0 => (int)(baseDamage * 1.3f),
        FungoreState.Burrow => (int)(baseDamage * 0.5f),

        // Face down after a slam: standing on him is the reward. Tested before the airborne arms, since gravity has
        // already put downward speed back on him by this point.
        FungoreState.Jump when landTick >= 0 => (int)(baseDamage * 0.5f),
        FungoreState.SuperSlam when landTick >= 0 => (int)(baseDamage * 0.5f),
        FungoreState.BunnyHop when landTick >= 0 => (int)(baseDamage * 0.6f),

        FungoreState.Punch when PunchIsStriking => (int)(baseDamage * (dashing ? 1.25f : 1.05f)),
        FungoreState.Jump when NPC.velocity.Y > 0f => (int)(baseDamage * 1.2f),
        FungoreState.SuperSlam when NPC.velocity.Y > 0f => (int)(baseDamage * 1.35f),

        _ => baseDamage,
    };

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        cooldownSlot = ImmunityCooldownID.Bosses;

        // Tied to ContactDamage so the two can't disagree.
        return ContactDamage > 0;
    }

    /// <summary>Never culled mid-performance its an entrance: a tunnel or a death needs to finish.</summary>
    public override bool CheckActive() => State is not (FungoreState.Emerge or FungoreState.Burrow or FungoreState.Death);

    /// <summary>Intercepts the kill so the death animation gets to run before he actually dies.</summary>
    public override bool CheckDead()
    {
        if (State == FungoreState.Death)
            return true;

        NPC.life = 1;
        NPC.dontTakeDamage = true;
        NPC.velocity.X = 0f;

        SetState(FungoreState.Death);

        return false;
    }

    #region entrance

    /// <summary>Kicks the title sequence off locally.</summary>
    private void StartIntro()
    {
        if (Main.dedServ || introStarted)
            return;

        introStarted = true;
        CameraSystem.MoveCameraOut(EMERGE_RUMBLE_TICKS, NPC.Center);

        if (State == FungoreState.Emerge && StateTimer <= EMERGE_RUMBLE_TICKS)
        {
            BossIntroSystem.Play(CreateIntro());
        }
    }

    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<FungoreBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(FungoreBoss)}.Epithet"),

        TrackTitle = "Fungal Fracas",
        TrackArtist = "wymsical",

        Accent = new Color(255, 176, 74),

        RevealTick = EMERGE_RUMBLE_TICKS,
    };

    private void UpdateEmerge()
    {
        // Both derived rather than latched, so they are identical on a client that never ran
        // OnSpawn, and so neither can be left switched on by a beat that did not run.
        NPC.dontTakeDamage = landTick < 0;
        NPC.noTileCollide = StateTimer < EmergeFallTick;

        NPC.velocity.X = 0f;

        if (StateTimer < EMERGE_RUMBLE_TICKS)
        {
            Buried();
            return;
        }

        int since = (int)StateTimer - EMERGE_RUMBLE_TICKS;

        if (since == 0)
            Erupt();

        if (since <= EMERGE_RISE_TICKS)
        {
            HopOutTheGround(since);
            return;
        }

        if (since <= EMERGE_RISE_TICKS + EMERGE_HANG_TICKS)
        {
            Hang(since - EMERGE_RISE_TICKS);
            return;
        }

        DoLanding();
    }

    /// <summary>Still under the floor. Only the ground above him has anything to say.</summary>
    private void Buried()
    {
        NPC.velocity.Y = 0f;

        SetFrame(EMERGE_HOLD_COLUMN, EMERGE_HOLD_ROW);

        if (Main.dedServ)
            return;

        if (StateTimer == 0f)
            SoundEngine.PlaySound(RumbleSound, new Vector2(NPC.Center.X, GroundY));

        // Dirt shaken loose out of the surface, building as he gets closer to it.
        float intensity = StateTimer / EMERGE_RUMBLE_TICKS;

        if (StateTimer % 3f != 0f)
            return;

        for (int i = 0; i < 1 + (int)(intensity * 3f); i++)
        {
            Dust dust = Dust.NewDustDirect(
                new Vector2(NPC.Center.X - 46f, GroundY - 6f), 92, 6, DustID.Dirt,
                Main.rand.NextFloat(-1.6f, 1.6f) * intensity,
                Main.rand.NextFloat(-4.5f, -0.8f) * intensity,
                Scale: Main.rand.NextFloat(0.9f, 1.4f)
            );

            dust.velocity.Y -= intensity;
        }
    }

    /// <summary>The floor gives way, and the fight starts.</summary>
    private void Erupt()
    {
        NPC.Bottom = new Vector2(NPC.Center.X, GroundY + NPC.height);

        var surface = new Vector2(NPC.Center.X, GroundY);

        Erupt(surface, scale: 1f);

        if (Main.dedServ)
            return;

        // Below the dedServ guard: building the score reads music slots, which servers lack. The short fade keeps the
        // downbeat from colliding with whatever vanilla is still fading out.
        MusicDirector.Play(FungoreMusic.Create(), fadeIn: 0.35f);
    }

    /// <summary>Ground breaking open, shared by the entrance and the burrow.</summary>
    /// <param name="scale">Fraction of full strength; the entrance is the loud one.</param>
    private void Erupt(Vector2 surface, float scale)
    {
        SoundEngine.PlaySound(EruptSound with { Volume = EruptSound.Volume * scale }, surface);
        SoundEngine.PlaySound(RoarSound, surface);

        TileShockwaveSystem.Spawn(new BlockWave(surface, 300f * scale, lifetime: 34)
        {
            Amplitude = 20f * scale,
            Thickness = 84f * scale,
            Reach = 128f * scale,
            Blend = 0.6f,
        });

        if (Main.dedServ)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)(14f * scale));

        SporeLayer.Burst(surface, 190f * scale);
        Rubble(surface, reach: Math.Max((int)(4 * scale), 1), puffs: (int)(28 * scale));

        int dirt = (int)(30 * scale);

        for (int i = 0; i < dirt; i++)
        {
            Dust.NewDustDirect(
                new Vector2(surface.X - 54f, surface.Y - 8f), 108, 8, DustID.Dirt,
                Main.rand.NextFloat(-5f, 5f) * scale, Main.rand.NextFloat(-9f, -2.5f) * scale,
                Scale: Main.rand.NextFloat(1f, 1.7f)
            );
        }
    }

    private void HopOutTheGround(int since)
    {
        float progress = since / (float)EMERGE_RISE_TICKS;
        float eased = EaseCubicOut.Ease(progress);

        NPC.velocity.Y = 0f;
        NPC.Bottom = new Vector2(
            NPC.Center.X,
            MathHelper.Lerp(GroundY + NPC.height, GroundY - EMERGE_APEX, eased)
        );

        squish = Vector2.Lerp(RiseSquish, Vector2.One, EaseCubicOut.Ease(progress));

        SetFrame(EMERGE_HOLD_COLUMN, EMERGE_HOLD_ROW);

        if (Main.dedServ)
            return;

        SporeLayer.Trail(
            new Vector2(NPC.Center.X + Main.rand.NextFloat(-34f, 34f), GroundY),
            new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-2.6f, -0.6f)),
            Main.rand.NextFloat(9f, 17f)
        );
    }

    private void Hang(int since)
    {
        float progress = since / (float)EMERGE_HANG_TICKS;

        NPC.velocity.Y = 0f;
        NPC.Bottom = new Vector2(
            NPC.Center.X,
            MathHelper.Lerp(GroundY - EMERGE_APEX, GroundY - EMERGE_APEX - EMERGE_DRIFT, EaseQuadOut.Ease(progress))
        );

        squish = Vector2.One;

        SetFrame(EMERGE_HOLD_COLUMN, EMERGE_HOLD_ROW);

        if (!Main.dedServ && since % 4 == 0)
            SporeLayer.Trail(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), Main.rand.NextVector2Circular(0.8f, 0.8f), Main.rand.NextFloat(8f, 15f));
    }

    private void DoLanding()
    {
        if (landTick < 0)
        {
            SetFrame(EMERGE_HOLD_COLUMN, JUMP_FALL);

            if (!grounded)
                return;

            landTick = (int)StateTimer;

            Slam(super: false, ordinary: false);

            // Everything the entrance owns comes off here, at the exact frame the fight starts.
            BossIntroSystem.Settle();
            CameraSystem.ReturnCamera(30);
            return;
        }

        int since = (int)StateTimer - landTick;

        Brake();

        squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(since / (float)EMERGE_LAND_TICKS));

        SetFrame(COLUMN_JUMP, since < EMERGE_LAND_TICKS / 2 ? JUMP_LAND : JUMP_RECOVER_FIRST);

        if (since >= EMERGE_LAND_TICKS)
            SetState(FungoreState.Idle);
    }

    #endregion

    #region decisions

    private void Retarget()
    {
        if (NPC.target is < 0 or >= Main.maxPlayers || Target is not { active: true, dead: false })
            NPC.TargetClosest(false);

        if (Target is not { active: true, dead: false })
            NPC.EncourageDespawn(30);
    }

    private void UpdateIdle()
    {
        Brake();
        FaceTarget();
        AnimateStand();

        if (StateTimer < IdleTicks || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        ChooseAction();
    }

    /// <summary>Breathing room between attacks. The fight speeds up by shortening this, not the attacks.</summary>
    private int IdleTicks => Critical ? 4 : Phase switch { 1 => 18, 2 => 12, _ => 6 };

    private int SlamsPerSuper => Phase switch { 1 => SLAMS_PER_SUPER, 2 => SLAMS_PER_SUPER - 1, _ => 1 };

    /// <summary>Picks the next attack. Server-only: every branch can roll, and clients rolling separately would desync.</summary>
    private static readonly string[] CRITICAL_PATTERNS =
    [
        "HHBB",
        "HBHB",
        "BHBH",
        "HHHB",
    ];

    private void ChooseAction()
    {
        if (Critical)
        {
            NextCriticalStep();

            return;
        }

        Player target = Target;

        if (target is not { active: true })
        {
            SetState(FungoreState.Walk);
            return;
        }

        float gap = Math.Abs(target.Center.X - NPC.Center.X);
        bool reachable = Math.Abs(target.Bottom.Y - NPC.Bottom.Y) < SAME_FLOOR;

        // Getting back into the fight comes first, or an owed super slam is spent on empty ground. Beyond BURROW_RANGE he
        // tunnels: walking would take longer and happen off screen, where his tells mean nothing.
        if (gap > BURROW_RANGE)
        {
            SetState(FungoreState.Burrow);

            return;
        }

        // Nothing on the ground is working. The floor stops being a constraint.
        if (frustration >= FRUSTRATION_LIMIT)
        {
            frustration = 0;

            SetState(FungoreState.Burrow);

            return;
        }

        if (slamsSinceSuper >= SlamsPerSuper)
        {
            slamsSinceSuper = 0;
            SetState(FungoreState.SuperSlam);

            return;
        }

        // Phase three stops queueing attacks by range and simply picks one. That is the whole
        // character of the phase: the reads that worked stop being reliable.
        if (Phase >= 3 && Main.rand.NextBool(2))
        {
            switch (Main.rand.Next(3))
            {
                case 0:
                    BeginPunch(dash: gap > MELEE_RANGE);
                    return;

                case 1:
                    SetState(FungoreState.Jump);
                    return;

                default:
                    SetState(FungoreState.SuperSlam);
                    return;
            }
        }

        if (reachable && gap <= MELEE_RANGE)
        {
            BeginPunch(dash: false);
            return;
        }

        // Available from phase one, just rarer: it's the move players most need to learn.
        if (reachable && gap <= DASH_RANGE && Main.rand.NextBool(DashOdds[Phase - 1]))
        {
            BeginPunch(dash: true);
            return;
        }

        if (!reachable || gap > WALK_GIVE_UP || Main.rand.NextBool(4))
        {
            // A jump at something he cannot reach is still an attempt, and enough failed attempts
            // are what send him under.
            if (!reachable)
                frustration++;

            SetState(FungoreState.Jump);

            return;
        }

        SetState(FungoreState.Walk);
    }

    /// <summary>Takes the next beat of the current pattern, and rolls a new one when it runs out.</summary>
    private void NextCriticalStep()
    {
        string pattern = CRITICAL_PATTERNS[patternIndex % CRITICAL_PATTERNS.Length];

        bool burrow = pattern[patternStep % pattern.Length] == 'B';

        patternStep++;

        if (patternStep >= pattern.Length)
        {
            patternStep = 0;
            patternIndex = Main.rand.Next(CRITICAL_PATTERNS.Length);
        }

        SetState(burrow ? FungoreState.Burrow : FungoreState.BunnyHop);
    }

    private void BeginPunch(bool dash)
    {
        dashing = dash;
        punchesLeft = Main.rand.Next(2, Phase >= 2 ? 5 : 4);

        // He got there. Whatever the arena was doing to him, it has stopped.
        frustration = 0;

        SetState(FungoreState.Punch);
    }

    private void SetState(FungoreState state)
    {
        State = state;
        StateTimer = 0f;

        // Claims this tick's increment, so the new state's frame 0 (which caches targets and sets up the attack) still runs.
        entered = true;

        landTick = -1;
        travelEnd = -1;
        hangTicks = 0;
        dropTicks = 0;
        drawShake = 0f;
        squish = Vector2.One;

        // The pose, the hitbox strength and the entrance clipping are all read straight off the
        // state, so a client that misses a change shows the wrong Fungore until the next one.
        NPC.netUpdate = true;
    }

    #endregion

    #region attacks

    private void UpdateWalk()
    {
        Player target = Target;

        FaceTarget();

        float speed = Phase switch { 1 => 2.7f, 2 => 3.2f, _ => 3.7f };

        NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X + facing * WALK_ACCELERATION, -speed, speed);

        // Blocked by something a step will not clear. Vanilla's collision has already tried.
        if (grounded && NPC.collideX)
            NPC.velocity.Y = -JUMP_SPEED * 0.62f;

        AnimateWalk();

        if (Main.netMode == NetmodeID.MultiplayerClient || target is not { active: true })
            return;

        float gap = Math.Abs(target.Center.X - NPC.Center.X);
        bool reachable = Math.Abs(target.Bottom.Y - NPC.Bottom.Y) < SAME_FLOOR;

        if (reachable && gap <= MELEE_RANGE)
        {
            BeginPunch(dash: false);
            return;
        }

        // A walk that has gone on this long is a walk that is not working.
        if (StateTimer > 120f)
        {
            frustration++;

            SetState(FungoreState.Jump);
        }
    }

    private void UpdatePunch()
    {
        int tick = (int)StateTimer;

        if (tick < PUNCH_WIND_TICKS)
        {
            // Facing is locked once the swing starts, so a jab can be side-stepped.
            FaceTarget();
            Brake();

            if (tick == 0)
                SoundEngine.PlaySound(SwingSound, NPC.Center);

            SetFrame(COLUMN_PUNCH, RowAcross(PUNCH_WIND_FIRST, PUNCH_WIND_LAST, tick / (float)PUNCH_WIND_TICKS));

            return;
        }

        int strike = tick - PUNCH_WIND_TICKS;
        int strikeTicks = StrikeTicks;

        if (strike < strikeTicks)
        {
            if (strike == 0)
            {
                NPC.velocity.X = facing * (dashing ? DASH_LUNGE : PUNCH_LUNGE);

                SoundEngine.PlaySound(RoarSound with { Volume = 0.6f, Pitch = -0.3f }, NPC.Center);
            }

            // A dash carries; a jab is a step. Same move, different follow-through.
            NPC.velocity.X *= dashing ? 0.968f : 0.9f;

            SetFrame(COLUMN_PUNCH, strike < strikeTicks / 2 ? PUNCH_STRIKE_FIRST : PUNCH_STRIKE_LAST);

            if (!Main.dedServ && strike % 3 == 0)
                SporeLayer.Trail(NPC.Center + new Vector2(facing * 40f, -6f), new Vector2(facing * 1.4f, -0.5f), Main.rand.NextFloat(7f, 13f));

            return;
        }

        int recover = strike - strikeTicks;

        Brake();

        SetFrame(
            COLUMN_PUNCH,
            recover < PUNCH_RECOVER_TICKS / 2
                ? RowAcross(PUNCH_RECOIL_FIRST, PUNCH_RECOIL_LAST, recover / (PUNCH_RECOVER_TICKS * 0.5f))
                : RowAcross(PUNCH_SETTLE_FIRST, PUNCH_SETTLE_LAST, (recover - PUNCH_RECOVER_TICKS * 0.5f) / (PUNCH_RECOVER_TICKS * 0.5f))
        );

        if (recover < PUNCH_RECOVER_TICKS || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        punchesLeft--;

        if (punchesLeft > 0)
        {
            // Follow-ups dash only if the target backed out of reach; a flurry that always dashed would cross the arena.
            dashing = Target is { active: true } target
                      && Math.Abs(target.Center.X - NPC.Center.X) > MELEE_RANGE;

            SetState(FungoreState.Punch);

            return;
        }

        SetState(FungoreState.Idle);
    }

    private int StrikeTicks => dashing ? DASH_STRIKE_TICKS : PUNCH_STRIKE_TICKS;

    private bool PunchIsStriking
    {
        get
        {
            int strike = (int)StateTimer - PUNCH_WIND_TICKS;

            return strike >= 0 && strike < StrikeTicks;
        }
    }

    /// <summary>Travelling on a dash punch. The only thing that leaves a trail.</summary>
    private bool Dashing => State == FungoreState.Punch && dashing && PunchIsStriking;

    private void UpdateJump()
    {
        int tick = (int)StateTimer;

        if (tick < JUMP_CROUCH_TICKS)
        {
            FaceTarget();
            Brake();

            SetFrame(COLUMN_JUMP, RowAcross(JUMP_CROUCH_FIRST, JUMP_CROUCH_LAST, tick / (float)JUMP_CROUCH_TICKS));

            return;
        }

        if (tick == JUMP_CROUCH_TICKS)
        {
            Leap(JUMP_SPEED, LeadHorizontal(JUMP_MAX_DRIFT, BallisticFlight(JUMP_SPEED)));

            SoundEngine.PlaySound(SwingSound with { Pitch = -0.85f }, NPC.Center);

            return;
        }

        if (landTick < 0)
        {
            Airborne(JUMP_DRIVE, JUMP_MAX_DRIFT);

            if (!grounded)
                return;

            landTick = tick;

            Slam(super: false, ordinary: true);

            return;
        }

        int since = tick - landTick;

        Brake();

        squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(since / (float)SLAM_STAGGER_TICKS));

        // Most of the recovery holds the landing pose; standing up early would cut the punish window without shortening
        // the animation.
        SetFrame(COLUMN_JUMP, since < SLAM_STAGGER_TICKS * 0.6f
            ? JUMP_LAND
            : RowAcross(JUMP_RECOVER_FIRST, JUMP_RECOVER_LAST, (since - SLAM_STAGGER_TICKS * 0.6f) / (SLAM_STAGGER_TICKS * 0.4f)));

        if (since >= SLAM_STAGGER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(FungoreState.Idle);
    }

    private void UpdateSuperSlam()
    {
        int tick = (int)StateTimer;

        if (tick < SUPER_CHARGE_TICKS)
        {
            Charge(tick);

            return;
        }

        if (tick == SUPER_CHARGE_TICKS)
        {
            Launch();

            return;
        }

        if (landTick < 0)
        {
            Fly(tick - SUPER_CHARGE_TICKS - 1);

            return;
        }

        int recover = tick - landTick;

        Brake();

        squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(recover / (float)SUPER_RECOVER_TICKS));

        // The impact pose holds on its own before the recovery runs, rather than being the first
        // slice of it -- it is the frame the attack is read from, and it should not be a blur.
        SetFrame(COLUMN_SUPER, recover < SUPER_IMPACT_TICKS
            ? SUPER_IMPACT
            : RowAcross(SUPER_RECOVER_FIRST, SUPER_RECOVER_LAST, (recover - SUPER_IMPACT_TICKS) / (float)Math.Max(SUPER_RECOVER_TICKS - SUPER_IMPACT_TICKS, 1)));

        if (recover >= SUPER_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(FungoreState.Idle);
    }

    /// <summary>Crouches, drops to one knee, and shakes there until it lets go.</summary>
    private void Charge(int tick)
    {
        FaceTarget();
        Brake();

        if (tick == 0)
            SoundEngine.PlaySound(RoarSound, NPC.Center);

        if (tick < SUPER_CROUCH_TICKS)
        {
            SetFrame(COLUMN_SUPER, RowAcross(SUPER_CROUCH_FIRST, SUPER_CROUCH_LAST, tick / (float)SUPER_CROUCH_TICKS));

            return;
        }

        SetFrame(COLUMN_SUPER, SUPER_LOAD);

        float load = (tick - SUPER_CROUCH_TICKS) / (float)Math.Max(SUPER_CHARGE_TICKS - SUPER_CROUCH_TICKS, 1);

        // He rattles rather than the screen alone, so the tell is attached to him and reads from
        // across the arena even with the camera already shaking for other reasons.
        drawShake = MathHelper.Lerp(0.5f, 4.5f, EaseQuadIn.Ease(load));

        if (Main.dedServ)
            return;

        SporeLayer.Coalesce(NPC.Center, load, radius: 90f);

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)MathHelper.Lerp(1f, 8f, EaseQuadIn.Ease(load)));
    }

    private void Launch()
    {
        drawShake = 0f;
        hangTicks = 0;
        dropTicks = 0;

        launchY = NPC.Bottom.Y;

        // Vertical is handed over to the curves below; only the lead is a real velocity.
        NPC.velocity = new Vector2(LeadHorizontal(7f, SUPER_FLIGHT_TICKS), 0f);
        NPC.netUpdate = true;

        SetFrame(COLUMN_SUPER, SUPER_AIRBORNE);

        SoundEngine.PlaySound(SwingSound with { Pitch = -0.9f }, NPC.Center);
    }

    /// <summary>
    ///     The climb, the float and the fall. The first two are position servos: the curve's target minus the current
    ///     position becomes velocity, so motion stays eased while ceilings still stop him.
    /// </summary>
    private void Fly(int since)
    {
        if (since < SUPER_RISE_TICKS)
        {
            // Circular-out: almost all the height in the first few frames, arriving at the top with
            // nothing left. That is what makes the launch read as a shove rather than a lift.
            float wanted = launchY - SUPER_HEIGHT * EaseCircularOut.Ease((since + 1) / (float)SUPER_RISE_TICKS);

            NPC.velocity.Y = wanted - NPC.Bottom.Y;

            SetFrame(COLUMN_SUPER, SUPER_AIRBORNE);

            return;
        }

        if (hangTicks < SUPER_HANG_TICKS)
        {
            hangTicks++;

            // Still creeping upward, barely. A hang held at a fixed height is a freeze frame, and
            // the eye reads it as the game stalling rather than as a boss hanging over you.
            float wanted = launchY - SUPER_HEIGHT - SUPER_HANG_DRIFT * EaseQuadOut.Ease(hangTicks / (float)SUPER_HANG_TICKS);

            NPC.velocity.Y = wanted - NPC.Bottom.Y;
            NPC.velocity.X *= 0.88f;

            SetFrame(COLUMN_SUPER, SUPER_AIRBORNE);

            return;
        }

        // Guarded on the counter: a servo that just arrived leaves vertical velocity at zero, which looks like standing.
        if (grounded && dropTicks > 1)
        {
            landTick = (int)StateTimer;

            Slam(super: true, ordinary: false);

            return;
        }

        float progress = Math.Clamp(dropTicks / (float)SUPER_DROP_TICKS, 0f, 1f);

        dropTicks++;

        // Driven rather than dropped -- terminal velocity under gravity alone is far too slow to
        // sell a slam this size -- but eased in, so the turn at the top is a fall and not a switch.
        NPC.velocity.Y = Math.Max(SUPER_DROP_SPEED * EaseQuadIn.Ease(progress), 1f);

        // Tracks the target on the way down. The wave is the threat rather than the body, so being
        // followed is fair -- there is nowhere on the ground to stand either way.
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, CloseHorizontal(6f), 0.06f);

        SetFrame(COLUMN_SUPER, RowAcross(SUPER_DROP_FIRST, SUPER_DROP_LAST, progress));
    }

    private void UpdateBunnyHop()
    {
        int tick = (int)StateTimer;

        if (tick < HOP_CROUCH_TICKS)
        {
            FaceTarget();
            Brake();

            SetFrame(COLUMN_JUMP, RowAcross(JUMP_CROUCH_FIRST, JUMP_CROUCH_LAST, tick / (float)HOP_CROUCH_TICKS));

            return;
        }

        if (tick == HOP_CROUCH_TICKS)
        {
            Leap(HOP_SPEED, LeadHorizontal(HOP_DRIVE, BallisticFlight(HOP_SPEED)));

            return;
        }

        if (landTick < 0)
        {
            Airborne(JUMP_DRIVE * 0.6f, HOP_DRIVE);

            if (!grounded)
                return;

            landTick = tick;

            // The last stretch of the fight is the super slam, worn down to something he can still
            // manage: same wave, a fraction of the reach.
            Slam(super: true, ordinary: false, scale: 0.45f);

            return;
        }

        int since = tick - landTick;

        Brake();

        squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(since / (float)HOP_RECOVER_TICKS));

        SetFrame(COLUMN_JUMP, JUMP_LAND);

        // Back through Idle rather than straight into another hop, so the pattern gets to decide
        // whether the next beat is a hop at all.
        if (since >= HOP_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(FungoreState.Idle);
    }

    #region burrow

    private bool Master => Main.masterMode;

    private int TellTicks => Master ? BURROW_TELL_TICKS_MASTER : BURROW_TELL_TICKS;

    /// <summary>Frames since he started coming out of the ground, or -1 while he is still under it.</summary>
    private int BreachTick => State == FungoreState.Burrow && travelEnd >= 0
        ? (int)StateTimer - travelEnd - TellTicks
        : -1;

    /// <summary>Shooting up through the floor, with part of him still below it.</summary>
    private bool Breaching => BreachTick >= 0 && BreachTick < BURROW_RISE_TICKS;

    /// <summary>
    ///     Goes under, chases beneath the player, commits, breaks the ground and comes down on it. The spot is committed
    ///     late, once he's beneath the target, so the tell is the whole reaction window; it's fixed once announced.
    /// </summary>
    private void UpdateBurrow()
    {
        int tick = (int)StateTimer;

        // Phased through terrain right up until he is on his way back down, so neither the tunnel
        // nor the shot out of it can be stopped by the floor he is going through.
        NPC.noTileCollide = BreachTick < BURROW_RISE_TICKS;

        NPC.dontTakeDamage = Underground;

        if (tick < BURROW_DIVE_TICKS)
        {
            Dive(tick);

            return;
        }

        if (travelEnd < 0)
        {
            Tunnel(tick);

            return;
        }

        if (tick - travelEnd < TellTicks)
        {
            Tell(tick - travelEnd);

            return;
        }

        Breach();
    }

    /// <summary>Folds into the floor at his feet. Nothing is decided here.</summary>
    private void Dive(int tick)
    {
        if (tick == 0)
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.6f }, NPC.Center);

        NPC.velocity.X *= 0.7f;

        float progress = tick / (float)BURROW_DIVE_TICKS;

        squish = Vector2.Lerp(Vector2.One, BurrowSquish, EaseQuadIn.Ease(progress));

        SetFrame(COLUMN_JUMP, RowAcross(JUMP_CROUCH_FIRST, JUMP_CROUCH_LAST, progress));

        if (Main.dedServ || tick % 3 != 0)
            return;

        for (int i = 0; i < 3; i++)
        {
            Dust.NewDustDirect(
                NPC.Bottom - new Vector2(NPC.width * 0.5f, 6f), NPC.width, 8, DustID.Dirt,
                Main.rand.NextFloat(-2.4f, 2.4f) * progress,
                Main.rand.NextFloat(-4.5f, -1f) * progress,
                Scale: Main.rand.NextFloat(0.9f, 1.4f)
            );
        }
    }

    /// <summary>Underground and out of the fight, chasing the player from below.</summary>
    private void Tunnel(int tick)
    {
        float chase = TrackedX();

        var goal = new Vector2(chase, TunnelDepth());

        // SafeNormalize, not DirectionTo: a zero vector would normalise to NaN and fail the check that ends the tunnel.
        NPC.velocity = (goal - NPC.Center).SafeNormalize(Vector2.UnitY) * BURROW_SPEED;

        squish = BurrowSquish;

        if (!Main.dedServ && tick % 2 == 0)
            MoleTrail();

        int travelled = tick - BURROW_DIVE_TICKS;

        // A floor on the tunnel even when he dives on top of them, so the attack always has an
        // underground beat and never resolves the instant it starts.
        if (travelled < BURROW_MIN_TICKS)
            return;

        bool beneath = Math.Abs(NPC.Center.X - chase) <= BURROW_ARRIVAL;

        // Out of time counts as arrived. Somewhere unreachable is still somewhere, and a boss that
        // tunnels forever is worse than one that comes up short.
        if (!beneath && travelled < BURROW_TRAVEL_TIMEOUT)
            return;

        Commit(tick, chase);
    }

    /// <summary>Freezes the spot and parks him under it. Everything after this is announced.</summary>
    private void Commit(int tick, float x)
    {
        burrowX = x;
        GroundY = FindGround(x, Target is { active: true } target ? target.Bottom.Y : NPC.Bottom.Y);

        // The tell starts next tick; this one belongs to the tunnel.
        travelEnd = tick + 1;

        // Parked directly under the surface he is about to come through, so the tell, the clip and
        // the eruption all agree on one line.
        NPC.Bottom = new Vector2(burrowX, GroundY + NPC.height);
        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;
    }

    /// <summary>Where he'd surface if he committed now, clamped into the world so a bad target can't send him off the map.</summary>
    private float TrackedX()
    {
        float wanted = Target is { active: true } target ? target.Center.X : NPC.Center.X;

        if (!float.IsFinite(wanted))
            return NPC.Center.X;

        return Math.Clamp(wanted, WORLD_EDGE, Main.maxTilesX * 16f - WORLD_EDGE);
    }

    /// <summary>Depth the tunnel runs at, measured off the player's feet rather than a fixed line.</summary>
    private float TunnelDepth()
    {
        float floor = Target is { active: true } target ? target.Bottom.Y : NPC.Bottom.Y;

        return floor + BURROW_DEPTH;
    }

    /// <summary>The reaction window. Nothing moves; the ground gives it away.</summary>
    private void Tell(int since)
    {
        NPC.velocity = Vector2.Zero;

        if (Main.dedServ)
            return;

        if (since == 0)
            SoundEngine.PlaySound(RumbleSound with { Volume = 0.7f, Pitch = 0.35f }, new Vector2(burrowX, GroundY));

        float urgency = since / (float)TellTicks;

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)MathHelper.Lerp(2f, 9f, urgency));

        // Builds toward the eruption, so the last few frames of the window read as the last few.
        if (since % Math.Max((int)MathHelper.Lerp(5f, 2f, urgency), 1) != 0)
            return;

        for (int i = 0; i < 2; i++)
        {
            Dust.NewDustDirect(
                new Vector2(burrowX - 40f, GroundY - 6f), 80, 6, DustID.Dirt,
                Main.rand.NextFloat(-2f, 2f),
                Main.rand.NextFloat(-5f, -1f) * (0.5f + urgency),
                Scale: Main.rand.NextFloat(0.9f, 1.5f)
            );
        }

        SporeLayer.Trail(
            new Vector2(burrowX + Main.rand.NextFloat(-30f, 30f), GroundY),
            new Vector2(0f, Main.rand.NextFloat(-1.4f, -0.4f)),
            Main.rand.NextFloat(5f, 10f) * (0.5f + urgency)
        );
    }

    /// <summary>Out of the ground, up, and back down onto the spot.</summary>
    private void Breach()
    {
        if (landTick >= 0)
        {
            int recover = (int)StateTimer - landTick;

            Brake();

            squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(recover / (float)BURROW_RECOVER_TICKS));

            SetFrame(Master ? COLUMN_SUPER : COLUMN_JUMP, Master ? SUPER_IMPACT : JUMP_LAND);

            if (recover >= BURROW_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
                SetState(FungoreState.Idle);

            return;
        }

        int since = BreachTick;

        if (since == 0)
            Erupt(new Vector2(burrowX, GroundY), scale: 0.7f);

        if (since < BURROW_RISE_TICKS)
        {
            // Same servo the super slam climbs on, over a shorter distance: he is fired out of the
            // floor rather than jumping off it.
            float wanted = MathHelper.Lerp(GroundY + NPC.height, GroundY - BURROW_APEX, EaseCircularOut.Ease((since + 1) / (float)BURROW_RISE_TICKS));

            NPC.velocity = new Vector2(0f, wanted - NPC.Bottom.Y);

            squish = Vector2.Lerp(BurrowSquish, Vector2.One, EaseCubicOut.Ease((since + 1) / (float)BURROW_RISE_TICKS));

            SetFrame(Master ? COLUMN_SUPER : COLUMN_JUMP, Master ? SUPER_AIRBORNE : JUMP_RISE);

            return;
        }

        squish = Vector2.One;

        // The tick guard matters: an arrived servo has zero vertical velocity, which reads as standing far above the floor.
        if (grounded && since > BURROW_RISE_TICKS + 1)
        {
            landTick = (int)StateTimer;

            // Master gets the full wave out of this, which is what turns a positional attack into a
            // room-wide one and is most of why the window is shorter there.
            Slam(super: Master, ordinary: !Master);

            return;
        }

        SetFrame(Master ? COLUMN_SUPER : COLUMN_JUMP, Master ? SUPER_DROP_LAST : JUMP_FALL);
    }

    /// <summary>Dirt thrown up above the tunnel, so the burrow reads as travel rather than vanishing.</summary>
    private void MoleTrail()
    {
        if (!TryFindSurface(NPC.Center, out float surfaceY))
            return;

        Dust dust = Dust.NewDustDirect(
            new Vector2(NPC.Center.X - 10f, surfaceY - 4f), 20, 4, DustID.Dirt,
            Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.4f, -1f),
            Scale: 1.15f
        );

        dust.velocity.Y -= 0.6f;
    }

    /// <summary>Open air above <paramref name="from" />: where a tunnel underneath would break out.</summary>
    private static bool TryFindSurface(Vector2 from, out float surfaceY)
    {
        int tileX = (int)(from.X / 16f);
        int tileY = (int)(from.Y / 16f);

        for (int i = 0; i <= GROUND_SEARCH; i++)
        {
            int y = tileY - i;

            if (y < 0)
                break;

            if (WorldGen.SolidTile(tileX, y))
                continue;

            surfaceY = (y + 1) * 16f;

            return true;
        }

        surfaceY = from.Y;

        return false;
    }

    #endregion

    private void UpdateDeath()
    {
        NPC.dontTakeDamage = true;
        NPC.damage = 0;

        // Fired here rather than in CheckDead (server-only): each client schedules the fill against its own transport.
        if (!deathFillFired)
        {
            deathFillFired = true;

            MusicDirector.Fire(FungoreMusic.DEATH_FILL, FungoreMusic.FILL_GRID);
        }

        Brake();

        // Looping the leap he can no longer commit to: the animation of an attack that never fires.
        int frame = JUMP_LAUNCH_FIRST + (int)(StateTimer / 3f) % (JUMP_FALL - JUMP_LAUNCH_FIRST + 1);

        SetFrame(COLUMN_JUMP, frame);

        float violence = Math.Clamp(StateTimer / DEATH_TICKS, 0f, 1f);

        drawShake = MathHelper.Lerp(1f, 7f, violence);

        if (!Main.dedServ)
        {
            float intensity = violence;

            CameraSystem.shake = Math.Max(CameraSystem.shake, (int)MathHelper.Lerp(3f, 14f, intensity));

            if (StateTimer % 3f == 0f)
                SporeLayer.Trail(NPC.Center + Main.rand.NextVector2Circular(40f, 44f), Main.rand.NextVector2Circular(1.6f, 1.6f), Main.rand.NextFloat(9f, 20f));

            if (StateTimer % 30f == 0f)
                SoundEngine.PlaySound(RoarSound with { Volume = 0.8f, Pitch = -0.3f + Main.rand.NextFloat(0.3f) }, NPC.Center);
        }

        if (StateTimer < DEATH_TICKS)
            return;

        // Fired off the exact tick rather than off the branch, so the cloud goes up once on every
        // client instead of every frame until the kill packet lands.
        if (StateTimer == DEATH_TICKS)
            Bloom();

        // The kill itself is the server's, like any other. A client doing it locally would remove
        // the boss early and then have it reappear on the next sync.
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        NPC.life = 0;
        NPC.HitEffect();
        NPC.checkDead();
    }

    /// <summary>The last of him: a cloud that hangs over the arena and does nothing at all.</summary>
    private void Bloom()
    {
        SoundEngine.PlaySound(SuperSound with { Volume = 1.2f, Pitch = -0.7f }, NPC.Center);

        if (Main.dedServ)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, 22);

        for (int i = 0; i < 6; i++)
            SporeLayer.Burst(NPC.Center + Main.rand.NextVector2Circular(46f, 46f), 220f);

        for (int i = 0; i < 40; i++)
        {
            SporeLayer.Trail(
                NPC.Center + Main.rand.NextVector2Circular(60f, 60f),
                Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 8f),
                Main.rand.NextFloat(14f, 30f)
            );
        }
    }

    #endregion

    #region shared movement

    private void Leap(float speed, float horizontal)
    {
        NPC.velocity = new Vector2(horizontal, -speed);
        NPC.netUpdate = true;

        hangTicks = 0;
        dropTicks = 0;

        SetFrame(COLUMN_JUMP, JUMP_LAUNCH_LAST);
    }

    private void Airborne(float drive, float maxDrift)
    {
        NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X + HorizontalDirection * drive, -maxDrift, maxDrift);

        SetFrame(COLUMN_JUMP, NPC.velocity.Y switch
        {
            < -2f => JUMP_RISE,
            < 2f => JUMP_APEX,
            _ => JUMP_FALL,
        });
    }

    private int HorizontalDirection => Target is { active: true } target ? Math.Sign(target.Center.X - NPC.Center.X) : 0;

    /// <summary>Launch speed that lands him roughly on the target, capped at <paramref name="max" />; solved against flight time.</summary>
    /// <param name="flightTicks"><see cref="BallisticFlight" /> for gravity-owned jumps; a fixed figure for the curve-driven super slam.</param>
    private float LeadHorizontal(float max, float flightTicks)
    {
        if (Target is not { active: true } target)
            return 0f;

        return MathHelper.Clamp((target.Center.X - NPC.Center.X) / Math.Max(flightTicks, 1f), -max, max);
    }

    /// <summary>Frames a jump at <paramref name="leapSpeed" /> spends in the air under our gravity.</summary>
    private static float BallisticFlight(float leapSpeed) => 2f * leapSpeed / GRAVITY;

    /// <summary>Proportional steering toward the target for use mid-flight, capped at <paramref name="max" />.</summary>
    private float CloseHorizontal(float max)
    {
        if (Target is not { active: true } target)
            return 0f;

        return MathHelper.Clamp((target.Center.X - NPC.Center.X) * 0.08f, -max, max);
    }

    /// <summary>
    ///     Drops him through his platform when the fight is below it, by briefly suspending tile collision (long enough
    ///     for one platform only). Skipped for states that own <see cref="NPC.noTileCollide" />.
    /// </summary>
    private void UpdatePlatformDrop()
    {
        if (State is FungoreState.Emerge or FungoreState.Burrow)
            return;

        if (dropThrough > 0)
        {
            dropThrough--;
            NPC.noTileCollide = true;

            return;
        }

        NPC.noTileCollide = false;

        if (!grounded || Target is not { active: true } target)
            return;

        // Clearly below, not merely lower. A player on the same floor standing in a dip should not
        // have him fall out of the arena chasing them.
        if (target.Top.Y < NPC.Bottom.Y + PLATFORM_DROP_MARGIN)
            return;

        if (!StandingOnPlatform())
            return;

        dropThrough = PLATFORM_DROP_TICKS;
    }

    /// <summary>Whether he stands on a fall-through tile; checks <c>tileSolidTop</c> so modded platforms count.</summary>
    private bool StandingOnPlatform()
    {
        int tileX = (int)(NPC.Center.X / 16f);
        int tileY = (int)((NPC.Bottom.Y + 1f) / 16f);

        if (!WorldGen.InWorld(tileX, tileY, 2))
            return false;

        Tile tile = Framing.GetTileSafely(tileX, tileY);

        return tile.HasUnactuatedTile && Main.tileSolidTop[tile.TileType];
    }

    private void Brake() => NPC.velocity.X *= GROUND_FRICTION;

    private void FaceTarget()
    {
        if (Target is not { active: true } target)
            return;

        float delta = target.Center.X - NPC.Center.X;

        // Deadzone, so a player standing on him does not flip him every frame.
        if (Math.Abs(delta) > 16f)
            facing = Math.Sign(delta);
    }

    #endregion

    #region impacts

    /// <summary>A landing that shifts the ground.</summary>
    /// <param name="super">The charged version: a wider, deeper wave with troughs either side, and spores.</param>
    /// <param name="ordinary">Whether this counts toward the next super slam.</param>
    /// <param name="scale">Fraction of full strength, for the worn-down slams of the last phase.</param>
    private void Slam(bool super, bool ordinary, float scale = 1f)
    {
        var impact = new Vector2(NPC.Center.X, NPC.Bottom.Y);

        if (ordinary)
            slamsSinceSuper++;

        SoundEngine.PlaySound((super ? SuperSound : SlamSound) with { Pitch = -0.5f }, impact);

        TileShockwaveSystem.Spawn(new BlockWave(impact, (super ? 620f : 280f) * scale, lifetime: super ? 58 : 34)
        {
            Amplitude = (super ? 34f : 20f) * scale,
            Thickness = (super ? 116f : 82f) * scale,
            Reach = (super ? 190f : 140f) * scale,

            // Odd cycle count: a crest with a trough either side of it, which is the shape that
            // reads as the ground rolling rather than as one shove.
            Cycles = super ? 3f : 1f,
            Blend = 0.6f,
        });

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            if (super)
            {
                FungoreSpore.SpawnFan(NPC, impact, scale);

                // A super slam seeds the whole band it just moved, so the ground it covered stays
                // expensive to stand on well after the wave has gone.
                PlantSacs(impact, SAC_COUNT_SUPER, scale);
            }
            else if (++slamsSinceSac >= SLAMS_PER_SAC)
            {
                slamsSinceSac = 0;

                PlantSacs(impact, 1, scale);
            }
        }

        if (Main.dedServ)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)((super ? 20f : 12f) * scale));

        // Both sheets stand on the impact line, so no offset is needed.
        if (super)
            FungoreSuperSlamBurst.Spawn(impact, scale);
        else
            FungoreSlamSmoke.Spawn(impact, scale);

        SporeLayer.Burst(impact, (super ? 200f : 130f) * scale);
        Rubble(impact, reach: (int)((super ? 5 : 3) * scale), puffs: (int)((super ? 30 : 16) * scale));
    }

    /// <summary>Scatters spore sacs across the slammed ground, searching down from just above the impact line.</summary>
    private void PlantSacs(Vector2 impact, int count, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            float slot = count == 1 ? 0.5f : i / (float)(count - 1);

            float offset = MathHelper.Lerp(-SAC_SPREAD, SAC_SPREAD, slot) * scale
                           + Main.rand.NextFloat(-18f, 18f);

            FungoreSporeSac.Plant(impact + new Vector2(offset, -28f));
        }
    }

    /// <summary>Material-correct debris via <see cref="WorldGen.KillTile" /> with <c>fail: true</c>; the world is left intact.</summary>
    private static void Rubble(Vector2 impact, int reach, int puffs)
    {
        Point centre = impact.ToTileCoordinates();

        for (int x = -reach; x <= reach; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Point coords = new(centre.X + x, centre.Y + y);

                if (WorldGen.InWorld(coords.X, coords.Y, 2) && Framing.GetTileSafely(coords) is { HasUnactuatedTile: true } tile && Main.tileSolid[tile.TileType])
                    WorldGen.KillTile(coords.X, coords.Y, fail: true);
            }
        }

        for (int i = 0; i < puffs; i++)
        {
            Dust.NewDustPerfect(
                impact + Main.rand.NextVector2Circular(reach * 12f, 8f),
                DustID.Smoke,
                new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f)),
                120,
                Color.White,
                Main.rand.NextFloat(1.2f, 2f)
            );
        }
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ || NPC.life <= 0)
            return;

        for (int i = 0; i < 3; i++)
            SporeLayer.Trail(NPC.Center + Main.rand.NextVector2Circular(30f, 34f), new Vector2(hit.HitDirection * 1.4f, -0.8f), Main.rand.NextFloat(6f, 12f));
    }

    #endregion

    #region animation

    private void SetFrame(int sheetColumn, int sheetRow)
    {
        column = sheetColumn;
        row = Math.Clamp(sheetRow, 0, SHEET_ROWS - 1);
    }

    /// <summary>Row <paramref name="progress" /> of the way through the inclusive range.</summary>
    private static int RowAcross(int first, int last, float progress)
    {
        int span = last - first;

        return first + Math.Clamp((int)(progress * (span + 1)), 0, span);
    }

    private void AnimateStand()
    {
        walkFrameTimer = 0;

        SetFrame(COLUMN_WALK, 0);
    }

    private void AnimateWalk()
    {
        if (!grounded)
        {
            SetFrame(COLUMN_JUMP, NPC.velocity.Y < 0f ? JUMP_RISE : JUMP_FALL);
            return;
        }

        float speed = Math.Abs(NPC.velocity.X);

        if (speed < 0.4f)
        {
            AnimateStand();
            return;
        }

        // Cadence off distance covered rather than a fixed interval, so the last shuffling pixels
        // into range do not sprint on the spot.
        int ticks = Math.Clamp((int)(WALK_FRAME_PIXELS / speed), WALK_FRAME_MIN, WALK_FRAME_MAX);

        if (++walkFrameTimer < ticks)
        {
            SetFrame(COLUMN_WALK, Math.Clamp(row, 0, WALK_LAST));
            return;
        }

        walkFrameTimer = 0;

        SetFrame(COLUMN_WALK, column == COLUMN_WALK && row < WALK_LAST ? row + 1 : 0);
    }

    public override void FindFrame(int frameHeight)
    {
        NPC.frame = new Rectangle(column * FRAME_WIDTH, row * FRAME_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Underground)
            return false;

        Texture2D texture = TextureAssets.Npc[Type].Value;

        Rectangle source = NPC.frame;

        // Bottom-centre: the sheet puts his feet on the last row of every cell, so anchoring there
        // is what keeps him on the floor through frames of wildly different heights.
        var origin = new Vector2(FRAME_WIDTH * 0.5f, FRAME_HEIGHT);
        var position = new Vector2(NPC.Center.X, NPC.Bottom.Y + NPC.gfxOffY);

        if (!ClipToGround(ref source, ref origin, ref position))
            return false;

        SpriteEffects effects = facing == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        // Behind the body, and before the shake -- the afterimages trace where he has been, which is
        // a smooth path, not a rattling one.
        if (Dashing)
            DrawDashTrail(spriteBatch, texture, source, origin, screenPos, effects, drawColor);

        // Rattling in place: loading up a super slam, or coming apart at the end.
        if (drawShake > 0f)
            position += Main.rand.NextVector2Circular(drawShake, drawShake * 0.5f);

        spriteBatch.Draw(
            texture,
            position - screenPos,
            source,
            NPC.GetAlpha(drawColor),
            NPC.rotation,
            origin,
            squish * NPC.scale,
            effects,
            0f
        );

        return false;
    }

    /// <summary>
    ///     Afterimages along a dash, from <see cref="NPC.oldPos" /> (already warm, since the trail cache is always on).
    ///     Entries are top-left positions, so each is moved onto the body's bottom-centre anchor.
    /// </summary>
    private void DrawDashTrail(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Vector2 origin, Vector2 screenPos, SpriteEffects effects, Color drawColor)
    {
        if (NPC.oldPos is not { Length: > 1 })
            return;

        var anchor = new Vector2(NPC.width * 0.5f, NPC.height);

        // Back to front, so the faintest copy is furthest behind and the body lands on top.
        for (int i = NPC.oldPos.Length - 1; i >= 1; i--)
        {
            // Unfilled slots sit at the origin of the world, which is a long way from here.
            if (NPC.oldPos[i] == Vector2.Zero)
                continue;

            float fade = 1f - i / (float)NPC.oldPos.Length;

            spriteBatch.Draw(
                texture,
                NPC.oldPos[i] + anchor - screenPos,
                source,
                NPC.GetAlpha(drawColor) * (fade * fade * TRAIL_OPACITY),
                NPC.rotation,
                origin,
                squish * NPC.scale,
                effects,
                0f
            );
        }
    }

    /// <summary>
    ///     Trims whatever is still below the floor he is rising through and re-anchors the rest; false if nothing is left.
    ///     Skipped when <see cref="GroundY" /> is unset, including bestiary mannequins.
    /// </summary>
    private bool ClipToGround(ref Rectangle source, ref Vector2 origin, ref Vector2 position)
    {
        if (GroundY <= 0f)
            return true;

        // Only while he is actually passing through a surface. Past that he is stood on the floor,
        // where a stray pixel of gfxOffY would otherwise saw his feet off.
        bool emerging = State switch
        {
            FungoreState.Emerge => StateTimer < EmergeFallTick,
            FungoreState.Burrow => Breaching,
            _ => false,
        };

        if (!emerging)
            return true;

        float below = position.Y - GroundY;

        if (below <= 0f)
            return true;

        // Divided by the vertical squish, because the trim is measured in world pixels and the
        // source rectangle is in texels the draw is about to stretch.
        int cut = (int)MathF.Ceiling(below / Math.Max(squish.Y * NPC.scale, 0.01f));

        if (cut >= source.Height)
            return false;

        source.Height -= cut;

        // Re-anchored to the surface: the visible remainder ends exactly at the floor line.
        origin.Y = source.Height;
        position.Y = GroundY;

        return true;
    }

    #endregion

    #region net

    /// <summary>
    ///     State kept outside <see cref="NPC.ai" />. Facing is here because <see cref="Entity.direction" /> isn't in the vanilla packet.
    /// </summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((sbyte)facing);
        writer.Write((byte)Math.Clamp(punchesLeft, 0, byte.MaxValue));
        writer.Write(dashing);
        writer.Write((byte)Math.Clamp(slamsSinceSuper, 0, byte.MaxValue));
        writer.Write((short)Math.Clamp(landTick, short.MinValue, short.MaxValue));

        // Every height in the super slam is measured from here, so a client that guessed at it would
        // draw the whole leap at the wrong altitude.
        writer.Write(launchY);

        // The burrow's whole shape: when the tunnel ended, and the spot it committed to. A client
        // that worked these out for itself would put the tell somewhere he is not coming up.
        writer.Write((short)Math.Clamp(travelEnd, short.MinValue, short.MaxValue));
        writer.Write(burrowX);

        writer.Write((byte)Math.Clamp(frustration, 0, byte.MaxValue));
        writer.Write((byte)Math.Clamp(slamsSinceSac, 0, byte.MaxValue));
        writer.Write((byte)Math.Clamp(patternIndex, 0, byte.MaxValue));
        writer.Write((byte)Math.Clamp(patternStep, 0, byte.MaxValue));
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        facing = reader.ReadSByte() < 0 ? -1 : 1;
        punchesLeft = reader.ReadByte();
        dashing = reader.ReadBoolean();
        slamsSinceSuper = reader.ReadByte();
        landTick = reader.ReadInt16();
        launchY = reader.ReadSingle();

        travelEnd = reader.ReadInt16();
        burrowX = reader.ReadSingle();

        frustration = reader.ReadByte();
        slamsSinceSac = reader.ReadByte();
        patternIndex = reader.ReadByte();
        patternStep = reader.ReadByte();
    }

    #endregion

    /// <summary>First solid tile below <paramref name="startY" /> in the column at <paramref name="worldX" />.</summary>
    private static float FindGround(float worldX, float startY)
    {
        int tileX = (int)(worldX / 16f);
        int tileY = (int)(startY / 16f);

        for (int i = 0; i <= GROUND_SEARCH; i++)
        {
            int y = tileY + i;

            if (y < 0 || y >= Main.maxTilesY)
                break;

            if (WorldGen.SolidTile(tileX, y))
                return y * 16f;
        }

        return startY;
    }
}