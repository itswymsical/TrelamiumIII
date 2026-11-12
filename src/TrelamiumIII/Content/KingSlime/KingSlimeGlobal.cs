using System.Collections.Generic;
using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues.Types;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.KingSlime;

public class KingSlimeRework : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
    {
        return entity.type == NPCID.KingSlime;
    }

    private static KingSlimeRework Of(NPC npc) => npc.GetGlobalNPC<KingSlimeRework>();

    private AIState GetState(NPC npc)
    {
        return (AIState)npc.ai[0];
    }

    private void SetState(NPC npc, AIState value)
    {
        npc.ai[0] = (float)value;
    }

    #region Pattern Management

    private enum PatternType
    {
        Phase1,
        Phase2,
        Phase3,
    }

    private struct AttackPattern
    {
        public AIState[] States;
        public int[] Durations;
        public int[] Repetitions;

        public AttackPattern(AIState[] states, int[] durations, int[] repetitions)
        {
            States = states;
            Durations = durations;
            Repetitions = repetitions;
        }
    }

    private static readonly Dictionary<PatternType, AttackPattern> AttackPatterns = new()
    {
        [PatternType.Phase1] = new AttackPattern(
            new[] { AIState.Strolling, AIState.Jumping, AIState.Strolling, AIState.GroundPound, AIState.Strolling, AIState.Jumping },
            new[] { 180, -1, 120, -1, 240, -1 },
            new[] { 1, 3, 1, 1, 1, 2 }
        ),

        [PatternType.Phase2] = new AttackPattern(
            new[] { AIState.Strolling, AIState.Jumping, AIState.ConsumingSlimes, AIState.GroundPound, AIState.Strolling, AIState.BounceHouse, AIState.ConsumingSlimes },
            new[] { 150, -1, -1, -1, 180, -1, 120 },
            new[] { 1, 4, 1, 1, 1, 1, 1 }
        ),

        [PatternType.Phase3] = new AttackPattern(
            new[] { AIState.Jumping, AIState.BounceHouse, AIState.ConsumingSlimes, AIState.GroundPound, AIState.BounceHouse },
            new[] { -1, -1, -1, -1, -1 },
            new[] { 5, 1, 1, 4, 1 }
        )
    };

    /// <summary>Server-side decision state; clients never read it.</summary>
    private PatternData pattern;

    private struct PatternData
    {
        public PatternType currentPattern;
        public int patternStep;
        public int currentRepetition;
        public int patternTimer;
        public bool patternOverride;
    }

    private PatternData GetPatternData(NPC npc) => Of(npc).pattern;

    private void SetPatternData(NPC npc, PatternData data) => Of(npc).pattern = data;

    #endregion

    private NPCExtraData extra = NPCExtraData.Initial;

    private struct NPCExtraData
    {
        public Vector2 squishScale;
        public bool wasGliding;
        public int targetStrolls;
        public float rotationSway;
        public float wobbleTimer;
        public bool isWobbling;
        public int groundPoundCount;
        public int targetGroundPounds;
        public int baseDefense;
        public float lastConsumeTime;
        public float lineOfSightTimer;
        public float teleportTimer;
        public TeleportPhase teleportPhase;
        public Vector2 teleportDestination;
        public float teleportScaleMultiplier;
        public Vector2 lastPosition;
        public Vector2 teleportStartPos;
        public float stuckTimer;
        public int bounceHouseSlams;
        public int targetBounceSlams;
        public bool deathAnimationStarted;
        public float deathScale;
        public int deathAnimationPhase;

        /// <summary>Set by the authority right before the kill, so <see cref="CheckDead" /> lets it through.</summary>
        public bool deathAnimationFinished;

        /// <summary>Local, unsynced: each side plays the death cue and the finale burst once.</summary>
        public bool deathCuePlayed;

        public bool finaleFired;
        public bool introStarted;
        public float introScaleMultiplier;
        public int feedings;
        public int slimesEaten;

        public static NPCExtraData Initial => new()
        {
            squishScale = Vector2.One,
            targetStrolls = 3,
            targetGroundPounds = 1,
            baseDefense = 18,
            lastConsumeTime = -CONSUME_COOLDOWN,
            teleportPhase = TeleportPhase.Prepare,
            teleportScaleMultiplier = 1f,
            targetBounceSlams = 3,
            deathScale = 1f,
            introScaleMultiplier = 1f,
        };
    }

    private NPCExtraData GetExtraData(NPC npc) => Of(npc).extra;

    private void SetExtraData(NPC npc, NPCExtraData data) => Of(npc).extra = data;

    /// <summary>The per-attack counters clients need to run the current state; pattern choice stays on the server.</summary>
    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        var data = GetExtraData(npc);

        bitWriter.WriteBit(data.teleportPhase == TeleportPhase.Execute);
        bitWriter.WriteBit(data.deathAnimationStarted);

        binaryWriter.Write((byte)data.deathAnimationPhase);
        binaryWriter.Write(data.deathScale);
        binaryWriter.Write((byte)Math.Clamp(data.groundPoundCount, 0, byte.MaxValue));
        binaryWriter.Write((byte)Math.Clamp(data.targetGroundPounds, 0, byte.MaxValue));
        binaryWriter.Write((byte)Math.Clamp(data.bounceHouseSlams, 0, byte.MaxValue));
        binaryWriter.Write((byte)Math.Clamp(data.targetBounceSlams, 0, byte.MaxValue));
    }

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
    {
        var data = GetExtraData(npc);

        data.teleportPhase = bitReader.ReadBit() ? TeleportPhase.Execute : TeleportPhase.Prepare;
        data.deathAnimationStarted = bitReader.ReadBit();

        data.deathAnimationPhase = binaryReader.ReadByte();
        data.deathScale = binaryReader.ReadSingle();
        data.groundPoundCount = binaryReader.ReadByte();
        data.targetGroundPounds = binaryReader.ReadByte();
        data.bounceHouseSlams = binaryReader.ReadByte();
        data.targetBounceSlams = binaryReader.ReadByte();

        SetExtraData(npc, data);
    }

    /// <summary>Scale from health alone, before any teleport or intro multiplier.</summary>
    private static float GetHealthScale(NPC npc)
        => Math.Max(MathHelper.Lerp(0.65f, 1.55f, (float)npc.life / npc.lifeMax), 0.45f);

    private void UpdateScale(NPC npc)
    {
        var newScale = GetHealthScale(npc);

        var extraData = GetExtraData(npc);
        if (GetState(npc) == AIState.Teleporting)
            newScale *= extraData.teleportScaleMultiplier;
        else if (GetState(npc) == AIState.Intro)
            newScale *= extraData.introScaleMultiplier;

        npc.scale = newScale;
        UpdateDefenseBasedOnScale(npc);
        UpdateHitbox(npc);
    }

    private void UpdateDefenseBasedOnScale(NPC npc)
    {
        const int MIN_DEFENSE = 12;
        const int MAX_DEFENSE = 25;

        var extraData = GetExtraData(npc);
        var normalizedScale = (npc.scale - 0.45f) / (1.35f - 0.45f);
        normalizedScale = MathHelper.Clamp(normalizedScale, 0f, 1f);

        var defenseFactor = normalizedScale * normalizedScale * (3 - 2 * normalizedScale);
        var calculatedDefense = (int)(MIN_DEFENSE + (extraData.baseDefense - MIN_DEFENSE) * defenseFactor);

        npc.defense = (int)MathHelper.Clamp(calculatedDefense, MIN_DEFENSE, MAX_DEFENSE);
    }

    private void UpdateHitbox(NPC npc)
    {
        var center = npc.Center;
        var bottom = npc.Bottom;

        npc.width = (int)(BASE_WIDTH * npc.scale);
        npc.height = (int)(BASE_HEIGHT * npc.scale);

        // The intro and teleport scale it in and out, so it resizes on its feet rather than sinking into the floor.
        if (GetState(npc) is AIState.Intro or AIState.Teleporting)
            npc.Bottom = bottom;
        else
            npc.position = center - new Vector2(npc.width / 2, npc.height / 2);
    }

    private float GetScaledJumpHeight(NPC npc)
    {
        var baseHeight = -16f;
        var scaleMultiplier = MathHelper.Lerp(1.4f, 0.8f, npc.scale / 1.35f);
        return baseHeight * scaleMultiplier;
    }

    /// <summary>Horizontal speed factor: <see cref="MIN_SPEED_MULTIPLIER" /> at full health, rising to 1 as health runs out.</summary>
    private static float GetSpeedMultiplier(NPC npc)
        => MathHelper.Lerp(1f, MIN_SPEED_MULTIPLIER, (float)npc.life / npc.lifeMax);

    private ref float GetTimer(NPC npc) => ref npc.ai[1];

    internal enum AIState
    {
        Strolling,
        Jumping,
        GroundPound,
        Teleporting,
        Despawning,
        ConsumingSlimes,
        BounceHouse,
        DeathAnimation,
        Intro
    }

    internal enum TeleportPhase
    {
        Prepare,
        Execute
    }

    private ref float GetJumpPhase(NPC npc) => ref npc.ai[2];
    private ref float GetStrollCount(NPC npc) => ref npc.ai[3];

    private const int BASE_WIDTH = 158;
    private const int BASE_HEIGHT = 106;

    /// <summary>Horizontal speed factor at full health.</summary>
    private const float MIN_SPEED_MULTIPLIER = 0.55f;

    private const float CONSUME_DETECTION_RANGE = 200f;

    /// <summary>Health restored per slime eaten, as a fraction of max health.</summary>
    private const float CONSUME_HEAL_FRACTION = 0.012f;
    private const int CONSUME_MAX_SLIMES = 3;
    private const int CONSUME_MAX_FEEDINGS = 2;
    private const float CONSUME_MAX_TICKS = 180f;

    private const float CONSUME_COOLDOWN = 600f;

    private const float TELEPORT_COOLDOWN = 240f;
    private const float LINE_OF_SIGHT_TIMEOUT = 240f;

    /// <summary>A teleport lands at least the minimum from every player, and up to the maximum to either side of its target.</summary>
    private const int TELEPORT_MIN_TILES = 24;
    private const int TELEPORT_MAX_TILES = 40;

    /// <summary>Tiles above and below the player the landing search starts from.</summary>
    private const int TELEPORT_SEARCH_HEIGHT = 12;
    private const int TELEPORT_ATTEMPTS = 60;

    private const int INTRO_REVEAL_TICKS = 75;
    private const int INTRO_HOLD_TICKS = 110;
    private const int INTRO_MIN_TILES = 26;
    private const int INTRO_MAX_TILES = 34;

    private const float STUCK_TIMEOUT = 180f;
    private const float MIN_MOVEMENT_THRESHOLD = 16f;

    private const float BOUNCE_HOUSE_JUMP_HEIGHT = -16f;
    private const float BOUNCE_HOUSE_SLAM_SPEED = 28f;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[NPCID.KingSlime] = 6;

        NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new()
        {
            Position = new Vector2(0f, -16f),
        };
    }

    public override void SetDefaults(NPC npc)
    {
        npc.damage = 12;
        npc.defense = 18;
        npc.lifeMax = 1400;
        npc.width = 158;
        npc.height = 106;
        npc.alpha = 30;
        npc.aiStyle = -1;
        npc.value = Item.buyPrice(gold: 4);
        npc.boss = true;
        npc.lavaImmune = true;
        npc.knockBackResist = 0f;

        npc.HitSound = Assets.SFX.NPC.KingSlime.KingSlimeHit.Asset with { PitchVariance = 0.3f };
        npc.DeathSound = Assets.SFX.NPC.KingSlime.KingSlimeDeath.Asset with { PitchVariance = 0.2f };
    }

    public override void AI(NPC npc)
    {        
        //if (!Main.dedServ)
        //    MusicLoader.GetMusicSlot($"{MUSIC_DIRECTORY}GelatinousJoust");

        var target = Main.player[npc.target];

        if (!target.active || target.dead)
        {
            npc.TargetClosest();
            target = Main.player[npc.target];
            if (!target.active || target.dead)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SetState(npc, AIState.Despawning);

                ApplyStateFlags(npc);
                return;
            }
        }

        ref float timer = ref GetTimer(npc);
        timer++;

        var extraData = GetExtraData(npc);

        StartIntro(npc);

        if (GetState(npc) != AIState.DeathAnimation)
        {
            UpdateScale(npc);
        }
        else
        {
            npc.scale = extraData.deathScale;
            UpdateHitbox(npc);
        }

        HandleSquishScale(npc);
        HandleRotation(npc);
        HandleDustTrail(npc);
        HandleSlimeTrail(npc);

        // Clients run the current state from synced ai[] and extra AI; only the authority picks the next one.
        if (GetState(npc) is not (AIState.DeathAnimation or AIState.Intro) && Main.netMode != NetmodeID.MultiplayerClient)
        {
            UpdateLOS(npc, target);
            HandlePatternTeleport(npc, target);
            HandlePatternConsume(npc, target);
            UpdatePatternFlow(npc, target);
        }

        switch (GetState(npc))
        {
            case AIState.Intro:
                DoIntro(npc, target);
                break;
            case AIState.Strolling:
                DoStrolling(npc, target);
                break;
            case AIState.Jumping:
                DoJumping(npc, target);
                break;
            case AIState.GroundPound:
                DoGroundPound(npc, target);
                break;
            case AIState.Teleporting:
                DoTeleporting(npc, target);
                break;
            case AIState.ConsumingSlimes:
                DoConsumingSlimes(npc, target);
                break;
            case AIState.BounceHouse:
                DoBounceHouse(npc, target);
                break;
            case AIState.DeathAnimation:
                DoDeathAnimation(npc, target);
                break;
        }

        ApplyStateFlags(npc);

        NPCUtils.SlopedCollision(npc);
        NPCUtils.CheckPlatform(npc, target);
    }

    /// <summary>
    ///     Sets <see cref="NPC.hide" /> and <see cref="NPC.dontTakeDamage" /> from the synced state every tick, on every side.
    ///     Neither is in the NPC packet, so a latched value outlives the state on a client that missed its exit.
    /// </summary>
    private void ApplyStateFlags(NPC npc)
    {
        var timer = GetTimer(npc);

        var (hidden, invulnerable) = GetState(npc) switch
        {
            AIState.Intro => (timer < INTRO_REVEAL_TICKS, true),
            AIState.Teleporting when IsTeleportHidden(GetExtraData(npc).teleportPhase, timer) => (true, true),
            AIState.DeathAnimation => (false, true),
            _ => (false, false),
        };

        npc.hide = hidden;
        npc.dontTakeDamage = invulnerable;
    }

    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        if (npc.type == NPCID.KingSlime)
        {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GelatinousBlaster>(), 2));
        }
    }

    #region Pattern System Implementation

    private PatternType GetCurrentPhase(NPC npc)
    {
        var healthPercent = (float)npc.life / npc.lifeMax;

        PatternType phase;
        if (healthPercent <= 0.25f)
            phase = PatternType.Phase3;
        else if (healthPercent <= 0.60f)
            phase = PatternType.Phase2;
        else
            phase = PatternType.Phase1;

        return phase;
    }

    private void UpdatePatternFlow(NPC npc, Player target)
    {
        var patternData = GetPatternData(npc);

        if (patternData.patternOverride)
        {
            patternData.patternTimer++;
            SetPatternData(npc, patternData);
            return;
        }

        var targetPhase = GetCurrentPhase(npc);
        if (patternData.currentPattern != targetPhase)
        {
            TransitionToPhase(npc, targetPhase);
            return;
        }

        var pattern = AttackPatterns[patternData.currentPattern];
        var currentPatternState = pattern.States[patternData.patternStep];

        var shouldAdvance = false;

        if (pattern.Durations[patternData.patternStep] > 0)
        {
            if (patternData.patternTimer >= pattern.Durations[patternData.patternStep])
                shouldAdvance = true;
        }
        else
        {
            if (IsCurrentAttackComplete(npc))
                shouldAdvance = true;
        }

        if (shouldAdvance)
        {
            patternData.currentRepetition++;

            if (patternData.currentRepetition >= pattern.Repetitions[patternData.patternStep])
            {
                AdvancePatternStep(npc);
                return; // AdvancePatternStep handles saving data
            }
            else
            {
                StartPatternAttack(npc, currentPatternState, target);
            }

            patternData.patternTimer = 0;
        }
        else
        {
            patternData.patternTimer++;
        }

        SetPatternData(npc, patternData);
    }

    private void TransitionToPhase(NPC npc, PatternType newPhase)
    {
        var patternData = GetPatternData(npc);
        patternData.currentPattern = newPhase;
        patternData.patternStep = 0;
        patternData.currentRepetition = 0;
        patternData.patternTimer = 0;
        SetPatternData(npc, patternData);

        var pattern = AttackPatterns[patternData.currentPattern];
        StartPatternAttack(npc, pattern.States[0], Main.player[npc.target]);
    }

    private void AdvancePatternStep(NPC npc)
    {
        var patternData = GetPatternData(npc);
        var pattern = AttackPatterns[patternData.currentPattern];

        patternData.patternStep++;
        patternData.currentRepetition = 0;

        if (patternData.patternStep >= pattern.States.Length)
        {
            patternData.patternStep = 0;
        }

        SetPatternData(npc, patternData);
        StartPatternAttack(npc, pattern.States[patternData.patternStep], Main.player[npc.target]);
    }

    private void StartPatternAttack(NPC npc, AIState newState, Player target)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        SetState(npc, newState);
        GetTimer(npc) = 0;

        var patternData = GetPatternData(npc);
        var extraData = GetExtraData(npc);
        var pattern = AttackPatterns[patternData.currentPattern];

        switch (newState)
        {
            case AIState.Jumping:
                GetJumpPhase(npc) = 0;
                break;

            case AIState.GroundPound:
                GetJumpPhase(npc) = 0;
                extraData.groundPoundCount = 0;

                var maxReps = pattern.Repetitions[patternData.patternStep];

                if (maxReps >= 3)
                    extraData.targetGroundPounds = 3;
                else if (maxReps >= 2)
                    extraData.targetGroundPounds = 2;
                else
                    extraData.targetGroundPounds = 1;

                SetExtraData(npc, extraData);
                break;

            case AIState.BounceHouse:
                GetJumpPhase(npc) = 0;
                extraData.bounceHouseSlams = 0;
                extraData.targetBounceSlams = Main.rand.Next(3, 6);
                SetExtraData(npc, extraData);
                break;

            case AIState.ConsumingSlimes:
                if (extraData.feedings >= CONSUME_MAX_FEEDINGS || !HasNearbySlimes(npc))
                {
                    AdvancePatternStep(npc);
                    return;
                }

                BeginFeeding(npc);
                break;

            case AIState.Strolling:
                GetStrollCount(npc) = 0;
                extraData.targetStrolls = pattern.Durations[patternData.patternStep] > 0 ? 1 : 3;
                SetExtraData(npc, extraData);
                break;
        }

        npc.netUpdate = true;
    }

    private bool IsCurrentAttackComplete(NPC npc)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        switch (GetState(npc))
        {
            case AIState.Jumping:
                return GetJumpPhase(npc) == 1 && npc.velocity.Y == 0f && timer > 30;

            case AIState.GroundPound:
                return GetJumpPhase(npc) == 3 && timer >= 90f && extraData.groundPoundCount >= extraData.targetGroundPounds;

            case AIState.BounceHouse:
                return extraData.bounceHouseSlams >= extraData.targetBounceSlams && npc.velocity.Y == 0f && timer > 60;

            case AIState.ConsumingSlimes:
                return IsFeedingDone(npc, timer);

            case AIState.Strolling:
                return GetStrollCount(npc) >= extraData.targetStrolls;

            case AIState.Teleporting:
                return extraData.teleportPhase == TeleportPhase.Prepare && timer >= 25f;

            default:
                return true;
        }
    }

    private void HandlePatternTeleport(NPC npc, Player target)
    {
        var patternData = GetPatternData(npc);
        var extraData = GetExtraData(npc);

        if (patternData.patternOverride || GetState(npc) == AIState.Teleporting || GetState(npc) == AIState.ConsumingSlimes ||
            GetState(npc) == AIState.BounceHouse ||
            GetState(npc) == AIState.GroundPound && GetJumpPhase(npc) != 0 ||
            GetState(npc) == AIState.Jumping && npc.velocity.Y != 0f)
            return;

        var distToPlayer = Vector2.Distance(npc.Center, target.Center);
        var shouldTeleport = false;

        if (distToPlayer > 1000f && extraData.teleportTimer >= TELEPORT_COOLDOWN * 0.8f)
            shouldTeleport = true;

        if (extraData.stuckTimer >= STUCK_TIMEOUT)
            shouldTeleport = true;

        if (extraData.lineOfSightTimer >= LINE_OF_SIGHT_TIMEOUT * 1.5f)
            shouldTeleport = true;

        if (shouldTeleport && BeginTeleport(npc, target))
        {
            patternData.patternOverride = true;
            SetPatternData(npc, patternData);
        }
    }

    private void HandlePatternConsume(NPC npc, Player target)
    {
        var patternData = GetPatternData(npc);
        var extraData = GetExtraData(npc);

        if (patternData.patternOverride || GetState(npc) == AIState.Teleporting || GetState(npc) == AIState.ConsumingSlimes ||
            GetState(npc) == AIState.GroundPound && GetJumpPhase(npc) != 0 ||
            GetState(npc) == AIState.Jumping && npc.velocity.Y != 0f ||
            GetState(npc) == AIState.BounceHouse && GetJumpPhase(npc) != 0)
            return;

        var currentPhase = GetCurrentPhase(npc);
        if (currentPhase == PatternType.Phase1)
            return;

        if (extraData.feedings >= CONSUME_MAX_FEEDINGS || Main.GameUpdateCount - extraData.lastConsumeTime < CONSUME_COOLDOWN)
            return;

        var nearbySlimes = CountNearbySlimes(npc);
        var shouldOverride = false;

        if (nearbySlimes >= 5)
        {
            shouldOverride = true;
        }
        else if (nearbySlimes >= 3)
        {
            var healthPercentage = (float)npc.life / npc.lifeMax;
            if (healthPercentage <= 0.4f)
                shouldOverride = true;
            else if (healthPercentage <= 0.7f && Main.rand.NextBool(3))
                shouldOverride = true;
        }

        if (shouldOverride)
        {
            patternData.patternOverride = true;
            SetPatternData(npc, patternData);
            SetState(npc, AIState.ConsumingSlimes);
            GetTimer(npc) = 0;
            BeginFeeding(npc);
            npc.netUpdate = true;
        }
    }

    private void ResumeBattlePattern(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        var patternData = GetPatternData(npc);
        patternData.patternOverride = false;
        SetPatternData(npc, patternData);

        var pattern = AttackPatterns[patternData.currentPattern];
        StartPatternAttack(npc, pattern.States[patternData.patternStep], Main.player[npc.target]);
    }

    #endregion

    #region State Methods
    /// <summary>
    ///     Holds the slime at 1 life until the death animation has played. The authority starts it; a client's own lethal
    ///     hit only waits for the server's state.
    /// </summary>
    public override bool CheckDead(NPC npc)
    {
        var extraData = GetExtraData(npc);

        if (extraData.deathAnimationFinished)
            return true;

        npc.life = 1;
        npc.dontTakeDamage = true;

        if (extraData.deathAnimationStarted || Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        extraData.deathAnimationStarted = true;
        extraData.deathAnimationPhase = 0;
        extraData.deathScale = npc.scale;
        SetExtraData(npc, extraData);

        SetState(npc, AIState.DeathAnimation);
        GetTimer(npc) = 0;
        npc.netUpdate = true;

        Main.StopSlimeRain();

        return false;
    }

    /// <summary>Ticks the authority waits after the finale before the kill, so lagging clients still play it.</summary>
    private const int DEATH_KILL_DELAY = 10;

    private void DoDeathAnimation(NPC npc, Player target)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        npc.velocity *= 0.92f;

        if (!extraData.deathCuePlayed)
        {
            extraData.deathCuePlayed = true;

            SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.KingSlimeDeath.Asset with
            {
                Volume = 0.8f,
                PitchVariance = 0.1f
            }, npc.Center);
        }

        switch (extraData.deathAnimationPhase)
        {
            case 0:
                if (timer <= 120f)
                {
                    var shrinkProgress = timer / 120f;
                    extraData.deathScale = MathHelper.Lerp(npc.scale, 0.45f, EaseFunction.EaseQuadIn.Ease(shrinkProgress));

                    if (timer % 8 == 0)
                    {
                        for (var i = 0; i < 5; i++)
                        {
                            var dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                            var dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.t_Slime,
                                Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f),
                                150, new Color(86, 162, 255, 150), 1.2f);
                            dust.noGravity = true;
                            dust.velocity *= 0.8f;
                        }
                    }

                    if (timer % 15 == 0)
                        CameraSystem.shake = (int)MathHelper.Lerp(2, 8, shrinkProgress);
                }
                else
                {
                    extraData.deathAnimationPhase = 1;
                    timer = 0;
                }
                break;

            case 1:
                extraData.deathScale = 0.35f;

                if (timer <= 60f)
                {
                    if (timer % 3 == 0)
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            var dustVel = Vector2.One.RotatedBy(MathHelper.ToRadians(i * 45)) * 4f;
                            var dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.t_Slime,
                                dustVel.X, dustVel.Y, 150, new Color(86, 162, 255, 200), 1.5f);
                            dust.noGravity = true;
                        }
                    }

                    if (timer % 5 == 0)
                        CameraSystem.shake = (int)MathHelper.Lerp(3, 12, timer / 60f);
                }
                else
                {
                    extraData.deathAnimationPhase = 2;
                    timer = 0;
                }
                break;

            case 2: // big finish
                if (!extraData.finaleFired)
                {
                    extraData.finaleFired = true;

                    for (var i = 0; i < 150; i++)
                    {
                        var dustVel = Main.rand.NextVector2CircularEdge(12f, 12f);
                        var dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.t_Slime,
                            dustVel.X, dustVel.Y, 150, new Color(86, 162, 255, 150), 2f);
                        dust.noGravity = true;
                        dust.velocity = dustVel;
                    }

                    GelVolley.Radial(npc, count: 12, minSpeed: 7f, maxSpeed: 11f);

                    // The Nerdy Slime comes from vanilla's kill handler, once per world.
                    CameraSystem.shake = 20;

                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlam.Asset with
                    {
                        Volume = 1f,
                        Pitch = -0.3f
                    }, npc.Center);

                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.KingSlimeRoar.Asset with
                    {
                        Volume = 1f,
                        Pitch = -0.25f
                    }, npc.Center);
                }

                if (timer < DEATH_KILL_DELAY || Main.netMode == NetmodeID.MultiplayerClient)
                    break;

                extraData.deathAnimationFinished = true;
                SetExtraData(npc, extraData);

                npc.life = 0;
                NPC.downedSlimeKing = true;
                npc.checkDead();
                return;
        }

        SetExtraData(npc, extraData);
    }

    private void DoStrolling(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var distToPlayer = Vector2.Distance(npc.Center, target.Center);
        float dirToPlayer = Math.Sign(target.Center.X - npc.Center.X);
        var strollDuration = 54f;
        var progress = timer / strollDuration;

        var speedMultiplier = GetSpeedMultiplier(npc);

        var easedProgress = EaseFunction.EaseCubicInOut.Ease(progress);
        var velocityCurve = (float)Math.Sin(easedProgress * Math.PI);
        var targetSpeed = velocityCurve * MathHelper.PiOver2 * 1.25f * speedMultiplier;

        var glideStrength = 0.92f;
        var accelStrength = 0.112f;

        npc.velocity.X = npc.velocity.X * glideStrength + targetSpeed * dirToPlayer * accelStrength;

        var maxVel = 5f * speedMultiplier;
        if (Math.Abs(npc.velocity.X) > maxVel)
            npc.velocity.X = maxVel * Math.Sign(npc.velocity.X);

        if (Math.Abs(npc.velocity.X) > 0.1f)
            npc.spriteDirection = -Math.Sign(npc.velocity.X);

        if (timer >= strollDuration)
        {
            GetStrollCount(npc)++;
            timer = 0;
        }
    }

    private void DoJumping(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var extraData = GetExtraData(npc);

        float dirToPlayer = Math.Sign(target.Center.X - npc.Center.X);
        npc.spriteDirection = -Math.Sign(dirToPlayer);

        if (npc.velocity.Y == 0f)
        {
            npc.velocity.X *= 0.96f;
            if (Math.Abs(npc.velocity.X) < 0.1f)
                npc.velocity.X = 0f;

            if (timer >= 40f)
            {
                npc.netUpdate = true;

                var baseJumpHeight = GetScaledJumpHeight(npc);
                var baseXVelocity = 2.08f * GetSpeedMultiplier(npc);
                var overshoot = Main.rand.NextFloat() < 0.33f ? 1.3f : 1f;
                npc.velocity.Y = baseJumpHeight;
                npc.velocity.X = baseXVelocity * dirToPlayer * overshoot;
                extraData.squishScale = new Vector2(0.8f, 1.2f);
                SetExtraData(npc, extraData);
                GetJumpPhase(npc) = 1;
                timer = 0;
            }
        }
        else
        {
            var speedMultiplier = GetSpeedMultiplier(npc);
            var maxSpeed = 4f * speedMultiplier;
            if (dirToPlayer == 1 && npc.velocity.X < maxSpeed ||
                dirToPlayer == -1 && npc.velocity.X > -maxSpeed)
            {
                npc.velocity.X += 0.15f * dirToPlayer * speedMultiplier;
            }

            if (timer > 30 && npc.velocity.Y > 0)
            {
                // Let pattern system handle transitions
                extraData.isWobbling = true;
                extraData.wobbleTimer = 0f;
                SetExtraData(npc, extraData);
            }
        }
    }

    private void DoGroundPound(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var extraData = GetExtraData(npc);
        float dirToPlayer = Math.Sign(target.Center.X - npc.Center.X);

        switch (GetJumpPhase(npc))
        {
            case 0:
                npc.velocity.X *= 0.5f;
                if (Math.Abs(npc.velocity.X) < 0.1f)
                    npc.velocity.X = 0f;

                float chargeTime;
                if (extraData.targetGroundPounds > 1 && extraData.groundPoundCount > 0)
                    chargeTime = 25f;
                else if (timer < 0)
                    chargeTime = 45f;
                else
                    chargeTime = 60f;

                if (timer >= chargeTime)
                {
                    npc.netUpdate = true;
                    npc.spriteDirection = -Math.Sign(dirToPlayer);
                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlamCharge.Asset with { PitchVariance = 0.2f }, npc.Center);

                    npc.velocity.Y = -14f;
                    npc.velocity.X = 2f * dirToPlayer * GetSpeedMultiplier(npc);

                    for (var i = 0; i < 10; i++)
                    {
                        var dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.t_Slime, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f,
                            150, new Color(86, 162, 255, 100), 1.5f);
                        dust.noGravity = true;
                        dust.velocity *= 0.4f;
                    }

                    GetJumpPhase(npc) = 1;
                    timer = 0;
                }
                break;

            case 1:
                npc.damage = 30;
                var speedMultiplier = GetSpeedMultiplier(npc);
                var maxSpeed = 3f * speedMultiplier;
                if (dirToPlayer == 1 && npc.velocity.X < maxSpeed ||
                    dirToPlayer == -1 && npc.velocity.X > -maxSpeed)
                {
                    npc.velocity.X += 0.1f * dirToPlayer * speedMultiplier;
                }

                if (Math.Abs(npc.velocity.X) > 0.1f)
                    npc.spriteDirection = -Math.Sign(npc.velocity.X);

                if (npc.velocity.Y > 0f)
                {
                    npc.velocity = new Vector2(
                        (target.Center.X > npc.Center.X ? 2f : -2f) * speedMultiplier,
                        12f
                    );
                    npc.spriteDirection = target.Center.X > npc.Center.X ? -1 : 1;
                    GetJumpPhase(npc) = 2;
                    timer = 0;
                }
                break;

            case 2:
                dirToPlayer = Math.Sign(target.Center.X - npc.Center.X);
                if (timer % 10 == 0)
                    npc.spriteDirection = -Math.Sign(dirToPlayer);

                var hitGround = false;
                var tileX = (int)(npc.position.X / 16);
                var tileEndX = (int)((npc.position.X + npc.width) / 16);
                var tileY = (int)((npc.position.Y + npc.height) / 16);

                for (var i = tileX; i <= tileEndX; i++)
                {
                    var tile = Framing.GetTileSafely(i, tileY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        hitGround = true;
                        break;
                    }
                }

                if (hitGround || npc.velocity.Y == 0f)
                {
                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlam.Asset with { PitchVariance = 0.2f }, npc.Center);
                    CameraSystem.shake = 6;

                    for (var i = 0; i < 30; i++)
                    {
                        var dustVel = Vector2.One.RotatedBy(MathHelper.ToRadians(i * 12)) * 8f;
                        var dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.t_Slime, dustVel.X, dustVel.Y,
                            newColor: new Color(86, 162, 255, 100));
                        dust.noGravity = true;
                        dust.scale = 2f;
                    }

                    var distToPlayer = Vector2.Distance(npc.Center, target.Center) / 16f;

                    // The spray reaches further the further away the player is.
                    var (perSide, reachTiles) = distToPlayer switch
                    {
                        <= 20f => (4, 20f),
                        <= 42f => (5, 30f),
                        _ => (6, 40f)
                    };

                    GelVolley.Fan(npc, perSide, near: 3 * 16f, far: reachTiles * 16f, phase: 0f, damage: 3, knockback: 0.5f);

                    if (extraData.targetGroundPounds > 1 && Main.rand.NextBool(2))
                    {
                        var spawnPos = npc.Center + new Vector2(0f, npc.height / 2f);
                        var leftSlime = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X - 20, (int)spawnPos.Y, NPCID.BlueSlime);
                        if (leftSlime < Main.maxNPCs)
                        {
                            Main.npc[leftSlime].velocity = new Vector2(-4f + Main.rand.NextFloat(-1f, 0f),
                                                                       -3f + Main.rand.NextFloat(-1f, 1f));
                            Main.npc[leftSlime].scale = 1.2f;
                        }
                        var rightSlime = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X + 20, (int)spawnPos.Y, NPCID.GreenSlime);
                        if (rightSlime < Main.maxNPCs)
                        {
                            Main.npc[rightSlime].scale = 1.2f;
                        }
                    }
                    GetJumpPhase(npc) = 3;
                    timer = 0;
                    extraData.isWobbling = true;
                    extraData.wobbleTimer = 0f;
                    SetExtraData(npc, extraData);
                }
                else
                {
                    var timeInPhase = timer;
                    var accelCurve = MathHelper.Clamp(timeInPhase / 20f, 0.1f, 1f);
                    var baseAccel = 0.25f;
                    var currentAccel = baseAccel + accelCurve * 0.4f;

                    npc.velocity.Y += currentAccel;

                    if (npc.velocity.Y > 14f)
                        npc.velocity.Y = 14f;

                    if (Main.rand.NextBool(3))
                    {
                        var dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.t_Slime, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f,
                            150, new Color(86, 162, 255, 100), 1.5f);
                        dust.noGravity = true;
                        dust.velocity *= 0.4f;
                    }
                }
                break;

            case 3:
                npc.damage = 12;
                npc.velocity *= 0.8f;

                var recoveryTime = extraData.targetGroundPounds > 1 ? 45f : 90f;

                if (timer >= recoveryTime)
                {
                    extraData.groundPoundCount++;

                    if (extraData.groundPoundCount < extraData.targetGroundPounds)
                    {
                        GetJumpPhase(npc) = 0;
                        timer = -15f;
                        extraData.isWobbling = false;
                    }
                    else
                    {
                        extraData.isWobbling = false;
                    }
                    SetExtraData(npc, extraData);
                }
                break;
        }
    }

    private void DoBounceHouse(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var extraData = GetExtraData(npc);

        switch (GetJumpPhase(npc))
        {
            case 0: // Setup and launch
                npc.velocity.X *= 0.5f;
                if (Math.Abs(npc.velocity.X) < 0.1f)
                    npc.velocity.X = 0f;

                var chargeTime = extraData.bounceHouseSlams > 0 ? 5f : 45f;

                if (timer >= chargeTime)
                {
                    npc.netUpdate = true;

                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlamCharge.Asset with { PitchVariance = 0.2f }, npc.Center);

                    npc.velocity.Y = BOUNCE_HOUSE_JUMP_HEIGHT;
                    npc.velocity.X = 0f;

                    for (var i = 0; i < 12; i++)
                    {
                        var dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.t_Slime, 0f, npc.velocity.Y * 0.4f,
                            150, new Color(86, 162, 255, 100), 1.8f);
                        dust.noGravity = true;
                        dust.velocity *= 0.5f;
                    }

                    GetJumpPhase(npc) = 1;
                    timer = 0;
                }
                break;

            case 1: // Airborne
                npc.damage = 40;
                npc.velocity.X *= 0.95f;

                if (npc.velocity.Y > 0f)
                {
                    npc.velocity = new Vector2(0f, BOUNCE_HOUSE_SLAM_SPEED);
                    GetJumpPhase(npc) = 2;
                    timer = 0;
                }
                break;

            case 2: // Slamming down
                var hitGround = false;
                var tileX = (int)(npc.position.X / 16);
                var tileEndX = (int)((npc.position.X + npc.width) / 16);
                var tileY = (int)((npc.position.Y + npc.height) / 16);

                for (var i = tileX; i <= tileEndX; i++)
                {
                    var tile = Framing.GetTileSafely(i, tileY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        hitGround = true;
                        break;
                    }
                }

                if (hitGround || npc.velocity.Y == 0f)
                {
                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlam.Asset with { PitchVariance = 0.2f }, npc.Center);
                    CameraSystem.shake = 8;

                    for (var i = 0; i < 25; i++)
                    {
                        var dustVel = Vector2.One.RotatedBy(MathHelper.ToRadians(i * 14.4f)) * 9f;
                        var dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.t_Slime, dustVel.X, dustVel.Y,
                            newColor: new Color(86, 162, 255, 100));
                        dust.noGravity = true;
                        dust.scale = 2.2f;
                    }

                    BounceHouseProjectiles(npc);

                    if (Main.rand.NextBool(3))
                    {
                        var spawnPos = npc.Center + new Vector2(0f, npc.height / 2f);
                        var leftSlime = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X - 25, (int)spawnPos.Y, NPCID.BlueSlime);
                        if (leftSlime < Main.maxNPCs)
                        {
                            Main.npc[leftSlime].velocity = new Vector2(-5f + Main.rand.NextFloat(-1.5f, 0f),
                                                                       -4f + Main.rand.NextFloat(-1f, 1f));
                            Main.npc[leftSlime].scale = 1.3f;
                        }
                        var rightSlime = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X + 25, (int)spawnPos.Y, NPCID.GreenSlime);
                        if (rightSlime < Main.maxNPCs)
                        {
                            Main.npc[rightSlime].velocity = new Vector2(5f + Main.rand.NextFloat(0f, 1.5f),
                                                                        -4f + Main.rand.NextFloat(-1f, 1f));
                            Main.npc[rightSlime].scale = 1.3f;
                        }
                    }

                    GetJumpPhase(npc) = 3;
                    timer = 0;
                    extraData.isWobbling = true;
                    extraData.wobbleTimer = 0f;
                    extraData.bounceHouseSlams++;
                    SetExtraData(npc, extraData);
                }
                else
                {
                    var timeInPhase = timer;
                    var accelCurve = MathHelper.Clamp(timeInPhase / 15f, 0.1f, 1f);
                    var baseAccel = 0.4f;
                    var currentAccel = baseAccel + accelCurve * 0.6f;

                    npc.velocity.Y += currentAccel;

                    if (npc.velocity.Y > BOUNCE_HOUSE_SLAM_SPEED)
                        npc.velocity.Y = BOUNCE_HOUSE_SLAM_SPEED;

                    if (Main.rand.NextBool(2))
                    {
                        var dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.t_Slime, 0f, npc.velocity.Y * 0.3f,
                            150, new Color(86, 162, 255, 100), 1.6f);
                        dust.noGravity = true;
                        dust.velocity *= 0.5f;
                    }
                }
                break;

            case 3: // Brief recovery
                npc.damage = 12;
                npc.velocity *= 0.85f;

                var recoveryTime = 10f;

                if (timer >= recoveryTime)
                {
                    if (extraData.bounceHouseSlams < extraData.targetBounceSlams)
                    {
                        GetJumpPhase(npc) = 0;
                        timer = 0;
                        extraData.isWobbling = false;
                        SetExtraData(npc, extraData);
                    }
                    else
                    {
                        extraData.isWobbling = false;
                        SetExtraData(npc, extraData);
                    }
                }
                break;
        }
    }

    private void BounceHouseProjectiles(NPC npc)
    {
        // Every other slam lands between the previous one's shots, so a run of slams never repeats its gaps.
        var phase = GetExtraData(npc).bounceHouseSlams % 2 * 0.5f;

        GelVolley.Fan(npc, perSide: 4, near: 4 * 16f, far: 26 * 16f, phase: phase, damage: 4, knockback: 0.7f);
    }

    private void DoTeleporting(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var extraData = GetExtraData(npc);

        switch (extraData.teleportPhase)
        {
            case TeleportPhase.Prepare:
                npc.aiAction = 1;

                if (timer <= 20f)
                {
                    var fadeProgress = MathHelper.Clamp((20f - timer) / 20f, 0f, 1f);
                    extraData.teleportScaleMultiplier = 0.5f + fadeProgress * 0.5f;

                    if (timer == 20f)
                    {
                        Gore.NewGore(npc.GetSource_FromAI(),
                                    npc.Center + new Vector2(-40f, -npc.height / 2),
                                    npc.velocity, 734);
                    }
                }
                else if (timer <= 35f)
                {
                    extraData.teleportScaleMultiplier = 0.5f;
                }
                else if (timer >= 35f && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    PlaceAtDestination(npc, target);
                    extraData.teleportPhase = TeleportPhase.Execute;
                    timer = 0;
                    npc.netUpdate = true;
                }

                SetExtraData(npc, extraData);
                TeleportDust(npc, 1f);
                return;

            case TeleportPhase.Execute:
                npc.aiAction = 0;

                if (timer <= 10f)
                {
                    extraData.teleportScaleMultiplier = 0.5f;

                    var windUpProgress = timer / 10f;
                    var dustIntensity = 1f + windUpProgress * 2f;
                    TeleportDust(npc, dustIntensity);

                    if (timer == 5f)
                    {
                        SoundEngine.PlaySound(SoundID.QueenSlime with { PitchVariance = 0.2f }, npc.Center);
                    }

                    if (timer > 5f && timer % 2 == 0)
                    {
                        CameraSystem.shake = (int)(3 + windUpProgress * 4);
                    }
                }
                else if (timer <= 25f)
                {
                    var fadeInProgress = MathHelper.Clamp((timer - 10f) / 15f, 0f, 1f);
                    extraData.teleportScaleMultiplier = 0.5f + fadeInProgress * 0.5f;

                    TeleportDust(npc, 3f - fadeInProgress * 2f);
                }
                else if (timer >= 25f && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    SetExtraData(npc, extraData);
                    CompleteTeleport(npc);
                    return;
                }
                break;
        }

        SetExtraData(npc, extraData);
    }

    /// <summary>Hidden and untouchable from tick 20 of the wind-up until tick 12 of the reappearance.</summary>
    private static bool IsTeleportHidden(TeleportPhase phase, float timer)
        => phase == TeleportPhase.Prepare ? timer >= 20f : timer < 12f;


    /// <summary>
    ///     Moves the slime to the landing picked when the teleport began, or to a fresh one if a player has moved within
    ///     the safe distance of it since.
    /// </summary>
    private static void PlaceAtDestination(NPC npc, Player target)
    {
        var bottom = new Vector2(npc.localAI[1], npc.localAI[2]);
        var scale = GetHealthScale(npc);

        if (!IsSafeLanding(bottom, (int)(BASE_WIDTH * scale), (int)(BASE_HEIGHT * scale), TELEPORT_MIN_TILES)
            && !TryFindSafeSpot(npc, target, TELEPORT_MIN_TILES, TELEPORT_MAX_TILES, out bottom))
            bottom = npc.Bottom;

        npc.localAI[1] = bottom.X;
        npc.localAI[2] = bottom.Y;
        npc.Bottom = bottom;
        npc.velocity = Vector2.Zero;
    }

    private void CompleteTeleport(NPC npc)
    {
        var extraData = GetExtraData(npc);

        npc.TargetClosest();
        extraData.teleportScaleMultiplier = 1f;
        extraData.teleportPhase = TeleportPhase.Prepare;
        extraData.lastPosition = npc.Center;

        SetExtraData(npc, extraData);
        ResumeBattlePattern(npc);
    }

    private void DoConsumingSlimes(NPC npc, Player target)
    {
        ref float timer = ref GetTimer(npc);
        var extraData = GetExtraData(npc);

        if (timer == 1)
        {
            CameraSystem.shake = 15;
            SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.KingSlimeRoar.Asset with { Volume = 0.5f, }, npc.Center);

            for (var i = 0; i < 20; i++)
            {
                var dustVel = Vector2.One.RotatedBy(MathHelper.ToRadians(i * 18)) * 5f;
                var dust = Dust.NewDustDirect(npc.Center, 0, 0, DustID.t_Slime, dustVel.X, dustVel.Y,
                    newColor: new Color(78, 136, 255, 150));
                dust.noGravity = true;
                dust.scale = 1.8f;
            }
        }

        if (timer <= 90f && timer % 30 == 0 && timer > 1)
            CameraSystem.shake = (int)MathHelper.Lerp(8, 3, timer / 90f);

        npc.velocity *= 0.85f;

        // Never on the entry tick, which is before the feeding is counted.
        if (timer > 0 && timer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
        {
            for (var i = 0; i < Main.maxNPCs && extraData.slimesEaten < CONSUME_MAX_SLIMES; i++)
            {
                var slime = Main.npc[i];

                if (IsEdible(npc, slime) && npc.Hitbox.Intersects(slime.Hitbox))
                {
                    ConsumeSlime(npc, slime);
                    extraData.slimesEaten++;
                }
            }
        }

        var pulse = (float)Math.Sin(timer * 0.05f) * 0.1f;
        extraData.squishScale = new Vector2(1f + pulse, 1f - pulse);
        SetExtraData(npc, extraData);

        if (Main.rand.NextBool(10))
        {
            var offset = new Vector2(Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                Main.rand.NextFloat(-npc.height * 0.5f, npc.height * 0.5f));
            var position = npc.Center + offset;

            var dust = Dust.NewDustDirect(position, 0, 0, DustID.t_Slime, 0f, 0f,
                150, new Color(78, 136, 255, 150), 1.3f);
            dust.noGravity = true;
            dust.velocity = Vector2.Zero;
            dust.fadeIn = 1.2f;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient && IsFeedingDone(npc, timer))
        {
            extraData.lastConsumeTime = Main.GameUpdateCount;
            SetExtraData(npc, extraData);

            var patternData = GetPatternData(npc);
            if (patternData.patternOverride)
            {
                ResumeBattlePattern(npc);
            }
            else
            {
                extraData.isWobbling = true;
                extraData.wobbleTimer = 0f;
                SetExtraData(npc, extraData);
            }
        }
    }
    #endregion

    #region Intro

    /// <summary>Starts the title card on this client. Cosmetic, so it is never synced and a client that joins mid-fight gets none.</summary>
    private void StartIntro(NPC npc)
    {
        var extraData = GetExtraData(npc);

        if (Main.dedServ || extraData.introStarted || GetState(npc) != AIState.Intro)
            return;

        extraData.introStarted = true;
        SetExtraData(npc, extraData);

        if (GetTimer(npc) <= INTRO_REVEAL_TICKS)
            BossIntroSystem.Play(CreateIntro());
    }

    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(NPCID.KingSlime),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.KingSlime.Epithet"),

        TrackTitle = "Boss 1",
        TrackArtist = "Scott Lloyd Shelly",

        Accent = new Color(86, 162, 255),

        RevealTick = INTRO_REVEAL_TICKS,
        Duration = INTRO_HOLD_TICKS,

        LineRamp = 0,
        LineDensity = 0,
        LineIntensity = 0,
    };

    /// <summary>Hidden and invulnerable until <see cref="INTRO_REVEAL_TICKS" />, visible through <see cref="INTRO_HOLD_TICKS" />, then the pattern starts.</summary>
    private void DoIntro(NPC npc, Player target)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        npc.damage = 0;
        npc.velocity.X = 0f;

        if (timer == 1 && Main.netMode != NetmodeID.MultiplayerClient)
            MoveToEntrance(npc, target);

        if (timer < INTRO_REVEAL_TICKS)
        {
            extraData.introScaleMultiplier = 0.5f;
            IntroGeyser(npc, timer / INTRO_REVEAL_TICKS);
        }
        else
        {
            var sinceReveal = timer - INTRO_REVEAL_TICKS;

            if (sinceReveal == 0)
                RevealBurst(npc, ref extraData);

            extraData.introScaleMultiplier = MathHelper.Lerp(0.5f, 1f, EaseFunction.EaseCubicOut.Ease(Math.Min(sinceReveal / 24f, 1f)));
        }

        SetExtraData(npc, extraData);

        if (timer >= INTRO_REVEAL_TICKS + INTRO_HOLD_TICKS)
            EndIntro(npc, target);
    }

    /// <summary>Puts the slime somewhere in view of the player, at the same safe distance a teleport keeps. Stays put if there is no room.</summary>
    private static void MoveToEntrance(NPC npc, Player target)
    {
        if (!TryFindSafeSpot(npc, target, INTRO_MIN_TILES, INTRO_MAX_TILES, out var bottom))
            return;

        npc.Bottom = bottom;
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;
    }

    private static void IntroGeyser(NPC npc, float progress)
    {
        if (Main.dedServ)
            return;

        var gel = FieldSystem.GetLayer<GelField>();

        // Packed into a narrow column so the nodes merge into one rising mass rather than separate droplets.
        for (var i = 0; i < 2 + (int)(progress * 4f); i++)
        {
            var position = new Vector2(npc.Bottom.X + Main.rand.NextFloat(-0.2f, 0.2f) * BASE_WIDTH, npc.Bottom.Y - 4f);
            var velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(3f, 4f + progress * 4f));

            gel?.SpawnClingNode(position, velocity, Main.rand.NextFloat(9f, 14f), Main.rand.NextFloat(22f, 34f));
        }

        if (Main.rand.NextBool(2))
        {
            var dust = Dust.NewDustDirect(npc.Bottom + new Vector2(-BASE_WIDTH * 0.4f, -8f), (int)(BASE_WIDTH * 0.8f), 8, DustID.t_Slime,
                0f, -Main.rand.NextFloat(1f, 3f), 150, new Color(86, 162, 255, 100), 1.4f);
            dust.noGravity = true;
        }
    }

    private static void RevealBurst(NPC npc, ref NPCExtraData extraData)
    {
        extraData.squishScale = new Vector2(0.7f, 1.3f);
        extraData.isWobbling = true;
        extraData.wobbleTimer = 0f;

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.KingSlimeRoar.Asset with { Volume = 0.9f, Pitch = -0.1f }, npc.Center);
        SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeSlam.Asset with { Volume = 0.7f, Pitch = -0.2f }, npc.Bottom);

        var gel = FieldSystem.GetLayer<GelField>();

        for (var i = 0; i < 40; i++)
        {
            var velocity = Main.rand.NextVector2CircularEdge(8f, 5f) * Main.rand.NextFloat(0.5f, 1f) - Vector2.UnitY * 3f;
            var dust = Dust.NewDustPerfect(npc.Bottom - Vector2.UnitY * 12f, DustID.t_Slime, velocity, 150, new Color(86, 162, 255, 150), 1.8f);
            dust.noGravity = true;
        }

        for (var i = 0; i < 14; i++)
        {
            var velocity = new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 7f));

            gel?.SpawnClingNode(npc.Bottom - Vector2.UnitY * 12f, velocity, Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(24f, 36f));
        }
    }

    private void EndIntro(NPC npc, Player target)
    {
        var extraData = GetExtraData(npc);
        extraData.introScaleMultiplier = 1f;
        SetExtraData(npc, extraData);

        npc.damage = 12;

        StartPatternAttack(npc, AttackPatterns[PatternType.Phase1].States[0], target);
    }

    #endregion

    #region Helper Methods
    public override void Load()
    {
        base.Load();
        On_NPC.VanillaFindFrame += FindFrame;
    }

    public override void Unload()
    {
        base.Unload();
        On_NPC.VanillaFindFrame -= FindFrame;
    }

    private void FindFrame(On_NPC.orig_VanillaFindFrame orig, NPC self, int num, bool isLikeATownNPC, int type)
    {
        if (self.type != NPCID.KingSlime)
        {
            orig(self, num, isLikeATownNPC, type);
            return;
        }

        HandleKingSlimeFrames(self, num);
    }

    private void HandleKingSlimeFrames(NPC npc, int frameHeight)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        if (GetState(npc) == AIState.Intro)
        {
            // Stretched as it surfaces, then at rest for the title card.
            npc.frame.Y = timer < INTRO_REVEAL_TICKS + 12 ? frameHeight * 5 : frameHeight * 4;
        }
        else if (GetState(npc) == AIState.Jumping)
        {
            if (npc.velocity.Y == 0f)
            {
                var animSpeed = 4f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                    {
                        SoundEngine.PlaySound(SoundID.QueenSlime with { Volume = 0.25f }, npc.Center);
                        npc.frame.Y = frameHeight;
                    }
                }
            }
            else
            {
                npc.frame.Y = frameHeight * 5;
            }
        }
        else if (GetState(npc) == AIState.DeathAnimation)
        {
            switch (extraData.deathAnimationPhase)
            {
                case 0:
                    var animSpeed = 6f + timer / 120f * 10f;
                    npc.frameCounter++;
                    if (npc.frameCounter >= animSpeed)
                    {
                        npc.frameCounter = 0;
                        npc.frame.Y += frameHeight;

                        if (npc.frame.Y >= frameHeight * 4)
                            npc.frame.Y = frameHeight;
                    }
                    break;

                case 1:
                    var buildupSpeed = 3f;
                    npc.frameCounter++;
                    if (npc.frameCounter >= buildupSpeed)
                    {
                        npc.frameCounter = 0;
                        npc.frame.Y += frameHeight;

                        if (npc.frame.Y >= frameHeight * 4)
                            npc.frame.Y = frameHeight;
                    }
                    break;

                case 2:
                case 3:
                    npc.frame.Y = frameHeight * 5;
                    break;
            }
            return;
        }
        else if (GetState(npc) == AIState.GroundPound)
        {
            if (GetJumpPhase(npc) == 0)
            {
                var animSpeed = 2.5f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                    {
                        SoundEngine.PlaySound(SoundID.QueenSlime with { Volume = 0.25f }, npc.Center);
                        npc.frame.Y = frameHeight;
                    }
                }
            }
            else if (GetJumpPhase(npc) == 3)
            {
                var animSpeed = 3f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y > frameHeight * 2)
                        npc.frame.Y = 0;
                }
            }
            else
            {
                npc.frame.Y = frameHeight * 5;
            }
        }
        else if (GetState(npc) == AIState.BounceHouse)
        {
            if (GetJumpPhase(npc) == 0)
            {
                var animSpeed = 4f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 3)
                    {
                        SoundEngine.PlaySound(SoundID.QueenSlime with { Volume = 0.35f }, npc.Center);
                        npc.frame.Y = frameHeight;
                    }
                }
            }
            else if (GetJumpPhase(npc) == 1)
            {
                npc.frame.Y = frameHeight * 5;
            }
            else if (GetJumpPhase(npc) == 2)
            {
                npc.frame.Y = frameHeight * 4;
            }
            else
            {
                var animSpeed = 8f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                    {
                        SoundEngine.PlaySound(SoundID.QueenSlime with { Volume = 0.35f }, npc.Center);
                        npc.frame.Y = frameHeight;
                    }
                }
            }
        }
        else if (GetState(npc) == AIState.Teleporting)
        {
            if (extraData.teleportPhase == TeleportPhase.Prepare)
            {
                var animSpeed = 4f;
                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                        npc.frame.Y = frameHeight;
                }
            }
            else
            {
                npc.frame.Y = frameHeight * 4;
            }
        }
        else
        {
            var speed = Math.Abs(npc.velocity.X);
            float animSpeed;
            var isGliding = speed < 0.3f;

            if (isGliding)
            {
                if (!extraData.wasGliding)
                {
                    SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.KingSlimeGlide.Asset with { Pitch = -0.1f, Volume = 0.35f }, npc.Center);
                }

                animSpeed = 60f;
                npc.frame.Y = frameHeight * 4;
            }
            else if (speed < 1f)
            {
                if (npc.frame.Y == frameHeight)
                    npc.frame.Y = frameHeight;

                animSpeed = MathHelper.Lerp(12f, 6f, speed);

                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                        npc.frame.Y = frameHeight;
                }
            }
            else
            {
                animSpeed = MathHelper.Lerp(6f, 3f, Math.Min(speed / 3f, 1f));

                npc.frameCounter++;
                if (npc.frameCounter >= animSpeed)
                {
                    npc.frameCounter = 0;
                    npc.frame.Y += frameHeight;

                    if (npc.frame.Y >= frameHeight * 4)
                        npc.frame.Y = 0;
                }
            }

            extraData.wasGliding = isGliding;
            SetExtraData(npc, extraData);
        }
    }

    private void UpdateLOS(NPC npc, Player target)
    {
        var extraData = GetExtraData(npc);

        var hasLineOfSight = Collision.CanHitLine(npc.Center, 0, 0, target.Center, 0, 0);
        var heightDifferenceOk = Math.Abs(npc.Top.Y - target.Bottom.Y) <= 160f;

        var isAirborne = npc.velocity.Y != 0f;
        var shouldIgnoreLineOfSight = isAirborne && (GetState(npc) == AIState.Jumping || GetState(npc) == AIState.GroundPound || GetState(npc) == AIState.BounceHouse);

        if ((!hasLineOfSight || !heightDifferenceOk) && !shouldIgnoreLineOfSight)
        {
            extraData.teleportTimer += 2f;
            extraData.lineOfSightTimer += 1.5f;
        }
        else
        {
            extraData.lineOfSightTimer -= 2f;
            if (extraData.lineOfSightTimer < 0f)
                extraData.lineOfSightTimer = 0f;
        }

        if (!isAirborne)
        {
            var distanceMoved = Vector2.Distance(npc.Center, extraData.lastPosition);
            if (distanceMoved < MIN_MOVEMENT_THRESHOLD && npc.velocity.Y == 0f)
            {
                extraData.stuckTimer++;
            }
            else
            {
                extraData.stuckTimer = 0f;
                extraData.lastPosition = npc.Center;
            }
        }
        else
        {
            extraData.lastPosition = npc.Center;
        }

        SetExtraData(npc, extraData);
    }

    /// <returns>False when there is no safe landing, in which case the slime stays put.</returns>
    private bool BeginTeleport(NPC npc, Player target)
    {
        var extraData = GetExtraData(npc);

        // Reset either way, so a search that finds nothing is not repeated every tick.
        extraData.teleportTimer = 0f;
        extraData.stuckTimer = 0f;
        extraData.lineOfSightTimer = 0f;

        if (!TryFindSafeSpot(npc, target, TELEPORT_MIN_TILES, TELEPORT_MAX_TILES, out var bottom))
        {
            SetExtraData(npc, extraData);
            return false;
        }

        npc.localAI[1] = bottom.X;
        npc.localAI[2] = bottom.Y;

        SetState(npc, AIState.Teleporting);
        extraData.teleportPhase = TeleportPhase.Prepare;
        GetTimer(npc) = 0;

        SetExtraData(npc, extraData);
        npc.netUpdate = true;

        return true;
    }

    /// <summary>
    ///     Finds ground for the slime's full-size hitbox between <paramref name="minTiles" /> and <paramref name="maxTiles" />
    ///     tiles to either side of <paramref name="target" />, and at least <paramref name="minTiles" /> from every player.
    /// </summary>
    private static bool TryFindSafeSpot(NPC npc, Player target, int minTiles, int maxTiles, out Vector2 bottom)
    {
        var scale = GetHealthScale(npc);
        var width = (int)(BASE_WIDTH * scale);
        var height = (int)(BASE_HEIGHT * scale);
        var targetTile = target.Center.ToTileCoordinates();

        for (var attempt = 0; attempt < TELEPORT_ATTEMPTS; attempt++)
        {
            var side = Main.rand.NextBool() ? -1 : 1;
            var tileX = targetTile.X + side * Main.rand.Next(minTiles, maxTiles + 1);
            var tileY = targetTile.Y + Main.rand.Next(-TELEPORT_SEARCH_HEIGHT, TELEPORT_SEARCH_HEIGHT + 1);

            if (!WorldGen.InWorld(tileX, tileY, 60) || !TryFindGround(tileX, ref tileY)
                || Math.Abs(tileY - targetTile.Y) > TELEPORT_SEARCH_HEIGHT)
                continue;

            bottom = new Vector2(tileX * 16f + 8f, (tileY + 1) * 16f);

            if (IsSafeLanding(bottom, width, height, minTiles))
                return true;
        }

        bottom = Vector2.Zero;
        return false;
    }

    /// <summary>Moves <paramref name="tileY" /> to the open tile resting on the ground at <paramref name="tileX" />.</summary>
    private static bool TryFindGround(int tileX, ref int tileY)
    {
        for (var climbed = 0; WorldGen.SolidTile(tileX, tileY); climbed++)
        {
            if (climbed >= TELEPORT_SEARCH_HEIGHT)
                return false;

            tileY--;
        }

        for (var dropped = 0; dropped <= TELEPORT_SEARCH_HEIGHT * 2; dropped++, tileY++)
        {
            if (WorldGen.SolidTile(tileX, tileY + 1))
                return true;
        }

        return false;
    }

    /// <summary>Whether a hitbox standing at <paramref name="bottom" /> is clear, dry, mostly supported and far enough from every player.</summary>
    private static bool IsSafeLanding(Vector2 bottom, int width, int height, int minTiles)
    {
        var position = new Vector2(bottom.X - width / 2f, bottom.Y - height);

        if (Collision.SolidCollision(position, width, height) || Collision.WetCollision(position, width, height)
            || Collision.LavaCollision(position, width, height))
            return false;

        var first = (int)(position.X / 16f);
        var last = (int)((position.X + width) / 16f);
        var supported = 0;

        for (var x = first; x <= last; x++)
        {
            if (WorldGen.SolidTile(x, (int)(bottom.Y / 16f)))
                supported++;
        }

        if (supported * 2 < last - first + 1)
            return false;

        var center = position + new Vector2(width, height) / 2f;

        for (var i = 0; i < Main.maxPlayers; i++)
        {
            var player = Main.player[i];

            if (player.active && !player.dead && Vector2.Distance(center, player.Center) < minTiles * 16f)
                return false;
        }

        return true;
    }

    private void TeleportDust(NPC npc, float velocityMultiplier)
    {
        if (IsTeleportHidden(GetExtraData(npc).teleportPhase, GetTimer(npc)))
            return;

        for (var i = 0; i < 10; i++)
        {
            var dustIndex = Dust.NewDust(
                npc.position + Vector2.UnitX * -20f,
                npc.width + 40,
                npc.height,
                DustID.t_Slime,
                npc.velocity.X,
                npc.velocity.Y,
                150,
                new Color(86, 162, 255, 100),
                2f
            );

            Main.dust[dustIndex].noGravity = true;
            Main.dust[dustIndex].velocity *= velocityMultiplier;
        }
    }

    private void HandleRotation(NPC npc)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        var isAirborne = npc.velocity.Y != 0f;
        if (isAirborne)
        {
            var fallSpeed = Math.Max(npc.velocity.Y, 0f);
            var swayIntensity = fallSpeed * 0.005f;

            if (GetState(npc) == AIState.GroundPound && (GetJumpPhase(npc) == 1 || GetJumpPhase(npc) == 2))
            {
                var groundPoundIntensity = Math.Abs(npc.velocity.Y) * 0.006f;
                swayIntensity = Math.Max(swayIntensity, groundPoundIntensity * 1.2f);
            }

            var swayFreq = 0.12f + Math.Abs(npc.velocity.Y) * 0.009f;
            var sway = (float)Math.Sin(timer * swayFreq) * swayIntensity * 2.25f;
            extraData.rotationSway = MathHelper.Lerp(extraData.rotationSway, sway, 0.08f);
        }
        else
        {
            extraData.rotationSway = MathHelper.Lerp(extraData.rotationSway, 0f, 0.15f);
        }
        npc.rotation = extraData.rotationSway;

        SetExtraData(npc, extraData);
    }

    private void HandleSquishScale(NPC npc)
    {
        var extraData = GetExtraData(npc);
        ref float timer = ref GetTimer(npc);

        var isAirborne = npc.velocity.Y != 0f;
        if (isAirborne)
        {
            var yVel = npc.velocity.Y;
            var stretchIntensity = Math.Abs(yVel) * 0.06f;

            if (GetState(npc) == AIState.GroundPound && GetJumpPhase(npc) == 2)
                stretchIntensity *= 0.75f;

            if (GetState(npc) == AIState.Jumping)
                stretchIntensity *= 0.55f;

            stretchIntensity = MathHelper.Clamp(stretchIntensity, 0f, 0.33f);
            if (yVel < 0)
            {
                extraData.squishScale.Y = 1f + stretchIntensity;
                extraData.squishScale.X = 1f - stretchIntensity * 0.18f;
            }
            else
            {
                extraData.squishScale.Y = 1f + stretchIntensity;
                extraData.squishScale.X = 1f - stretchIntensity * 0.25f;
            }
        }
        else if (extraData.isWobbling)
        {
            extraData.wobbleTimer += 0.15f;
            var wobbleIntensity = GetState(npc) == AIState.GroundPound && GetJumpPhase(npc) == 3 ? 0.06f : 0.085f;

            var dampening = MathHelper.Clamp(1f - extraData.wobbleTimer * 0.05f, 0.1f, 1f);
            wobbleIntensity *= dampening;
            var wobbleY = (float)Math.Sin(extraData.wobbleTimer * 1.2f) * wobbleIntensity;
            var wobbleX = (float)Math.Sin(extraData.wobbleTimer * 0.8f) * wobbleIntensity;
            extraData.squishScale.Y = 1f + wobbleY;
            extraData.squishScale.X = 1f + wobbleX;

            if (dampening <= 0.3f)
                extraData.isWobbling = false;
        }
        else
        {
            extraData.squishScale = Vector2.Lerp(extraData.squishScale, Vector2.One, 0.15f);
        }
        if (Math.Abs(npc.velocity.X) > 0.5f && !isAirborne)
        {
            var horizontalSquish = 0.995f;
            extraData.squishScale.Y *= horizontalSquish;
            extraData.squishScale.X *= 1.001f;
        }

        SetExtraData(npc, extraData);
    }

    private void HandleDustTrail(NPC npc)
    {
        if (Math.Abs(npc.velocity.X) > 0.3f && Math.Abs(npc.velocity.Y) < 2f)
        {
            var tileX = (int)(npc.Center.X / 16f);
            var tileY = (int)((npc.position.Y + npc.height + 4) / 16f);

            if (WorldGen.InWorld(tileX, tileY))
            {
                var tile = Framing.GetTileSafely(tileX, tileY);

                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    var dustChance = Math.Abs(npc.velocity.X) * 0.4f;

                    if (Main.rand.NextFloat() < dustChance)
                    {
                        var dustPos = new Vector2(
                            npc.position.X + Main.rand.Next(npc.width),
                            npc.position.Y + npc.height - 8
                        );

                        var dust = Dust.NewDustDirect(dustPos, 8, 8, DustID.t_Slime);
                        dust.velocity.X = npc.velocity.X * 0.3f + Main.rand.NextFloat(-1f, 1f);
                        dust.velocity.Y = Main.rand.NextFloat(-1f, 0.5f);
                        dust.color = new Color(86, 162, 255, 100);
                        dust.scale = Main.rand.NextFloat(0.8f, 1.3f);
                        dust.alpha = Main.rand.Next(50, 150);
                    }
                }
            }
        }
    }

    private void HandleSlimeTrail(NPC npc)
    {
        if (Math.Abs(npc.velocity.X) > 0.3f && Math.Abs(npc.velocity.Y) < 2f)
        {
            var tileX = (int)(npc.Center.X / 16f);
            var tileY = (int)((npc.position.Y + npc.height + 2) / 16f);

            if (WorldGen.InWorld(tileX, tileY))
            {
                var tile = Framing.GetTileSafely(tileX, tileY);

                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    var slimeChance = Math.Abs(npc.velocity.X) * 0.6f;

                    if (Main.rand.NextFloat() < slimeChance)
                    {
                        if (Main.rand.NextBool(3))
                        {
                            var offsetX = Main.rand.NextBool() ? -3 : 3;
                            if (WorldGen.InWorld(tileX + offsetX, tileY))
                            {
                                var adjacentTile = Framing.GetTileSafely(tileX + offsetX, tileY);
                                if (adjacentTile.HasTile && Main.tileSolid[adjacentTile.TileType])
                                    SlimedResidue.Splatter(new Vector2(tileX + offsetX, tileY), npc.width / npc.scale);
                            }
                        }
                    }
                }
            }
        }
    }

    private void ConsumeSlime(NPC npc, NPC slime)
    {
        for (var d = 0; d < 15; d++)
        {
            var velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
            var dust = Dust.NewDustDirect(slime.Center, 0, 0, DustID.t_Slime, velocity.X, velocity.Y,
                150, new Color(78, 136, 255, 200), 1.5f);
            dust.noGravity = true;
        }

        var healed = Math.Min((int)(npc.lifeMax * CONSUME_HEAL_FRACTION), npc.lifeMax - npc.life);

        if (healed > 0)
        {
            npc.life += healed;
            npc.HealEffect(healed);
            npc.netUpdate = true;
        }

        SoundEngine.PlaySound(Assets.SFX.NPC.KingSlime.SlimeConsume.Asset with
        {
            PitchVariance = 0.3f,
            MaxInstances = 3
        }, slime.Center);

        slime.life = 0;
        slime.HitEffect();
        slime.active = false;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: slime.whoAmI);
    }

    /// <summary>Whether <paramref name="slime" /> is a hostile slime within reach of <paramref name="npc" />.</summary>
    private static bool IsEdible(NPC npc, NPC slime)
    {
        return slime.active && slime.aiStyle == NPCAIStyleID.Slime
            && slime.type != NPCID.KingSlime && !slime.boss && !slime.friendly && !slime.townNPC
            && Vector2.Distance(npc.Center, slime.Center) <= CONSUME_DETECTION_RANGE;
    }

    /// <summary>Counts a new feeding. Runs where the state is entered, so the last feeding's tally cannot end this one early.</summary>
    private void BeginFeeding(NPC npc)
    {
        var extraData = GetExtraData(npc);

        extraData.feedings++;
        extraData.slimesEaten = 0;

        SetExtraData(npc, extraData);
    }

    /// <summary>A feeding ends once it has eaten its share, timed out, or run out of slimes to reach.</summary>
    private bool IsFeedingDone(NPC npc, float timer)
        => timer >= CONSUME_MAX_TICKS || GetExtraData(npc).slimesEaten >= CONSUME_MAX_SLIMES || !HasNearbySlimes(npc);

    private bool HasNearbySlimes(NPC npc) => CountNearbySlimes(npc) > 0;

    private int CountNearbySlimes(NPC npc)
    {
        var count = 0;
        for (var i = 0; i < Main.maxNPCs; i++)
        {
            if (IsEdible(npc, Main.npc[i]))
                count++;
        }
        return count;
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        var extraData = GetExtraData(npc);

        var texture = TextureAssets.Npc[npc.type].Value;
        var origin = npc.frame.Size() / 2f;

        var groundOffset = npc.frame.Height * (1f - extraData.squishScale.Y) / 2f;
        var drawPos = npc.Center - screenPos + new Vector2(0f, groundOffset + npc.gfxOffY);

        var effects = npc.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        var bodyScale = npc.scale * extraData.squishScale;

        spriteBatch.Draw(texture, drawPos, npc.frame, npc.GetAlpha(drawColor * 0.75f), npc.rotation, origin, bodyScale, effects, 0f);

        DrawCrown(npc, spriteBatch, drawPos, bodyScale, drawColor, effects);

        return false;
    }

    /// <summary>Crown height from the top edge of each animation frame, in frame pixels. Vanilla's values.</summary>
    private static readonly float[] CrownOffsets = [2f, -6f, 2f, 10f, 2f, 0f];

    /// <summary>Vanilla draws the crown as part of its own NPC draw, which <see cref="PreDraw" /> replaces.</summary>
    private static void DrawCrown(NPC npc, SpriteBatch spriteBatch, Vector2 drawPos, Vector2 bodyScale, Color drawColor, SpriteEffects effects)
    {
        var crown = TextureAssets.Extra[ExtrasID.KingSlimeCrown].Value;

        var frame = Math.Clamp(npc.frame.Y / Math.Max(npc.frame.Height, 1), 0, CrownOffsets.Length - 1);
        var anchor = new Vector2(0f, CrownOffsets[frame] - npc.frame.Height / 2f) * bodyScale;

        spriteBatch.Draw(crown, drawPos + anchor.RotatedBy(npc.rotation), null, npc.GetNPCColorTintedByBuffs(drawColor),
            npc.rotation, crown.Size() / 2f, npc.scale, effects, 0f);
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        if (ClientSpawnGuard.Reject(npc))
            return;

        SetState(npc, AIState.Intro);

        Main.StartSlimeRain();

        Main.slimeRain = true;
    }
    #endregion
}