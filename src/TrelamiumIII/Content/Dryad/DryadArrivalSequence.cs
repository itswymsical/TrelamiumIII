using SubworldLibrary;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.Localization;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     One run of the arrival. A single tick clock drives both halves: the world side (server or singleplayer)
///     spawns the Dryad and scripts her walk, and the presentation side (any client that is watching) plays the
///     rift, cues, camera and conversation.
/// </summary>
/// <remarks>
///     The timeline is laid out in bars of <c>Assets/Music/DryadArrival.mp3</c> (104 BPM, 12 bars, 27.7 s),
///     which starts with the cutscene. Change <see cref="BPM" /> and the bar numbers if the track changes.
/// </remarks>
internal sealed class DryadArrivalSequence(Vector2 rift, Vector2 destination)
{
    private const float BPM = 104f;

    private const float BAR_TICKS = 4f * 3600f / BPM;

    /// <summary>The camera reaches the rift.</summary>
    private const int PAN_END = (int)(1.8f * BAR_TICKS + 0.5f);

    /// <summary>First seam of light (bar 3).</summary>
    private const int SEAM = (int)(2f * BAR_TICKS + 0.5f);

    /// <summary>The slit widens with the swell (bar 4) and is fully open at <see cref="OPEN_FULL" />.</summary>
    private const int SWELL = (int)(3f * BAR_TICKS + 0.5f);

    private const int OPEN_FULL = (int)(3.9f * BAR_TICKS + 0.5f);

    /// <summary>The rising eighth-note run before the downbeat.</summary>
    private const int RUN_UP = (int)(4.4f * BAR_TICKS + 0.5f);

    /// <summary>Downbeat of bar 6, the strongest hit in the track: the Dryad appears.</summary>
    private const int EMERGE = (int)(5f * BAR_TICKS + 0.5f);

    /// <summary>Two beats after <see cref="EMERGE" />.</summary>
    private const int WALK_START = EMERGE + (int)(2f * 3600f / BPM + 0.5f);

    private const int FOLLOW_START = WALK_START - 10;

    /// <summary>Downbeat of bar 8, where the track hits again: the rift shuts.</summary>
    private const int CLOSE = (int)(7f * BAR_TICKS + 0.5f);

    private const int CLOSE_TICKS = 26;

    /// <summary>The walk is paced to end here (bar 12), and the scene ends with the track.</summary>
    private const int ARRIVE_BY = (int)(11f * BAR_TICKS + 0.5f);

    private const int SCENE_END = (int)(12f * BAR_TICKS + 0.5f);

    /// <summary>Alpha removed per tick while she fades in.</summary>
    private const int FADE_STEP = 6;

    /// <summary>
    ///     After arriving she holds still, facing the player, until her conversation ends, or this long after the
    ///     run finishes when no conversation reports back (a server never sees one). The cap covers the whole hold.
    /// </summary>
    private const int POST_ARRIVAL_HOLD = 20 * 60;

    private const int HOLD_MAX = 45 * 60;

    /// <summary>Past the scene's end, how long the world side waits for her to arrive, and the client for the stage.</summary>
    private const int ARRIVAL_TIMEOUT = 15 * 60;

    private const int CONVERSATION_TIMEOUT = 30 * 60;

    private const float MIN_SPEED = 0.5f;

    private const float MAX_SPEED = 1.8f;

    /// <summary>Camera push-in, as a multiple of the player's zoom, eased in from the swell to the emergence.</summary>
    private const float PUSH_ZOOM = 1.3f;

    /// <summary>How far above the rift's floor point the camera aims, and how far away a client still takes part.</summary>
    private const float FOCUS_HEIGHT = 70f;

    private const float PRESENT_RANGE = 90f * 16f;

    private int dryadIndex = -1;
    private int holdTicks;
    private bool dryadSpawned;
    private bool dryadArrived;
    private bool worldFinished;
    private bool announced;

    private bool presenting;
    private bool released;
    private bool conversationStarted;
    private bool conversationOver;
    private Vector2 camera;
    private Vector2 cameraStart;

    public Vector2 Rift { get; } = rift;

    public Vector2 Destination { get; } = destination;

    /// <summary>Started from a command or the cutscene command: present on this client whatever the distance or state.</summary>
    public bool Forced { get; init; }

    /// <summary>The stage to fall back to if the run is aborted.</summary>
    public DryadArrivalStage StageBefore { get; init; }

    public int Ticks { get; private set; }

    public bool Finished { get; private set; }

    /// <summary>True while the cutscene proper (the part scored by the track) is playing.</summary>
    public bool InScene => presenting && !released && Ticks < SCENE_END;

    private Vector2 Focus => Rift - new Vector2(0f, FOCUS_HEIGHT);

    public void Tick()
    {
        if (Finished)
            return;

        Ticks++;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            TickWorld();

        if (presenting)
            TickPresentation();

        TryAnnounce();

        if (!presenting && (Main.netMode == NetmodeID.MultiplayerClient || worldFinished))
            Finished = true;
    }

    /// <summary>Drops the run without finishing it: the world side stops and the presentation is released.</summary>
    public void Cancel()
    {
        Finished = true;
        Abandon();
    }

    #region world

    private void TickWorld()
    {
        if (worldFinished)
            return;

        if (Ticks == EMERGE && !dryadSpawned)
            SpawnDryad();

        if (dryadSpawned && DryadNpc() is null)
        {
            Abort();

            return;
        }

        if (Ticks >= SCENE_END && (dryadArrived || Ticks >= SCENE_END + ARRIVAL_TIMEOUT))
            FinishWorld();
    }

    /// <summary>Spawns her at the rift, or borrows the existing one when a replay runs in a world that has her.</summary>
    private void SpawnDryad()
    {
        int index = NPC.FindFirstNPC(NPCID.Dryad);
        bool borrowed = index >= 0;

        if (!borrowed)
            index = NPC.NewNPC(NPC.GetSource_None(), (int)Rift.X, (int)Rift.Y, NPCID.Dryad);

        if (index < 0 || index >= Main.maxNPCs)
        {
            Abort();

            return;
        }

        NPC npc = Main.npc[index];

        npc.position = Rift - new Vector2(npc.width * 0.5f, npc.height);
        npc.velocity = Vector2.Zero;
        npc.direction = npc.spriteDirection = Destination.X >= Rift.X ? 1 : -1;
        npc.alpha = 255;
        npc.dontTakeDamage = true;
        npc.netUpdate = true;

        if (!borrowed)
        {
            npc.homeless = true;
            npc.homeTileX = -1;
            npc.homeTileY = -1;
        }

        dryadIndex = index;
        dryadSpawned = true;

        if (npc.TryEnable(out NPCDirector director))
            director.Play(npc, BuildScript(npc.height));
    }

    private IBehavior BuildScript(int npcHeight)
    {
        float speed = MathHelper.Clamp(MathF.Abs(Destination.X - Rift.X) / (ARRIVE_BY - WALK_START), MIN_SPEED, MAX_SPEED);
        Vector2 target = Destination - new Vector2(0f, npcHeight * 0.5f);

        return Behavior.Sequence(
            Behavior.Until(FadeIn),
            Behavior.Wait(Math.Max(WALK_START - EMERGE - 255 / FADE_STEP, 0)),
            new Stroll(target, speed),
            Behavior.Do(_ => dryadArrived = true),
            Behavior.Until(HoldForConversation).For(HOLD_MAX).FacingNearestPlayer(2000f));
    }

    private static bool FadeIn(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.Decelerate(npc);
        npc.alpha = Math.Max(npc.alpha - FADE_STEP, 0);

        return npc.alpha == 0;
    }

    /// <summary>Stands still until the conversation is over, or <see cref="POST_ARRIVAL_HOLD" /> after the run finished.</summary>
    private bool HoldForConversation(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);
        BehaviorMovement.ApplyTerrainCollision(npc);

        return conversationOver || (worldFinished && ++holdTicks >= POST_ARRIVAL_HOLD);
    }

    private void FinishWorld()
    {
        worldFinished = true;

        if (DryadNpc() is { } npc)
        {
            npc.dontTakeDamage = false;
            npc.alpha = 0;
            npc.netUpdate = true;
        }

        DryadArrivalSystem.Complete();
    }

    private void Abort()
    {
        worldFinished = true;
        Finished = true;

        DryadArrivalSystem.Abort(this);
        Abandon();
    }

    /// <summary>Skips the rest of the walk: she is placed on the destination and the run counts as arrived.</summary>
    private void JumpToEnd()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            if (!dryadSpawned)
                SpawnDryad();

            if (DryadNpc() is { } npc)
            {
                npc.position = Destination - new Vector2(npc.width * 0.5f, npc.height);
                npc.velocity = Vector2.Zero;
                npc.alpha = 0;
                npc.netUpdate = true;
            }

            dryadArrived = true;
        }

        Ticks = Math.Max(Ticks, SCENE_END);
    }

    private NPC? DryadNpc()
    {
        if (dryadIndex < 0)
            return null;

        NPC npc = Main.npc[dryadIndex];

        return npc.active && npc.type == NPCID.Dryad ? npc : null;
    }

    /// <summary>The Dryad on any peer: the one this run spawned, or the first in the world.</summary>
    private NPC? TrackedDryad()
    {
        if (DryadNpc() is { } known)
            return known;

        int index = NPC.FindFirstNPC(NPCID.Dryad);

        return index >= 0 ? Main.npc[index] : null;
    }

    /// <summary>Vanilla's arrival message. Held until the presentation ends, which hides chat while it runs.</summary>
    private void TryAnnounce()
    {
        if (announced || !worldFinished || (Main.netMode == NetmodeID.SinglePlayer && presenting))
            return;

        announced = true;

        if (DryadNpc() is not { } npc)
            return;

        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasArrived", npc.GetFullNetName()), new Color(50, 125, 255));
        else if (Main.netMode == NetmodeID.SinglePlayer)
            Main.NewText(Language.GetTextValue("Announcement.HasArrived", npc.FullName), 50, 125, 255);
    }

    /// <summary>
    ///     Walks to a point at a fixed speed. Unlike <see cref="WalkTo" /> it never leaps gaps, so the pace holds on
    ///     uneven ground; it teleports to the target if it makes no progress for <see cref="STUCK_TICKS" />.
    /// </summary>
    private sealed class Stroll(Vector2 target, float speed) : Behavior
    {
        private const float ARRIVAL = 28f;

        private const int STUCK_TICKS = 240;

        private int stuck;

        protected override void Run(BehaviorContext ctx)
        {
            NPC npc = ctx.NPC;

            BehaviorMovement.WalkTowardX(npc, target.X, speed, stopDistance: ARRIVAL);
            BehaviorMovement.TryJump(npc);
            BehaviorMovement.ApplyTerrainCollision(npc);

            if (Vector2.Distance(npc.Center, target) <= ARRIVAL)
            {
                Finish();

                return;
            }

            stuck = MathF.Abs(npc.velocity.X) < 0.1f ? stuck + 1 : 0;

            if (stuck < STUCK_TICKS)
                return;

            npc.position = target - new Vector2(npc.width * 0.5f, npc.height * 0.5f);
            npc.velocity = Vector2.Zero;
            npc.netUpdate = true;
            Finish();
        }
    }

    #endregion

    #region presentation

    /// <summary>Whether this client takes part: near the rift and free to be interrupted, or forced.</summary>
    public bool ShouldPresent()
    {
        if (Main.dedServ)
            return false;

        return Forced || (DryadArrivalSystem.CanPresent() && Vector2.Distance(Main.LocalPlayer.Center, Rift) <= PRESENT_RANGE);
    }

    public void BeginPresentation()
    {
        presenting = true;
        cameraStart = camera = Main.LocalPlayer.Center;

        Letterbox.Show(duration: 75, heightPercentage: 0.085f);

        if (CutsceneSystem.CurrentCutscene is not DryadArrivalCutscene)
            CutsceneSystem.PlayCutscene<DryadArrivalCutscene>();
    }

    /// <summary>Called when the cutscene shell ends. A skip fast-forwards the run to its last frame.</summary>
    public void OnSceneEnded(bool skipped)
    {
        if (!skipped || !presenting || released)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            Abandon();

            return;
        }

        JumpToEnd();
        DryadRift.Clear();
        StopEffects();
    }

    public void OnConversationEnded()
    {
        if (conversationStarted)
            conversationOver = true;
    }

    private void TickPresentation()
    {
        if (Main.LocalPlayer.dead || SubworldSystem.AnyActive())
        {
            Abandon();

            return;
        }

        if (Ticks < SCENE_END)
        {
            DryadRift.Set(Rift, RiftOpen(), RiftSurge());
            FireCues();
        }

        // Keeps her readable at night once the rift has shut.
        if (Ticks >= EMERGE && !conversationOver && TrackedDryad() is { } dryad)
            Lighting.AddLight(dryad.Center, 0.26f, 0.46f, 0.32f);

        UpdateCamera();
        UpdateConversation();
    }

    private void FireCues()
    {
        Vector2 center = DryadRift.CenterAt(Rift);

        switch (Ticks)
        {
            case SEAM:
                SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.5f, Pitch = 0.25f }, center);
                break;

            case SWELL:
                SoundEngine.PlaySound(Assets.SFX.RiftOpen.Asset with { Volume = 0.45f, Pitch = 0.05f }, center);
                ScreenWobbleSystem.Set(0.08f, 3.5f, 100);
                ZoomHandler.SetZoomAnimation(Main.GameZoomTarget * PUSH_ZOOM, EMERGE - SWELL);
                break;

            case RUN_UP:
                RadialBlurEffect.Set(0.26f, falloff: 1.4f, rSpin: 0.1f, worldFocus: center, rampTicks: 80);
                ChromaticScreenEffect.Set(0.3f, 3.5f, 80);
                break;

            case EMERGE:
                ScreenFlashSystem.Flash(hold: 4, fade: 64);
                StopEffects();
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.25f }, center);
                DryadRift.Burst(24);
                break;

            case CLOSE:
                SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.45f, Pitch = -0.5f }, center);
                DryadRift.Burst(18);
                break;
        }
    }

    /// <summary>0 shut, 0.3 at the first seam, 1 fully open, and back to 0 as the rift shuts.</summary>
    private float RiftOpen()
    {
        if (Ticks < SEAM)
            return 0f;

        if (Ticks < SWELL)
            return 0.3f * Smooth(Progress(SEAM, SWELL));

        if (Ticks < OPEN_FULL)
            return MathHelper.Lerp(0.3f, 1f, Smooth(Progress(SWELL, OPEN_FULL)));

        if (Ticks < CLOSE)
            return 1f;

        return 1f - Smooth(Progress(CLOSE, CLOSE + CLOSE_TICKS));
    }

    /// <summary>A flare around the emergence and a smaller one as the rift shuts.</summary>
    private float RiftSurge() => Bump(EMERGE, 22) + 0.6f * Bump(CLOSE, 14);

    private static void StopEffects()
    {
        RadialBlurEffect.Stop(50);
        ScreenWobbleSystem.Stop(60);
        ChromaticScreenEffect.Stop(50);
    }

    private void UpdateCamera()
    {
        if (conversationOver)
        {
            ReturnCamera();

            return;
        }

        if (Ticks <= PAN_END)
        {
            camera = Vector2.Lerp(cameraStart, Focus, Smooth(Progress(0, PAN_END)));
        }
        else
        {
            Vector2 target = Focus;

            if (TrackedDryad() is { } dryad && Ticks >= FOLLOW_START)
                target = dryad.Center + new Vector2(dryad.velocity.X * 24f, -24f);

            camera = Vector2.Lerp(camera, target, Ticks < FOLLOW_START ? 0.1f : 0.05f);
        }

        CameraSystem.LockCamera(camera);
    }

    private void ReturnCamera()
    {
        Vector2 player = Main.LocalPlayer.Center;

        camera = Vector2.Lerp(camera, player, 0.1f);

        if (Vector2.DistanceSquared(camera, player) < 64f)
            Abandon();
        else
            CameraSystem.LockCamera(camera);
    }

    /// <summary>Opens the conversation once she has arrived and the scene is over, or gives up waiting for her.</summary>
    private void UpdateConversation()
    {
        if (conversationStarted || Ticks < SCENE_END)
            return;

        bool arrived = DryadArrivalSystem.Stage == DryadArrivalStage.Arrived;

        if (!arrived && Ticks < SCENE_END + CONVERSATION_TIMEOUT)
            return;

        conversationStarted = true;

        if (!arrived || !StartConversation())
            conversationOver = true;
    }

    private bool StartConversation() =>
        DialogueManager.Instance.StartDialogue(
            DryadArrivalSystem.GreetingKey,
            zoomIn: true,
            letterbox: true,
            style: DialogueStyle.Projected,
            anchor: DialogueAnchor.OnSpeaker(TrackedDryad()));

    /// <summary>
    ///     Ends this client's part: releases the camera, rift and effects. The letterbox is hidden here only if the
    ///     conversation never took it over, since restarting a hide already in progress snaps the bars back open.
    /// </summary>
    private void Abandon()
    {
        if (released)
            return;

        released = true;
        presenting = false;

        if (Main.dedServ)
            return;

        CameraSystem.UnlockCamera();
        DryadRift.Clear();
        StopEffects();

        // A conversation that zoomed in restores the zoom itself when it ends.
        if (!DialogueManager.Instance.IsZoomedIn)
            ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, 60);

        if (CutsceneSystem.CurrentCutscene is DryadArrivalCutscene { IsPlaying: true })
            CutsceneSystem.EndCurrentCutscene();

        if (!conversationStarted && !DialogueManager.Instance.IsAnyActive())
            Letterbox.Hide();
    }

    #endregion

    private float Progress(int from, int to) => MathHelper.Clamp((Ticks - from) / (float)Math.Max(to - from, 1), 0f, 1f);

    private float Bump(int center, int width)
    {
        float distance = 1f - MathF.Abs(Ticks - center) / width;

        return distance > 0f ? distance * distance : 0f;
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);
}
