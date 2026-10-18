using SubworldLibrary;
using Terraria.GameContent.UI;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

/// <summary>
///     One meeting with Silvian, on the client having it. The camera finds him at his work, he notices the player, walks
///     over, and the greeting plays between the two of them. The camera is handed back once it is over.
/// </summary>
/// <remarks>
///     <para>
///         His part is a behaviour script (<see cref="BuildScript" />) played as an interrupt on his director, so his
///         usual script picks up underneath when it ends and he wanders home. This client plays it on its own copy of him;
///         in multiplayer the server plays the same script on the copy that counts.
///     </para>
///     <para>
///         The <see cref="SilvianEncounterCutscene" /> shell covers the part before he speaks: input lock, hidden HUD and
///         skip. The letterbox belongs to this class, so it stays up across the hand-off into the conversation, which
///         takes it over.
///     </para>
/// </remarks>
internal sealed class SilvianEncounterScene(NPC silvian)
{
    /// <summary>He is at his work until this tick, then looks up.</summary>
    private const int NOTICE = 45;

    /// <summary>The camera reaches him.</summary>
    private const int PAN_END = 60;

    /// <summary>He has taken the player in, and sets off toward them.</summary>
    private const int WALK_START = 85;

    /// <summary>Past this he gives up on the path and talks from where he stands.</summary>
    private const int WALK_TIMEOUT = 7 * 60;

    /// <summary>A beat between him stopping and him speaking.</summary>
    private const int SETTLE_TICKS = 16;

    /// <summary>He never speaks before this, so the letterbox has finished opening.</summary>
    private const int EARLIEST_TALK = 100;

    /// <summary>Horizontal gap, in world pixels, he stops at: about three tiles.</summary>
    private const float TALK_DISTANCE = 52f;

    /// <summary>Longest he holds his ground waiting for the conversation to end, on a peer that never hears it does.</summary>
    private const int HOLD_MAX = 90 * 60;

    /// <summary>Past this distance, in world pixels, the scene is dropped: the player was carried off.</summary>
    private const float ABANDON_RANGE = 80f * 16f;

    /// <summary>Camera push-in while he walks over, as a multiple of the player's zoom.</summary>
    private const float PUSH_ZOOM = 1.15f;

    /// <summary>How far above the midpoint between them the camera aims.</summary>
    private const float FOCUS_HEIGHT = 24f;

    private readonly int npcIndex = silvian.whoAmI;
    private readonly int npcType = silvian.type;

    private IBehavior? script;
    private Vector2 camera;
    private Vector2 cameraStart;
    private int arrivedAt = -1;
    private bool conversationStarted;
    private bool conversationOver;
    private bool released;

    public int Ticks { get; private set; }

    /// <summary>The camera has been handed back.</summary>
    public bool Finished => released;

    /// <summary>The part the cutscene shell covers: from the start until he speaks.</summary>
    public bool InScene => !released && !conversationStarted;

    private NPC? Silvian
    {
        get
        {
            NPC npc = Main.npc[npcIndex];

            return npc.active && npc.type == npcType ? npc : null;
        }
    }

    public void Begin()
    {
        cameraStart = camera = Main.LocalPlayer.Center;

        Letterbox.Show(duration: 75, heightPercentage: 0.085f);
        ZoomHandler.SetZoomAnimation(Main.GameZoomTarget * PUSH_ZOOM, WALK_START);

        if (CutsceneSystem.CurrentCutscene is not SilvianEncounterCutscene)
            CutsceneSystem.PlayCutscene<SilvianEncounterCutscene>();

        if (Silvian is { } npc && npc.TryEnable(out NPCDirector director))
        {
            script = BuildScript(Main.myPlayer, () => conversationOver || released, () => arrivedAt = Ticks);
            director.Interrupt(npc, script);
        }

        SilvianEncounter.SendApproach();
    }

    public void Tick()
    {
        if (released)
            return;

        Ticks++;

        Player player = Main.LocalPlayer;
        NPC? npc = Silvian;

        if (!conversationStarted)
        {
            if (player.dead || SubworldSystem.AnyActive() || npc is null || NPC.AnyDanger()
                || Vector2.Distance(player.Center, npc.Center) > ABANDON_RANGE)
            {
                Abandon();

                return;
            }

            // The player can't move until he speaks, so nothing gets a free hit in meanwhile.
            player.immune = true;
            player.immuneNoBlink = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            // A multiplayer client's copy of him may never finish the walk the server is running.
            if (arrivedAt < 0 && Ticks >= WALK_START + WALK_TIMEOUT + 30)
                arrivedAt = Ticks;

            if (arrivedAt >= 0 && Ticks >= Math.Max(arrivedAt + SETTLE_TICKS, EARLIEST_TALK))
                StartConversation();
        }
        else if (!conversationOver && !DialogueManager.Instance.IsAnyActive())
        {
            conversationOver = true;
            SilvianEncounter.SendRelease();
        }

        UpdateCamera(player, npc);
    }

    /// <summary>Called when the cutscene shell ends. A skip cuts straight to him speaking, beside the player.</summary>
    public void OnSceneEnded(bool skipped)
    {
        if (!InScene)
            return;

        if (!skipped)
        {
            Abandon();

            return;
        }

        // The rest of his walk is swapped for standing still until they are done. The server's copy finishes its own walk,
        // and waits for the release at the end as usual.
        CancelScript();

        if (Silvian is { } npc)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                PlaceBesidePlayer(npc);

            if (npc.TryEnable(out NPCDirector director))
            {
                script = new Hold(Main.myPlayer, HOLD_MAX, () => conversationOver || released);
                director.Interrupt(npc, script);
            }
        }

        StartConversation();

        // Behind the skip's fade to black, so a cut reads better than a pan.
        camera = Focus(Main.LocalPlayer, Silvian, 0.5f);
    }

    /// <summary>Drops the scene as the world unloads: only the screen needs handing back, since he is going with it.</summary>
    public void Cancel() => Release();

    private void StartConversation()
    {
        conversationStarted = true;

        bool started = DialogueManager.Instance.StartDialogue(
            SilvianEncounter.GreetingKey,
            zoomIn: true,
            letterbox: true,
            music: Assets.Music.HomeForagerMusic.Slot,
            anchor: DialogueAnchor.OnSpeaker(Silvian));

        if (started)
            return;

        TrelamiumIII.Instance.Logger.Error($"Silvian encounter: '{SilvianEncounter.GreetingKey}' would not open; ending the scene.");
        Main.LocalPlayer.GetModPlayer<SilvianPlayer>().Met = true;
        conversationOver = true;
        SilvianEncounter.SendRelease();
    }

    #region camera

    private void UpdateCamera(Player player, NPC? npc)
    {
        if (conversationOver)
        {
            ReturnCamera(player);

            return;
        }

        if (!conversationStarted && Ticks <= PAN_END)
        {
            float progress = Ticks / (float)PAN_END;

            camera = Vector2.Lerp(cameraStart, Focus(player, npc, 0.7f), progress * progress * (3f - 2f * progress));
        }
        else
        {
            camera = Vector2.Lerp(camera, Focus(player, npc, conversationStarted ? 0.5f : 0.6f), 0.08f);
        }

        CameraSystem.LockCamera(camera);
    }

    /// <summary>A point between the player and him, <paramref name="weight" /> of the way toward him.</summary>
    private static Vector2 Focus(Player player, NPC? npc, float weight)
    {
        Vector2 between = npc is null ? player.Center : Vector2.Lerp(player.Center, npc.Center, weight);

        return between - new Vector2(0f, FOCUS_HEIGHT);
    }

    private void ReturnCamera(Player player)
    {
        camera = Vector2.Lerp(camera, player.Center, 0.1f);

        if (Vector2.DistanceSquared(camera, player.Center) < 64f)
            Release();
        else
            CameraSystem.LockCamera(camera);
    }

    #endregion

    /// <summary>Ends the scene before he spoke: his script is cut, and the watcher may try again later.</summary>
    private void Abandon()
    {
        CancelScript();
        SilvianEncounter.SendRelease();
        SilvianEncounter.OnAbandoned();
        Release();
    }

    /// <summary>
    ///     Hands the camera and screen back. The letterbox is hidden here only if the conversation never took it over,
    ///     since restarting a hide already in progress snaps the bars back open.
    /// </summary>
    private void Release()
    {
        if (released)
            return;

        released = true;
        CameraSystem.UnlockCamera();

        // A conversation that zoomed in restores the zoom itself when it ends.
        if (!DialogueManager.Instance.IsZoomedIn)
            ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, 60);

        if (CutsceneSystem.CurrentCutscene is SilvianEncounterCutscene { IsPlaying: true })
            CutsceneSystem.EndCurrentCutscene();

        if (!conversationStarted && !DialogueManager.Instance.IsAnyActive())
            Letterbox.Hide();
    }

    /// <summary>Takes this scene's script off his director, if it is still the one running.</summary>
    private void CancelScript()
    {
        if (script is null || Silvian is not { } npc || !npc.TryEnable(out NPCDirector director))
            return;

        if (ReferenceEquals(director.Current, script))
            director.Resume(npc);

        script = null;
    }

    /// <summary>Puts him on the ground at talking distance from the player, on his own side, if there is room.</summary>
    private static void PlaceBesidePlayer(NPC npc)
    {
        Player player = Main.LocalPlayer;
        int side = npc.Center.X >= player.Center.X ? 1 : -1;
        int tileX = (int)((player.Center.X + side * TALK_DISTANCE) / 16f);

        if (!SilvianEncounter.TryFindGround(tileX, (int)(player.Bottom.Y / 16f), out Vector2 bottom))
            return;

        npc.Bottom = bottom;
        npc.velocity = Vector2.Zero;
        npc.direction = npc.spriteDirection = -side;
        npc.netUpdate = true;
    }

    #region his part

    /// <summary>
    ///     What he does: tends to his work until he notices the player, turns and takes them in, walks over, then holds his
    ///     ground facing them until <paramref name="released" /> says the conversation is over.
    /// </summary>
    /// <param name="arrived">Told when he has stopped in front of the player. Null on the server.</param>
    internal static IBehavior BuildScript(int playerIndex, Func<bool> released, Action? arrived) =>
        Behavior.Sequence(
            new Tend(playerIndex, NOTICE),
            Behavior.Do(ctx => Notice(ctx, playerIndex)),
            new Hold(playerIndex, WALK_START - NOTICE, null),
            new Approach(playerIndex, TALK_DISTANCE, WALK_TIMEOUT),
            Behavior.Do(_ => arrived?.Invoke()),
            new Hold(playerIndex, HOLD_MAX, released));

    /// <summary>He looks up: turns to the player, with a "!" over his head.</summary>
    private static void Notice(BehaviorContext ctx, int playerIndex)
    {
        ctx.Face(Main.player[playerIndex].Center);

        // The server's bubble reaches every client, so a client of a server leaves it to the server.
        if (Main.netMode != NetmodeID.MultiplayerClient)
            EmoteBubble.NewBubble(EmoteID.EmotionAlert, new WorldUIAnchor(ctx.NPC), 90);
    }

    /// <summary>At his work, back to the player, with a swing of the sickle over the herbs at his feet.</summary>
    private sealed class Tend(int playerIndex, int ticks) : Behavior
    {
        protected override void Begin(BehaviorContext ctx)
        {
            NPC npc = ctx.NPC;
            int away = Main.player[playerIndex].Center.X < npc.Center.X ? 1 : -1;

            ctx.Face(npc.Center + new Vector2(away * 32f, 0f));

            if (Main.netMode != NetmodeID.MultiplayerClient)
                ctx.Puppet?.BeginUse(ItemID.Sickle, npc.Bottom + new Vector2(away * 28f, -4f));
        }

        protected override void Run(BehaviorContext ctx)
        {
            Stand(ctx.NPC);

            if (Age + 1 >= ticks)
                Finish();
        }

        protected override void End(BehaviorContext ctx, bool canceled) => ctx.Puppet?.Hold(ItemID.None);
    }

    /// <summary>Walks toward the player until he is close enough to talk, is blocked, or runs out of time.</summary>
    private sealed class Approach(int playerIndex, float stopDistance, int timeout) : Behavior
    {
        /// <summary>A little brisker than his usual stroll: he has someone to meet.</summary>
        private const float SPEED = 1.6f;

        /// <summary>Ticks without headway before he stops where he is: a wall, a closed door, a drop.</summary>
        private const int STUCK_TICKS = 45;

        private int stuck;

        protected override void Run(BehaviorContext ctx)
        {
            NPC npc = ctx.NPC;
            Player target = Main.player[playerIndex];

            if (!target.active || target.dead)
            {
                Finish();

                return;
            }

            if (Math.Abs(target.Center.X - npc.Center.X) <= stopDistance)
            {
                Stand(npc);
                ctx.Face(target.Center);

                if (Math.Abs(npc.velocity.X) < 0.1f)
                    Finish();

                return;
            }

            BehaviorMovement.WalkTowardX(npc, target.Center.X, SPEED, stopDistance: stopDistance);

            if (!BehaviorMovement.TryJump(npc))
                BehaviorMovement.TryGapJump(npc);

            BehaviorMovement.ApplyTerrainCollision(npc);

            stuck = npc.position.X == npc.oldPosition.X && Math.Abs(npc.velocity.X) < 0.1f ? stuck + 1 : 0;

            if (stuck < STUCK_TICKS && Age + 1 < timeout)
                return;

            Stand(npc);
            ctx.Face(target.Center);
            Finish();
        }
    }

    /// <summary>Stands facing the player for <paramref name="ticks" />, or until <paramref name="released" /> says so.</summary>
    private sealed class Hold(int playerIndex, int ticks, Func<bool>? released) : Behavior
    {
        protected override void Run(BehaviorContext ctx)
        {
            Stand(ctx.NPC);

            if (Main.player[playerIndex] is { active: true } player)
                ctx.Face(player.Center);

            if (released?.Invoke() == true || Age + 1 >= ticks)
                Finish();
        }
    }

    private static void Stand(NPC npc)
    {
        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    #endregion
}
