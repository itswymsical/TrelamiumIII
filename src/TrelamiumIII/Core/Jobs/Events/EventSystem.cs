using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Jobs.Events;

/// <summary>
/// Large system responsible for managing Jobs and challenges via events. Auto-registers all Jobs and challenges in the assembly. Broadcasts events for Job/challenge tracking. Also handles some custom events that aren't easily tracked via vanilla hooks (e.g. tile interactions, biome entry).
/// </summary>
public class EventSystem : ModSystem
{
    private static readonly Dictionary<int, Type> JobTypes = [];
    private static bool isExploding = false;

    /// <summary>debounce keyed by canonical room id</summary>
    private static readonly Dictionary<Point, int> HousingCooldowns = [];
    private const int CooldownTicks = 60;
    private const int CooldownPruneInterval = 3600;
    private static int _lastCooldownPrune;
    public override void Load()
    {
        RegisterJobs();

        SubscribeToEvents();
        On_WorldGen.StartRoomCheck += HookStartRoomCheck;
        On_Projectile.ExplodeTiles += HookExplodeTiles;
    }

    public override void Unload()
    {
        UnsubscribeFromEvents();

        JobTypes.Clear();
        On_WorldGen.StartRoomCheck -= HookStartRoomCheck;
        HousingCooldowns.Clear();
        On_Projectile.ExplodeTiles -= HookExplodeTiles;
    }

    public static IEnumerable<Job> GetAllAvailableJobs(JobPlayer player)
    {
        return player.UnlockedJobs();
    }

    private bool HookStartRoomCheck(On_WorldGen.orig_StartRoomCheck orig, int x, int y)
    {
        // vanilla boundary logic
        bool isValid = orig(x, y);

        // furniture and Biome checks
        if (isValid)
        {
            isValid = WorldGen.RoomNeeds(0);

            if (isValid)
            {
                WorldGen.ScoreRoom(-1, -1);

                // ScoreRoom sets hiScore to -1 and pauses if someone lives there.
                // The room is valid if it passes the score check OR is currently occupied.
                isValid = WorldGen.hiScore > 0 || WorldGen.roomOccupied;
            }
        }

        if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
            return isValid;

        int currentTick = (int)Main.GameUpdateCount;

        // Resolve the room's canonical identity. StartRoomCheck is called with whichever tile the
        // check was started from, so keying anything off (x, y) treats every tile of the same house
        // as a distinct house. Falls back to the probe point only when the room can't be resolved.
        Point roomId = TryGetRoomId(out Point resolved) ? resolved : new Point(x, y);

        PruneHousingCooldowns(currentTick);

        // Debounce: the housing UI calls StartRoomCheck many times per frame.
        if (HousingCooldowns.TryGetValue(roomId, out int lastTick) && currentTick - lastTick < CooldownTicks)
        {
            return isValid;
        }
        HousingCooldowns[roomId] = currentTick;

        // occupancy Check
        bool isOccupied = WorldGen.roomOccupied;

        var evt = GameEvent.HousingChecked(x, y, isValid, isOccupied, Main.LocalPlayer, roomId);
        BroadcastEvent(evt);

        return isValid;
    }

    /// <summary>
    /// Derives a stable identity for the room vanilla just flood-filled into
    /// <see cref="WorldGen.roomX"/>/<see cref="WorldGen.roomY"/>, using the lexicographic-minimum
    /// tile in the set. Any tile inside the same room resolves to the same id.
    /// </summary>
    private static bool TryGetRoomId(out Point roomId)
    {
        roomId = default;

        int tileCount = Math.Min(WorldGen.numRoomTiles, WorldGen.roomX.Length);
        if (tileCount <= 0)
            return false;

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < tileCount; i++)
        {
            int tx = WorldGen.roomX[i];
            int ty = WorldGen.roomY[i];

            if (tx < minX || (tx == minX && ty < minY))
            {
                minX = tx;
                minY = ty;
            }
        }

        roomId = new Point(minX, minY);
        return true;
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
        var Jobs = Mod.Code.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Job)));

        foreach (var type in Jobs)
        {
            try
            {
                var instance = (Job)Activator.CreateInstance(type);
                JobTypes[instance.ID] = type;
            }
            catch (Exception ex)
            {
                TrelamiumIII.Instance.Logger.Error($"Failed to register Job {type.Name}: {ex.Message}");
            }
        }

        TrelamiumIII.Instance.Logger.Info($"Auto-registered {JobTypes.Count} Jobs");
    }

    public static Job CreateJob(int JobId)
    {
        if (JobTypes.TryGetValue(JobId, out var type))
        {
            return (Job)Activator.CreateInstance(type);
        }
        return null;
    }

    public static bool IsJobRegistered(int JobId) => JobTypes.ContainsKey(JobId);
    #endregion

    #region Event Subscriptions
    private void SubscribeToEvents()
    {
        EventTile.OnTileBreak += OnTileBreak;
        EventTile.OnTilePlace += OnTilePlace;
        EventTile.OnTileInteract += OnTileInteract;
        EventNPC.OnNPCKill += OnNPCKill;
        EventNPC.OnNPCChat += OnNPCChat;
        EventItem.OnItemPickup += OnItemPickup;
        EventItem.OnItemCreated += OnItemCraft;
        //EventItem.OnItemUse += OnItemUse;
        EventItem.OnItemConsume += OnItemConsume;
        EventPlayer.OnBiomeEnter += OnBiomeEnter;
        EventPlayer.OnCaughtFish += OnFishingCatch;
        EventPlayer.OnFishingAttempt += OnFishingAttempt;

        EventPlayer.OnTakenDamage += OnTakenDamage;
        EventPlayer.OnCheckDistance += OnNPCDistCheck;
    }


    private void UnsubscribeFromEvents()
    {
        EventTile.OnTileBreak -= OnTileBreak;
        EventTile.OnTilePlace -= OnTilePlace;
        EventTile.OnTileInteract -= OnTileInteract;
        EventNPC.OnNPCKill -= OnNPCKill;
        EventNPC.OnNPCChat -= OnNPCChat;
        EventItem.OnItemPickup -= OnItemPickup;
        EventItem.OnItemCreated -= OnItemCraft;
        //EventItem.OnItemUse -= OnItemUse;
        EventItem.OnItemConsume -= OnItemConsume;
        EventPlayer.OnBiomeEnter -= OnBiomeEnter;
        EventPlayer.OnCaughtFish -= OnFishingCatch;
        EventPlayer.OnFishingAttempt -= OnFishingAttempt;
        EventPlayer.OnTakenDamage -= OnTakenDamage;
        EventPlayer.OnCheckDistance -= OnNPCDistCheck;

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
    private void HookExplodeTiles(On_Projectile.orig_ExplodeTiles orig, Projectile self, Vector2 compareSpot, int radius, int minI, int maxI, int minJ, int maxJ, bool wallSplode)
    {
        isExploding = true;
        orig(self, compareSpot, radius, minI, maxI, minJ, maxJ, wallSplode);
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

    private void OnItemUse(Item item, Player player)
    {
        var evt = GameEvent.ItemUse(item, player);
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

    public void OnNPCDistCheck(Player player, NPC npc, float distance)
    {

        var evt = GameEvent.NPCDistCheck(player, npc, distance);
        BroadcastEvent(evt);
    }

    #endregion

    #region Event Broadcasting
    public override void PostUpdateEverything()
    {
        ConditionChecker.Update();
    }

    private static void BroadcastEvent(GameEvent evt)
    {
        if (evt.Player == null) return;


        // Player Jobs (sideline)
        var JobPlayer = evt.Player.GetModPlayer<JobPlayer>();
        JobPlayer?.OnGameEvent(evt);

        ConditionChecker.OnGameEvent(evt);
        if (evt.Type == GameEventType.HousingChecked)
        {
            bool isValid = evt.Amount == 1;
            TrelamiumIII.Instance.Logger.Info
                ($"EventSystem.HousingChecked() at X:{evt.TileX} Y:{evt.TileY} | Valid: {isValid} | Occupied: {evt.IsOccupied} | Player: ({evt.Player.name})");
        }
        else
        {
            TrelamiumIII.Instance.Logger.Info($" EventSystem.{evt.Type}() event triggered by player: ({evt.Player.name})");
        }
    }
    #endregion
}