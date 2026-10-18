using System.IO;
using SubworldLibrary;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics.Cinematics;

namespace TrelamiumIII.Content.Dryad;

public enum DryadArrivalStage : byte
{
    /// <summary>The Eye of Cthulhu has not died since this system existed.</summary>
    Dormant,

    /// <summary>The Eye died and the Guide has not explained it yet.</summary>
    Awakened,

    /// <summary>
    ///     The Guide has spoken, and her arrival is queued for the daytime: the morning after the night the Eye died.
    ///     By day she arrives, by the world spawn, as soon as a player is near it; the Guide calls back a player who is
    ///     on the surface somewhere else, and a player underground is left alone until they come up.
    /// </summary>
    Briefed,

    /// <summary>The arrival is running.</summary>
    Arriving,

    /// <summary>She is in the world, and vanilla housing owns her from here.</summary>
    Arrived,
}

/// <summary>
///     Progression state for the Dryad's arrival after the Eye of Cthulhu: the stage, the veto on vanilla's own
///     Dryad spawn, the client watcher that plays the Guide's briefing and asks the server to start the arrival,
///     and the day she spends settling in before her job (<see cref="Jobs.CrossingOver" />) goes on offer.
/// </summary>
/// <remarks>
///     The stage is replicated through the world-data packet. The server owns every transition and the Dryad
///     spawn; clients only present. A world with no saved stage that already has a first boss down or a Dryad
///     is treated as <see cref="DryadArrivalStage.Arrived" />, so the scene never plays over an existing town.
/// </remarks>
public sealed class DryadArrivalSystem : ModSystem, ISharedWorldData
{
    public const string BriefingKey = "DryadArrival.Briefing";

    public const string GreetingKey = "DryadArrival.Greeting";

    public const string ReturnToTownKey = "DryadArrival.ReturnToTown";

    private const string StageKey = "stage";

    private const string SettledKey = "settled";

    private const string HousedForKey = "housedFor";

    /// <summary>Ticks the local player must be free before the briefing, the arrival or the Guide's call.</summary>
    private const int QUIET_TICKS = 90;

    private const int REQUEST_COOLDOWN = 3 * 60;

    /// <summary>How long the Guide waits before calling a player back home again.</summary>
    private const int NUDGE_INTERVAL = 5 * 60 * 60;

    /// <summary>How near the world spawn, in tiles, a player must be for her to arrive: sideways, and above it.</summary>
    private const int SPAWN_RANGE_X = 50;

    private const int SPAWN_RANGE_Y = 45;

    /// <summary>
    ///     How far below the world spawn, in tiles, still counts as near it: a basement or a hillside house counts,
    ///     the caves underneath do not.
    /// </summary>
    private const int SPAWN_DEPTH = 15;

    /// <summary>A full in-game day, dawn to dawn, in ticks at the normal time rate.</summary>
    private const double DAY_TICKS = 86400.0;

    private const double DAY_LENGTH = 54000.0;

    private static bool stageLoaded;
    private static DryadArrivalSequence? sequence;

    private static int quiet;
    private static int requestCooldown;
    private static int nudgeCooldown;
    private static bool briefingPlaying;

    /// <summary>Game time since she first had a home, in ticks at the normal time rate; -1 before then. Server side.</summary>
    private static double housedFor = -1;

    /// <summary>Time of day at the last world tick, dawn being 0; -1 until the first tick of a session.</summary>
    private static double lastTimeOfDay = -1;

    public static DryadArrivalStage Stage { get; private set; }

    /// <summary>She has lived in a house for a full day, so her job is on offer.</summary>
    public static bool Settled { get; private set; }

    /// <summary>True while the scored part of the cutscene is playing on this client.</summary>
    internal static bool SceneRunning => sequence is { InScene: true };

    /// <summary>True from the start of this client's presentation until the camera is handed back.</summary>
    internal static bool Presenting => sequence is { Presenting: true };

    /// <summary>True while a run is underway on this peer, the world side included.</summary>
    internal static bool Running => sequence is { Finished: false };

    public override void Load()
    {
        On_WorldGen.CheckSpecialTownNPCSpawningConditions += VetoVanillaDryad;
        DialogueManager.OnDialogueEnd += OnDialogueEnded;
    }

    public override void Unload()
    {
        On_WorldGen.CheckSpecialTownNPCSpawningConditions -= VetoVanillaDryad;
        DialogueManager.OnDialogueEnd -= OnDialogueEnded;
    }

    /// <summary>
    ///     Vanilla rotates through every type whose <c>townNPCCanSpawn</c> is set and asks this per candidate, so
    ///     refusing the Dryad here lets the next type spawn instead of stalling the queue behind her.
    /// </summary>
    private static bool VetoVanillaDryad(On_WorldGen.orig_CheckSpecialTownNPCSpawningConditions orig, int type) =>
        (type != NPCID.Dryad || Stage == DryadArrivalStage.Arrived) && orig(type);

    #region persistence

    public override void ClearWorld()
    {
        sequence?.Cancel();
        sequence = null;

        Stage = DryadArrivalStage.Dormant;
        Settled = false;
        housedFor = -1;
        lastTimeOfDay = -1;
        stageLoaded = false;
        quiet = 0;
        requestCooldown = 0;
        nudgeCooldown = 0;
        briefingPlaying = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag[StageKey] = (byte)Stage;
        tag[SettledKey] = Settled;
        tag[HousedForKey] = housedFor;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        stageLoaded = tag.ContainsKey(StageKey);
        Stage = stageLoaded ? (DryadArrivalStage)tag.GetByte(StageKey) : DryadArrivalStage.Dormant;
        Settled = tag.GetBool(SettledKey);
        housedFor = tag.TryGet(HousedForKey, out double housed) ? housed : -1;
    }

    /// <summary>Stage and settling only move forward. Another world's run in progress counts as briefed; the housing timer stays this world's.</summary>
    public void MergeWorldData(TagCompound tag)
    {
        var incoming = tag.ContainsKey(StageKey) ? (DryadArrivalStage)tag.GetByte(StageKey) : DryadArrivalStage.Dormant;

        if (incoming == DryadArrivalStage.Arriving)
            incoming = DryadArrivalStage.Briefed;

        if (incoming > Stage)
            Stage = incoming;

        Settled |= tag.GetBool(SettledKey);
    }

    public override void PostWorldLoad()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!stageLoaded && Stage == DryadArrivalStage.Dormant && HasProgressedWithoutStage())
            Stage = DryadArrivalStage.Arrived;

        // A save made mid-run has no sequence to resume: keep her if she made it out, otherwise run it again.
        if (Stage == DryadArrivalStage.Arriving)
            Stage = NPC.AnyNPCs(NPCID.Dryad) ? DryadArrivalStage.Arrived : DryadArrivalStage.Briefed;

        stageLoaded = true;
    }

    public override void OnWorldUnload()
    {
        sequence?.Cancel();
        sequence = null;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)Stage);
        writer.Write(Settled);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Stage = (DryadArrivalStage)reader.ReadByte();
        Settled = reader.ReadBoolean();
    }

    private static bool HasProgressedWithoutStage() =>
        NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3 || NPC.AnyNPCs(NPCID.Dryad);

    #endregion

    #region settling in

    /// <summary>Server or singleplayer: counts her first day in a house, then puts her job on offer.</summary>
    /// <remarks>
    ///     The day counts from when she first had a home, so losing it later does not restart the wait. It adds up
    ///     how far the time of day moves each tick, so a sundial or a night's sleep counts in full.
    /// </remarks>
    public override void PostUpdateWorld()
    {
        if (Settled || Stage != DryadArrivalStage.Arrived || SubworldSystem.AnyActive())
            return;

        double timeOfDay = Main.dayTime ? Main.time : DAY_LENGTH + Main.time;
        double elapsed = lastTimeOfDay < 0 ? 0 : (timeOfDay - lastTimeOfDay + DAY_TICKS) % DAY_TICKS;

        lastTimeOfDay = timeOfDay;

        if (housedFor < 0)
        {
            if (IsHoused())
                housedFor = 0;

            return;
        }

        housedFor += elapsed;

        if (housedFor >= DAY_TICKS)
            SetSettled(true);
    }

    private static bool IsHoused()
    {
        int index = NPC.FindFirstNPC(NPCID.Dryad);

        return index >= 0 && !Main.npc[index].homeless;
    }

    private static void SetSettled(bool settled)
    {
        if (Settled == settled)
            return;

        Settled = settled;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    #endregion

    #region server transitions

    /// <summary>Server or singleplayer, from the Eye of Cthulhu's kill.</summary>
    internal static void OnEyeOfCthulhuKilled()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient && Stage == DryadArrivalStage.Dormant)
            SetStage(DryadArrivalStage.Awakened);
    }

    private static void SetStage(DryadArrivalStage next)
    {
        if (Stage == next)
            return;

        Stage = next;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }

    private static void ServerBriefed()
    {
        if (Stage == DryadArrivalStage.Awakened)
            SetStage(DryadArrivalStage.Briefed);
    }

    /// <param name="requester">The player whose position picks the side of the spawn the rift opens on.</param>
    /// <param name="forced">Skips the stage check. Used by commands and the standalone cutscene.</param>
    private static void ServerBeginArrival(int requester, bool forced)
    {
        if (sequence is not null || (!forced && Stage != DryadArrivalStage.Briefed))
            return;

        Vector2 hint = Main.player[requester].active ? Main.player[requester].Center : new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);

        if (!DryadArrivalSite.TryFind(hint, out Vector2 rift, out Vector2 destination))
        {
            TrelamiumIII.Instance.Logger.Warn("Dryad arrival: no open ground near the spawn or the requesting player.");

            return;
        }

        var started = new DryadArrivalSequence(rift, destination) { Forced = forced, StageBefore = Stage };

        sequence = started;
        SetStage(DryadArrivalStage.Arriving);

        if (Main.netMode == NetmodeID.Server)
        {
            ModPacket packet = TrelamiumIII.Instance.GetPacket();

            packet.Write((byte)TrelamiumIII.MessageType.DryadArrivalStart);
            packet.Write(rift.X);
            packet.Write(rift.Y);
            packet.Write(destination.X);
            packet.Write(destination.Y);
            packet.Write(forced);
            packet.Send();
        }
        else
        {
            started.BeginPresentation();
        }
    }

    /// <summary>The run finished: she is in the world.</summary>
    internal static void Complete() => SetStage(DryadArrivalStage.Arrived);

    /// <summary>The run cannot finish, so put the stage back and let the watcher try again.</summary>
    internal static void Abort(DryadArrivalSequence aborted)
    {
        if (ReferenceEquals(sequence, aborted))
            sequence = null;

        SetStage(aborted.StageBefore == DryadArrivalStage.Arriving ? DryadArrivalStage.Briefed : aborted.StageBefore);
    }

    #endregion

    #region client

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu)
            return;

        if (sequence is { } running)
        {
            running.Tick();

            if (running.Finished && ReferenceEquals(sequence, running))
                sequence = null;
        }

        if (!Main.dedServ)
            Watch();
    }

    /// <summary>
    ///     Waits for a quiet moment on the local client, then plays the briefing. Once briefed, holds the arrival
    ///     until daytime and then checks every tick: the moment the local player is near the world spawn it asks the
    ///     server to start it, and while they are on the surface somewhere else the Guide calls them back.
    ///     Underground, it waits for them to come up.
    /// </summary>
    private static void Watch()
    {
        requestCooldown = Math.Max(requestCooldown - 1, 0);
        nudgeCooldown = Math.Max(nudgeCooldown - 1, 0);

        if (Stage is not (DryadArrivalStage.Awakened or DryadArrivalStage.Briefed) || !CanPresent())
        {
            quiet = 0;

            return;
        }

        if (quiet < QUIET_TICKS)
        {
            quiet++;

            return;
        }

        if (Stage == DryadArrivalStage.Awakened)
        {
            if (!briefingPlaying)
                PlayBriefing();

            return;
        }

        if (!Main.dayTime || sequence is not null || requestCooldown > 0)
            return;

        Player player = Main.LocalPlayer;

        if (NearSpawn(player))
        {
            requestCooldown = REQUEST_COOLDOWN;
            RequestArrival();
        }
        else if (player.ZoneOverworldHeight && nudgeCooldown == 0)
        {
            nudgeCooldown = NUDGE_INTERVAL;
            DialogueManager.Instance.StartDialogue(ReturnToTownKey);
        }
    }

    /// <summary>
    ///     The local player is free to be shown a scene: in the main world, alive, awake, and nothing else on screen or
    ///     in danger. Awake matters because the arrival waits for morning, which is when a sleeper is still in bed.
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

    /// <summary>Near the world spawn, where she arrives, and at its level: above it or a little below, not in the caves under it.</summary>
    private static bool NearSpawn(Player player)
    {
        Point at = player.Center.ToTileCoordinates();

        return Math.Abs(at.X - Main.spawnTileX) <= SPAWN_RANGE_X && at.Y >= Main.spawnTileY - SPAWN_RANGE_Y && at.Y <= Main.spawnTileY + SPAWN_DEPTH;
    }

    private static void PlayBriefing()
    {
        briefingPlaying = DialogueManager.Instance.StartDialogue(BriefingKey);

        if (briefingPlaying)
            return;

        TrelamiumIII.Instance.Logger.Error($"Dryad arrival: '{BriefingKey}' has no lines; skipping the briefing.");
        RequestBriefed();
    }

    /// <summary>Any end of the conversation counts, including a right-click dismissal: the story should not stall on it.</summary>
    private static void OnDialogueEnded(string dialogueKey)
    {
        if (dialogueKey == BriefingKey)
        {
            briefingPlaying = false;
            RequestBriefed();
        }
        else if (dialogueKey == GreetingKey)
        {
            sequence?.OnConversationEnded();
        }
    }

    private static void RequestBriefed()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            SendToServer(TrelamiumIII.MessageType.DryadBriefed);
        else
            ServerBriefed();
    }

    private static void RequestArrival()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            SendToServer(TrelamiumIII.MessageType.DryadArrivalRequest);
        else
            ServerBeginArrival(Main.myPlayer, forced: false);
    }

    private static void SendToServer(TrelamiumIII.MessageType type)
    {
        ModPacket packet = TrelamiumIII.Instance.GetPacket();

        packet.Write((byte)type);
        packet.Send();
    }

    /// <summary>Called by the cutscene shell when it ends.</summary>
    internal static void OnSceneEnded(bool skipped) => sequence?.OnSceneEnded(skipped);

    /// <summary>Called by the cutscene shell when it starts without a run: starts a forced one.</summary>
    internal static void EnsureRunning()
    {
        if (sequence is null && Main.netMode == NetmodeID.SinglePlayer)
            ServerBeginArrival(Main.myPlayer, forced: true);
    }

    #endregion

    #region packets

    internal static void Receive(TrelamiumIII.MessageType type, BinaryReader reader, int whoAmI)
    {
        switch (type)
        {
            case TrelamiumIII.MessageType.DryadBriefed when Main.netMode == NetmodeID.Server:
                ServerBriefed();
                break;

            case TrelamiumIII.MessageType.DryadArrivalRequest when Main.netMode == NetmodeID.Server:
                ServerBeginArrival(whoAmI, forced: false);
                break;

            case TrelamiumIII.MessageType.DryadArrivalStart when Main.netMode == NetmodeID.MultiplayerClient:
                ReceiveStart(reader);
                break;
        }
    }

    private static void ReceiveStart(BinaryReader reader)
    {
        var rift = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        var destination = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        bool forced = reader.ReadBoolean();

        var incoming = new DryadArrivalSequence(rift, destination) { Forced = forced };

        if (!incoming.ShouldPresent())
            return;

        sequence = incoming;
        incoming.BeginPresentation();
    }

    #endregion

    #region dev

    /// <summary>Singleplayer only: starts a forced arrival now.</summary>
    internal static bool DevBegin()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || sequence is not null)
            return false;

        ServerBeginArrival(Main.myPlayer, forced: true);

        return sequence is not null;
    }

    internal static void DevSetStage(DryadArrivalStage next)
    {
        sequence?.Cancel();
        sequence = null;
        briefingPlaying = false;
        quiet = 0;

        SetStage(next);
    }

    /// <summary>Skips her day of settling in: her job goes on offer at the next unlock check.</summary>
    internal static void DevSettle() => SetSettled(true);

    /// <summary>Back to before the Eye died: stage <see cref="DryadArrivalStage.Dormant" /> and no Dryad in the world.</summary>
    internal static void DevReset()
    {
        DevSetStage(DryadArrivalStage.Dormant);
        SetSettled(false);
        housedFor = -1;
        nudgeCooldown = 0;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || npc.type != NPCID.Dryad)
                continue;

            npc.active = false;
            npc.netSkip = -1;
            npc.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: i);
        }
    }

    internal static bool DevPlayBriefing() => Main.netMode == NetmodeID.SinglePlayer && DialogueManager.Instance.StartDialogue(BriefingKey);

    internal static string Describe()
    {
        Point at = Main.LocalPlayer.Center.ToTileCoordinates();
        string run = sequence is null ? "idle" : $"tick {sequence.Ticks}";
        string dryad = NPC.AnyNPCs(NPCID.Dryad) ? IsHoused() ? "housed" : "homeless" : "absent";
        string settled = Settled ? "yes" : housedFor < 0 ? "not housed yet" : $"in {Math.Max(DAY_TICKS - housedFor, 0) / 3600:0.0} min at normal time";
        string where = NearSpawn(Main.LocalPlayer) ? "near spawn" : Main.LocalPlayer.ZoneOverworldHeight ? "on the surface" : "away";
        string waiting = Stage == DryadArrivalStage.Briefed ? Main.dayTime ? ", due now" : ", due in the morning" : "";

        return $"stage {Stage}{waiting}, run {run}, Dryad {dryad}, settled {settled}, spawn tile ({Main.spawnTileX}, {Main.spawnTileY}), "
            + $"player tile ({at.X}, {at.Y}) {where}";
    }

    #endregion
}
