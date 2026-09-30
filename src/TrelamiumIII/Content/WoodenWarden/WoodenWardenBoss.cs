using System.IO;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Audio;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Core.Graphics.Shockwaves;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     Hovers over the target, slams the ground and conjures chunks of terrain at it. Owns both <see cref="WardenArm" />s
///     and hands them their attacks through <see cref="WardenArm.Command" />.
/// </summary>
[AutoloadBossHead]
public sealed class WoodenWardenBoss : ModNPC
{
    public enum WardenState
    {
        Idle = 0,

        Arrive,
        Slam,
        Conjure,
        Taunt,
        Death,
    }

    #region sheet

    // One 194x204 frame: the tense, the drop and the death are squash, stretch and jitter in PreDraw.

    private const float SPRITE_WIDTH = 194f;
    private const float SPRITE_HEIGHT = 204f;

    public const float BODY_HALF_HEIGHT = SPRITE_HEIGHT * 0.5f;

    /// <summary>Centre of the eye, in sprite pixels.</summary>
    private static readonly Vector2 EyePixel = new(100f, 69.5f);

    public static readonly Color EyeColor = new(165, 230, 37);

    #endregion

    #region tuning

    /// <summary>Height the hover anchor sits above the target, in world pixels.</summary>
    private const float HOVER_HEIGHT = 272f;

    /// <summary>Velocity asked for per pixel of distance to the anchor, on top of the target's own velocity.</summary>
    private const float TRACK_GAIN = 0.08f;

    /// <summary>Fraction of the way to the wanted velocity the hover moves each tick; the give in it is the dance.</summary>
    private const float TRACK_RESPONSE = 0.1f;

    private const float TRACK_MAX_SPEED = 18f;

    /// <summary>Half-extents of the small figure the hover anchor traces around the spot over the target, in world pixels.</summary>
    private const float DANCE_SWAY = 36f;

    private const float DANCE_BOB = 22f;

    /// <summary>Ticks for one pass of the hover figure. The vertical term runs at twice this rate.</summary>
    private const float DANCE_PERIOD = 186f;

    /// <summary>Ticks of hovering before the next action.</summary>
    private const int IDLE_TICKS = 170;

    private const int IDLE_TICKS_MASTER = 120;

    /// <summary>Fraction of maximum life at which the second phase starts.</summary>
    private const float PHASE_TWO_LIFE = 0.5f;

    /// <summary>Fraction of the idle dwell kept in the second phase.</summary>
    private const float PHASE_TWO_IDLE = 0.55f;

    /// <summary>Ticks between re-picks of the closest target while idle.</summary>
    private const int RETARGET_INTERVAL = 180;

    /// <summary>Wrap point for <see cref="ArmClock" />. A whole number of hover figures.</summary>
    private const int CLOCK_WRAP = 7440;

    /// <summary>Inset applied to the contact hitbox, in world pixels; see <see cref="CanHitPlayer" />.</summary>
    private const int BODY_INSET_X = 46;

    private const int BODY_INSET_Y = 26;

    /// <summary>Squared distance within which the local screen reacts to this fight's shakes and screen effects.</summary>
    private const float EFFECT_RANGE_SQ = 2400f * 2400f;

    #endregion

    #region slam

    /// <summary>Ticks spent braking to a stop over the locked column before the tense.</summary>
    private const int SLAM_SETTLE_TICKS = 22;

    /// <summary>Ticks spent shaking in place before the drop.</summary>
    private const int TENSE_TICKS = 54;

    private const int TENSE_TICKS_PHASE_TWO = 42;

    /// <summary>Horizontal distance to the locked column within which the tense commits.</summary>
    private const float SLAM_ALIGN = 36f;

    /// <summary>Upward drift across the tense, in pixels per tick.</summary>
    private const float TENSE_RISE = 1.5f;

    private const float TENSE_SHAKE = 6f;

    private const float SLAM_ACCELERATION = 2.4f;
    private const float SLAM_MAX_SPEED = 27f;

    /// <summary>Ticks of falling before a slam that never reaches a floor gives up.</summary>
    private const int SLAM_TIMEOUT = 110;

    private const int SLAM_RECOVER_TICKS = 72;

    private const int SLAM_DAMAGE = 55;

    /// <summary>Half-extents of the band the impact hurts, in world pixels. Kept to the body's width.</summary>
    private const float SLAM_REACH_X = 150f;

    private const float SLAM_REACH_Y = 80f;

    /// <summary>Tiles either side of the impact that shed rubble and debris.</summary>
    private const int RUBBLE_REACH = 4;

    /// <summary>Loose blocks thrown out along the ground by a slam.</summary>
    private const int DEBRIS_COUNT = 16;

    #endregion

    #region conjure

    /// <summary>Ticks the eye charges before the first chunk tears free.</summary>
    private const int FLARE_TICKS = 44;

    private const int CONJURE_COUNT = 2;

    /// <summary>Boulders per conjure below half life: fewer chunks, far larger.</summary>
    private const int BOULDER_COUNT = 2;

    /// <summary>Ticks between chunks tearing free.</summary>
    private const int CONJURE_STAGGER = 8;

    /// <summary>Ticks the first chunk hovers after its rise, before it launches.</summary>
    private const int CONJURE_HOVER = 30;

    /// <summary>Ticks between launches, for scoops and for boulders.</summary>
    private const int CONJURE_LAUNCH_STAGGER = 16;

    private const int BOULDER_LAUNCH_STAGGER = 30;

    /// <summary>Ticks after the last launch before the body moves again.</summary>
    private const int CONJURE_TAIL = 24;

    /// <summary>Half-width the dig sites are spread across, centred on the target, in world pixels.</summary>
    private const float CONJURE_SPREAD = 220f;

    private const int CONJURE_DAMAGE = 14;
    private const int CONJURE_DAMAGE_EXPERT = 18;
    private const int CONJURE_DAMAGE_MASTER = 22;

    private const int BOULDER_DAMAGE = 20;
    private const int BOULDER_DAMAGE_EXPERT = 26;
    private const int BOULDER_DAMAGE_MASTER = 32;

    /// <summary>Tiles searched above and below the target's feet for ground to lift.</summary>
    private const int SURFACE_SEARCH_UP = 8;

    private const int SURFACE_SEARCH_DOWN = 30;

    /// <summary>Slams between conjures, per phase.</summary>
    private const int SLAMS_PER_CONJURE = 2;

    private const int SLAMS_PER_CONJURE_PHASE_TWO = 1;

    #endregion

    #region arms

    /// <summary><see cref="NPC.ai" /> slot holding <see cref="ArmClock" />, read by <see cref="WardenArm" />.</summary>
    public const int ARM_CLOCK_SLOT = 3;

    /// <summary>The order each arm works through while both are alive; the right arm starts at <see cref="ROUTINE_OFFSET" />.</summary>
    private static readonly WardenArm.ArmState[] Routine =
    [
        WardenArm.ArmState.Punch,
        WardenArm.ArmState.PalmSlam,
        WardenArm.ArmState.Spear,
        WardenArm.ArmState.Punch,
        WardenArm.ArmState.PalmSlam,
        WardenArm.ArmState.Punch,
    ];

    /// <summary>What the survivor falls back on once the other hand is gone.</summary>
    private static readonly WardenArm.ArmState[] SoloRoutine =
    [
        WardenArm.ArmState.PalmSlam,
        WardenArm.ArmState.Spear,
        WardenArm.ArmState.Grapple,
        WardenArm.ArmState.Punch,
        WardenArm.ArmState.PalmSlam,
        WardenArm.ArmState.Punch,
    ];

    private const int ROUTINE_OFFSET = 3;

    /// <summary>Wrap point for the routine cursors on the wire. A multiple of both routine lengths.</summary>
    private const int CURSOR_WRAP = 1200;

    /// <summary>Orders issued between synchronised clasps.</summary>
    private const int CLASP_INTERVAL = 5;

    /// <summary>
    ///     Ticks between grapples, whichever hand throws them. The grab pins the target, so without a floor here it
    ///     stacks with a slam and there is no way out of the pair.
    /// </summary>
    private const int GRAPPLE_COOLDOWN = 600;

    #endregion

    #region entrance

    /// <summary>Ticks of descent. The intro reveals on the tick it arrives.</summary>
    private const int ARRIVE_TICKS = 60;

    /// <summary>Ticks it holds station after arriving. The intro settles itself on the same tick.</summary>
    private const int ARRIVE_HOLD_TICKS = 100;

    /// <summary>Height above its station the descent starts from, in world pixels.</summary>
    private const float ARRIVE_DROP = 520f;

    #endregion

    #region taunt

    /// <summary>Ticks of knuckle smashing: nine smashes of <see cref="WardenArm.TAUNT_PERIOD" />.</summary>
    public const int TAUNT_TICKS = 216;

    /// <summary>Ticks spent flying off before it despawns.</summary>
    private const int TAUNT_LEAVE_TICKS = 90;

    #endregion

    #region death

    /// <summary>Ticks falling to the floor before the scream.</summary>
    public const int DEATH_DROP_TICKS = 34;

    /// <summary>Ticks of the arms pounding the floor.</summary>
    public const int DEATH_TANTRUM_TICKS = 150;

    /// <summary>Ticks of shaking after the pounding, before the body bursts.</summary>
    private const int DEATH_SHAKE_TICKS = 36;

    public const int DEATH_BURST_TICK = DEATH_DROP_TICKS + DEATH_TANTRUM_TICKS + DEATH_SHAKE_TICKS;

    /// <summary>Ticks the server waits after the burst before the kill, so every client reaches the burst first.</summary>
    private const int DEATH_KILL_DELAY = 3;

    /// <summary>Furthest the dying body drops to find a floor, in world pixels.</summary>
    private const float DEATH_FLOOR_SEARCH = 900f;

    /// <summary>Side of a splinter cut from the body sprite, in pixels.</summary>
    private const int SPLINTER_CELL = 14;

    /// <summary>Horizontal offset of a revived arm's dig site from the body, under where its pounds land.</summary>
    private const float REVIVE_X = 174f;

    #endregion

    #region audio

    // Stand-ins until the Warden has its own roar, charge and flare.
    private static readonly SoundStyle RoarSound = SoundID.Roar with { Volume = 1f, Pitch = -0.6f };
    private static readonly SoundStyle TenseSound = SoundID.Roar with { Volume = 0.85f, Pitch = -0.65f };
    private static readonly SoundStyle ChargeSound = SoundID.Item8 with { Volume = 0.9f, Pitch = -0.6f };
    private static readonly SoundStyle FlareSound = SoundID.Item12 with { Volume = 1f, Pitch = -0.7f };

    private static readonly SoundStyle SlamSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 1.1f, Pitch = -0.45f };
    private static readonly SoundStyle ScreamSound = Assets.SFX.NPC.WardenDeath.Asset with { Volume = 1f, PitchVariance = 0.1f };
    private static readonly SoundStyle BurstSound = Assets.SFX.NPC.WoodenKilled.Asset with { Volume = 1.2f, Pitch = -0.3f };

    #endregion

    public WardenState State
    {
        get => (WardenState)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    /// <summary>World X a slam commits to for its whole length.</summary>
    private ref float LockedX => ref NPC.ai[2];

    /// <summary>Free-running tick count both arms derive their idle figure from. Synced, so it lives in ai.</summary>
    private ref float ArmClock => ref NPC.ai[ARM_CLOCK_SLOT];

    private Player Target => Main.player[NPC.target];

    private float LifeFraction => NPC.lifeMax <= 0 ? 1f : NPC.life / (float)NPC.lifeMax;

    /// <summary>1 until half life is gone, 2 after. The second phase slams sooner and conjures more.</summary>
    private int Phase => LifeFraction > PHASE_TWO_LIFE ? 1 : 2;

    /// <summary>True once either hand is gone. The survivor switches to <see cref="SoloRoutine" />.</summary>
    private bool Solo => leftArm < 0 || rightArm < 0;

    /// <summary>True while it performs rather than fights: arriving, taunting or dying. Nothing hurts or is hurt.</summary>
    public bool Performing => State is WardenState.Arrive or WardenState.Taunt or WardenState.Death;

    /// <summary>World Y of the floor the dying body drops to, or -1 to die where it hangs.</summary>
    public float DeathFloorY { get; private set; } = -1f;

    /// <summary>World position of the eye, following the squash and the rotation.</summary>
    public Vector2 EyePosition
    {
        get
        {
            Vector2 anchor = DrawAnchor(out Vector2 origin);

            return anchor + ((EyePixel - origin) * squish * NPC.scale).RotatedBy(NPC.rotation);
        }
    }

    private int leftArm = -1;
    private int rightArm = -1;

    /// <summary>Arms are spawned once and never replaced, so a downed hand stays down.</summary>
    private bool armsSpawned;

    private int leftCursor;
    private int rightCursor = ROUTINE_OFFSET;

    /// <summary>Orders left before the next clasp.</summary>
    private int claspCountdown = CLASP_INTERVAL;

    /// <summary>Ticks until a grapple may be ordered again.</summary>
    private int grappleCooldown = GRAPPLE_COOLDOWN;

    private int slamsSinceConjure;

    /// <summary><see cref="StateTimer" /> a slam's drop began on, or -1 while it is still tensing.</summary>
    private int dropTick = -1;

    /// <summary><see cref="StateTimer" /> a slam landed on, or -1 while it is still airborne.</summary>
    private int landTick = -1;

    /// <summary>World Y of the centre when the death started; the drop is measured from it.</summary>
    private float deathStartY;

    /// <summary>Whether the current state was entered this tick, and so still owes its tick 0.</summary>
    private bool entered;

    /// <summary>Damage the NPC was given once difficulty scaling has had its go. Everything scales off it.</summary>
    private int baseDamage;

    // Cosmetic, client-side and never synced.
    private float drawShake;
    private Vector2 squish = Vector2.One;

    /// <summary>0..1 flare on top of the eye's state glow, decaying every tick.</summary>
    private float eyeFlash;

    private bool introStarted;
    private bool musicStarted;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);

        var drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers
        {
            CustomTexturePath = AssetReferences.Content.WoodenWarden.WardenBestiary.KEY,
            PortraitScale = 0.6f,
            PortraitPositionYOverride = 0f,
        };

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([
            new MoonLordPortraitBackgroundProviderBestiaryInfoElement(),
            new FlavorTextBestiaryInfoElement(Mod.GetLocalizationValue("WoodenWardenBoss.Bestiary")),
        ]);
    }

    public override void SetDefaults()
    {
        NPC.width = 200;
        NPC.height = 184;

        NPC.damage = 12;
        NPC.defense = 10;
        NPC.lifeMax = 2020;

        NPC.aiStyle = -1;
        AIType = -1;

        NPC.noGravity = true;
        NPC.noTileCollide = true;

        NPC.boss = true;
        NPC.npcSlots = 10f;
        NPC.lavaImmune = true;
        NPC.knockBackResist = 0f;

        NPC.value = Item.buyPrice(gold: 4);

        NPC.HitSound = Assets.SFX.NPC.WoodHit2.Asset with
        {
            Volume = 1.4f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };

        // The burst plays the death; the kill itself lands a few ticks later on an empty sprite.
        NPC.DeathSound = null;

        // ModNPC.Music is unset on purpose: MusicDirector owns the track from its first sample, or the death fill lands
        // off the beat.
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        // Read once: NPC.damage is rewritten every tick from baseDamage, and NPC.life has already been scaled by
        // tModLoader before this runs.
        baseDamage = NPC.damage;

        if (Main.expertMode)
        {
            baseDamage += (int)(baseDamage * 0.2f);
            NPC.defense = 14;
        }

        if (Main.masterMode)
        {
            baseDamage += (int)(baseDamage * 0.35f);
            NPC.defense = 18;
        }

        NPC.damage = baseDamage;
    }

    public override void OnKill() => NPC.SetEventFlagCleared(ref DownedBossSystem.DownedWoodenWarden, -1);

    public override void BossLoot(ref int potionType) => potionType = ItemID.LesserHealingPotion;

    public override void Unload() => WardenSplinter.ClearCache();

    /// <summary>Places it above its station over whoever summoned it, and starts the descent.</summary>
    public override void OnSpawn(IEntitySource source)
    {
        NPC.TargetClosest(false);

        NPC.Center = HoverAnchor() - Vector2.UnitY * ARRIVE_DROP;
        NPC.velocity = Vector2.Zero;

        SetState(WardenState.Arrive);
    }

    public override void AI()
    {
        if (baseDamage <= 0)
            baseDamage = NPC.damage;

        // Cleared before the switch and re-set by any SetState inside it, so OnSpawn's SetState still gets a real tick 0.
        entered = false;

        // Derived rather than latched, so a client that never ran OnSpawn or CheckDead agrees without being told.
        NPC.dontTakeDamage = Performing;

        StartPresentation();

        // Wrapped rather than left to grow: the arms take a sine of it, and a float that large loses the precision the
        // figure needs.
        ArmClock = (ArmClock + 1f) % CLOCK_WRAP;

        switch (State)
        {
            case WardenState.Arrive:
                UpdateArrive();
                break;

            case WardenState.Slam:
                UpdateSlam();
                break;

            case WardenState.Conjure:
                UpdateConjure();
                break;

            case WardenState.Taunt:
                UpdateTaunt();
                break;

            case WardenState.Death:
                UpdateDeath();
                break;

            default:
                UpdateIdle();
                break;
        }

        DriveArms();

        NPC.damage = ContactDamage;

        if (!Main.dedServ)
        {
            eyeFlash = Math.Max(eyeFlash - 0.03f, 0f);

            if (State != WardenState.Death || StateTimer < DEATH_BURST_TICK)
                Lighting.AddLight(EyePosition, EyeColor.ToVector3() * (0.3f + 0.5f * EyeGlow));
        }

        // A state entered this tick keeps tick 0; incrementing unconditionally would skip every state's first tick.
        if (!entered)
            StateTimer++;
    }

    private void SetState(WardenState state)
    {
        State = state;
        StateTimer = 0f;

        // Claims this tick's increment, so the new state's tick 0 still runs.
        entered = true;

        dropTick = -1;
        landTick = -1;
        drawShake = 0f;

        NPC.netUpdate = true;
    }

    /// <summary>Contact damage for the current pose.</summary>
    private int ContactDamage => State switch
    {
        WardenState.Arrive or WardenState.Taunt or WardenState.Death => 0,

        // Falling onto the column. The slam's own damage is dealt by HurtBand on landing.
        WardenState.Slam when dropTick >= 0 && landTick < 0 => baseDamage * 2,

        _ => baseDamage,
    };

    /// <summary>
    ///     Tied to <see cref="ContactDamage" />, and tested against an inset box rather than the full frame, whose corners
    ///     are foliage. Without the inset a slam connects from well outside the body.
    /// </summary>
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        cooldownSlot = ImmunityCooldownID.Bosses;

        if (ContactDamage <= 0)
            return false;

        Rectangle body = NPC.Hitbox;

        body.Inflate(-BODY_INSET_X, -BODY_INSET_Y);

        return body.Intersects(target.Hitbox);
    }

    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
    {
        if (State == WardenState.Slam && dropTick >= 0 && landTick < 0)
            modifiers.Knockback += 2f;
        else
            modifiers.Knockback *= 0.5f;
    }

    /// <summary>Never culled mid-performance: an entrance or a death needs to finish.</summary>
    public override bool CheckActive() => State is not (WardenState.Arrive or WardenState.Death);

    /// <summary>Intercepts the kill so the death can play out before it actually dies. Server-side.</summary>
    public override bool CheckDead()
    {
        if (State == WardenState.Death)
            return true;

        NPC.life = 1;
        NPC.dontTakeDamage = true;
        NPC.velocity *= 0.3f;

        deathStartY = NPC.Center.Y;
        DeathFloorY = FindFloor(NPC.Center.X, NPC.Bottom.Y);

        SetState(WardenState.Death);

        return false;
    }

    #region entrance

    /// <summary>Starts the title card and the score locally. Cosmetic, so each client does it once for itself.</summary>
    private void StartPresentation()
    {
        if (Main.dedServ)
            return;

        if (!introStarted)
        {
            introStarted = true;

            // Only for a client that saw the entrance; one that joins mid-fight gets no card for a fight in progress.
            if (State == WardenState.Arrive && StateTimer <= ARRIVE_TICKS)
                BossIntroSystem.Play(CreateIntro());
        }

        if (musicStarted || State == WardenState.Death || (State == WardenState.Arrive && StateTimer < ARRIVE_TICKS))
            return;

        musicStarted = true;

        MusicDirector.Play(WardenMusic.Create(), fadeIn: 0.35f);
    }

    /// <summary>Reveals on arrival and settles itself <see cref="ARRIVE_HOLD_TICKS" /> later, as the fight starts.</summary>
    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<WoodenWardenBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(WoodenWardenBoss)}.Epithet"),

        TrackTitle = "Wooden Warden",
        TrackArtist = "wymsical",

        Accent = Color.YellowGreen,

        RevealTick = ARRIVE_TICKS,
        Duration = ARRIVE_HOLD_TICKS,
    };

    /// <summary>Descends onto its station, lights up on arrival, and holds there while the card is up.</summary>
    private void UpdateArrive()
    {
        int tick = (int)StateTimer;

        Vector2 station = HoverAnchor();

        if (tick < ARRIVE_TICKS)
        {
            float progress = EaseCubicOut.Ease((tick + 1f) / ARRIVE_TICKS);

            // Position servo, so the descent lands on the station exactly on the reveal tick.
            Vector2 wanted = station - Vector2.UnitY * (ARRIVE_DROP * (1f - progress));

            NPC.velocity = wanted - NPC.Center;
            NPC.rotation = 0f;

            squish = Vector2.Lerp(new Vector2(0.9f, 1.14f), Vector2.One, progress);

            if (!Main.dedServ && tick % 3 == 0)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture, 0f, -1f, Scale: 1.1f);

            return;
        }

        if (tick == ARRIVE_TICKS)
            Reveal();

        squish = Vector2.Lerp(squish, Vector2.One, 0.12f);

        Track();

        if (tick >= ARRIVE_TICKS + ARRIVE_HOLD_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(WardenState.Idle);
    }

    /// <summary>The descent ends: the eye lights and it announces itself. The intro supplies the shake.</summary>
    private void Reveal()
    {
        eyeFlash = 1f;
        squish = new Vector2(1.1f, 0.9f);

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(RoarSound, NPC.Center);

        Vector2 eye = EyePosition;

        for (int i = 0; i < 24; i++)
        {
            Dust dust = Dust.NewDustPerfect(eye, DustID.GreenTorch, Main.rand.NextVector2CircularEdge(7f, 4f), Scale: 1.5f);

            dust.noGravity = true;
        }
    }

    #endregion

    #region decisions

    private void UpdateIdle()
    {
        squish = Vector2.Lerp(squish, Vector2.One, 0.15f);
        drawShake = 0f;

        Track();

        // Server-only: every branch below can change the target or roll, and clients doing it separately would desync.
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Retarget();

        if (State != WardenState.Idle || StateTimer < IdleTicks)
            return;

        ChooseAction();
    }

    /// <summary>Re-picks the closest living player whenever the target is lost and periodically otherwise; with none left, it has won.</summary>
    private void Retarget()
    {
        int previous = NPC.target;

        if (!NPC.HasValidTarget || StateTimer % RETARGET_INTERVAL == 0f)
            NPC.TargetClosest(false);

        if (NPC.target != previous)
            NPC.netUpdate = true;

        if (!NPC.HasValidTarget)
            SetState(WardenState.Taunt);
    }

    /// <summary>Breathing room between actions. The fight speeds up by shortening this, not the attacks.</summary>
    private int IdleTicks
    {
        get
        {
            int dwell = Main.masterMode ? IDLE_TICKS_MASTER : IDLE_TICKS;

            return Phase >= 2 ? (int)(dwell * PHASE_TWO_IDLE) : dwell;
        }
    }

    private int SlamsPerConjure => Phase >= 2 ? SLAMS_PER_CONJURE_PHASE_TWO : SLAMS_PER_CONJURE;

    private void ChooseAction()
    {
        // A slam cannot land on a target above the body, so that is conjuring's turn too.
        if (slamsSinceConjure >= SlamsPerConjure || Target.Center.Y < NPC.Bottom.Y)
        {
            slamsSinceConjure = 0;
            SetState(WardenState.Conjure);

            return;
        }

        LockedX = Target.Center.X;

        SetState(WardenState.Slam);
    }

    #endregion

    #region slam

    private int TenseTicks => Phase >= 2 ? TENSE_TICKS_PHASE_TWO : TENSE_TICKS;

    /// <summary>Settles over the locked column, tenses and shakes, drops onto it and recovers.</summary>
    private void UpdateSlam()
    {
        int tick = (int)StateTimer;

        if (landTick >= 0)
        {
            SlamRecover(tick - landTick);
            return;
        }

        if (dropTick >= 0)
        {
            SlamDrop(tick - dropTick);
            return;
        }

        if (tick < SLAM_SETTLE_TICKS)
        {
            SlamSettle(tick);
            return;
        }

        SlamTense(tick - SLAM_SETTLE_TICKS, tick);
    }

    /// <summary>Brakes to a stop over the column and draws itself up. The first tell that a slam is coming.</summary>
    private void SlamSettle(int tick)
    {
        float progress = tick / (float)SLAM_SETTLE_TICKS;

        NPC.velocity *= 0.86f;
        NPC.velocity.X += Math.Clamp((LockedX - NPC.Center.X) * 0.01f, -0.6f, 0.6f);

        NPC.rotation = NPC.rotation.AngleLerp(0f, 0.15f);

        squish = Vector2.Lerp(squish, new Vector2(0.94f, 1.06f), EaseQuadOut.Ease(progress));
    }

    private void SlamTense(int since, int tick)
    {
        float progress = Math.Clamp(since / (float)TenseTicks, 0f, 1f);

        // Horizontal only: the column is locked, and tracking the target's height here would undo the tell.
        float toColumn = LockedX - NPC.Center.X;

        NPC.velocity.X = Math.Clamp(toColumn * 0.08f, -6f, 6f);
        NPC.velocity.Y = -TENSE_RISE;

        NPC.rotation = NPC.rotation.AngleLerp(0f, 0.2f);

        // Winds up rather than jitters at a constant rate, so the drop reads as the release.
        drawShake = TENSE_SHAKE * progress;
        squish = new Vector2(1f + 0.14f * progress, 1f - 0.12f * progress);

        if (!Main.dedServ)
        {
            if (since == 0)
            {
                SoundEngine.PlaySound(TenseSound, NPC.Center);
                eyeFlash = Math.Max(eyeFlash, 0.6f);
            }

            if (since % 4 == 0)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture, 0f, 1f, Scale: 1f);
        }

        if (Main.netMode == NetmodeID.MultiplayerClient || since < TenseTicks)
            return;

        if (Math.Abs(toColumn) > SLAM_ALIGN && since < TenseTicks * 2)
            return;

        dropTick = tick;

        NPC.netUpdate = true;
    }

    private void SlamDrop(int since)
    {
        drawShake = 0f;
        squish = Vector2.Lerp(squish, new Vector2(0.86f, 1.18f), 0.2f);

        NPC.velocity.X = Math.Clamp(LockedX - NPC.Center.X, -4f, 4f);
        NPC.velocity.Y = Math.Min(NPC.velocity.Y + SLAM_ACCELERATION, SLAM_MAX_SPEED);

        NPC.rotation = 0f;

        // Manual, because the descent is scripted and vanilla collision would fight it. fallThrough keeps platforms out
        // of it, so the slam only stops on real ground.
        Vector2 resolved = Collision.TileCollision(NPC.position, NPC.velocity, NPC.width, NPC.height, fallThrough: true, fall2: true);

        if (resolved.Y < NPC.velocity.Y)
        {
            NPC.velocity = resolved;
            landTick = (int)StateTimer;

            SlamImpact();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;

            return;
        }

        if (since >= SLAM_TIMEOUT && Main.netMode != NetmodeID.MultiplayerClient)
        {
            landTick = (int)StateTimer;

            NPC.netUpdate = true;
        }
    }

    private void SlamRecover(int since)
    {
        // Still solid to walk into, but no longer the slam's damage.
        squish = Vector2.Lerp(squish, Vector2.One, 0.09f);
        drawShake = 0f;

        // Sits in the crater for a beat, then takes up station over the target again.
        if (since < SLAM_RECOVER_TICKS * 0.4f)
            NPC.velocity *= 0.8f;
        else
            Track(TRACK_RESPONSE * 0.7f);

        if (since >= SLAM_RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(WardenState.Idle);
    }

    private void SlamImpact()
    {
        var impact = new Vector2(NPC.Center.X, NPC.Bottom.Y);

        slamsSinceConjure++;

        SoundEngine.PlaySound(SlamSound, impact);

        // Kept near the reach of the band that hurts: a front that runs on past the damage reads as a hit the player
        // should have taken and did not.
        var wave = new BlockWave(impact, 300f, lifetime: 40)
        {
            Amplitude = 24f,
            Thickness = 80f,
            Reach = 148f,
            Cycles = 3f,
            Blend = 0.6f,
        };

        TileShockwaveSystem.Spawn(wave);

        HurtBand(impact);

        if (Main.dedServ)
            return;

        if (NearLocalPlayer)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 20);

        Rubble(impact);
        Debris(impact, wave.Speed);
    }

    /// <summary>
    ///     The slam's own damage, separate from contact. Each client hurts only itself; Player.Hurt is not safe to call
    ///     for a remote player, and a dedicated server has no local player.
    /// </summary>
    private void HurtBand(Vector2 impact)
    {
        foreach (Player target in Main.ActivePlayers)
        {
            if (target.whoAmI != Main.myPlayer || target.dead || target.immune)
                continue;

            Vector2 offset = target.Center - impact;

            if (Math.Abs(offset.X) > SLAM_REACH_X || Math.Abs(offset.Y) > SLAM_REACH_Y)
                continue;

            int damage = Main.masterMode ? (int)(SLAM_DAMAGE * 1.5f) : SLAM_DAMAGE;

            target.Hurt(
                PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(target.name + SlamQuotes())),
                damage,
                offset.X < 0f ? -1 : 1,
                knockback: 8f
            );
        }
    }

    /// <summary>Material-correct dust via <see cref="WorldGen.KillTile" /> with <c>fail: true</c>; the world is left intact.</summary>
    private static void Rubble(Vector2 impact)
    {
        Point centre = impact.ToTileCoordinates();

        for (int x = -RUBBLE_REACH; x <= RUBBLE_REACH; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Point coords = new(centre.X + x, centre.Y + y);

                if (!WorldGen.InWorld(coords.X, coords.Y, 2))
                    continue;

                if (Framing.GetTileSafely(coords) is { HasUnactuatedTile: true } tile && Main.tileSolid[tile.TileType])
                    WorldGen.KillTile(coords.X, coords.Y, fail: true);
            }
        }

        for (int i = 0; i < 26; i++)
        {
            Dust.NewDustPerfect(
                impact + Main.rand.NextVector2Circular(RUBBLE_REACH * 14f, 10f),
                DustID.WoodFurniture,
                new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-7f, -1f)),
                100,
                Color.White,
                Main.rand.NextFloat(1.1f, 1.9f)
            );
        }
    }

    /// <summary>Loose blocks of the struck ground thrown outward along the wave front, which travels at <paramref name="speed" />.</summary>
    private static void Debris(Vector2 impact, float speed)
    {
        Point centre = impact.ToTileCoordinates();

        for (int i = 0; i < DEBRIS_COUNT; i++)
        {
            int offset = Main.rand.Next(-RUBBLE_REACH, RUBBLE_REACH + 1);
            Tile tile = Framing.GetTileSafely(centre.X + offset, centre.Y + Main.rand.Next(2));

            if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType] || Main.tileFrameImportant[tile.TileType])
                continue;

            float direction = offset != 0 ? Math.Sign(offset) : Main.rand.NextBool() ? 1f : -1f;

            TileClusterShards.Debris(
                tile.TileType,
                impact + new Vector2(offset * 16f, -8f),
                new Vector2(direction * speed * Main.rand.NextFloat(0.7f, 1.4f), -Main.rand.NextFloat(3f, 7.5f))
            );
        }
    }

    #endregion

    #region conjure

    /// <summary>Below half life the conjure lifts fewer, far larger boulders.</summary>
    private bool Boulders => Phase >= 2;

    private int ConjureCount => Boulders ? BOULDER_COUNT : CONJURE_COUNT;

    private int LaunchStagger => Boulders ? BOULDER_LAUNCH_STAGGER : CONJURE_LAUNCH_STAGGER;

    /// <summary>Hang handed to chunk <paramref name="index" />, so launches follow each other <see cref="LaunchStagger" /> apart.</summary>
    private int ChunkHang(int index)
        => WardenTileCluster.RISE_TICKS + CONJURE_HOVER + index * (LaunchStagger - CONJURE_STAGGER);

    /// <summary>Ticks from the flare to the end of the state: the last launch plus the tail.</summary>
    private int ConjureLength(int count)
        => WardenTileCluster.RISE_TICKS + CONJURE_HOVER + (count - 1) * LaunchStagger + CONJURE_TAIL;

    /// <summary>
    ///     Holds still while the eye charges its flare, then tears chunks of the ground around the target free one after
    ///     another. The chunks time their own launches.
    /// </summary>
    private void UpdateConjure()
    {
        int tick = (int)StateTimer;
        int count = ConjureCount;

        // Keeps station over the target through the cast; only a slam lets go of it.
        Track();

        squish = Vector2.Lerp(squish, Vector2.One, 0.1f);
        drawShake = 0f;

        if (tick < FLARE_TICKS)
        {
            if (!Main.dedServ)
            {
                if (tick == 0)
                    SoundEngine.PlaySound(ChargeSound, EyePosition);

                ChargeMote(tick / (float)FLARE_TICKS);
            }

            return;
        }

        int since = tick - FLARE_TICKS;

        if (since == 0)
        {
            eyeFlash = 1f;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(FlareSound, EyePosition);

                if (NearLocalPlayer)
                    CameraSystem.shake = Math.Max(CameraSystem.shake, 6);
            }
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (since % CONJURE_STAGGER == 0 && since / CONJURE_STAGGER < count)
            ConjureChunk(since / CONJURE_STAGGER, count);

        if (since >= ConjureLength(count))
            SetState(WardenState.Idle);
    }

    /// <summary>Motes pulled into the eye while it charges.</summary>
    private void ChargeMote(float progress)
    {
        if (!Main.rand.NextBool(progress > 0.5f ? 1 : 2))
            return;

        Vector2 eye = EyePosition;
        Vector2 offset = Main.rand.NextVector2CircularEdge(70f, 70f) * (1.2f - progress * 0.5f);

        Dust mote = Dust.NewDustPerfect(eye + offset, DustID.GreenTorch, -offset * 0.07f, Scale: 1.3f);

        mote.noGravity = true;
    }

    /// <summary>Sites alternate outside-in across the spread, so consecutive launches come from opposite flanks.</summary>
    private void ConjureChunk(int index, int count)
    {
        if (Target is not { active: true } target)
            return;

        int pair = index / 2;
        int slotIndex = index % 2 == 0 ? pair : count - 1 - pair;
        float slot = count == 1 ? 0.5f : slotIndex / (float)(count - 1);

        float x = target.Center.X + MathHelper.Lerp(-CONJURE_SPREAD, CONJURE_SPREAD, slot);

        int damage = Boulders
            ? NPC.GetAttackDamage_ForProjectiles_MultiLerp(BOULDER_DAMAGE, BOULDER_DAMAGE_EXPERT, BOULDER_DAMAGE_MASTER)
            : NPC.GetAttackDamage_ForProjectiles_MultiLerp(CONJURE_DAMAGE, CONJURE_DAMAGE_EXPERT, CONJURE_DAMAGE_MASTER);

        WardenTileCluster.Conjure(
            NPC,
            FindSurface(x, target.Bottom.Y),
            Boulders ? WardenTileCluster.BOULDER_RADIUS : WardenTileCluster.CHUNK_RADIUS,
            ChunkHang(index),
            damage,
            Main.rand.Next(1 << 20)
        );
    }

    /// <summary>Top block of the ground nearest <paramref name="feetY" /> in the column at <paramref name="worldX" />, below first.</summary>
    private static Point FindSurface(float worldX, float feetY)
    {
        int x = (int)(worldX / 16f);
        int feet = (int)(feetY / 16f);

        for (int distance = 0; distance <= SURFACE_SEARCH_DOWN; distance++)
        {
            if (IsSurface(x, feet + distance))
                return new Point(x, feet + distance);

            if (distance > 0 && distance <= SURFACE_SEARCH_UP && IsSurface(x, feet - distance))
                return new Point(x, feet - distance);
        }

        // No ground in reach: the sample comes up mostly empty and fills itself solid.
        return new Point(x, feet + 4);
    }

    private static bool IsSurface(int x, int y)
        => WorldGen.InWorld(x, y, 10) && WorldGen.SolidTile(x, y) && !WorldGen.SolidTile(x, y - 1);

    #endregion

    #region taunt

    /// <summary>Everyone is down: it smashes its knuckles together for a while, then leaves.</summary>
    private void UpdateTaunt()
    {
        int tick = (int)StateTimer;

        drawShake = 0f;

        if (tick == 0)
        {
            eyeFlash = 1f;

            if (!Main.dedServ)
                SoundEngine.PlaySound(RoarSound with { Pitch = -0.3f }, NPC.Center);
        }

        if (tick < TAUNT_TICKS)
        {
            NPC.velocity *= 0.9f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);

            // Rocks with every knuckle smash, which lands as the arms' cycle wraps.
            float beat = tick % WardenArm.TAUNT_PERIOD / (float)WardenArm.TAUNT_PERIOD;

            squish = Vector2.Lerp(new Vector2(1.06f, 0.94f), Vector2.One, EaseQuadOut.Ease(beat));

            if (tick % WardenArm.TAUNT_PERIOD == 0)
                eyeFlash = Math.Max(eyeFlash, 0.7f);

            return;
        }

        squish = Vector2.Lerp(squish, Vector2.One, 0.1f);

        NPC.velocity.X *= 0.95f;
        NPC.velocity.Y = Math.Max(NPC.velocity.Y - 0.45f, -16f);

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        NPC.EncourageDespawn(10);

        if (tick >= TAUNT_TICKS + TAUNT_LEAVE_TICKS)
            Despawn();
    }

    /// <summary>Removes it without a kill: no loot, no flag. The server syncs the removal.</summary>
    private void Despawn()
    {
        NPC.life = 0;
        NPC.active = false;
        NPC.netSkip = -1;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }

    #endregion

    #region death

    /// <summary>Resting on the floor it dropped to, and drawn from its base.</summary>
    private bool Grounded => State == WardenState.Death && DeathFloorY >= 0f && StateTimer >= DEATH_DROP_TICKS;

    /// <summary>
    ///     Drops to the floor, screams, and pounds the ground with both arms in a tantrum that speeds up until it bursts
    ///     into splinters of its own sprite. Timed entirely off <see cref="StateTimer" />, which the arms also read.
    /// </summary>
    private void UpdateDeath()
    {
        int tick = (int)StateTimer;

        if (tick == 0 && !Main.dedServ && MusicDirector.Playing == WardenMusic.NAME)
            MusicDirector.Fire(WardenMusic.DEATH_FILL, WardenMusic.FILL_GRID);

        if (tick < DEATH_DROP_TICKS)
        {
            DeathDrop(tick);
            return;
        }

        if (tick == DEATH_DROP_TICKS)
            Scream();

        if (tick < DEATH_BURST_TICK)
        {
            Tantrum(tick - DEATH_DROP_TICKS);
            return;
        }

        if (tick == DEATH_BURST_TICK)
            Burst();

        NPC.velocity = Vector2.Zero;

        // The kill itself is the server's, like any other. A client doing it locally would remove the boss early and
        // then have it reappear on the next sync.
        if (tick < DEATH_BURST_TICK + DEATH_KILL_DELAY || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        NPC.life = 0;
        NPC.HitEffect();
        NPC.checkDead();
    }

    /// <summary>Position servo onto the floor, eased in, so it lands exactly on the scream.</summary>
    private void DeathDrop(int tick)
    {
        float progress = EaseQuadIn.Ease((tick + 1f) / DEATH_DROP_TICKS);

        NPC.rotation = NPC.rotation.AngleLerp(0f, 0.2f);

        drawShake = 1.5f;
        squish = Vector2.Lerp(Vector2.One, new Vector2(0.9f, 1.12f), progress);

        if (DeathFloorY < 0f)
        {
            NPC.velocity *= 0.85f;
            return;
        }

        float rest = DeathFloorY - BODY_HALF_HEIGHT;

        NPC.velocity = new Vector2(NPC.velocity.X * 0.8f, MathHelper.Lerp(deathStartY, rest, progress) - NPC.Center.Y);
    }

    /// <summary>Lands and screams: the cue for the action lines, the radial blur and the shake.</summary>
    private void Scream()
    {
        eyeFlash = 1f;
        squish = new Vector2(1.22f, 0.8f);

        NPC.velocity = Vector2.Zero;

        if (Main.dedServ)
            return;

        var floor = new Vector2(NPC.Center.X, DeathFloorY >= 0f ? DeathFloorY : NPC.Center.Y + BODY_HALF_HEIGHT);

        SoundEngine.PlaySound(SlamSound, floor);
        SoundEngine.PlaySound(ScreamSound, NPC.Center);
        SoundEngine.PlaySound(RoarSound with { Volume = 1.2f }, NPC.Center);

        if (DeathFloorY >= 0f)
        {
            TileShockwaveSystem.Spawn(new BlockWave(floor, 360f, lifetime: 44)
            {
                Amplitude = 26f,
                Thickness = 90f,
                Reach = 160f,
                Cycles = 3f,
                Blend = 0.6f,
            });
        }

        if (!NearLocalPlayer)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, 22);

        ActionLinesSystem.Play(0.9f, 16);

        // Timed to outlast the tantrum, so an interrupted death cannot leave the blur up.
        RadialBlurEffect.Set(0.22f, falloff: 1.4f, rSpin: 0.04f, ticks: DEATH_BURST_TICK - DEATH_DROP_TICKS + 20, worldFocus: NPC.Center, rampTicks: 12);
    }

    /// <summary>Kneels and convulses while the arms pound; jolts on every pound, and shakes itself apart at the end.</summary>
    private void Tantrum(int since)
    {
        bool pounding = since < DEATH_TANTRUM_TICKS;

        NPC.velocity = Vector2.Zero;

        drawShake = pounding
            ? MathHelper.Lerp(1.5f, 4f, since / (float)DEATH_TANTRUM_TICKS)
            : MathHelper.Lerp(5f, 9f, (since - DEATH_TANTRUM_TICKS) / (float)DEATH_SHAKE_TICKS);

        bool pound = WardenArm.PoundsOn(since, -1) || WardenArm.PoundsOn(since, 1);

        if (pound)
            squish = new Vector2(1.1f, 0.9f);

        squish = Vector2.Lerp(squish, new Vector2(1.04f, 0.96f), 0.15f);

        if (Main.dedServ)
            return;

        eyeFlash = Math.Max(eyeFlash, pounding ? 0.6f : 0.9f);

        // With no arms left to pound, the body stamps the floor on the same schedule itself.
        if (pound && leftArm < 0 && rightArm < 0)
            Stomp();

        if (since % 3 == 0)
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture, Main.rand.NextFloat(-2f, 2f), -2f, Scale: 1.2f);

        if (since == DEATH_TANTRUM_TICKS / 2)
            SoundEngine.PlaySound(ScreamSound, NPC.Center);

        if (since == DEATH_TANTRUM_TICKS && NearLocalPlayer)
            ActionLinesSystem.Play(1f, 20);
    }

    private void Stomp()
    {
        var floor = new Vector2(NPC.Center.X, DeathFloorY >= 0f ? DeathFloorY : NPC.Center.Y + BODY_HALF_HEIGHT);

        SoundEngine.PlaySound(SlamSound with { Volume = 0.7f }, floor);

        TileShockwaveSystem.Spawn(new BlockWave(floor, 140f, lifetime: 24)
        {
            Amplitude = 12f,
            Thickness = 56f,
            Reach = 96f,
            Blend = 0.6f,
        });

        if (NearLocalPlayer)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 8);
    }

    /// <summary>Bursts into splinters cut from its own sprite, and lets the screen effects go. Client-side.</summary>
    private void Burst()
    {
        drawShake = 0f;

        if (Main.dedServ)
            return;

        Vector2 centre = BodyCentre;

        SoundEngine.PlaySound(BurstSound, centre);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.6f }, centre);

        Asset<Texture2D> sprite = TextureAssets.Npc[Type];

        if (sprite.Value is { } texture)
            WardenSplinter.Burst(sprite, texture.Bounds, centre, NPC.rotation, SpriteEffects.None, SPLINTER_CELL, 10f);

        for (int i = 0; i < 30; i++)
        {
            Dust smoke = Dust.NewDustPerfect(centre + Main.rand.NextVector2Circular(70f, 80f), DustID.Smoke, Main.rand.NextVector2Circular(5f, 5f), 110, Color.White, Main.rand.NextFloat(1.4f, 2.2f));

            smoke.noGravity = true;
        }

        for (int i = 0; i < 24; i++)
        {
            Dust spark = Dust.NewDustPerfect(EyePosition, DustID.GreenTorch, Main.rand.NextVector2Circular(9f, 9f), Scale: 1.6f);

            spark.noGravity = true;
        }

        // Released regardless of distance: a player who walked off mid-tantrum still has them up from the scream.
        ActionLinesSystem.Stop(40);
        RadialBlurEffect.Stop(40);

        if (!NearLocalPlayer)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, 28);

        ScreenFlashSystem.Flash(hold: 2, fade: 30);
    }

    /// <summary>World Y of the first floor within <see cref="DEATH_FLOOR_SEARCH" /> below <paramref name="fromY" />, or -1.</summary>
    private static float FindFloor(float worldX, float fromY)
    {
        int x = (int)(worldX / 16f);
        int start = (int)(fromY / 16f);
        int reach = (int)(DEATH_FLOOR_SEARCH / 16f);

        for (int y = start; y < start + reach; y++)
        {
            if (!WorldGen.InWorld(x, y, 10))
                break;

            if (Framing.GetTileSafely(x, y) is { HasUnactuatedTile: true } tile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                return y * 16f;
        }

        return -1f;
    }

    #endregion

    #region arms

    /// <summary>Spawns both arms once and issues their orders. All of it is server-side.</summary>
    private void DriveArms()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!armsSpawned)
        {
            armsSpawned = true;

            leftArm = SpawnArm(-1, NPC.Center - Vector2.UnitX * 120f);
            rightArm = SpawnArm(1, NPC.Center + Vector2.UnitX * 120f);

            NPC.netUpdate = true;
        }

        ResolveArms();

        if (grappleCooldown > 0)
            grappleCooldown--;

        switch (State)
        {
            // The hands hold station through the entrance for the same reason the body does.
            case WardenState.Arrive:
                return;

            case WardenState.Taunt:
                CommandAll(WardenArm.ArmState.Taunt);
                return;

            case WardenState.Death:
                ReviveArms();
                CommandAll(WardenArm.ArmState.Tantrum);
                return;
        }

        TickArm(leftArm, ref leftCursor);
        TickArm(rightArm, ref rightCursor);

        TryClasp();
    }

    private static WardenArm? ArmAt(int index)
        => index >= 0 && index < Main.maxNPCs && Main.npc[index] is { active: true, ModNPC: WardenArm arm } ? arm : null;

    /// <summary>Drops indices that no longer point at one of our arms, and syncs the change so clients know who is left.</summary>
    private void ResolveArms()
    {
        int left = IsOurArm(leftArm, -1) ? leftArm : -1;
        int right = IsOurArm(rightArm, 1) ? rightArm : -1;

        if (left == leftArm && right == rightArm)
            return;

        leftArm = left;
        rightArm = right;

        NPC.netUpdate = true;
    }

    private bool IsOurArm(int index, int side)
    {
        if (index < 0 || index >= Main.maxNPCs)
            return false;

        NPC npc = Main.npc[index];

        return npc.active
               && npc.type == ModContent.NPCType<WardenArm>()
               && (int)npc.ai[3] == NPC.whoAmI
               && npc.ModNPC is WardenArm arm
               && arm.Side == side;
    }

    /// <summary>Puts every living arm into <paramref name="state" />; one already in it is left alone.</summary>
    private void CommandAll(WardenArm.ArmState state)
    {
        if (ArmAt(leftArm) is { } left && left.State != state)
            left.Command(state);

        if (ArmAt(rightArm) is { } right && right.State != state)
            right.Command(state);
    }

    private void TickArm(int index, ref int cursor)
    {
        if (ArmAt(index) is not { AwaitingOrder: true } arm)
            return;

        WardenArm.ArmState[] routine = Solo ? SoloRoutine : Routine;

        WardenArm.ArmState order = routine[cursor % routine.Length];

        cursor++;
        claspCountdown--;

        // Substituted rather than delayed: the cursor has already moved on, so a blocked order becomes a different
        // attack instead of stalling the hand.
        if (order == WardenArm.ArmState.Grapple && !CanGrapple())
            order = WardenArm.ArmState.Punch;

        if (order == WardenArm.ArmState.Spear && OtherArm(index) is { State: WardenArm.ArmState.Spear })
            order = WardenArm.ArmState.PalmSlam;

        if (order == WardenArm.ArmState.Grapple)
            grappleCooldown = GRAPPLE_COOLDOWN;

        arm.Command(order);
    }

    private WardenArm? OtherArm(int index) => ArmAt(index == leftArm ? rightArm : leftArm);

    /// <summary>
    ///     A grapple pins the target, so it is only allowed off cooldown, while the body is hovering rather than committed
    ///     to an attack, and once the warden is down to one hand.
    /// </summary>
    private bool CanGrapple() => grappleCooldown <= 0 && Solo && State == WardenState.Idle;

    /// <summary>Sends both arms at the target at once. Only fires when neither is mid-order.</summary>
    private void TryClasp()
    {
        if (claspCountdown > 0)
            return;

        if (ArmAt(leftArm) is not { AwaitingOrder: true } left || ArmAt(rightArm) is not { AwaitingOrder: true } right)
            return;

        claspCountdown = CLASP_INTERVAL;

        left.Command(WardenArm.ArmState.Clasp);
        right.Command(WardenArm.ArmState.Clasp);
    }

    /// <summary>
    ///     Brings back any hand lost in the fight for the tantrum. It starts buried under the pound site and swings up out
    ///     of the floor; mid-air, with no floor, it starts at the shoulder instead.
    /// </summary>
    private void ReviveArms()
    {
        if (leftArm < 0)
            leftArm = SpawnArm(-1, RevivePoint(-1));

        if (rightArm < 0)
            rightArm = SpawnArm(1, RevivePoint(1));
    }

    private Vector2 RevivePoint(int side) => DeathFloorY >= 0f
        ? new Vector2(NPC.Center.X + REVIVE_X * side, DeathFloorY + WardenArm.EMERGE_DEPTH)
        : NPC.Center + new Vector2(REVIVE_X * side, 0f);

    /// <summary>Spawned after the boss's slot, so the arms update after it and read this tick's clock.</summary>
    private int SpawnArm(int side, Vector2 position)
    {
        int index = NPC.NewNPC(
            NPC.GetSource_FromAI(),
            (int)position.X,
            (int)position.Y,
            ModContent.NPCType<WardenArm>(),
            NPC.whoAmI + 1,
            ai3: NPC.whoAmI
        );

        if (index < 0 || index >= Main.maxNPCs)
            return -1;

        if (Main.npc[index].ModNPC is WardenArm arm)
            arm.Side = side;

        Main.npc[index].netUpdate = true;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: index);

        return index;
    }

    #endregion

    #region shared movement

    /// <summary>A point above the target, offset along a 1:2 figure so the hover never sits still.</summary>
    private Vector2 HoverAnchor()
    {
        Vector2 centre = Target is { active: true } target
            ? target.Center - Vector2.UnitY * HOVER_HEIGHT
            : NPC.Center;

        float t = ArmClock / DANCE_PERIOD * MathHelper.TwoPi;

        return centre + new Vector2(MathF.Cos(t) * DANCE_SWAY, MathF.Sin(t * 2f) * DANCE_BOB);
    }

    /// <summary>
    ///     Follows <see cref="HoverAnchor" /> with the target's velocity fed forward, so a running target stays underneath
    ///     rather than being trailed. <paramref name="response" /> is how quickly a change in the wanted velocity is taken up.
    /// </summary>
    private void Track(float response = TRACK_RESPONSE)
    {
        Vector2 lead = Target is { active: true, dead: false } target ? target.velocity : Vector2.Zero;
        Vector2 wanted = lead + (HoverAnchor() - NPC.Center) * TRACK_GAIN;

        if (wanted.LengthSquared() > TRACK_MAX_SPEED * TRACK_MAX_SPEED)
            wanted = wanted.SafeNormalize(Vector2.Zero) * TRACK_MAX_SPEED;

        NPC.velocity = Vector2.Lerp(NPC.velocity, wanted, response);
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.012f, 0.07f);
    }

    private bool NearLocalPlayer => !Main.dedServ && Main.LocalPlayer.DistanceSQ(NPC.Center) < EFFECT_RANGE_SQ;

    #endregion

    #region drawing

    /// <summary>World point the sprite is drawn at, with the matching origin: centred, or on its base while grounded.</summary>
    private Vector2 DrawAnchor(out Vector2 origin)
    {
        origin = new Vector2(SPRITE_WIDTH, SPRITE_HEIGHT) * 0.5f;

        if (!Grounded)
            return NPC.Center + new Vector2(0f, NPC.gfxOffY);

        origin.Y = SPRITE_HEIGHT;

        return new Vector2(NPC.Center.X, DeathFloorY);
    }

    /// <summary>World centre of the drawn sprite, squash included.</summary>
    private Vector2 BodyCentre
    {
        get
        {
            Vector2 anchor = DrawAnchor(out Vector2 origin);

            return anchor + ((new Vector2(SPRITE_WIDTH, SPRITE_HEIGHT) * 0.5f - origin) * squish * NPC.scale).RotatedBy(NPC.rotation);
        }
    }

    /// <summary>0..1 brightness of the eye: a per-state baseline with <see cref="eyeFlash" /> on top.</summary>
    private float EyeGlow
    {
        get
        {
            float tick = StateTimer;

            float baseline = State switch
            {
                WardenState.Arrive => tick < ARRIVE_TICKS ? 0.1f : 0.35f,
                WardenState.Conjure => tick < FLARE_TICKS ? EaseQuadIn.Ease(tick / FLARE_TICKS) : 0.85f,
                WardenState.Slam when dropTick < 0 && tick >= SLAM_SETTLE_TICKS
                    => 0.35f + 0.45f * Math.Clamp((tick - SLAM_SETTLE_TICKS) / TenseTicks, 0f, 1f),
                WardenState.Death => 0.7f,
                _ => 0.3f,
            };

            return Math.Clamp(Math.Max(baseline, eyeFlash), 0f, 1f);
        }
    }

    /// <summary>0..1 strength of the horizontal laser streak across the eye.</summary>
    private float EyeFlare => State == WardenState.Conjure
        ? StateTimer < FLARE_TICKS ? EaseQuadIn.Ease(StateTimer / FLARE_TICKS) * 0.6f : Math.Max(eyeFlash, 0.45f)
        : eyeFlash * 0.8f;

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (State == WardenState.Death && StateTimer >= DEATH_BURST_TICK)
            return false;

        Texture2D texture = TextureAssets.Npc[Type].Value;

        Vector2 position = DrawAnchor(out Vector2 origin);

        if (drawShake > 0f)
            position += Main.rand.NextVector2Circular(drawShake, drawShake * 0.6f);

        spriteBatch.Draw(
            texture,
            position - screenPos,
            null,
            NPC.GetAlpha(drawColor),
            NPC.rotation,
            origin,
            squish * NPC.scale,
            SpriteEffects.None,
            0f
        );

        if (!NPC.IsABestiaryIconDummy)
        {
            Vector2 eye = position + ((EyePixel - origin) * squish * NPC.scale).RotatedBy(NPC.rotation);

            DrawEye(spriteBatch, eye - screenPos, EyeGlow, EyeFlare);
        }

        return false;
    }

    /// <summary>Additive glow, glint and horizontal streak over the eye. Zero-alpha colours add in the NPC batch.</summary>
    private static void DrawEye(SpriteBatch spriteBatch, Vector2 eye, float glow, float flare)
    {
        Texture2D bloom = Assets.Textures.VFX.LightNode.Asset.Value;
        Texture2D star = Assets.Textures.VFX.PixelStar.Asset.Value;

        spriteBatch.Draw(bloom, eye, null, Additive(EyeColor, 0.45f * glow), 0f, bloom.Size() * 0.5f, 0.08f + 0.1f * glow, SpriteEffects.None, 0f);
        spriteBatch.Draw(star, eye, null, Additive(Color.White, 0.8f * glow), 0f, star.Size() * 0.5f, 0.35f + 0.35f * glow, SpriteEffects.None, 0f);

        if (flare <= 0f)
            return;

        Texture2D streak = Assets.Textures.VFX.GlowFlare.Asset.Value;
        Vector2 origin = streak.Size() * 0.5f;

        spriteBatch.Draw(streak, eye, null, Additive(EyeColor, flare), 0f, origin, new Vector2(1.4f + 1.2f * flare, 0.45f), SpriteEffects.None, 0f);
        spriteBatch.Draw(streak, eye, null, Additive(Color.White, 0.6f * flare), 0f, origin, new Vector2(0.8f + 0.6f * flare, 0.25f), SpriteEffects.None, 0f);
    }

    private static Color Additive(Color color, float opacity) => (color * opacity) with { A = 0 };

    #endregion

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ || NPC.life <= 0)
            return;

        for (int i = 0; i < 4; i++)
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture, hit.HitDirection * 1.6f, -1f, Scale: 1.2f);
    }

    public static string SlamQuotes()
    {
        string[] quotes =
        [
            " was flattened into a pancake.",
            "'s body was contorted into a twig.",
            " was crushed by massive amounts of wood.",
            "--... really? that's how you died?",
        ];

        return quotes[Main.rand.Next(quotes.Length)];
    }

    #region net

    /// <summary>State kept outside <see cref="NPC.ai" />. Arm bookkeeping is only acted on by the server.</summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((short)leftArm);
        writer.Write((short)rightArm);
        writer.Write(armsSpawned);

        writer.Write((ushort)(leftCursor % CURSOR_WRAP));
        writer.Write((ushort)(rightCursor % CURSOR_WRAP));

        writer.Write((short)Math.Clamp(claspCountdown, short.MinValue, short.MaxValue));
        writer.Write((short)Math.Clamp(grappleCooldown, 0, short.MaxValue));
        writer.Write((byte)Math.Clamp(slamsSinceConjure, 0, byte.MaxValue));

        writer.Write((short)Math.Clamp(dropTick, short.MinValue, short.MaxValue));
        writer.Write((short)Math.Clamp(landTick, short.MinValue, short.MaxValue));

        // Every height in the death drop is measured from these, so a client that guessed at them would put the
        // tantrum at the wrong floor.
        writer.Write(DeathFloorY);
        writer.Write(deathStartY);

        writer.Write((short)Math.Clamp(baseDamage, 0, short.MaxValue));
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        leftArm = reader.ReadInt16();
        rightArm = reader.ReadInt16();
        armsSpawned = reader.ReadBoolean();

        leftCursor = reader.ReadUInt16();
        rightCursor = reader.ReadUInt16();

        claspCountdown = reader.ReadInt16();
        grappleCooldown = reader.ReadInt16();
        slamsSinceConjure = reader.ReadByte();

        dropTick = reader.ReadInt16();
        landTick = reader.ReadInt16();

        DeathFloorY = reader.ReadSingle();
        deathStartY = reader.ReadSingle();

        baseDamage = reader.ReadInt16();
    }

    #endregion
}
