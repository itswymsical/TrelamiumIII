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

    public override void Load()
    {
        RegisterJobs();

        SubscribeToEvents();

        On_Projectile.ExplodeTiles += HookExplodeTiles;
    }

    public override void Unload()
    {
        UnsubscribeFromEvents();

        JobTypes.Clear();

        On_Projectile.ExplodeTiles -= HookExplodeTiles;
    }

    public static IEnumerable<Job> GetAllAvailableJobs(JobPlayer player)
    {
        return player.UnlockedJobs();
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

        TrelamiumIII.Instance.Logger.Debug($"{evt.Type} event triggered by player: ({evt.Player.name})");
    }
    #endregion
}