using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.Enums;
using TrelamiumIII.Core.ExperientialSystem.Acts;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Events;

/// <summary>A registered job's identity, readable without constructing the job.</summary>
public readonly record struct JobInfo(string NameKey, string DescriptionKey, int ProviderNPC);

/// <summary>
/// The event bus. Owns the job registry, subscribes to the <c>Event*</c> globals, and adds the
/// events vanilla has no hook for -- notably the housing check, reconstructed from
/// <see cref="WorldGen.StartRoomCheck"/>.
/// </summary>
public class EventSystem : ModSystem
{
    private static readonly Dictionary<int, Type> JobTypes = [];

    /// <summary>
    /// What a job is, without building one. Answering "what is job 2 called" used to mean running a
    /// whole <see cref="Job"/> constructor -- task lists, reward lists, and in GoldMine's case a sum
    /// over its pools -- and then throwing the instance away.
    /// </summary>
    private static readonly Dictionary<int, JobInfo> JobMetadata = [];

    private static bool isExploding = false;

    /// <summary>debounce keyed by canonical room id</summary>
    private static readonly Dictionary<Point, int> HousingCooldowns = [];

    /// <summary>Scratch set of the tiles in the room vanilla last flood-filled. Rebuilt per room check.</summary>
    private static readonly HashSet<Point> RoomTiles = [];

    /// <summary>
    /// True while a player-initiated housing check is running.
    /// </summary>
    private static bool isManualHousingCheck;
    private const int CooldownTicks = 60;
    private const int CooldownPruneInterval = 3600;
    private static int _lastCooldownPrune;

    public override void Load()
    {
        RegisterJobs();

        SubscribeToEvents();
        On_WorldGen.StartRoomCheck += HookStartRoomCheck;
        On_WorldGen.MoveTownNPC += HookMoveTownNPC;
        On_Projectile.ExplodeTiles += HookExplodeTiles;
    }

    public override void Unload()
    {
        UnsubscribeFromEvents();

        JobTypes.Clear();
        JobMetadata.Clear();
        JobUnlockRegistry.Clear();
        On_WorldGen.StartRoomCheck -= HookStartRoomCheck;
        On_WorldGen.MoveTownNPC -= HookMoveTownNPC;
        HousingCooldowns.Clear();
        RoomTiles.Clear();
        On_Projectile.ExplodeTiles -= HookExplodeTiles;
    }

    public static IEnumerable<Job> GetAllAvailableJobs(JobPlayer player) => player.AvailableJobs();

    private bool HookStartRoomCheck(On_WorldGen.orig_StartRoomCheck orig, int x, int y, IRoomCheckFeedback feedback)
    {
        // vanilla boundary logic. Flood-fills the room containing (x, y) into WorldGen.roomTiles.
        bool isValid = orig(x, y, feedback);

        // Resolve the room's canonical identity and its residents from the tiles vanilla just filled in.
        // StartRoomCheck is called with whichever tile the check was started from, so keying anything
        // off (x, y) treats every tile of the same house as a distinct house. Falls back to the probe
        // point only when the room can't be resolved.
        Point roomId = TryScanRoom(out Point resolved, out bool isOccupied) ? resolved : new Point(x, y);

        // Structural failures are already spelled out by vanilla's own reason code.
        HousingIssue issue = isValid ? HousingIssue.None : TranslateRoomCheckFailure();

        // furniture and Biome checks
        if (isValid)
        {
            // RoomNeeds/ScoreRoom write WorldGen's shared housing scratch state, which vanilla callers
            // (QuickFindHome, SpawnTownNPC, MoveTownNPC) read back after StartRoomCheck returns.
            // Snapshot it so this probe stays invisible to them.
            HousingScratch scratch = HousingScratch.Capture();

            isValid = WorldGen.RoomNeeds();

            if (!isValid)
            {
                issue = MissingFurniture();
            }
            else
            {
                WorldGen.ScoreRoom(-1, -1);

                // ScoreRoom sets hiScore to -1 and bails out when a resident refuses to share with the
                // NPC type asking. The room is valid if it passes the score check OR bailed for that reason.
                isValid = WorldGen.hiScore > 0 || WorldGen.roomOccupied;
                isOccupied |= WorldGen.roomOccupied;

                if (!isValid)
                    issue = WorldGen.roomEvil ? HousingIssue.Corrupt : HousingIssue.NoValidSpot;
            }

            scratch.Restore();
        }

        // A tenant is the headline problem even when the room itself is spotless.
        if (isOccupied)
            issue = HousingIssue.Occupied;

        if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
            return isValid;

        int currentTick = (int)Main.GameUpdateCount;

        PruneHousingCooldowns(currentTick);

        // Debounce: the housing UI calls StartRoomCheck many times per frame.
        if (HousingCooldowns.TryGetValue(roomId, out int lastTick) && currentTick - lastTick < CooldownTicks)
        {
            return isValid;
        }
        HousingCooldowns[roomId] = currentTick;

        var evt = GameEvent.HousingChecked(x, y, isValid, isOccupied, Main.LocalPlayer, roomId, issue, isManualHousingCheck);
        BroadcastEvent(evt);

        return isValid;
    }

    /// <summary>
    /// Marks everything underneath as player-initiated. <see cref="WorldGen.MoveTownNPC"/> is what the housing UI calls
    /// </summary>
    private bool HookMoveTownNPC(On_WorldGen.orig_MoveTownNPC orig, int x, int y, int n, IRoomCheckFeedback feedback)
    {
        bool wasManual = isManualHousingCheck;
        isManualHousingCheck = true;

        try
        {
            return orig(x, y, n, feedback);
        }
        finally
        {
            isManualHousingCheck = wasManual;
        }
    }

    /// <summary>Maps vanilla's room-check failure reason onto the issues the mod reports back to the player.</summary>
    private static HousingIssue TranslateRoomCheckFailure() => WorldGen.roomCheckFailureReason switch
    {
        TownNPCRoomCheckFailureReason.RoomCheckStartedInASolidTile => HousingIssue.SolidTile,
        TownNPCRoomCheckFailureReason.RoomIsTooSmall => HousingIssue.TooSmall,

        // Both of these mean the flood fill went somewhere it shouldn't have: out through a gap in the
        // walls (an empty doorway counts), or on past the 750-tile budget.
        TownNPCRoomCheckFailureReason.HoleInWallIsTooBig or
        TownNPCRoomCheckFailureReason.RoomIsTooBig or
        TownNPCRoomCheckFailureReason.TooCloseToWorldEdge => HousingIssue.NotEnclosed,

        _ => HousingIssue.NoValidSpot
    };

    /// <summary>
    /// The first thing <see cref="WorldGen.RoomNeeds"/> found missing, in the order a player would build them.
    /// Only meaningful straight after that call, before the scratch state is restored.
    /// </summary>
    private static HousingIssue MissingFurniture()
    {
        if (!WorldGen.roomDoor)
            return HousingIssue.NeedsDoor;

        if (!WorldGen.roomTorch)
            return HousingIssue.NeedsLight;

        if (!WorldGen.roomTable)
            return HousingIssue.NeedsTable;

        if (!WorldGen.roomChair)
            return HousingIssue.NeedsChair;

        return HousingIssue.NoValidSpot;
    }

    /// <summary>
    /// Reads the room vanilla just flood-filled into <see cref="WorldGen.roomTiles"/>, within the
    /// <see cref="WorldGen.roomX1"/>..<see cref="WorldGen.roomY2"/> bounds it grew alongside.
    /// </summary>
    private static bool TryScanRoom(out Point roomId, out bool isOccupied)
    {
        roomId = default;
        isOccupied = false;

        if (WorldGen.numRoomTiles <= 0)
            return false;

        RoomTiles.Clear();

        bool found = false;

        // Column by column, so the first tile found is the leftmost, topmost within its column.
        for (int tx = WorldGen.roomX1; tx <= WorldGen.roomX2; tx++)
        {
            for (int ty = WorldGen.roomY1; ty <= WorldGen.roomY2; ty++)
            {
                if (!WorldGen.Housing_CheckIfInRoom(tx, ty))
                    continue;

                if (!found)
                {
                    roomId = new Point(tx, ty);
                    found = true;
                }

                RoomTiles.Add(new Point(tx, ty));
            }
        }

        if (!found)
            return false;

        isOccupied = RoomHasResident();
        return true;
    }

    /// <summary>
    /// True when any town NPC (town pets included) has its home tile inside <see cref="RoomTiles"/>.
    /// </summary>
    private static bool RoomHasResident()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || !npc.townNPC || npc.homeless)
                continue;

            if (RoomTiles.Contains(new Point(npc.homeTileX, npc.homeTileY)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Snapshot of the WorldGen fields <see cref="WorldGen.RoomNeeds"/> and <see cref="WorldGen.ScoreRoom"/>
    /// write to, so this system can run them as a read-only probe from inside a vanilla room check.
    /// </summary>
    private readonly struct HousingScratch
    {
        private readonly bool canSpawn, roomChair, roomDoor, roomTable, roomTorch, roomOccupied, roomEvil;
        private readonly int hiScore, bestX, bestY, sharedRoomX;

        public HousingScratch()
        {
            canSpawn = WorldGen.canSpawn;
            roomChair = WorldGen.roomChair;
            roomDoor = WorldGen.roomDoor;
            roomTable = WorldGen.roomTable;
            roomTorch = WorldGen.roomTorch;
            roomOccupied = WorldGen.roomOccupied;
            roomEvil = WorldGen.roomEvil;
            hiScore = WorldGen.hiScore;
            bestX = WorldGen.bestX;
            bestY = WorldGen.bestY;
            sharedRoomX = WorldGen.sharedRoomX;
        }

        public static HousingScratch Capture() => new();

        public void Restore()
        {
            WorldGen.canSpawn = canSpawn;
            WorldGen.roomChair = roomChair;
            WorldGen.roomDoor = roomDoor;
            WorldGen.roomTable = roomTable;
            WorldGen.roomTorch = roomTorch;
            WorldGen.roomOccupied = roomOccupied;
            WorldGen.roomEvil = roomEvil;
            WorldGen.hiScore = hiScore;
            WorldGen.bestX = bestX;
            WorldGen.bestY = bestY;
            WorldGen.sharedRoomX = sharedRoomX;
        }
    }

    /// <summary>Drops cooldown entries that can no longer suppress anything, so the dictionary doesn't grow with the world.</summary>
    private static void PruneHousingCooldowns(int currentTick)
    {
        if (currentTick - _lastCooldownPrune < CooldownPruneInterval)
            return;

        _lastCooldownPrune = currentTick;

        var stale = HousingCooldowns
            .Where(kvp => currentTick - kvp.Value >= CooldownTicks)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in stale)
            HousingCooldowns.Remove(key);
    }

    #region Auto-Registration

    private void RegisterJobs()
    {
        var jobTypes = Mod.Code.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Job)));

        foreach (var type in jobTypes)
        {
            try
            {
                // The one instance built at load: its identity is kept as metadata so nothing has to
                // construct another just to read a name.
                var instance = (Job)Activator.CreateInstance(type);

                if (JobTypes.TryGetValue(instance.ID, out var existing))
                {
                    TrelamiumIII.Instance.Logger.Error(
                        $"Job id {instance.ID} is claimed by both {existing.Name} and {type.Name}; ignoring the latter.");

                    continue;
                }

                JobTypes[instance.ID] = type;
                JobMetadata[instance.ID] = new JobInfo(instance.NameKey, instance.DescriptionKey, instance.ProviderNPC);

                // Kept with the metadata for the same reason: a gate has to be answerable for a job no
                // player has an instance of, which is every job before the one that opens it.
                if (instance.UnlockCondition is { } condition)
                    JobUnlockRegistry.Register(new JobUnlockRule(instance.ID, condition, instance.RevokeWhenUnmet));
            }
            catch (Exception ex)
            {
                TrelamiumIII.Instance.Logger.Error($"Failed to register Job {type.Name}: {ex.Message}");
            }
        }

        TrelamiumIII.Instance.Logger.Info($"Auto-registered {JobTypes.Count} Jobs");
    }

    /// <summary>
    /// A fresh, unowned instance of a job. The caller is responsible for giving it a
    /// <see cref="Job.Context"/> before it does anything -- <see cref="JobPlayer.GetOrCreateJob"/> is
    /// the path that does.
    /// </summary>
    public static Job CreateJob(int jobId)
        => JobTypes.TryGetValue(jobId, out var type) ? (Job)Activator.CreateInstance(type) : null;

    /// <summary>A registered job's identity, without building one.</summary>
    public static bool TryGetJobInfo(int jobId, out JobInfo info) => JobMetadata.TryGetValue(jobId, out info);

    public static bool IsJobRegistered(int jobId) => JobTypes.ContainsKey(jobId);

    /// <summary>Every registered job id, in registration order.</summary>
    public static IEnumerable<int> RegisteredJobIds => JobTypes.Keys;
    #endregion

    #region Event Subscriptions
    private void SubscribeToEvents()
    {
        EventTile.OnTileBreak += OnTileBreak;
        EventTile.OnTilePlace += OnTilePlace;
        EventTile.OnTileInteract += OnTileInteract;
        EventNPC.OnNPCKill += OnNPCKill;
        EventNPC.OnNPCHit += OnNPCHit;
        EventNPC.OnNPCChat += OnNPCChat;
        EventItem.OnItemPickup += OnItemPickup;
        EventItem.OnItemCreated += OnItemCraft;
        EventItem.OnItemConsume += OnItemConsume;
        EventPlayer.OnBiomeEnter += OnBiomeEnter;
        EventPlayer.OnCaughtFish += OnFishingCatch;
        EventPlayer.OnFishingAttempt += OnFishingAttempt;

        EventPlayer.OnTakenDamage += OnTakenDamage;
    }


    private void UnsubscribeFromEvents()
    {
        EventTile.OnTileBreak -= OnTileBreak;
        EventTile.OnTilePlace -= OnTilePlace;
        EventTile.OnTileInteract -= OnTileInteract;
        EventNPC.OnNPCKill -= OnNPCKill;
        EventNPC.OnNPCHit -= OnNPCHit;
        EventNPC.OnNPCChat -= OnNPCChat;
        EventItem.OnItemPickup -= OnItemPickup;
        EventItem.OnItemCreated -= OnItemCraft;
        EventItem.OnItemConsume -= OnItemConsume;
        EventPlayer.OnBiomeEnter -= OnBiomeEnter;
        EventPlayer.OnCaughtFish -= OnFishingCatch;
        EventPlayer.OnFishingAttempt -= OnFishingAttempt;
        EventPlayer.OnTakenDamage -= OnTakenDamage;

    }
    #endregion

    #region Event Handlers
    public void OnTileBreak(int i, int j, int type)
    {

        var player = Main.LocalPlayer;

        // Check if this tile break happened during an explosion
        int? explosiveType = isExploding ? 1 : null; // 1 = generic explosive

        var evt = GameEvent.TileBreak(i, j, type, player, explosiveType);
        BroadcastEvent(evt);
    }

    /// <summary>
    /// Hooks into Projectile.ExplodeTiles to track when tiles are being exploded
    /// </summary>
    private void HookExplodeTiles(On_Projectile.orig_ExplodeTiles orig, Projectile self, Vector2 compareSpot, int radius, int minI, int maxI, int minJ, int maxJ, bool wallSplode, bool explodeHardmodeOres)
    {
        isExploding = true;
        orig(self, compareSpot, radius, minI, maxI, minJ, maxJ, wallSplode, explodeHardmodeOres);
        isExploding = false;
    }

    private void OnFishingAttempt(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        var evt = GameEvent.FishingAttempt(attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition, Main.LocalPlayer);
        BroadcastEvent(evt);
    }

    private void OnFishingCatch(Player player, Item fish)
    {
        var evt = GameEvent.CaughtFish(fish.type, player);
        BroadcastEvent(evt);
    }
    private void OnTilePlace(int i, int j, int type)
    {
        var evt = GameEvent.TilePlace(i, j, type, Main.LocalPlayer);
        BroadcastEvent(evt);
    }

    private void OnTileInteract(int i, int j, int type)
    {
        var evt = GameEvent.TileInteract(i, j, type, Main.LocalPlayer);
        BroadcastEvent(evt);
    }

    /// <summary>
    /// Called when an NPC is killed
    /// </summary>
    /// <param name="npc"></param>
    public void OnNPCKill(NPC npc, DamageClass damageClass)
    {
        var player = Main.LocalPlayer;
        var evt = GameEvent.NPCKill(npc.type, player, damageClass);
        BroadcastEvent(evt);
    }
    /// <summary>
    /// Called when an NPC is hit
    /// </summary>
    /// <param name="npc"></param>
    /// <param name="damage"></param>
    public void OnNPCHit(NPC npc, int damage)
    {
        var player = Main.LocalPlayer;
        var evt = GameEvent.NPCHit(npc.type, damage, player);
        BroadcastEvent(evt);
    }

    private void OnTakenDamage(Player player, int damage)
    {
        var evt = GameEvent.TakeDamage(damage, player);
        BroadcastEvent(evt);
    }
    private void OnNPCChat(NPC npc, ref string chat)
    {
        var evt = GameEvent.NPCChat(npc, Main.LocalPlayer);
        BroadcastEvent(evt);
    }

    private void OnItemPickup(Item item, Player player)
    {
        var evt = GameEvent.ItemPickup(item, player);
        BroadcastEvent(evt);
    }

    private void OnItemCraft(Item item, ItemCreationContext context)
    {
        var evt = GameEvent.ItemCraft(item, context, Main.LocalPlayer);
        BroadcastEvent(evt);
    }

    private void OnItemConsume(Item item, Player player)
    {
        var evt = GameEvent.ItemConsume(item, player);
        BroadcastEvent(evt);
    }

    private void OnBiomeEnter(Player player, int timeSpent)
    {
        var evt = GameEvent.BiomeEnter(timeSpent, player);
        BroadcastEvent(evt);
    }


    #endregion

    #region Event Broadcasting
    private static void BroadcastEvent(GameEvent evt)
    {
        if (evt.Player == null)
            return;

        evt.Player.GetModPlayer<JobPlayer>()?.OnGameEvent(evt);
        evt.Player.GetModPlayer<ActTrackerPlayer>()?.OnGameEvent(evt);

        // Checked before describing, not inside Trace: Describe formats a string and resolves content
        // names, so calling it unconditionally paid the full cost of a log line the gate then dropped.
        if (EventLog.Enabled)
            EventLog.Trace(EventLog.Describe(evt));
    }
    #endregion
}