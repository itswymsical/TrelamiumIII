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

    /// <summary>The Guide has spoken. She arrives once a player is near the town.</summary>
    Briefed,

    /// <summary>The arrival is running.</summary>
    Arriving,

    /// <summary>She is in the world, and vanilla housing owns her from here.</summary>
    Arrived,
}

/// <summary>
///     Progression state for the Dryad's arrival after the Eye of Cthulhu: the stage, the veto on vanilla's own
///     Dryad spawn, and the client watcher that plays the Guide's briefing and asks the server to start the arrival.
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

    private const string StageKey = "stage";

    /// <summary>Ticks the local player must be free before the briefing starts; the arrival adds <see cref="ARRIVAL_DELAY" />.</summary>
    private const int QUIET_TICKS = 90;

    private const int ARRIVAL_DELAY = 6 * 60;

    private const int REQUEST_COOLDOWN = 3 * 60;

    /// <summary>How near the town centre, in tiles, a player must be for the arrival to start.</summary>
    private const int TRIGGER_RANGE_X = 70;

    private const int TRIGGER_RANGE_Y = 45;

    private static bool stageLoaded;
    private static DryadArrivalSequence? sequence;

    private static int quiet;
    private static int requestCooldown;
    private static bool briefingPlaying;

    public static DryadArrivalStage Stage { get; private set; }

    /// <summary>True while the scored part of the cutscene is playing on this client.</summary>
    internal static bool SceneRunning => sequence is { InScene: true };

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
        stageLoaded = false;
        quiet = 0;
        requestCooldown = 0;
        briefingPlaying = false;
    }

    public override void SaveWorldData(TagCompound tag) => tag[StageKey] = (byte)Stage;

    public override void LoadWorldData(TagCompound tag)
    {
        stageLoaded = tag.ContainsKey(StageKey);
        Stage = stageLoaded ? (DryadArrivalStage)tag.GetByte(StageKey) : DryadArrivalStage.Dormant;
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

    public override void NetSend(BinaryWriter writer) => writer.Write((byte)Stage);

    public override void NetReceive(BinaryReader reader) => Stage = (DryadArrivalStage)reader.ReadByte();

    private static bool HasProgressedWithoutStage() =>
        NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3 || NPC.AnyNPCs(NPCID.Dryad);

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

    /// <param name="requester">The player whose position picks the side of the town the rift opens on.</param>
    /// <param name="forced">Skips the stage check. Used by commands and the standalone cutscene.</param>
    private static void ServerBeginArrival(int requester, bool forced)
    {
        if (sequence is not null || (!forced && Stage != DryadArrivalStage.Briefed))
            return;

        Vector2 hint = Main.player[requester].active ? Main.player[requester].Center : new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);

        if (!DryadArrivalSite.TryFind(hint, out Vector2 rift, out Vector2 destination))
        {
            TrelamiumIII.Instance.Logger.Warn("Dryad arrival: no open ground near the town or the requesting player.");

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
    ///     Waits for a quiet moment on the local client, then plays the briefing, and once briefed asks the
    ///     server to start the arrival when a player is near the town.
    /// </summary>
    private static void Watch()
    {
        requestCooldown = Math.Max(requestCooldown - 1, 0);

        if (Stage is not (DryadArrivalStage.Awakened or DryadArrivalStage.Briefed) || !CanPresent())
        {
            quiet = 0;

            return;
        }

        int needed = Stage == DryadArrivalStage.Awakened ? QUIET_TICKS : QUIET_TICKS + ARRIVAL_DELAY;

        if (++quiet < needed)
            return;

        if (Stage == DryadArrivalStage.Awakened)
        {
            if (!briefingPlaying)
                PlayBriefing();
        }
        else if (sequence is null && requestCooldown == 0 && NearTown(Main.LocalPlayer))
        {
            requestCooldown = REQUEST_COOLDOWN;
            RequestArrival();
        }
    }

    /// <summary>The local player is free to be shown a scene: in the main world, alive, and nothing else on screen or in danger.</summary>
    internal static bool CanPresent()
    {
        Player player = Main.LocalPlayer;

        return player.active
            && !player.dead
            && player.talkNPC < 0
            && !Main.mapFullscreen
            && !SubworldSystem.AnyActive()
            && CutsceneSystem.CurrentCutscene is null
            && !EyeOfCthulhuWakeSystem.Active
            && !DialogueManager.Instance.IsAnyActive()
            && !Letterbox.IsActive
            && !NPC.AnyDanger();
    }

    private static bool NearTown(Player player)
    {
        Point town = DryadArrivalSite.TownCenter();
        Point at = player.Center.ToTileCoordinates();

        return Math.Abs(at.X - town.X) <= TRIGGER_RANGE_X && Math.Abs(at.Y - town.Y) <= TRIGGER_RANGE_Y;
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

    /// <summary>Back to before the Eye died: stage <see cref="DryadArrivalStage.Dormant" /> and no Dryad in the world.</summary>
    internal static void DevReset()
    {
        DevSetStage(DryadArrivalStage.Dormant);

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
        Point town = DryadArrivalSite.TownCenter();
        Point at = Main.LocalPlayer.Center.ToTileCoordinates();
        string run = sequence is null ? "idle" : $"tick {sequence.Ticks}";
        string dryad = NPC.AnyNPCs(NPCID.Dryad) ? "in the world" : "absent";

        return $"stage {Stage}, run {run}, Dryad {dryad}, town centre tile ({town.X}, {town.Y}), player tile ({at.X}, {at.Y})";
    }

    #endregion
}
