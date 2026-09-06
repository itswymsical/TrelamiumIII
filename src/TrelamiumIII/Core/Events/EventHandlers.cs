using TrelamiumIII.Utilities;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.UI.Jobs.Board;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Core.Events;

/// <summary>
/// Handles item-based events. Such as item creation, pickup, update (in inventory or equipped), consumption, and use. Broadcasts these events for Job/challenge tracking. Also handles some custom logic for item progress updates and Job indicators on NPCs that provide Jobs.
/// </summary>
public class EventItem : GlobalItem
{
    public delegate void ItemCreatedHandler(Item item, ItemCreationContext context);
    public delegate void ItemPickupHandler(Item item, Player player);
    public delegate void ItemUpdateHandler(Item item, Player player);
    public delegate void ItemConsumeHandler(Item item, Player player);
    public delegate void ItemUseHandler(Item item, Player player);

    public static event ItemCreatedHandler OnItemCreated;
    public static event ItemPickupHandler OnItemPickup;
    public static event ItemUpdateHandler OnItemUpdate;
    public static event ItemConsumeHandler OnItemConsume;
    public static event ItemUseHandler OnItemUse;

    public override void OnCreated(Item item, ItemCreationContext context)
    {
        OnItemCreated?.Invoke(item, context);
        base.OnCreated(item, context);
    }

    public override bool OnPickup(Item item, Player player)
    {
        // Every ongoing job gets to see the pickup; whether it counts is the job's own call.
        // Filtering here by item identity used to swallow them: world item slots reuse the same
        // Item instance forever, so the second copper ore to land in a given slot looked like the
        // first one being picked up twice and never reached the job.
        if (player.GetModPlayer<JobPlayer>().ActiveJobs().Any())
        {
            OnItemPickup?.Invoke(item, player);
        }

        return base.OnPickup(item, player);
    }

    public override bool? UseItem(Item item, Player player)
    {
        OnItemUse?.Invoke(item, player);
        return base.UseItem(item, player);
    }
    public override bool ConsumeItem(Item item, Player player)
    {
        OnItemConsume?.Invoke(item, player);
        return base.ConsumeItem(item, player);
    }
    public override void RightClick(Item item, Player player)
    {
        if (ItemID.Sets.OpenableBag[item.type])
        {
            OnItemConsume?.Invoke(item, player);
        }
        base.RightClick(item, player);
    }
    public override void UpdateEquip(Item item, Player player)
    {
        base.UpdateEquip(item, player);

        var jobPlayer = player.GetModPlayer<JobPlayer>();
        bool itemContributed = false;

        foreach (var job in jobPlayer.ActiveJobs())
        {
            if (JobUtils.CanItemContributeToJob(item, job))
            {
                itemContributed = true;
            }
        }

        if (itemContributed)
        {
            OnItemUpdate?.Invoke(item, player);
        }
    }

    public override void UpdateInventory(Item item, Player player)
    {
        base.UpdateInventory(item, player);

        var jobPlayer = player.GetModPlayer<JobPlayer>();
        bool itemContributed = false;

        foreach (var job in jobPlayer.ActiveJobs())
        {
            if (JobUtils.CanItemContributeToJob(item, job))
            {
                itemContributed = true;
            }
        }

        if (itemContributed)
        {
            OnItemUpdate?.Invoke(item, player);
        }
    }

    public override void OnConsumeItem(Item item, Player player)
    {
        base.OnConsumeItem(item, player);
        OnItemConsume?.Invoke(item, player);
    }
}
/// <summary>
/// Handles NPC-based events. Such as chat, hit, kill, spawn, and catch. Broadcasts these events for Job/challenge tracking. Also handles Job indicators on NPCs that provide Jobs.
/// </summary>
public class EventNPC : GlobalNPC
{
    public delegate void NPCChatHandler(NPC npc, ref string chat);
    public delegate void NPCKillHandler(NPC npc, DamageClass damage);
    public delegate void NPCHitHandler(NPC npc, int damage);
    public delegate void NPCSpawnHandler(NPC npc);
    public delegate void NPCCatchHandler(NPC npc, Player player, Item item, bool failed);

    public static event NPCChatHandler OnNPCChat;
    public static event NPCKillHandler OnNPCKill;
    public static event NPCHitHandler OnNPCHit;
    public static event NPCSpawnHandler OnNPCSpawn;
    public static event NPCCatchHandler OnNPCCatch;

    public override bool InstancePerEntity => true;
    private DamageClass lastDamageClass = DamageClass.Default;

    // Cache: NPC type -> every Job that NPC is offering, rebuilt every CacheRefreshInterval ticks.
    // A list rather than a single Job: an NPC may have several jobs going at once, and the board
    // lets the player pick between them.
    private static readonly Dictionary<int, List<Job>> JobsByNPCType = [];
    private static int lastCacheRefreshTick = -1;
    private const int CacheRefreshInterval = 30; // every half second

    /// <summary>This NPC's own marker, if it is offering anything. Owned here so nothing has to
    /// keep a side table of which NPC has which indicator.</summary>
    private JobIndicator? jobIndicator;

    /// <summary>
    /// Forces the Job indicator cache to rebuild on the next AI tick.
    /// Call this when Jobs are accepted, completed, or unlocked.
    /// </summary>
    public static void InvalidateJobCache() => lastCacheRefreshTick = -1;

    public override bool? CanChat(NPC npc)
    {
        return (npc.friendly && !npc.CountsAsACritter || npc.CanBeTalkedTo || npc.townNPC) /*&& !DialogueManager.Instance.IsAnyActive()*/;
    }

    public override void GetChat(NPC npc, ref string chat)
    {
        base.GetChat(npc, ref chat);
        OnNPCChat?.Invoke(npc, ref chat);
    }

    public override void AI(NPC npc)
    {
        base.AI(npc);

        if (!npc.friendly)
            return;

        int currentTick = (int)Main.GameUpdateCount;

        if (currentTick - lastCacheRefreshTick >= CacheRefreshInterval)
        {
            lastCacheRefreshTick = currentTick;
            RebuildJobCache();
        }

        UpdateJobIndicator(npc);
    }

    /// <summary>
    /// Keeps this NPC's marker in step with whether it still has work to offer. The indicator is
    /// held on the NPC's own instance, so it lives and dies with the NPC and nothing has to track
    /// which marker belongs to which whoAmI.
    /// </summary>
    private void UpdateJobIndicator(NPC npc)
    {
        if (Main.dedServ)
            return;

        // The indicator retires itself once its jobs are gone; drop the stale reference so a
        // later unlock can put a fresh one up.
        if (jobIndicator is { IsActive: false })
            jobIndicator = null;

        bool hasJobs = npc.active && JobsByNPCType.ContainsKey(npc.type);

        if (!hasJobs)
        {
            if (jobIndicator != null)
            {
                jobIndicator.IsActive = false;
                jobIndicator = null;
            }

            return;
        }

        if (jobIndicator != null)
            return;

        jobIndicator = JobIndicator.TryCreate(npc);

        if (jobIndicator != null)
            WorldUISystem.Register(jobIndicator);
    }

    private static void RebuildJobCache()
    {
        JobsByNPCType.Clear();
        var jobPlayer = Main.LocalPlayer.GetModPlayer<JobPlayer>();

        // Grouped rather than first-one-wins: an NPC offering three jobs should surface all three.
        foreach (var job in EventSystem.GetAllAvailableJobs(jobPlayer))
        {
            if (job.Progress != JobProgress.Inactive)
                continue;

            if (!JobsByNPCType.TryGetValue(job.ProviderNPC, out var jobs))
                JobsByNPCType[job.ProviderNPC] = jobs = [];

            jobs.Add(job);
        }
    }

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitByItem(npc, player, item, hit, damageDone);

        lastDamageClass = item.DamageType;

        OnNPCHit?.Invoke(npc, damageDone);
    }

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitByProjectile(npc, projectile, hit, damageDone);

        lastDamageClass = projectile.DamageType;

        OnNPCHit?.Invoke(npc, damageDone);
    }

    public override void OnKill(NPC npc)
    {
        base.OnKill(npc);

        if (!npc.immortal)
        {
            OnNPCKill?.Invoke(npc, lastDamageClass);
        }
    }

    public override void OnCaughtBy(NPC npc, Player player, Item item, bool failed)
    {
        base.OnCaughtBy(npc, player, item, failed);
        OnNPCCatch?.Invoke(npc, player, item, failed);
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        base.OnSpawn(npc, source);
        OnNPCSpawn?.Invoke(npc);
    }
}
/// <summary>
/// Handles tile-based events. Such as break, place, contact (stepping on), and interact (right-click). Broadcasts these events for Job/challenge tracking. Also handles some custom logic for tile interactions that may be relevant for Jobs or challenges.
/// </summary>
public class EventTile : GlobalTile
{
    public delegate void TileBreakHandler(int i, int j, int type);
    public delegate void TilePlaceHandler(int i, int j, int type);
    public delegate void TileContactHandler(int type, Player player);
    public delegate void TileInteractHandler(int i, int j, int type);

    public static event TileBreakHandler OnTileBreak;
    public static event TilePlaceHandler OnTilePlace;
    public static event TileContactHandler OnTileContact;
    public static event TileInteractHandler OnTileInteract;

    public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (Main.gameMenu)
            return;

        base.KillTile(i, j, type, ref fail, ref effectOnly, ref noItem);
        if (!fail && !effectOnly)
        {
            OnTileBreak?.Invoke(i, j, type);
        }
    }

    public override void PlaceInWorld(int i, int j, int type, Item item)
    {
        if (Main.gameMenu)
            return;

        OnTilePlace?.Invoke(i, j, type);

        base.PlaceInWorld(i, j, type, item);
    }

    public override void FloorVisuals(int type, Player player)
    {
        if (Main.gameMenu)
            return;

        OnTileContact?.Invoke(type, player);

        base.FloorVisuals(type, player);
    }

    public override void RightClick(int i, int j, int type)
    {
        if (Main.gameMenu)
            return;

        OnTileInteract?.Invoke(i, j, type);

        base.RightClick(i, j, type);
    }

    // use later for clentaminator Job or purification challenges?
    public override void OnTileConverted(int i, int j, int fromType, int toType, int conversionType)
    {
        if (Main.gameMenu)
            return;

        base.OnTileConverted(i, j, fromType, toType, conversionType);
    }


    // i think this is for blockswap. not sure if it will be useful but might as well have it ready
    public override void ReplaceTile(int i, int j, int type, int targetType, int targetStyle)
    {
        base.ReplaceTile(i, j, type, targetType, targetStyle);
    }
}
/// <summary>
/// Handles player-based events. Such as entering biomes, taking damage, using items, and interacting with NPCs. Broadcasts these events for Job/challenge tracking. Also handles some custom logic for tracking time spent in biomes for biome-related Jobs or challenges.
/// </summary>
public class EventPlayer : ModPlayer
{
    public delegate void BiomeEnterHandler(Player player, int timeSpent);
    public static event BiomeEnterHandler OnBiomeEnter;

    public delegate void NPCDistanceHandler(Player player, NPC npc, float distance);
    public static event NPCDistanceHandler OnCheckDistance;

    public delegate void TakenDamageHandler(Player player, int damage);
    public static event TakenDamageHandler OnTakenDamage;

    public delegate void FishingAttemptHandler(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition);
    public static event FishingAttemptHandler OnFishingAttempt;

    public delegate void FishingCatchHandler(Player player, Item fish);
    public static event FishingCatchHandler OnCaughtFish;

    private int continuousTime;
    private (BitsByte z1, BitsByte z2, BitsByte z3, BitsByte z4, BitsByte z5) lastZoneState;
    private bool hasFiredThisZone;

    private const int CheckInterval = 60; // once per second

    public override void PostUpdate()
    {
        base.PostUpdate();
        TrackBiomeTime();
    }
    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        OnFishingAttempt?.Invoke(attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition);
        base.CatchFish(attempt, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition);
    }

    public override void ModifyCaughtFish(Item fish)
    {
        OnCaughtFish?.Invoke(Player, fish);
        base.ModifyCaughtFish(fish);
    }

    // for later use
    public override void OnConsumeAmmo(Item weapon, Item ammo)
    {
        base.OnConsumeAmmo(weapon, ammo);
    }

    // for later use
    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        base.Kill(damage, hitDirection, pvp, damageSource);
    }

    private void TrackBiomeTime()
    {
        var current = (Player.zone1, Player.zone2, Player.zone3, Player.zone4, Player.zone5);

        if (current != lastZoneState)
        {
            continuousTime = 0;
            hasFiredThisZone = false;
            lastZoneState = current;
            return;
        }

        continuousTime++;

        if (!hasFiredThisZone && continuousTime % CheckInterval == 0)
        {
            OnBiomeEnter?.Invoke(Player, continuousTime);
        }
    }

    public int GetContinuousTime() => continuousTime;

    public override void UpdateEquips()
    {
        base.UpdateEquips();
    }
    public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo)
    {
        base.OnHitByNPC(npc, hurtInfo);
        OnTakenDamage?.Invoke(Player, hurtInfo.Damage);
    }
    public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo)
    {
        base.OnHitByProjectile(proj, hurtInfo);
        OnTakenDamage?.Invoke(Player, hurtInfo.Damage);
    }
}

/// <summary>
/// Handles projectile-based events. Such as hitting NPCs, colliding with tiles, and exploding. Broadcasts these events for Job/challenge tracking. Also handles some custom logic for tracking projectile damage classes for Jobs or challenges that require using specific types of weapons.
/// </summary>
public class EventProjectile : GlobalProjectile
{
    public delegate void ProjectileHitNPCHandler(Projectile projectile, NPC target, int damage, bool crit);
    public delegate void ProjectileTileCollideHandler(Projectile projectile, int tileX, int tileY);

    public static event ProjectileHitNPCHandler OnProjectileHitNPC;
    public static event ProjectileTileCollideHandler OnProjectileTileCollide;

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(projectile, target, hit, damageDone);
        OnProjectileHitNPC?.Invoke(projectile, target, damageDone, hit.Crit);
    }

    public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
    {
        var tileX = (int)(projectile.position.X / 16);
        var tileY = (int)(projectile.position.Y / 16);
        OnProjectileTileCollide?.Invoke(projectile, tileX, tileY);
        return base.OnTileCollide(projectile, oldVelocity);
    }

    // Helper to get projectile damage class
    public static DamageClass GetProjectileDamageClass(Projectile projectile)
    {
        return projectile.DamageType;
    }

    public override void OnKill(Projectile projectile, int timeLeft)
    {
        base.OnKill(projectile, timeLeft);
    }
}