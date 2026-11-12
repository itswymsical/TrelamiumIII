using System.IO;
using SubworldLibrary;
using Terraria.Audio;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     The Dryad's standing rift between the world and Ingress. In the main world it opens by the world spawn, where
///     she arrived (<see cref="DryadArrivalSite" />), once a player has taken <see cref="CrossingOver" />, and leads
///     into Ingress. In Ingress one stands beside the surface spawn and leads home, putting the player down at the
///     rift by the world spawn. Neither ever closes. Drawn by <see cref="DryadRift" /> and used with a right-click,
///     like <see cref="IngressScar" />.
/// </summary>
/// <remarks>
///     Each world keeps its own rift in its world data. The server opens the main world's; Ingress places its own
///     whenever it generates (<see cref="IngressRealm.OnLoad" />). Clients learn both from the world-data packet.
/// </remarks>
internal sealed class IngressRift : ModSystem
{
    private const string FloorKey = "floor";

    /// <summary>The slit's lean from vertical as <see cref="DryadRift" /> draws it; the top end leans right.</summary>
    private const float TILT = 20f * MathHelper.Pi / 180f;

    /// <summary>Half the slit's length along its axis, and half its hover width, in world pixels.</summary>
    private const float HALF_LENGTH = 90f;

    private const float HOVER_HALF_WIDTH = 26f;

    private const float INTERACT_RANGE = 180f;

    /// <summary>Beyond this from the screen centre the slit is left alone, so it sheds no light or motes off screen.</summary>
    private const float ACTIVE_RANGE = 2000f;

    /// <summary>
    ///     A rift that opens in front of the player: the camera pans over, taking longer the further it has to go,
    ///     then a seam, the swell, and the burst.
    /// </summary>
    private const int PAN_TICKS_MIN = 45;

    private const int PAN_TICKS_MAX = 120;

    /// <summary>World pixels per tick the pan is sized for, before <see cref="PAN_TICKS_MIN" /> and <see cref="PAN_TICKS_MAX" /> clamp it.</summary>
    private const float PAN_SPEED = 24f;

    private const int SWELL_TICK = 60;

    private const int OPEN_TICKS = 150;

    private const int SURGE_TICKS = 22;

    /// <summary>The camera holds on the rift until here, then goes back to the player.</summary>
    private const int HOLD_TICKS = OPEN_TICKS + 40;

    /// <summary>Past this, the rift opening for this client is not worth carrying the camera to.</summary>
    private const float PAN_RANGE = 4800f;

    private const int SETTLED = OPEN_TICKS + SURGE_TICKS;

    private const int REQUEST_COOLDOWN = 3 * 60;

    /// <summary>Ticks since the rift began to open on this client; negative while the camera is still on its way.</summary>
    private static int age = SETTLED;

    private static bool panning;
    private static int panTicks = PAN_TICKS_MIN;
    private static Vector2 camera;
    private static Vector2 cameraStart;

    /// <summary>This client asked the server for the world's rift and has not heard back.</summary>
    private static bool requested;

    private static int requestCooldown;

    /// <summary>The rift's floor point in the current world, or null when this world has none.</summary>
    public static Vector2? Floor { get; private set; }

    /// <summary>Set on leaving Ingress through its rift, so the player arrives at the rift by the world spawn.</summary>
    internal static bool ReturningHome { get; set; }

    private static bool InIngress => SubworldSystem.IsActive<IngressRealm>();

    private static bool IsOpen => age >= OPEN_TICKS;

    private static Vector2 Axis => new(MathF.Sin(TILT), -MathF.Cos(TILT));

    #region persistence

    public override void ClearWorld()
    {
        EndPan();

        Floor = null;
        age = SETTLED;
        requested = false;
        requestCooldown = 0;
    }

    public override void OnWorldUnload() => EndPan();

    public override void SaveWorldData(TagCompound tag)
    {
        if (Floor is { } floor)
            tag[FloorKey] = floor;
    }

    public override void LoadWorldData(TagCompound tag) => Floor = tag.TryGet(FloorKey, out Vector2 floor) ? floor : null;

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(Floor.HasValue);

        if (Floor is { } floor)
            writer.WriteVector2(floor);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Vector2? received = reader.ReadBoolean() ? reader.ReadVector2() : null;
        bool opened = Floor is null && received is not null && !Main.gameMenu;

        Floor = received;

        if (opened)
            BeginOpening(pan: requested);

        if (received is not null)
            requested = false;
    }

    #endregion

    #region opening

    /// <summary>From <see cref="IngressRealm" /> once it has generated: the way home, standing on <paramref name="floorTile" />.</summary>
    internal static void PlaceReturn(Point floorTile) => Place(new Vector2(floorTile.X * 16f + 8f, floorTile.Y * 16f));

    /// <summary>Server or singleplayer: opens the rift by the world spawn, where she arrived.</summary>
    private static void ServerOpen(int requester)
    {
        if (Floor is not null || SubworldSystem.AnyActive())
            return;

        Player player = Main.player[requester];
        Vector2 hint = player.active ? player.Center : new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);

        if (!DryadArrivalSite.TryFind(hint, out Vector2 rift, out _))
        {
            TrelamiumIII.Instance.Logger.Warn("Ingress rift: no open ground near the spawn or the requesting player.");

            return;
        }

        Place(rift);
    }

    /// <summary>A rift placed while playing opens in front of whoever asked; one placed while loading is simply there.</summary>
    private static void Place(Vector2 floor)
    {
        bool opening = Floor is null && !Main.gameMenu;

        Floor = floor;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
        else if (opening)
            BeginOpening(pan: true);
    }

    private static void BeginOpening(bool pan)
    {
        Player player = Main.LocalPlayer;
        float distance = Floor is { } floor ? Vector2.Distance(player.Center, floor) : 0f;

        panning = pan && Floor is not null && distance <= PAN_RANGE;
        panTicks = (int)MathHelper.Clamp(distance / PAN_SPEED, PAN_TICKS_MIN, PAN_TICKS_MAX);
        age = panning ? -panTicks : 0;
        cameraStart = camera = player.Center;
    }

    /// <summary>
    ///     Asks for the world's rift once the local player has taken the Dryad's job and her conversation is over, in
    ///     any world she lives in. A player who took the job in another world gets one here the same way.
    /// </summary>
    private static void WatchJob()
    {
        requestCooldown = Math.Max(requestCooldown - 1, 0);

        if (Floor is not null || requestCooldown > 0 || SubworldSystem.AnyActive() || !NPC.AnyNPCs(NPCID.Dryad))
            return;

        if (Main.LocalPlayer.dead || DialogueManager.Instance.IsAnyActive() || CutsceneSystem.CurrentCutscene is not null)
            return;

        if (Main.LocalPlayer.GetModPlayer<JobPlayer>().StateOf(JobID.CrossingOver) is not (JobState.Active or JobState.Completed))
            return;

        requestCooldown = REQUEST_COOLDOWN;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            requested = true;

            ModPacket packet = TrelamiumIII.Instance.GetPacket();
            packet.Write((byte)TrelamiumIII.MessageType.IngressRiftRequest);
            packet.Send();
        }
        else
        {
            ServerOpen(Main.myPlayer);
        }
    }

    internal static void Receive(int whoAmI)
    {
        if (Main.netMode == NetmodeID.Server)
            ServerOpen(whoAmI);
    }

    #endregion

    #region client

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        WatchJob();

        if (Floor is not { } floor)
            return;

        if (age < SETTLED || panning)
        {
            FireCues(floor);
            age++;
        }

        if (panning)
            UpdatePan(floor);

        Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

        // The arrival drives the same slit while its scene plays.
        if (!DryadArrivalSystem.SceneRunning && Vector2.DistanceSquared(screenCenter, floor) <= ACTIVE_RANGE * ACTIVE_RANGE)
            DryadRift.Set(floor, OpenAmount(), SurgeAmount());
    }

    public override void PostUpdatePlayers()
    {
        if (Main.dedServ || Floor is not { } floor || !IsOpen || panning)
            return;

        Player player = Main.LocalPlayer;
        Vector2 center = DryadRift.CenterAt(floor);

        if (player.dead || player.mouseInterface || Main.mapFullscreen || CutsceneSystem.CurrentCutscene is not null || RealmTransition.HoldingPlayer)
            return;

        if (!Hovered(center, Main.MouseWorld) || Vector2.Distance(player.Center, center) > INTERACT_RANGE + HALF_LENGTH)
            return;

        WardenAltarTile.ShowCursorText(TrelamiumIII.Instance.GetLocalization(InIngress ? "IngressRift.Return" : "IngressRift.Enter").Value);

        if (Main.mouseRight && Main.mouseRightRelease)
        {
            Main.mouseRightRelease = false;
            Travel(center);
        }
    }

    /// <summary>Through the rift: into Ingress from the world, or home from Ingress.</summary>
    private static void Travel(Vector2 center)
    {
        if (!InIngress)
        {
            RealmTransition.Enter<IngressRealm>(center);

            return;
        }

        ReturningHome = true;
        SubworldSystem.Exit();
    }

    private static bool Hovered(Vector2 center, Vector2 point)
    {
        Vector2 offset = point - center;
        float along = Vector2.Dot(offset, Axis);
        float across = Vector2.Dot(offset, new Vector2(-Axis.Y, Axis.X));

        return MathF.Abs(along) <= HALF_LENGTH && MathF.Abs(across) <= HOVER_HALF_WIDTH;
    }

    private static void FireCues(Vector2 floor)
    {
        Vector2 center = DryadRift.CenterAt(floor);

        switch (age)
        {
            case 0:
                SoundEngine.PlaySound(Assets.SFX.Cinematic.RiftOpenAlt.Asset with { Volume = 0.5f, Pitch = 0.25f }, center);
                break;

            case SWELL_TICK:
                SoundEngine.PlaySound(Assets.SFX.Cinematic.RiftOpen.Asset with { Volume = 0.45f, Pitch = 0.05f }, center);
                break;

            case OPEN_TICKS:
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.25f }, center);
                DryadRift.Burst(24);
                break;
        }
    }

    /// <summary>A seam first, then the swell to full: the arrival's rift, quicker.</summary>
    private static float OpenAmount()
    {
        if (age < 0)
            return 0f;

        if (age < SWELL_TICK)
            return 0.3f * Smooth(age / (float)SWELL_TICK);

        if (age < OPEN_TICKS)
            return MathHelper.Lerp(0.3f, 1f, Smooth((age - SWELL_TICK) / (float)(OPEN_TICKS - SWELL_TICK)));

        return 1f;
    }

    /// <summary>The flare as it finishes opening.</summary>
    private static float SurgeAmount()
    {
        int since = age - OPEN_TICKS;

        if (since < 0 || since >= SURGE_TICKS)
            return 0f;

        float fade = 1f - since / (float)SURGE_TICKS;

        return fade * fade;
    }

    /// <summary>Carries the camera to the rift while it opens, then hands it back.</summary>
    private static void UpdatePan(Vector2 floor)
    {
        Player player = Main.LocalPlayer;
        Vector2 focus = DryadRift.CenterAt(floor);

        if (player.dead || CutsceneSystem.CurrentCutscene is not null)
        {
            EndPan();

            return;
        }

        if (age < 0)
        {
            camera = Vector2.Lerp(cameraStart, focus, Smooth(1f + age / (float)panTicks));
        }
        else if (age < HOLD_TICKS)
        {
            camera = focus;
        }
        else
        {
            camera = Vector2.Lerp(camera, player.Center, 0.1f);

            if (Vector2.DistanceSquared(camera, player.Center) < 64f)
            {
                EndPan();

                return;
            }
        }

        CameraSystem.LockCamera(camera);
    }

    private static void EndPan()
    {
        if (!panning)
            return;

        panning = false;

        if (!Main.dedServ)
            CameraSystem.UnlockCamera();
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);

    #endregion

    #region dev

    /// <summary>Singleplayer: opens the world's rift by the spawn now, job or not.</summary>
    internal static bool DevOpen()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || SubworldSystem.AnyActive() || Floor is not null)
            return false;

        ServerOpen(Main.myPlayer);

        return Floor is not null;
    }

    /// <summary>Singleplayer: closes the world's rift. A player with the job has it reopened by the watcher.</summary>
    internal static bool DevClose()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || SubworldSystem.AnyActive() || Floor is null)
            return false;

        EndPan();
        Floor = null;
        age = SETTLED;
        DryadRift.Clear();

        return true;
    }

    internal static string Describe() =>
        Floor is { } floor ? $"rift at tile ({(int)(floor.X / 16f)}, {(int)(floor.Y / 16f)}){(IsOpen ? "" : ", opening")}" : "no rift";

    #endregion
}

/// <summary>Puts a player who left Ingress through its rift down at the rift by the world spawn, rather than at their bed or the spawn point itself.</summary>
public sealed class IngressRiftPlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        if (!IngressRift.ReturningHome)
            return;

        IngressRift.ReturningHome = false;

        if (SubworldSystem.AnyActive() || IngressRift.Floor is not { } floor)
            return;

        Player.position = floor - new Vector2(Player.width * 0.5f, Player.height);
        Player.velocity = Vector2.Zero;
        Player.fallStart = (int)(Player.position.Y / 16f);
    }
}
