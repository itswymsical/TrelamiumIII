using Terraria.DataStructures;

namespace TrelamiumIII.Core.Jobs.Events;

/// <summary>
/// Struct representing an in-game event that can be used for Job/challenge tracking. Contains relevant information about the event such as type, player involved, and specific details depending on the event type (e.g. tile type for tile events, NPC type for NPC events, etc.). This allows for a flexible and extensible way to track a wide variety of events without needing separate hooks for each one.
/// </summary>
public struct GameEvent
{
    public GameEventType Type;
    public Player Player;
    public int Amount;
    public FishingAttempt Attempt;
    // Tile events
    public int? TileType;
    public int? TileX;
    public int? TileY;

    // NPC events
    public int? NPCType;
    public int? Damage;
    public NPC NPC;

    // Item events
    public int? ItemType;
    public Item Item;
    public ItemCreationContext Context;


    // Biome events
    /// <summary>
    /// second counter for biome events.
    /// </summary>
    public int? seconds;

    // Projectile events
    public int? ExplosiveType;
    public int? ProjectileType;
    public DamageClass DamageClass;
    public bool WasCrit;
    public Vector2? NPCPosition;
    public AdvancedPopupRequest PopupRequest;
    public bool IsOccupied;

    // Housing events
    /// <summary>
    /// Canonical identity of the room that was checked, independent of which tile the check
    /// was started from. Null if the room could not be resolved (e.g. the check failed early).
    /// </summary>
    public Point? RoomId;

    /// <summary>What stopped the room from being usable housing. <see cref="HousingIssue.None"/> when it passed.</summary>
    public HousingIssue Issue;

    /// <summary>
    /// True when the player asked for this check from the housing UI, rather than it coming from one of
    /// vanilla's automatic scans (NPCs re-checking their home, the nightly spawn pass). Anything that talks
    /// back to the player should require this.
    /// </summary>
    public bool IsManualCheck;

    public static GameEvent HousingChecked(int x, int y, bool isValid, bool isOccupied, Player player,
        Point? roomId = null, HousingIssue issue = HousingIssue.None, bool isManualCheck = false)
    {
        return new GameEvent
        {
            Type = GameEventType.HousingChecked,
            TileX = x,
            TileY = y,
            Player = player,
            Amount = isValid ? 1 : 0,
            IsOccupied = isOccupied,
            RoomId = roomId,
            Issue = issue,
            IsManualCheck = isManualCheck
        };
    }

    public static GameEvent TileBreak(int i, int j, int type, Player player, int? explosiveType = null)
    {
        return new GameEvent
        {
            Type = GameEventType.TileBreak,
            TileX = i,
            TileY = j,
            TileType = type,
            Player = player,
            Amount = 1,
            ExplosiveType = explosiveType
        };
    }

    public static GameEvent TilePlace(int i, int j, int type, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.TilePlace,
            TileX = i,
            TileY = j,
            TileType = type,
            Player = player,
            Amount = 1
        };
    }
    public static GameEvent FishingAttempt(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.FishingAttempt,
            Player = player,
            Attempt = attempt,
            ItemType = itemDrop,
            NPCType = npcSpawn,
            Amount = 1,
        };
    }
    public static GameEvent CaughtFish(int itemType, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.CaughtFish,
            Player = player,
            ItemType = itemType,
            Amount = 1
        };
    }

    public static GameEvent TileInteract(int i, int j, int type, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.TileInteract,
            TileX = i,
            TileY = j,
            TileType = type,
            Player = player,
            Amount = 1
        };
    }

    public static GameEvent NPCKill(int npcType, Player player, DamageClass damageClass = null)
    {
        return new GameEvent
        {
            Type = GameEventType.NPCKill,
            NPCType = npcType,
            Player = player,
            DamageClass = damageClass ?? DamageClass.Default,
            Amount = 1
        };
    }
    public static GameEvent NPCHit(int npcType, int damage, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.NPCHit,
            NPCType = npcType,
            Damage = damage,
            Player = player,
            Amount = damage
        };
    }
    public static GameEvent TakeDamage(int damage, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.TakeDamage,
            Damage = damage,
            Player = player,
            Amount = damage
        };
    }
    public static GameEvent NPCChat(NPC npc, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.NPCChat,
            NPCType = npc.type,
            NPC = npc,
            Player = player,
            Amount = 1
        };
    }

    public static GameEvent ItemPickup(Item item, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.ItemPickup,
            ItemType = item.type,
            Item = item,
            Player = player,
            Amount = item.stack
        };
    }

    public static GameEvent ItemCraft(Item item, ItemCreationContext context, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.ItemCraft,
            ItemType = item.type,
            Item = item,
            Context = context,
            Player = player,
            Amount = item.stack
        };
    }

    public static GameEvent ItemUse(Item item, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.ItemUse,
            ItemType = item.type,
            Item = item,
            Player = player,
            Amount = 1
        };
    }
    public static GameEvent ItemConsume(Item item, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.ItemConsume,
            ItemType = item.type,
            Item = item,
            Player = player,
            Amount = 1
        };
    }

    public static GameEvent BiomeEnter(int timeSpent, Player player)
    {
        return new GameEvent
        {
            Type = GameEventType.BiomeEnter,
            seconds = timeSpent,
            Player = player,
            Amount = timeSpent
        };
    }

    public static GameEvent NPCDistCheck(Player player, NPC npc, float distance)
    {
        return new GameEvent
        {
            Type = GameEventType.OnCheckDistance,
            Player = player,
            NPC = npc,
            Amount = (int)distance
        };
    }
}
/// <summary>
/// Why a housing check came back unusable, at the granularity an NPC would comment on. Ordered from
/// "there is no room here" to "the room is fine but taken", which is roughly the order the player fixes them in.
/// </summary>
public enum HousingIssue
{
    /// <summary>The room passed every check.</summary>
    None,

    /// <summary>The check was started on a solid block, so there was no room to fill in the first place.</summary>
    SolidTile,

    /// <summary>Under vanilla's 60-tile minimum.</summary>
    TooSmall,

    /// <summary>The flood fill leaked out or ran past its tile budget: a gap in the walls, or a genuinely huge room.</summary>
    NotEnclosed,

    NeedsDoor,
    NeedsLight,
    NeedsTable,
    NeedsChair,

    /// <summary>Too much corruption/crimson counted inside the room.</summary>
    Corrupt,

    /// <summary>Structurally sound and fully furnished, but nothing scored as a spot to stand in.</summary>
    NoValidSpot,

    /// <summary>A town NPC already lives here.</summary>
    Occupied
}

/// <summary>
/// Enum representing different types of in-game events that can be tracked.
/// </summary>
public enum GameEventType
{
    TileBreak,
    TilePlace,
    TileInteract,
    NPCKill,
    NPCHit,
    NPCChat,
    ItemPickup,
    ItemCraft,
    ItemUse,
    ItemConsume,
    BiomeEnter,
    FishingAttempt,
    CaughtFish,
    TakeDamage,
    OnCheckDistance,
    HousingChecked
}