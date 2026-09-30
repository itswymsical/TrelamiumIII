using System.IO;
using SubworldLibrary;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Content.NPCs.Silvian;

/// <summary>
///     Meeting Silvian. The first time a player comes within sight of him, the scene in
///     <see cref="SilvianEncounterScene" /> plays: he notices them, walks over, and the greeting that offers his first job
///     plays out.
/// </summary>
/// <remarks>
///     <para>
///         Each player meets him once (<see cref="SilvianPlayer.Met" />), since the job the greeting unlocks is theirs.
///         Starting the greeting by any route counts, so right-clicking him before the scene triggers skips it for good
///         (<see cref="Silvian.GetEncounter" />).
///     </para>
///     <para>
///         The watcher and the presentation are client side. In multiplayer the client asks the server to walk him over
///         too, since the server's copy of him is the one that counts, and tells it when the conversation is over so it
///         lets him go home.
///     </para>
/// </remarks>
public sealed class SilvianEncounter : ModSystem
{
    public const string GreetingKey = "Silvian.Greeting";

    /// <summary>Offers his first job again, for a player who cut the greeting short.</summary>
    public const string JobOfferKey = "Silvian.JobOffer";

    /// <summary>How near him, in world pixels, a player must be for him to notice them: sideways, and up or down.</summary>
    private const float REACH_X = 20f * 16f;

    private const float REACH_Y = 10f * 16f;

    /// <summary>Ticks the local player must be free, and in sight of him, before the scene starts.</summary>
    private const int QUIET_TICKS = 30;

    /// <summary>After a scene is dropped, how long before he can be met again.</summary>
    private const int RETRY_COOLDOWN = 10 * 60;

    /// <summary>How far ahead of the player, in tiles, the dev command puts him when he is out of sight.</summary>
    private const int DEV_PLACE_TILES = 14;

    private static SilvianEncounterScene? scene;
    private static int quiet;
    private static int cooldown;

    /// <summary>Server: the client meeting him reported the conversation over, so he may go home.</summary>
    private static bool serverReleased;

    /// <summary>True from the start of the scene until he speaks.</summary>
    internal static bool SceneRunning => scene is { InScene: true };

    public override void Load() => DialogueManager.OnDialogueStart += OnDialogueStarted;

    public override void Unload() => DialogueManager.OnDialogueStart -= OnDialogueStarted;

    public override void ClearWorld() => Reset();

    public override void OnWorldUnload() => Reset();

    private static void Reset()
    {
        scene?.Cancel();
        scene = null;
        quiet = 0;
        cooldown = 0;
        serverReleased = false;
    }

    /// <summary>The greeting opening by any route is the meeting.</summary>
    private static void OnDialogueStarted(string dialogueKey)
    {
        if (dialogueKey == GreetingKey && !Main.dedServ)
            Main.LocalPlayer.GetModPlayer<SilvianPlayer>().Met = true;
    }

    #region client

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || Main.dedServ)
            return;

        if (scene is { } running)
        {
            running.Tick();

            if (running.Finished && ReferenceEquals(scene, running))
                scene = null;
        }

        Watch();
    }

    /// <summary>Waits for a player who has never met him to be free and in sight of him, then starts the scene.</summary>
    private static void Watch()
    {
        cooldown = Math.Max(cooldown - 1, 0);

        if (scene is not null || cooldown > 0 || !CanPresent())
        {
            quiet = 0;

            return;
        }

        Player player = Main.LocalPlayer;

        if (player.GetModPlayer<SilvianPlayer>().Met || FindSilvian() is not { } silvian || !InSight(player, silvian))
        {
            quiet = 0;

            return;
        }

        if (++quiet < QUIET_TICKS)
            return;

        quiet = 0;
        Begin(silvian);
    }

    /// <summary>
    ///     The local player is free to be shown a scene: in the main world, alive, awake, not talking to anyone, nothing
    ///     else on screen, and no boss or invasion about.
    /// </summary>
    internal static bool CanPresent()
    {
        Player player = Main.LocalPlayer;

        return player.active
            && !player.dead
            && !player.sleeping.isSleeping
            && player.talkNPC < 0
            && !Main.mapFullscreen
            && !SubworldSystem.AnyActive()
            && CutsceneSystem.CurrentCutscene is null
            && !EyeOfCthulhuWakeSystem.Active
            && !DialogueManager.Instance.IsAnyActive()
            && !Letterbox.IsActive
            && !NPC.AnyDanger();
    }

    /// <summary>Near him and able to see him: a wall between them, his cabin's included, hides him.</summary>
    private static bool InSight(Player player, NPC silvian) =>
        Math.Abs(silvian.Center.X - player.Center.X) <= REACH_X
        && Math.Abs(silvian.Center.Y - player.Center.Y) <= REACH_Y
        && Collision.CanHitLine(player.position, player.width, player.height, silvian.position, silvian.width, silvian.height);

    private static NPC? FindSilvian()
    {
        int index = NPC.FindFirstNPC(ModContent.NPCType<Silvian>());

        return index >= 0 ? Main.npc[index] : null;
    }

    private static void Begin(NPC silvian)
    {
        var started = new SilvianEncounterScene(silvian);

        scene = started;
        started.Begin();
    }

    /// <summary>Called by the scene when it is dropped before he spoke.</summary>
    internal static void OnAbandoned() => cooldown = RETRY_COOLDOWN;

    /// <summary>Called by the cutscene shell when it ends.</summary>
    internal static void OnSceneEnded(bool skipped) => scene?.OnSceneEnded(skipped);

    /// <summary>Called by the cutscene shell when it starts without a scene, as <c>/startc</c> does: starts one.</summary>
    internal static void EnsureRunning()
    {
        if (scene is null && Main.netMode == NetmodeID.SinglePlayer)
            DevMeet();
    }

    /// <summary>
    ///     Somewhere to stand in column <paramref name="tileX" />: ground with three open tiles above it, the one nearest
    ///     the row <paramref name="feetTileY" /> the player's feet are in. Nearest rather than first from the top, so a roof
    ///     overhead is never taken for the floor.
    /// </summary>
    internal static bool TryFindGround(int tileX, int feetTileY, out Vector2 bottom)
    {
        const int SEARCH = 12;

        bottom = default;

        if (tileX < 10 || tileX > Main.maxTilesX - 10)
            return false;

        for (int step = 0; step <= SEARCH * 2; step++)
        {
            // 0, +1, -1, +2, -2, ...: the standing row nearest the player's own.
            int y = feetTileY - 1 + (step % 2 == 0 ? step / 2 : -(step + 1) / 2);

            if (y < 10 || y > Main.maxTilesY - 10)
                continue;

            if (!WorldGen.SolidTile(tileX, y + 1)
                || WorldGen.SolidTile(tileX, y) || WorldGen.SolidTile(tileX, y - 1) || WorldGen.SolidTile(tileX, y - 2))
                continue;

            bottom = new Vector2(tileX * 16f + 8f, (y + 1) * 16f);

            return true;
        }

        return false;
    }

    #endregion

    #region packets

    /// <summary>Multiplayer client: asks the server to walk him over to this player.</summary>
    internal static void SendApproach() => Send(approach: true);

    /// <summary>Multiplayer client: tells the server the meeting is over, so he may go home.</summary>
    internal static void SendRelease() => Send(approach: false);

    private static void Send(bool approach)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = TrelamiumIII.Instance.GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.SilvianEncounter);
        packet.Write(approach);
        packet.Send();
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        bool approach = reader.ReadBoolean();

        if (Main.netMode != NetmodeID.Server)
            return;

        if (!approach)
        {
            serverReleased = true;

            return;
        }

        if (FindSilvian() is not { } silvian || !silvian.TryEnable(out NPCDirector director))
            return;

        serverReleased = false;
        director.Interrupt(silvian, SilvianEncounterScene.BuildScript(whoAmI, () => serverReleased, null));
    }

    #endregion

    #region dev

    /// <summary>
    ///     Singleplayer: plays the scene now, whether or not the player has met him. Out of sight, he is first put on the
    ///     ground a little ahead of the player, or spawned there if the world has no Silvian.
    /// </summary>
    internal static string DevMeet()
    {
        if (Main.netMode != NetmodeID.SinglePlayer)
            return "Only available in singleplayer.";

        if (scene is not null)
            return "The scene is already playing.";

        Player player = Main.LocalPlayer;
        NPC? silvian = FindSilvian();

        if (silvian is null || !InSight(player, silvian))
        {
            int tileX = (int)(player.Center.X / 16f) + player.direction * DEV_PLACE_TILES;

            if (!TryFindGround(tileX, (int)(player.Bottom.Y / 16f), out Vector2 bottom))
                return "No open ground ahead of you to put him on. Turn around, or find flatter ground.";

            if (silvian is null)
            {
                int index = NPC.NewNPC(NPC.GetSource_None(), (int)bottom.X, (int)bottom.Y, ModContent.NPCType<Silvian>());

                if (index >= Main.maxNPCs)
                    return "Could not spawn him.";

                silvian = Main.npc[index];
            }

            silvian.Bottom = bottom;
            silvian.velocity = Vector2.Zero;
            silvian.netUpdate = true;
        }

        Begin(silvian);

        return "Scene started.";
    }

    internal static string Describe()
    {
        Player player = Main.LocalPlayer;
        var state = player.GetModPlayer<SilvianPlayer>();
        NPC? silvian = FindSilvian();

        string where = silvian is null
            ? "not in the world"
            : $"at tile ({(int)(silvian.Center.X / 16f)}, {(int)(silvian.Center.Y / 16f)}), "
            + $"{Vector2.Distance(player.Center, silvian.Center) / 16f:0} tiles away{(InSight(player, silvian) ? ", in sight" : "")}";

        string running = scene is null ? "idle" : $"tick {scene.Ticks}{(scene.InScene ? "" : ", talking")}";

        return $"met {(state.Met ? "yes" : "no")}, first job {state.JobStateOf(JobID.SilvianBotany1)}, "
            + $"almanac {(state.AlmanacInherited ? "inherited" : SilvianAlmanac.IsDue(player) ? "due" : "not yet")}, "
            + $"Silvian {where}, scene {running}";
    }

    #endregion
}
