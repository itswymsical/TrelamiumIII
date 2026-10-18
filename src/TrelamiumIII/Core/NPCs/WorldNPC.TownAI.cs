using Terraria.GameContent.UI;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.NPCs;

/// <summary>
/// <c>ai[0]</c> of the <see cref="WorldNPC" /> town AI. Values shared with vanilla's aiStyle 7 mean the same thing there,
/// so a vanilla town NPC that starts a chat with a world NPC writes a state this AI runs.
/// </summary>
public enum TownState
{
    /// <summary>Standing. <c>ai[1]</c>: ticks until the next stroll.</summary>
    Idle = 0,

    /// <summary>Walking to world X <c>ai[3]</c>, or picking a stroll when it is 0. <c>ai[1]</c>: ticks before giving up.</summary>
    Walk = 1,

    /// <summary>Started a chat. <c>ai[1]</c>: ticks left, <c>ai[2]</c>: partner NPC.</summary>
    ChatLead = 3,

    /// <summary>Answering a chat. <c>ai[1]</c>: ticks left, <c>ai[2]</c>: partner NPC.</summary>
    ChatListen = 4,

    /// <summary>Facing a nearby player. <c>ai[1]</c>: ticks left, <c>ai[2]</c>: player. Vanilla's 6, 18 and 19 run the same way.</summary>
    Greet = 7,

    /// <summary>Started rock-paper-scissors. <c>ai[1]</c>: ticks left, <c>ai[2]</c>: partner NPC.</summary>
    GameLead = 16,

    /// <summary>Answering rock-paper-scissors. <c>ai[1]</c>: ticks left, <c>ai[2]</c>: partner NPC.</summary>
    GameListen = 17,

    /// <summary>Closing on a threat. <c>ai[1]</c>: ticks until the next attack, <c>ai[2]</c>: target NPC, <c>ai[3]</c>: anchor world X, the leash centre.</summary>
    Fight = 40,

    /// <summary>One weapon use. <c>ai[1]</c>: ticks left; <c>ai[2]</c> and <c>ai[3]</c> as in <see cref="Fight" />.</summary>
    Attack = 41,

    /// <summary>Running from a threat. <c>ai[2]</c>: threat NPC.</summary>
    Flee = 42,
}

/// <remarks>
/// The town AI runs from <see cref="PreAI" /> on every side. Transitions are made on the server (or in singleplayer) and
/// reach clients through <c>ai[]</c> in <see cref="MessageID.SyncNPC" />; each side runs a state's entry once when it
/// first sees it. A busy <see cref="Director" /> vetoes PreAI, so a behaviour pauses the AI without touching its state.
/// </remarks>
public abstract partial class WorldNPC
{
    private const int MinStand = 180;
    private const int MaxStand = 600;
    private const int StrollTimeout = 600;
    private const int StuckTimeout = 60;

    /// <summary>Vanilla's chat and greeting distances, in pixels.</summary>
    private const float ChatReach = 100f;
    private const float GreetReach = 150f;
    private const float AttentionLimit = 200f;

    /// <summary>One rock-paper-scissors match; rounds are revealed at 40, 100 and 160.</summary>
    private const int GameCycle = 420;

    /// <summary><c>ai[0..3]</c> at the end of the last tick, for <see cref="HitEffect" />.</summary>
    private Vector4 settledAI;

    private int enteredState = -1;
    private int stateTicks;
    private int turnAt = -1;
    private int stuckTicks;
    private int leadWins;
    private int partnerWins;

    public TownState State => (TownState)(int)NPC.ai[0];

    /// <summary>Ticks since this side entered <see cref="State" />.</summary>
    protected int StateTicks => stateTicks;

    /// <summary>Set false to leave the NPC to its vanilla aiStyle and <see cref="ModNPC.AI" />.</summary>
    protected virtual bool UsesTownAI => true;

    /// <summary>
    /// Tiles a stroll may end from the anchor. 0 holds the anchor, walking back when knocked more than two tiles off it.
    /// </summary>
    protected virtual int WanderRadius => 0;

    /// <summary>Tile area strolls stay inside. Defaults to the home's.</summary>
    protected virtual Rectangle? WanderBounds => Home?.Area;

    /// <summary>Whether strolls may lead through closed doors, asked before each one.</summary>
    protected virtual bool CanUseDoors => false;

    protected virtual float WalkSpeed => 1f;

    /// <summary>
    /// One in this many idle ticks starts a chat with an NPC nearby; rock-paper-scissors is six times rarer and greeting a
    /// player four times. 0 never does any. Vanilla town NPCs use 300.
    /// </summary>
    protected virtual int ChatChance => 0;

    private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

    public override bool PreAI()
    {
        if (!UsesTownAI)
            return true;

        UpdateTownAI();
        return false;
    }

    private void UpdateTownAI()
    {
        if ((int)NPC.ai[0] != enteredState)
            EnterState();

        stateTicks++;

        if (TownChat.Talker(NPC) is { } talker)
        {
            AttendTalker(talker);
        }
        else
        {
            if (Authority)
                CheckThreats();

            TickState();
        }

        BehaviorMovement.TryCloseDoor(NPC);
        BehaviorMovement.ApplyTerrainCollision(NPC);
    }

    private void SettleTownState() => settledAI = new Vector4(NPC.ai[0], NPC.ai[1], NPC.ai[2], NPC.ai[3]);

    /// <summary>
    /// Undoes vanilla's town NPC flinch: <see cref="NPC.StrikeNPC" /> puts any aiStyle 7 town NPC into a walk when hit,
    /// before calling this. A chat still breaks off as in vanilla; any other state, or one the director paused, is kept.
    /// </summary>
    public override void HitEffect(NPC.HitInfo hit)
    {
        if (!UsesTownAI || NPC.ai[0] != (float)TownState.Walk || NPC.ai[2] != 0f)
            return;

        bool chatting = (TownState)(int)settledAI.X is TownState.ChatLead or TownState.ChatListen or TownState.GameLead or TownState.GameListen;

        if (chatting && !IsDirected(NPC))
            return;

        NPC.ai[0] = settledAI.X;
        NPC.ai[1] = settledAI.Y;
        NPC.ai[2] = settledAI.Z;
        NPC.ai[3] = settledAI.W;
    }

    /// <summary>Switches state and syncs it. Server or singleplayer only.</summary>
    protected void SetState(TownState state, float timer = 0f, float target = 0f, float x = 0f)
    {
        NPC.ai[0] = (float)state;
        NPC.ai[1] = timer;
        NPC.ai[2] = target;
        NPC.ai[3] = x;
        NPC.netUpdate = true;

        EnterState();
    }

    private void EnterState()
    {
        enteredState = (int)NPC.ai[0];
        stateTicks = 0;
        stuckTicks = 0;
        calmTicks = 0;

        switch (State)
        {
            case TownState.Idle:
                int stand = (int)NPC.ai[1];
                turnAt = WanderRadius > 0 && stand > 4 && Main.rand.NextBool(3) ? Main.rand.Next(stand / 4, stand * 3 / 4) : -1;
                break;

            case TownState.GameLead:
                leadWins = 0;
                partnerWins = 0;
                break;

            case TownState.Attack:
                StartAttack();
                break;
        }
    }

    private void TickState()
    {
        switch (State)
        {
            case TownState.Idle:
                TickIdle();
                break;

            case TownState.Walk:
                TickWalk();
                break;

            case TownState.ChatLead or TownState.ChatListen:
                TickChat();
                break;

            case TownState.GameLead or TownState.GameListen:
                TickGame();
                break;

            case TownState.Greet or (TownState)6 or (TownState)18 or (TownState)19:
                TickGreet();
                break;

            case TownState.Fight:
                TickFight();
                break;

            case TownState.Attack:
                TickAttack();
                break;

            case TownState.Flee:
                TickFlee();
                break;

            default:
                BehaviorMovement.Decelerate(NPC);

                if (Authority)
                    Stand(60 + Main.rand.Next(60));

                break;
        }
    }

    /// <summary>Vanilla's chat-window hold: everything else stops and the NPC faces whoever is talking to it.</summary>
    private void AttendTalker(Player talker)
    {
        if (Authority && State != TownState.Idle)
        {
            EndChat();
            Stand(300);
        }

        NPC.ai[1] = 300f;
        BehaviorMovement.Decelerate(NPC);
        FaceToward(talker.Center);
    }

    // Idle and strolling

    private void TickIdle()
    {
        BehaviorMovement.Decelerate(NPC);
        NPC.ai[1]--;

        if (!Authority)
            return;

        if (ChatChance > 0 && NPC.CanTalk && !NPC.wet)
        {
            if (Main.rand.NextBool(ChatChance) && TryStartChat(game: false))
                return;

            if (Main.rand.NextBool(ChatChance * 6) && TryStartChat(game: true))
                return;

            if (Main.rand.NextBool(ChatChance * 4) && TryGreet())
                return;
        }

        if ((int)NPC.ai[1] == turnAt)
        {
            NPC.direction = -NPC.direction;
            NPC.spriteDirection = NPC.direction;
            NPC.netUpdate = true;
        }

        if (NPC.ai[1] <= 0f)
            StartStroll();
    }

    /// <summary>Stands for <paramref name="ticks" />, or a random stand time when null.</summary>
    private void Stand(int? ticks = null)
    {
        int stand = ticks ?? (WanderRadius > 0 ? Main.rand.Next(MinStand, MaxStand + 1) : 60);
        SetState(TownState.Idle, stand);
    }

    /// <summary>Walks to a random spot on the floor traced from the anchor, or back onto it when off it.</summary>
    private void StartStroll()
    {
        (int left, int right) = BehaviorMovement.TraceFloor(AnchorTile, WanderRadius, WanderBounds, CanUseDoors);
        int current = (int)(NPC.Center.X / 16f);
        int slack = WanderRadius > 0 ? 0 : 2;

        int? column = null;

        if (current < left - slack || current > right + slack)
        {
            column = Main.rand.Next(left, right + 1);
        }
        else if (WanderRadius > 0)
        {
            int options = 0;

            for (int x = left; x <= right; x++)
            {
                if (Math.Abs(x - current) >= 3 && Main.rand.Next(++options) == 0)
                    column = x;
            }
        }

        if (column is not { } target)
        {
            Stand();
            return;
        }

        SetState(TownState.Walk, StrollTimeout, 0f, target * 16f + 8f);
    }

    private void TickWalk()
    {
        float targetX = NPC.ai[3];

        if (targetX <= 0f)
        {
            BehaviorMovement.Decelerate(NPC);

            // Vanilla breaks off a chat by putting the partner into a timed walk with no destination.
            if (Authority)
                StartStroll();

            return;
        }

        BehaviorMovement.WalkTowardX(NPC, targetX, WalkSpeed);

        if (Authority)
            BehaviorMovement.TryOpenDoor(NPC);

        BehaviorMovement.TryJump(NPC);
        NPC.ai[1]--;

        if (!Authority)
            return;

        stuckTicks = NPC.position.X == NPC.oldPosition.X ? stuckTicks + 1 : 0;

        if (Math.Abs(targetX - NPC.Center.X) <= 8f)
        {
            Stand();
            return;
        }

        if (stuckTicks < StuckTimeout && NPC.ai[1] > 0f)
            return;

        if (DistanceFromAnchor > AnchorReturnDistance && !IsAnyoneWatching())
        {
            NPC.Center = AnchorTile.ToWorldCoordinates();
            NPC.velocity = Vector2.Zero;
        }

        Stand();
    }

    // Chatting

    private NPC? Partner => NPCAt(NPC.ai[2]);

    private static NPC? NPCAt(float index)
    {
        int i = (int)index;
        return i >= 0 && i < Main.maxNPCs && Main.npc[i].active ? Main.npc[i] : null;
    }

    /// <summary>Vanilla's chat start: an NPC 20-100 px away, in sight, free and grounded, is put in the listening state.</summary>
    private bool TryStartChat(bool game)
    {
        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI == NPC.whoAmI || !other.CanBeTalkedTo || other.wet || (game && NPCID.Sets.IsTownPet[other.type]))
                continue;

            if (other.ai[0] > 1f || (other.ai[0] == 1f && (other.closeDoor || other.ai[1] > 200f)))
                continue;

            float distance = NPC.Distance(other.Center);
            if (distance < 20f || distance > ChatReach || !Collision.CanHit(NPC.Center, 0, 0, other.Center, 0, 0))
                continue;

            if (IsDirected(other) || TownChat.Talker(other) != null)
                continue;

            int duration = 420 * (Main.rand.NextBool() ? Main.rand.Next(1, 3) : Main.rand.Next(1, 4));
            int side = NPC.Center.X < other.Center.X ? 1 : -1;

            other.ai[0] = game ? (float)TownState.GameListen : (float)TownState.ChatListen;
            other.ai[1] = duration;
            other.ai[2] = NPC.whoAmI;
            other.direction = -side;
            other.netUpdate = true;

            if (game)
            {
                other.localAI[2] = 0f;
                other.localAI[3] = 0f;
            }

            NPC.direction = side;
            NPC.spriteDirection = side;
            SetState(game ? TownState.GameLead : TownState.ChatLead, duration, other.whoAmI);
            return true;
        }

        return false;
    }

    private static bool IsDirected(NPC npc) => npc.TryGetGlobalNPC(out NPCDirector director) && director.Enabled && director.IsBusy;

    /// <summary>Breaks off a chat the way vanilla does on danger: the partner walks away.</summary>
    private void EndChat()
    {
        if (State is not (TownState.ChatLead or TownState.ChatListen or TownState.GameLead or TownState.GameListen))
            return;

        if (Partner is not { } partner || (int)partner.ai[2] != NPC.whoAmI)
            return;

        partner.ai[0] = (float)TownState.Walk;
        partner.ai[1] = 120 + Main.rand.Next(120);
        partner.ai[2] = 0f;
        partner.localAI[3] = 0f;
        partner.netUpdate = true;
    }

    private void TickChat()
    {
        BehaviorMovement.Decelerate(NPC);
        NPC.ai[1]--;

        NPC? partner = Partner;

        if (partner != null)
            FaceToward(partner.Center);

        if (!Authority)
            return;

        if (State == TownState.ChatLead && partner != null)
        {
            int beat = stateTicks % GameCycle;

            if (beat == 70 && partner.CanTalk)
                EmoteBubble.NewBubbleNPC(new WorldUIAnchor(partner), 90, new WorldUIAnchor(NPC));
            else if (beat == 320)
                EmoteBubble.NewBubbleNPC(new WorldUIAnchor(NPC), 100, new WorldUIAnchor(partner));
        }

        if (NPC.ai[1] <= 0f || partner == null || NPC.Distance(partner.Center) > AttentionLimit)
            Stand(60 + Main.rand.Next(60));
    }

    private void TickGame()
    {
        BehaviorMovement.Decelerate(NPC);
        NPC.ai[1]--;

        NPC? partner = Partner;

        if (partner != null)
            FaceToward(partner.Center);

        if (!Authority)
            return;

        if (State == TownState.GameLead && partner != null)
        {
            int beat = stateTicks % GameCycle;

            if (beat == 0)
            {
                leadWins = 0;
                partnerWins = 0;
            }
            else if (beat is 40 or 100 or 160)
            {
                PlayRound(partner, final: beat == 160);
            }
        }

        if (NPC.ai[1] <= 0f || partner == null || NPC.Distance(partner.Center) > AttentionLimit)
            Stand(60 + Main.rand.Next(60));
    }

    /// <summary>Shows both hands for one round; on the last, a player with two wins shows the winning hand.</summary>
    private void PlayRound(NPC partner, bool final)
    {
        int lead = Main.rand.Next(3);
        int outcome = Main.rand.Next(3);
        int other = outcome switch
        {
            0 => (lead + 2) % 3,
            1 => (lead + 1) % 3,
            _ => lead,
        };

        if (outcome == 0)
            leadWins++;
        else if (outcome == 1)
            partnerWins++;

        int time = final ? 75 : 60;
        EmoteBubble.NewBubble(Hand(lead, final && leadWins >= 2), new WorldUIAnchor(NPC), time);
        EmoteBubble.NewBubble(Hand(other, final && partnerWins >= 2), new WorldUIAnchor(partner), time);
    }

    /// <summary>Hands in beating order: each beats the one before it (paper beats rock, scissors beat paper, rock beats scissors).</summary>
    private static int Hand(int index, bool won) => index switch
    {
        0 => won ? EmoteID.RPSWinRock : EmoteID.RPSRock,
        1 => won ? EmoteID.RPSWinPaper : EmoteID.RPSPaper,
        _ => won ? EmoteID.RPSWinScissors : EmoteID.RPSScissors,
    };

    private bool TryGreet()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.CanBeTalkedTo || player.Distance(NPC.Center) >= GreetReach || !Collision.CanHitLine(NPC.Top, 0, 0, player.Top, 0, 0))
                continue;

            FaceToward(player.Center);
            SetState(TownState.Greet, 220f, player.whoAmI);
            return true;
        }

        return false;
    }

    private void TickGreet()
    {
        BehaviorMovement.Decelerate(NPC);
        NPC.ai[1]--;

        int index = (int)NPC.ai[2];
        Player? player = index is >= 0 and < Main.maxPlayers ? Main.player[index] : null;

        if (player is { active: true })
            FaceToward(player.Center);

        if (!Authority)
            return;

        if (NPC.ai[1] <= 0f || player is not { CanBeTalkedTo: true } || player.Distance(NPC.Center) > AttentionLimit
            || !Collision.CanHitLine(NPC.Top, 0, 0, player.Top, 0, 0))
        {
            Stand(60 + Main.rand.Next(60));
        }
    }

    private void FaceToward(Vector2 position)
    {
        int direction = position.X < NPC.Center.X ? -1 : 1;
        NPC.direction = direction;
        NPC.spriteDirection = direction;
    }
}
