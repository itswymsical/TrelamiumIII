using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Shockwaves;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     Fungore. A ground-bound bruiser: he walks you down, jabs, and periodically leaves the floor to
///     land on you.
/// </summary>
/// <remarks>
///     <para>
///         <b>The whole fight is a rhythm.</b> Every attack ends in a recovery long enough to be read
///         and punished, and the escalation across phases is one of tempo rather than of new moves --
///         the same three attacks, arriving faster and mixing sooner, until the last stretch drops the
///         ordering entirely.
///     </para>
///     <para>
///         <b>Netcode.</b> The AI runs on every client so animation, lunges and the impact effects all
///         advance locally, but every <em>decision</em> is server-only, because each one rolls
///         <see cref="Main.rand" /> and clients rolling their own would disagree about which attack is
///         happening. The resulting state rides <see cref="NPC.ai" /> out on the next update, which
///         <see cref="SetState" /> always forces.
///     </para>
/// </remarks>
public sealed class FungoreBoss : ModNPC
{
    public enum FungoreState
    {
        /// <summary>Default so a bestiary mannequin, which has no <c>ai</c>, stands rather than erupts.</summary>
        Idle = 0,

        Emerge,
        Walk,
        Punch,
        Jump,
        SuperSlam,
        BunnyHop,
        Death,
    }

    #region sheet

    // 552x2144, a 4x16 grid of 138x134 cells. Columns are attacks, rows are that attack's frames:
    //
    //   0 | walk           rows 0-7
    //   1 | punch          rows 0-11
    //   2 | jump / slam    rows 0-11
    //   3 | super slam     rows 0-15

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
    private const int SUPER_ROAR_FIRST = 3;
    private const int SUPER_ROAR_LAST = 4;
    private const int SUPER_RISE_FIRST = 5;
    private const int SUPER_RISE_LAST = 7;
    private const int SUPER_DESCEND = 8;
    private const int SUPER_IMPACT_FIRST = 9;
    private const int SUPER_IMPACT_LAST = 10;
    private const int SUPER_RECOVER_FIRST = 11;
    private const int SUPER_RECOVER_LAST = 15;

    /// <summary>
    ///     The pose he hangs on between clearing the ground and starting to fall.
    /// </summary>
    /// <remarks>
    ///     Arms overhead, feet clear -- the airborne half of the jump cycle read as a single held
    ///     frame. Change the row alone to hang on a different beat of that leap.
    /// </remarks>
    private const int EMERGE_HOLD_COLUMN = COLUMN_JUMP;

    private const int EMERGE_HOLD_ROW = JUMP_RISE;

    #endregion

    #region tuning

    private const float GRAVITY = 0.42f;
    private const float MAX_FALL = 15f;

    private const float WALK_ACCELERATION = 0.24f;
    private const float GROUND_FRICTION = 0.8f;

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

    private const int JUMP_CROUCH_TICKS = 17;
    private const float JUMP_SPEED = 13f;

    /// <summary>Horizontal steering while airborne. Enough to correct, not enough to home.</summary>
    private const float JUMP_DRIVE = 0.24f;

    private const float JUMP_MAX_DRIFT = 6f;

    /// <summary>The punish window. Everything else about the slam is set against this number.</summary>
    private const int SLAM_STAGGER_TICKS = 48;

    private const int SUPER_CHARGE_TICKS = 48;
    private const float SUPER_JUMP_SPEED = 19.5f;
    private const int SUPER_HANG_TICKS = 15;
    private const float SUPER_DROP_SPEED = 23f;
    private const int SUPER_RECOVER_TICKS = 62;

    /// <summary>Ordinary slams landed before the big one comes out. Falls with the phase.</summary>
    private const int SLAMS_PER_SUPER = 3;

    private const int HOP_CROUCH_TICKS = 9;
    private const float HOP_SPEED = 9f;
    private const float HOP_DRIVE = 4.2f;
    private const int HOP_RECOVER_TICKS = 14;

    private const int DEATH_TICKS = 160;

    /// <summary>Fraction of maximum life at which he stops fighting properly and starts hopping.</summary>
    private const float CRITICAL_LIFE = 0.12f;

    private const float PHASE_TWO_LIFE = 0.66f;
    private const float PHASE_THREE_LIFE = 0.33f;

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

    // TODO: every one of these is a stand-in until the real kit exists.
    private static readonly SoundStyle RumbleSound = SoundID.Item14 with { Volume = 0.4f, Pitch = -1f };
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

    /// <summary>
    ///     World Y of the floor he came up through. Zero when he did not arrive through one, which is
    ///     what tells the draw not to bother clipping against a surface that is not there.
    /// </summary>
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

    /// <summary>Buried, and there is nothing to draw yet.</summary>
    private bool Underground => State == FungoreState.Emerge && StateTimer < EMERGE_RUMBLE_TICKS;

    private int facing = 1;

    private bool grounded;

    /// <summary>Damage the NPC was given once expert scaling has had its go. Everything scales off it.</summary>
    private int baseDamage;

    private int column;
    private int row;

    private int walkFrameTimer;

    /// <summary>Jabs still owed in the current flurry.</summary>
    private int punchesLeft;

    private bool dashing;

    private int slamsSinceSuper;

    /// <summary>
    ///     <see cref="StateTimer" /> the current state's fall ended on, or -1 while still airborne.
    /// </summary>
    /// <remarks>
    ///     Every airborne state has a variable-length descent -- how far he fell decides it -- so the
    ///     recovery cannot be a fixed offset on the state clock and has to be measured from the
    ///     landing instead.
    /// </remarks>
    private int landTick = -1;

    /// <summary>Frames already spent hanging at the top of a super slam. Reset by every leap.</summary>
    private int hangTicks;

    /// <summary>Client-side; the entrance is cosmetic and is never synced.</summary>
    private bool introStarted;

    private Vector2 squish = Vector2.One;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = SHEET_ROWS;

        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
    }

    public override void SetDefaults()
    {
        NPC.width = 76;
        NPC.height = 96;

        NPC.damage = 26;
        NPC.defense = 8;
        NPC.lifeMax = 1900;

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

        if (!Main.dedServ)
            Music = Assets.Music.DruidsGarden.Slot;
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
        npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<FungoreMask>(), 7));

        // The bag hands out its own copy of this pool, so outside expert it is dropped directly.
        LeadingConditionRule notExpert = new(new Conditions.NotExpert());

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

    /// <summary>
    ///     Places him under the floor near whoever he was summoned on, and starts the entrance.
    /// </summary>
    /// <remarks>
    ///     Server-side only, like every <see cref="ModNPC.OnSpawn" />: remote clients receive the
    ///     placement and the state together in the spawn packet and pick the entrance up from there.
    /// </remarks>
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

        // Started fully under the surface. The rise interpolates up from here, and the draw clips
        // whatever is still below the floor, so he grows out of the ground rather than appearing
        // on top of it.
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

            case FungoreState.Death:
                UpdateDeath();
                break;

            default:
                UpdateIdle();
                break;
        }

        if (!Suspended)
            NPC.velocity.Y = Math.Min(NPC.velocity.Y + GRAVITY, MAX_FALL);

        NPC.damage = ContactDamage;
        NPC.spriteDirection = facing;

        if (!Main.dedServ)
            Lighting.AddLight(NPC.Center, 0.35f, 0.18f, 0.05f);

        StateTimer++;
    }

    /// <summary>True while something other than gravity owns his vertical position.</summary>
    private bool Suspended => State == FungoreState.Emerge && StateTimer < EmergeFallTick;

    /// <summary>First tick of the entrance on which he is allowed to fall.</summary>
    private static int EmergeFallTick => EMERGE_RUMBLE_TICKS + EMERGE_RISE_TICKS + EMERGE_HANG_TICKS;

    /// <summary>
    ///     Contact damage for the current pose.
    /// </summary>
    /// <remarks>
    ///     Fungore has no separate attack hitboxes: the jab is a lunge and the slam is a body drop, so
    ///     the thing that hits you is always him. Damage is modulated per pose instead, which keeps
    ///     the punish windows genuinely safe to stand next to.
    /// </remarks>
    private int ContactDamage => State switch
    {
        FungoreState.Death => 0,

        // Nothing about the entrance is a threat until he is actually standing on the floor. He
        // cannot be hurt for its duration either, and one of those without the other is a mugging.
        FungoreState.Emerge when landTick < 0 => 0,

        // Face down in the dirt after a slam. Standing on him should be the reward for reading it.
        //
        // Tested before the airborne arms below, not after: this runs at the end of the tick, by
        // which point gravity has already put a frame's worth of downward speed back on a Fungore
        // who is lying on the floor, and "moving down" would otherwise still be true of him.
        FungoreState.Jump when landTick >= 0 => (int)(baseDamage * 0.5f),
        FungoreState.SuperSlam when landTick >= 0 => (int)(baseDamage * 0.5f),
        FungoreState.BunnyHop when landTick >= 0 => (int)(baseDamage * 0.6f),

        FungoreState.Punch when PunchIsStriking => (int)(baseDamage * (dashing ? 1.5f : 1.3f)),
        FungoreState.Jump when NPC.velocity.Y > 0f => (int)(baseDamage * 1.2f),
        FungoreState.SuperSlam when NPC.velocity.Y > 0f => (int)(baseDamage * 1.35f),

        _ => baseDamage,
    };

    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        cooldownSlot = ImmunityCooldownID.Bosses;

        return State != FungoreState.Death && !(State == FungoreState.Emerge && landTick < 0);
    }

    public override bool CheckActive() => State != FungoreState.Emerge && State != FungoreState.Death;

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

    /// <summary>
    ///     Kicks the title sequence off locally.
    /// </summary>
    /// <remarks>
    ///     Driven from <see cref="AI" /> rather than <see cref="OnSpawn" /> because that hook never
    ///     runs on a multiplayer client -- remote NPCs arrive through the sync path, not through
    ///     <c>NewNPC</c>. The timer check is what stops a player who joins mid-fight from being shown
    ///     an entrance for a boss that walked out minutes ago.
    /// </remarks>
    private void StartIntro()
    {
        if (Main.dedServ || introStarted)
            return;

        introStarted = true;

        if (State == FungoreState.Emerge && StateTimer <= EMERGE_RUMBLE_TICKS)
            BossIntroSystem.Play(CreateIntro());
    }

    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<FungoreBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(FungoreBoss)}.Epithet"),

        // TODO: placeholder track, swapped with the real one when it lands.
        TrackTitle = "Fungal Fracas",
        TrackArtist = "wymsical",

        Accent = new Color(255, 176, 74),

        // The bars, the lines and the boss all arrive on the same frame, which is the frame he
        // breaks the surface on.
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
            Rise(since);
            return;
        }

        if (since <= EMERGE_RISE_TICKS + EMERGE_HANG_TICKS)
        {
            Hang(since - EMERGE_RISE_TICKS);
            return;
        }

        Arrive();
    }

    /// <summary>Still under the floor. Only the ground above him has anything to say.</summary>
    private void Buried()
    {
        NPC.velocity.Y = 0f;

        SetFrame(EMERGE_HOLD_COLUMN, EMERGE_HOLD_ROW);

        if (Main.dedServ)
            return;

        if (StateTimer % 24f == 0f)
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

    /// <summary>The floor gives way.</summary>
    private void Erupt()
    {
        NPC.Bottom = new Vector2(NPC.Center.X, GroundY + NPC.height);

        var surface = new Vector2(NPC.Center.X, GroundY);

        SoundEngine.PlaySound(EruptSound, surface);
        SoundEngine.PlaySound(RoarSound, surface);

        TileShockwaveSystem.Spawn(new BlockWave(surface, 300f, lifetime: 34)
        {
            Amplitude = 20f,
            Thickness = 84f,
            Reach = 128f,
            Blend = 0.6f,
        });

        if (Main.dedServ)
            return;

        SporeLayer.Burst(surface, 190f);
        Rubble(surface, reach: 4, puffs: 28);

        for (int i = 0; i < 30; i++)
        {
            Dust.NewDustDirect(
                new Vector2(surface.X - 54f, surface.Y - 8f), 108, 8, DustID.Dirt,
                Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-9f, -2.5f),
                Scale: Main.rand.NextFloat(1f, 1.7f)
            );
        }
    }

    /// <summary>Climbing out. Position is interpolated outright -- gravity gets no say until the top.</summary>
    private void Rise(int since)
    {
        float progress = since / (float)EMERGE_RISE_TICKS;
        float eased = EaseCubicOut.Ease(progress);

        NPC.velocity.Y = 0f;
        NPC.Bottom = new Vector2(
            NPC.Center.X,
            MathHelper.Lerp(GroundY + NPC.height, GroundY - EMERGE_APEX, eased)
        );

        // Unfolds out of the compression he was buried in.
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

    /// <summary>Hanging at the top while the card reads. Still drifting, just barely.</summary>
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

    /// <summary>Falls, lands, and hands the fight over.</summary>
    private void Arrive()
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

    /// <summary>
    ///     Picks the next attack.
    /// </summary>
    /// <remarks>
    ///     Server-only: every branch below can roll, and two clients rolling separately would put
    ///     Fungore in two different attacks until the next packet arrived.
    /// </remarks>
    private void ChooseAction()
    {
        if (Critical)
        {
            SetState(FungoreState.BunnyHop);
            return;
        }

        if (slamsSinceSuper >= SlamsPerSuper)
        {
            slamsSinceSuper = 0;
            SetState(FungoreState.SuperSlam);

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

        // The dash punch is what phase two adds: the safe spacing from phase one stops being safe.
        if (Phase >= 2 && reachable && gap <= DASH_RANGE && Main.rand.NextBool(3))
        {
            BeginPunch(dash: true);
            return;
        }

        if (!reachable || gap > WALK_GIVE_UP || Main.rand.NextBool(4))
        {
            SetState(FungoreState.Jump);
            return;
        }

        SetState(FungoreState.Walk);
    }

    private void BeginPunch(bool dash)
    {
        dashing = dash;
        punchesLeft = Main.rand.Next(2, Phase >= 2 ? 5 : 4);

        SetState(FungoreState.Punch);
    }

    private void SetState(FungoreState state)
    {
        State = state;
        StateTimer = 0f;

        landTick = -1;
        hangTicks = 0;
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
            SetState(FungoreState.Jump);
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
            // Only the opener dashes. A flurry of dashes would cross the arena.
            dashing = false;

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
            Leap(JUMP_SPEED, LeadHorizontal(JUMP_MAX_DRIFT, JUMP_SPEED));

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

        // The recovery deliberately spends most of its length on the landing pose rather than
        // walking back up through the frames. Standing back up early would cut the punish window
        // without shortening the animation.
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
            FaceTarget();
            Brake();

            float progress = tick / (float)SUPER_CHARGE_TICKS;

            SetFrame(COLUMN_SUPER, progress < 0.55f
                ? RowAcross(SUPER_CROUCH_FIRST, SUPER_CROUCH_LAST, progress / 0.55f)
                : RowAcross(SUPER_ROAR_FIRST, SUPER_ROAR_LAST, (progress - 0.55f) / 0.45f));

            if (tick == 0)
                SoundEngine.PlaySound(RoarSound, NPC.Center);

            // Spores drawn in around him while he loads up, so the tell is visible from off screen.
            if (!Main.dedServ)
            {
                SporeLayer.Coalesce(NPC.Center, progress, radius: 90f);

                if (tick % 6 == 0)
                    CameraSystem.shake = Math.Max(CameraSystem.shake, (int)(progress * 5f));
            }

            return;
        }

        if (tick == SUPER_CHARGE_TICKS)
        {
            Leap(SUPER_JUMP_SPEED, LeadHorizontal(7f, SUPER_JUMP_SPEED));

            return;
        }

        if (landTick < 0)
        {
            // Still climbing. Gravity does the decelerating, so the pose is read off the speed
            // rather than off a clock and stays in step however high he got.
            if (NPC.velocity.Y < 0f)
            {
                SetFrame(COLUMN_SUPER, RowAcross(SUPER_RISE_FIRST, SUPER_RISE_LAST, 1f - Math.Clamp(-NPC.velocity.Y / SUPER_JUMP_SPEED, 0f, 1f)));

                return;
            }

            // Held at the top for a beat. Without the pause the leap and the drop read as one long
            // arc, and the moment to get out from under him never announces itself.
            if (hangTicks < SUPER_HANG_TICKS)
            {
                hangTicks++;

                NPC.velocity = new Vector2(NPC.velocity.X * 0.86f, 0f);

                SetFrame(COLUMN_SUPER, SUPER_RISE_LAST);

                return;
            }

            if (grounded)
            {
                landTick = tick;

                Slam(super: true, ordinary: false);

                return;
            }

            // Driven down rather than dropped: terminal velocity under gravity alone is far too
            // slow to sell a slam this size.
            NPC.velocity.Y = SUPER_DROP_SPEED;

            // Tracks the target on the way down. The wave is the threat rather than the body, so
            // being followed is fair -- there is nowhere on the ground to stand either way.
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, CloseHorizontal(6f), 0.06f);

            SetFrame(COLUMN_SUPER, SUPER_DESCEND);

            return;
        }

        int recover = tick - landTick;

        Brake();

        squish = Vector2.Lerp(LandSquish, Vector2.One, EaseCubicOut.Ease(recover / (float)SUPER_RECOVER_TICKS));

        SetFrame(COLUMN_SUPER, recover < SUPER_RECOVER_TICKS * 0.45f
            ? RowAcross(SUPER_IMPACT_FIRST, SUPER_IMPACT_LAST, recover / (SUPER_RECOVER_TICKS * 0.45f))
            : RowAcross(SUPER_RECOVER_FIRST, SUPER_RECOVER_LAST, (recover - SUPER_RECOVER_TICKS * 0.45f) / (SUPER_RECOVER_TICKS * 0.55f)));

        if (recover >= SUPER_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(FungoreState.Idle);
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
            Leap(HOP_SPEED, LeadHorizontal(HOP_DRIVE, HOP_SPEED));

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

        if (since >= HOP_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(FungoreState.BunnyHop);
    }

    private void UpdateDeath()
    {
        NPC.dontTakeDamage = true;
        NPC.damage = 0;

        Brake();

        // Looping the leap he can no longer commit to: the animation of an attack that never fires.
        int frame = JUMP_LAUNCH_FIRST + (int)(StateTimer / 3f) % (JUMP_FALL - JUMP_LAUNCH_FIRST + 1);

        SetFrame(COLUMN_JUMP, frame);

        if (!Main.dedServ)
        {
            float intensity = Math.Clamp(StateTimer / DEATH_TICKS, 0f, 1f);

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

    /// <summary>
    ///     Launch speed that puts him roughly on top of the target, capped at <paramref name="max" />.
    /// </summary>
    /// <remarks>
    ///     Solved against the arc rather than aimed at the player. He is airborne long enough that
    ///     simply pointing at where they are standing now lands consistently short, and the whole
    ///     point of a slam is that it arrives where you are.
    /// </remarks>
    private float LeadHorizontal(float max, float leapSpeed)
    {
        if (Target is not { active: true } target)
            return 0f;

        // Time to go up and come back down again under our own gravity.
        float flight = Math.Max(2f * leapSpeed / GRAVITY, 1f);

        return MathHelper.Clamp((target.Center.X - NPC.Center.X) / flight, -max, max);
    }

    /// <summary>
    ///     Speed that closes on the target proportionally, capped at <paramref name="max" />.
    /// </summary>
    /// <remarks>
    ///     For steering that is already in flight, where there is no arc left to solve -- proportional
    ///     so he settles onto the target rather than swinging past it and back.
    /// </remarks>
    private float CloseHorizontal(float max)
    {
        if (Target is not { active: true } target)
            return 0f;

        return MathHelper.Clamp((target.Center.X - NPC.Center.X) * 0.08f, -max, max);
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

    /// <summary>
    ///     A landing that shifts the ground.
    /// </summary>
    /// <param name="super">The charged version: a wider, deeper wave with a trough either side of it, and spores.</param>
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

        if (super && Main.netMode != NetmodeID.MultiplayerClient)
            FungoreSpore.SpawnFan(NPC, impact, scale);

        if (Main.dedServ)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, (int)((super ? 20f : 12f) * scale));

        SporeLayer.Burst(impact, (super ? 200f : 130f) * scale);
        Rubble(impact, reach: (int)((super ? 5 : 3) * scale), puffs: (int)((super ? 30 : 16) * scale));
    }

    /// <summary>
    ///     Debris off whatever he landed on.
    /// </summary>
    /// <remarks>
    ///     Driven through <see cref="WorldGen.KillTile" /> with <c>fail: true</c>, the same path a
    ///     pickaxe swing that breaks nothing takes: correct dust and sound for the actual material,
    ///     world left intact, no netcode.
    /// </remarks>
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
        int ticks = Math.Clamp((int)(34f / speed), 4, 12);

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

        // The whole body shaking itself apart, worst right before it stops.
        if (State == FungoreState.Death)
        {
            float violence = MathHelper.Lerp(1f, 7f, Math.Clamp(StateTimer / DEATH_TICKS, 0f, 1f));

            position += Main.rand.NextVector2Circular(violence, violence * 0.5f);
        }

        SpriteEffects effects = facing == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

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
    ///     Trims whatever is still below the floor he is coming up through, and re-anchors what is
    ///     left to the surface.
    /// </summary>
    /// <remarks>
    ///     This is what makes the entrance read as breaking out of the ground: NPCs draw in front of
    ///     tiles, so without it the first half of the rise is a Fungore lying on top of the dirt.
    ///     <para>
    ///         Skipped whenever <see cref="GroundY" /> is unset, which covers everything that is not a
    ///         live entrance -- including a bestiary mannequin, which has no <c>ai</c> at all.
    ///     </para>
    /// </remarks>
    /// <returns>False when there is nothing left above the surface to draw.</returns>
    private bool ClipToGround(ref Rectangle source, ref Vector2 origin, ref Vector2 position)
    {
        // Only while he is still coming up. Past that he is stood on the floor, where a stray pixel
        // of gfxOffY would otherwise saw his feet off.
        if (State != FungoreState.Emerge || StateTimer >= EmergeFallTick || GroundY <= 0f)
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
    ///     Everything the state machine keeps outside <see cref="NPC.ai" />.
    /// </summary>
    /// <remarks>
    ///     Facing is here because <see cref="Entity.direction" /> is not in the vanilla NPC packet,
    ///     and the rest because a client that guessed at a flurry length or a landing frame would
    ///     drift further out of step with every attack rather than resynchronising on the next one.
    /// </remarks>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((sbyte)facing);
        writer.Write((byte)Math.Clamp(punchesLeft, 0, byte.MaxValue));
        writer.Write(dashing);
        writer.Write((byte)Math.Clamp(slamsSinceSuper, 0, byte.MaxValue));
        writer.Write((short)Math.Clamp(landTick, short.MinValue, short.MaxValue));
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        facing = reader.ReadSByte() < 0 ? -1 : 1;
        punchesLeft = reader.ReadByte();
        dashing = reader.ReadBoolean();
        slamsSinceSuper = reader.ReadByte();
        landTick = reader.ReadInt16();
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