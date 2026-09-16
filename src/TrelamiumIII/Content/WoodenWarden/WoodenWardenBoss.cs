using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Shockwaves;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     Hovers above the target and drops body slams on it. Owns both <see cref="WardenArm" />s:
///     attack selection happens here and is pushed into each arm's synced <see cref="Entity.ai" />.
/// </summary>
[AutoloadBossHead]
public class WoodenWardenBoss : ModNPC
{
    public enum WardenState
    {
        Hovering,
        Tensing,
        Slamming,
        Recovering,
    }

    #region tuning

    /// <summary>Height the hover anchor sits above the target, in world pixels.</summary>
    private const float HOVER_HEIGHT = 272f;

    private const float HOVER_ACCELERATION = 0.55f;
    private const float HOVER_MAX_SPEED = 10f;
    private const float HOVER_DAMPING = 0.93f;

    /// <summary>Half-extents of the figure the hover anchor traces, in world pixels.</summary>
    private const float DANCE_SWAY = 104f;

    private const float DANCE_BOB = 40f;

    /// <summary>Ticks for one pass of the hover figure. The vertical term runs at twice this rate.</summary>
    private const float DANCE_PERIOD = 186f;

    /// <summary>Ticks of hovering before a slam is considered.</summary>
    private const int HOVER_TICKS = 170;

    private const int HOVER_TICKS_MASTER = 120;

    /// <summary>Fraction of maximum life at which the second phase starts.</summary>
    private const float PHASE_TWO_LIFE = 0.5f;

    /// <summary>Fraction of the hover dwell kept in the second phase.</summary>
    private const float PHASE_TWO_HOVER = 0.55f;

    /// <summary>Horizontal distance to the locked column within which the tense commits.</summary>
    private const float SLAM_ALIGN = 36f;

    /// <summary>Ticks spent shaking in place before the drop.</summary>
    private const int TENSE_TICKS = 54;

    /// <summary>Upward drift across the tense, in pixels per tick.</summary>
    private const float TENSE_RISE = 1.5f;

    private const float TENSE_SHAKE = 6f;

    private const float SLAM_ACCELERATION = 2.4f;
    private const float SLAM_MAX_SPEED = 27f;

    /// <summary>Ticks before a slam that never reaches a floor gives up.</summary>
    private const int SLAM_TIMEOUT = 150;

    private const int RECOVER_TICKS = 72;

    private const int SLAM_DAMAGE = 55;

    /// <summary>
    ///     Half-extents of the band the impact hurts, in world pixels. Kept close to the width of
    ///     the body so the damage lands where the slam looks like it landed.
    /// </summary>
    private const float SLAM_REACH_X = 150f;

    private const float SLAM_REACH_Y = 80f;

    /// <summary>Inset applied to the contact hitbox, in world pixels; see <see cref="CanHitPlayer" />.</summary>
    private const int BODY_INSET_X = 46;

    private const int BODY_INSET_Y = 26;

    /// <summary>Tiles either side of the impact that shed debris.</summary>
    private const int RUBBLE_REACH = 4;

    /// <summary>Ticks between re-picks of the target while the current one is still valid.</summary>
    private const int RETARGET_INTERVAL = 180;

    /// <summary>Wrap point for <see cref="ArmClock" />. A whole number of hover figures.</summary>
    private const int CLOCK_WRAP = 7440;

    /// <summary>Orders issued between synchronised clasps.</summary>
    private const int CLASP_INTERVAL = 5;

    /// <summary>
    ///     The order each arm works through while both are alive. The right arm reads it from
    ///     <see cref="ROUTINE_OFFSET" />.
    /// </summary>
    private static readonly WardenArm.ArmOrder[] Routine =
    [
        WardenArm.ArmOrder.Punch,
        WardenArm.ArmOrder.Toss,
        WardenArm.ArmOrder.Punch,
        WardenArm.ArmOrder.Toss,
    ];

    /// <summary>What the survivor falls back on once the other hand is gone.</summary>
    private static readonly WardenArm.ArmOrder[] SoloRoutine =
    [
        WardenArm.ArmOrder.PalmSlam,
        WardenArm.ArmOrder.Toss,
        WardenArm.ArmOrder.Grapple,
        WardenArm.ArmOrder.Punch,
        WardenArm.ArmOrder.PalmSlam,
    ];

    private const int ROUTINE_OFFSET = 2;

    /// <summary>
    ///     Ticks between grapples, whichever hand throws them. The grab pins the target in place, so
    ///     without a floor here it stacks with a slam and there is no way out of the pair.
    /// </summary>
    private const int GRAPPLE_COOLDOWN = 600;

    /// <summary>Ticks of boulder conjuring to sit through before it can happen again.</summary>
    private const int CAST_INTERVAL = 330;

    private const int CAST_INTERVAL_PHASE_TWO = 190;

    private const int CAST_COUNT = 2;

    private const int CAST_COUNT_PHASE_TWO = 4;

    /// <summary>Ticks a conjured boulder hangs before it drops.</summary>
    private const int CAST_HANG_TICKS = 52;

    /// <summary>Height above the target the boulders are conjured at, in world pixels.</summary>
    private const float CAST_HEIGHT = 240f;

    /// <summary>Half-width the conjured boulders are spread across, in world pixels.</summary>
    private const float CAST_SPREAD = 190f;

    private const int CAST_DAMAGE = 30;

    /// <summary>
    ///     Ticks the warden holds station before it starts attacking. Counted rather than read off
    ///     <see cref="BossIntroSystem" />, which is client-side and would leave a server swinging
    ///     through the title card.
    /// </summary>
    private const int INTRO_HOLD_TICKS = 150;

    /// <summary>Distance to the hover anchor that counts as having taken station, in world pixels.</summary>
    private const float INTRO_ARRIVE = 90f;

    #endregion

    #region audio

    private static readonly SoundStyle TenseSound = SoundID.Roar with { Volume = 0.85f, Pitch = -0.65f };
    private static readonly SoundStyle SlamSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 1.1f, Pitch = -0.45f };

    #endregion

    public WardenState State
    {
        get => (WardenState)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    /// <summary>World X the warden commits to for the length of a tense and slam.</summary>
    private ref float LockedX => ref NPC.ai[2];

    /// <summary><see cref="NPC.ai" /> slot holding <see cref="ArmClock" />, read by <see cref="WardenArm" />.</summary>
    public const int ARM_CLOCK_SLOT = 3;

    /// <summary>
    ///     Free-running tick count, never reset. Both arms derive their idle orbit from it, so it
    ///     has to be in the synced block rather than <see cref="NPC.localAI" />.
    /// </summary>
    private ref float ArmClock => ref NPC.ai[ARM_CLOCK_SLOT];

    private Player Target => Main.player[NPC.target];

    private float LifeFraction => NPC.lifeMax <= 0 ? 1f : NPC.life / (float)NPC.lifeMax;

    /// <summary>1 until half life is gone, 2 after. The second phase slams and conjures harder.</summary>
    private int Phase => LifeFraction > PHASE_TWO_LIFE ? 1 : 2;

    /// <summary>True once either hand is gone. The survivor switches to <see cref="SoloRoutine" />.</summary>
    private bool Solo => leftArm < 0 || rightArm < 0;

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

    /// <summary>Ticks until the next boulder conjuring.</summary>
    private int castCooldown = CAST_INTERVAL;

    /// <summary>Ticks since the warden arrived, capped at <see cref="INTRO_HOLD_TICKS" />.</summary>
    private int introTicks;

    /// <summary>Pixels of positional jitter on the sprite. Cosmetic, so it is never synced.</summary>
    private float drawShake;

    private Vector2 squish = Vector2.One;

    private int baseDamage;

    /// <summary>Client-side; the intro is cosmetic and is never synced.</summary>
    private bool introStarted;

    private bool introSettled;

    private const int INTRO_TICKS = 60;

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

        NPCID.Sets.TrailCacheLength[Type] = 8;
        NPCID.Sets.TrailingMode[Type] = 2;
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

        if (!Main.dedServ)
            Music = Assets.Music.WoodenWarden.Slot;

        NPC.HitSound = Assets.SFX.NPC.WoodHit2.Asset with
        {
            Volume = 1.4f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };

        NPC.DeathSound = new SoundStyle($"{Assets.SFX.NPC.WardenDeath.KEY}" + Main.rand.Next(1, 4))
        {
            Volume = 0.9f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        // Read once: NPC.damage is rewritten every tick from baseDamage, and NPC.life has already
        // been scaled by tModLoader before this runs.
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

    public override void AI()
    {
        if (baseDamage <= 0)
            baseDamage = NPC.damage;

        StartIntro();

        if (introTicks < INTRO_HOLD_TICKS)
            introTicks++;

        SettleIntro();

        // Wrapped rather than left to grow: the arms take a sine of it, and a float that large
        // loses the precision the orbit needs.
        ArmClock = (ArmClock + 1f) % CLOCK_WRAP;

        Retarget();

        switch (State)
        {
            case WardenState.Hovering:
                UpdateHover();
                break;

            case WardenState.Tensing:
                UpdateTense();
                break;

            case WardenState.Slamming:
                UpdateSlam();
                break;

            case WardenState.Recovering:
                UpdateRecover();
                break;
        }

        DriveArms();

        StateTimer++;
    }

    /// <summary>Re-picks whenever the current target is gone, and periodically otherwise.</summary>
    private void Retarget()
    {
        if (NPC.HasValidTarget && !Target.dead && StateTimer % RETARGET_INTERVAL != 0f)
            return;

        NPC.TargetClosest(false);
    }

    private void SetState(WardenState state)
    {
        State = state;
        StateTimer = -1;

        NPC.netUpdate = true;
    }

    #region states

    private void UpdateHover()
    {
        NPC.damage = baseDamage;
        squish = Vector2.Lerp(squish, Vector2.One, 0.15f);
        drawShake = 0f;

        Vector2 anchor = HoverAnchor();

        Seek(anchor, HOVER_ACCELERATION, HOVER_MAX_SPEED, HOVER_DAMPING);

        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.012f, 0.07f);

        if (introTicks < INTRO_HOLD_TICKS)
            return;

        TryCast();

        int dwell = Main.masterMode ? HOVER_TICKS_MASTER : HOVER_TICKS;

        if (Phase >= 2)
            dwell = (int)(dwell * PHASE_TWO_HOVER);

        if (StateTimer < dwell || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (Target is not { active: true, dead: false } target || target.Center.Y < NPC.Bottom.Y)
            return;

        LockedX = target.Center.X;

        SetState(WardenState.Tensing);
    }

    /// <summary>Conjures a row of boulders over the target and lets them hang before they drop.</summary>
    private void TryCast()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (--castCooldown > 0)
            return;

        castCooldown = Phase >= 2 ? CAST_INTERVAL_PHASE_TWO : CAST_INTERVAL;

        if (Target is not { active: true, dead: false } target)
            return;

        int count = Phase >= 2 ? CAST_COUNT_PHASE_TWO : CAST_COUNT;
        int damage = Main.masterMode ? (int)(CAST_DAMAGE * 1.5f) : Main.expertMode ? (int)(CAST_DAMAGE * 1.2f) : CAST_DAMAGE;

        for (int i = 0; i < count; i++)
        {
            // Spread evenly rather than randomly, so the gap between them is a real dodge rather
            // than a roll, and the same on every client without syncing anything.
            float slot = count == 1 ? 0.5f : i / (float)(count - 1);

            var over = new Vector2(
                target.Center.X + MathHelper.Lerp(-CAST_SPREAD, CAST_SPREAD, slot),
                target.Center.Y - CAST_HEIGHT
            );

            WardenBoulder.Cast(NPC, over, damage, CAST_HANG_TICKS + i * 6);
        }

        NPC.netUpdate = true;
    }

    /// <summary>A point above the target, offset along a 1:2 figure so the hover never sits still.</summary>
    private Vector2 HoverAnchor()
    {
        Vector2 centre = Target is { active: true }
            ? new Vector2(Target.Center.X, Target.Center.Y - HOVER_HEIGHT)
            : NPC.Center;

        float t = ArmClock / DANCE_PERIOD * MathHelper.TwoPi;

        return centre + new Vector2(MathF.Cos(t) * DANCE_SWAY, MathF.Sin(t * 2f) * DANCE_BOB);
    }

    private void UpdateTense()
    {
        NPC.damage = baseDamage;

        float progress = Math.Clamp(StateTimer / TENSE_TICKS, 0f, 1f);

        // Horizontal only: the column is locked, and tracking the target's height here would undo
        // the tell.
        float toColumn = LockedX - NPC.Center.X;

        NPC.velocity.X = Math.Clamp(toColumn * 0.08f, -6f, 6f);
        NPC.velocity.Y = -TENSE_RISE;

        NPC.rotation = NPC.rotation.AngleLerp(0f, 0.2f);

        // Winds up rather than jitters at a constant rate, so the drop reads as the release.
        drawShake = TENSE_SHAKE * progress;
        squish = new Vector2(1f + 0.14f * progress, 1f - 0.12f * progress);

        if (StateTimer == 0)
            SoundEngine.PlaySound(TenseSound, NPC.Center);

        if (StateTimer < TENSE_TICKS || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (Math.Abs(toColumn) > SLAM_ALIGN && StateTimer < TENSE_TICKS * 2)
            return;

        SetState(WardenState.Slamming);
    }

    private void UpdateSlam()
    {
        NPC.damage = baseDamage * 2;

        drawShake = 0f;
        squish = Vector2.Lerp(squish, new Vector2(0.86f, 1.18f), 0.2f);

        NPC.velocity.X = Math.Clamp(LockedX - NPC.Center.X, -4f, 4f);
        NPC.velocity.Y = Math.Min(NPC.velocity.Y + SLAM_ACCELERATION, SLAM_MAX_SPEED);

        NPC.rotation = 0f;

        // Manual, because the descent is scripted and vanilla collision would fight it. fallThrough
        // keeps platforms out of it, so the slam only stops on real ground.
        Vector2 resolved = Collision.TileCollision(NPC.position, NPC.velocity, NPC.width, NPC.height, fallThrough: true, fall2: true);

        if (resolved.Y < NPC.velocity.Y)
        {
            NPC.velocity = resolved;

            Slam();

            SetState(WardenState.Recovering);

            return;
        }

        if (StateTimer >= SLAM_TIMEOUT && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(WardenState.Recovering);
    }

    private void UpdateRecover()
    {
        // Still solid to walk into, but no longer the slam's damage.
        NPC.damage = baseDamage;

        squish = Vector2.Lerp(squish, Vector2.One, 0.09f);
        drawShake = 0f;

        if (StateTimer < RECOVER_TICKS * 0.4f)
        {
            NPC.velocity *= 0.8f;
        }
        else
        {
            Seek(HoverAnchor(), HOVER_ACCELERATION * 0.7f, HOVER_MAX_SPEED, HOVER_DAMPING);

            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.012f, 0.07f);
        }

        if (StateTimer >= RECOVER_TICKS && Main.netMode != NetmodeID.MultiplayerClient)
            SetState(WardenState.Hovering);
    }

    /// <summary>Accelerates toward <paramref name="anchor" /> and damps, rather than assigning a velocity outright.</summary>
    private void Seek(Vector2 anchor, float acceleration, float maxSpeed, float damping)
    {
        Vector2 toAnchor = anchor - NPC.Center;

        NPC.velocity += toAnchor.SafeNormalize(Vector2.Zero) * acceleration;
        NPC.velocity *= damping;

        if (NPC.velocity.LengthSquared() > maxSpeed * maxSpeed)
            NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;

        // Kills the orbit an accelerating seek would otherwise settle into once it is close.
        if (toAnchor.LengthSquared() < 24f * 24f)
            NPC.velocity *= 0.86f;
    }

    #endregion

    #region impact

    private void Slam()
    {
        var impact = new Vector2(NPC.Center.X, NPC.Bottom.Y);

        SoundEngine.PlaySound(SlamSound, impact);

        // Kept near the reach of the band that hurts: a front that runs on past the damage reads as
        // a hit the player should have taken and did not.
        TileShockwaveSystem.Spawn(new BlockWave(impact, 300f, lifetime: 40)
        {
            Amplitude = 24f,
            Thickness = 80f,
            Reach = 148f,
            Cycles = 3f,
            Blend = 0.6f,
        });

        HurtBand(impact);

        if (Main.dedServ)
            return;

        CameraSystem.shake = Math.Max(CameraSystem.shake, 20);

        Rubble(impact);
    }

    /// <summary>
    ///     The slam's own damage, separate from contact. Each client hurts only itself; Player.Hurt
    ///     is not safe to call for a remote player, and a dedicated server has no local player.
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

    /// <summary>Material-correct debris via <see cref="WorldGen.KillTile" /> with <c>fail: true</c>; the world is left intact.</summary>
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

            leftArm = SpawnArm(-1);
            rightArm = SpawnArm(1);
        }

        ResolveArms();

        if (grappleCooldown > 0)
            grappleCooldown--;

        // The hands hold station through the entrance for the same reason the body does.
        if (introTicks < INTRO_HOLD_TICKS)
            return;

        TickArm(leftArm, ref leftCursor);
        TickArm(rightArm, ref rightCursor);

        TryClasp();
    }

    /// <summary>
    ///     Drops indices that no longer point at one of our arms. A client never runs this, so the
    ///     cached indices are only ever meaningful on the server.
    /// </summary>
    private void ResolveArms()
    {
        if (!IsOurArm(leftArm, -1))
            leftArm = -1;

        if (!IsOurArm(rightArm, 1))
            rightArm = -1;
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

    private void TickArm(int index, ref int cursor)
    {
        if (index < 0 || Main.npc[index].ModNPC is not WardenArm arm || !arm.AwaitingOrder)
            return;

        WardenArm.ArmOrder[] routine = Solo ? SoloRoutine : Routine;

        WardenArm.ArmOrder order = routine[cursor % routine.Length];

        cursor++;
        claspCountdown--;

        // Skipped rather than delayed: the cursor has already moved on, so a grapple that lands on
        // cooldown becomes the next order in the routine instead of stalling the hand.
        if (order == WardenArm.ArmOrder.Grapple && !CanGrapple())
            order = WardenArm.ArmOrder.Punch;

        if (order == WardenArm.ArmOrder.Grapple)
            grappleCooldown = GRAPPLE_COOLDOWN;

        Issue(Main.npc[index], order);
    }

    /// <summary>
    ///     A grapple pins the target, so it is only allowed off cooldown, while the body is hovering
    ///     rather than committed to a slam, and once the warden is down to one hand.
    /// </summary>
    private bool CanGrapple() => grappleCooldown <= 0 && Solo && State == WardenState.Hovering;

    /// <summary>Sends both arms at the target at once. Only fires when neither is mid-order.</summary>
    private void TryClasp()
    {
        if (claspCountdown > 0 || leftArm < 0 || rightArm < 0)
            return;

        if (Main.npc[leftArm].ModNPC is not WardenArm left || !left.AwaitingOrder)
            return;

        if (Main.npc[rightArm].ModNPC is not WardenArm right || !right.AwaitingOrder)
            return;

        claspCountdown = CLASP_INTERVAL;

        Issue(Main.npc[leftArm], WardenArm.ArmOrder.Clasp);
        Issue(Main.npc[rightArm], WardenArm.ArmOrder.Clasp);
    }

    private static void Issue(NPC arm, WardenArm.ArmOrder order)
    {
        arm.ai[0] = (int)order;
        arm.ai[1] = 0f;

        arm.netUpdate = true;
    }

    private int SpawnArm(int side)
    {
        int index = NPC.NewNPC(
            NPC.GetSource_FromAI(),
            (int)NPC.Center.X + 120 * side,
            (int)NPC.Center.Y,
            ModContent.NPCType<WardenArm>(),
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

    private void StartIntro()
    {
        if (Main.dedServ || introStarted)
            return;

        introStarted = true;

        BossIntroSystem.Play(CreateIntro());
    }

    /// <summary>
    ///     Ends the entrance once the warden has arrived. For a hovering boss that is taking
    ///     station rather than landing; <see cref="INTRO_HOLD_TICKS" /> is the backstop for a
    ///     target that keeps running and never lets it settle.
    /// </summary>
    private void SettleIntro()
    {
        if (Main.dedServ || introSettled || !BossIntroSystem.IsPlaying)
            return;

        bool onStation = Vector2.DistanceSquared(NPC.Center, HoverAnchor()) <= INTRO_ARRIVE * INTRO_ARRIVE;

        if (!onStation && introTicks < INTRO_HOLD_TICKS)
            return;

        introSettled = true;

        BossIntroSystem.Settle();
    }

    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<WoodenWardenBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(WoodenWardenBoss)}.Epithet"),

        TrackTitle = "Wooden Warden",
        TrackArtist = "wymsical",

        Accent = Color.YellowGreen,

        RevealTick = INTRO_TICKS,
    };

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value;

        Vector2 position = NPC.Center + new Vector2(0f, NPC.gfxOffY);

        if (drawShake > 0f)
            position += Main.rand.NextVector2Circular(drawShake, drawShake * 0.6f);

        spriteBatch.Draw(
            texture,
            position - screenPos,
            null,
            NPC.GetAlpha(drawColor),
            NPC.rotation,
            texture.Size() * 0.5f,
            squish * NPC.scale,
            SpriteEffects.None,
            0f
        );

        return false;
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ || NPC.life <= 0)
            return;

        for (int i = 0; i < 4; i++)
        {
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture,
                hit.HitDirection * 1.6f, -1f, Scale: 1.2f);
        }
    }

    /// <summary>
    ///     Contact is tested against an inset box rather than the full 200x184 frame, whose corners
    ///     are foliage. Without this a slam connects from well outside the body.
    /// </summary>
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
    {
        Rectangle body = NPC.Hitbox;

        body.Inflate(-BODY_INSET_X, -BODY_INSET_Y);

        return body.Intersects(target.Hitbox);
    }

    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
    {
        // The slam's own damage is dealt by HurtBand; this only shapes the contact hit.
        if (State == WardenState.Slamming)
            modifiers.Knockback += 2f;
        else
            modifiers.Knockback *= 0.5f;
    }

    public override bool PreKill()
    {
        //DownedBossSystem.downedWarden = true;
        return base.PreKill();
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

    /// <summary>Arm bookkeeping kept outside <see cref="NPC.ai" />. Only the server acts on any of it.</summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((short)leftArm);
        writer.Write((short)rightArm);
        writer.Write(armsSpawned);

        writer.Write((byte)(leftCursor % SoloRoutine.Length));
        writer.Write((byte)(rightCursor % SoloRoutine.Length));

        writer.Write((byte)Math.Clamp(claspCountdown, 0, byte.MaxValue));
        writer.Write((short)Math.Clamp(grappleCooldown, 0, short.MaxValue));
        writer.Write((short)Math.Clamp(castCooldown, 0, short.MaxValue));

        writer.Write((short)Math.Clamp(introTicks, 0, short.MaxValue));
        writer.Write((short)Math.Clamp(baseDamage, 0, short.MaxValue));
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        leftArm = reader.ReadInt16();
        rightArm = reader.ReadInt16();
        armsSpawned = reader.ReadBoolean();

        leftCursor = reader.ReadByte();
        rightCursor = reader.ReadByte();

        claspCountdown = reader.ReadByte();
        grappleCooldown = reader.ReadInt16();
        castCooldown = reader.ReadInt16();

        introTicks = reader.ReadInt16();
        baseDamage = reader.ReadInt16();
    }

    #endregion
}
