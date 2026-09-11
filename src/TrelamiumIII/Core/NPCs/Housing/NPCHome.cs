using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs.Housing;

/// <summary>
/// A place in the world reserved for one NPC type. Nothing else may be housed in
/// it - vanilla or modded, however well the room scores - and the owner stands at
/// <see cref="Anchor"/> when it has nothing better to do.
/// </summary>
public sealed class NPCHome
{
    /// <summary>The NPC type this home belongs to.</summary>
    public int OwnerType { get; }

    /// <summary>The reserved area, in tile coordinates.</summary>
    public Rectangle Area { get; }

    /// <summary>The tile the resident occupies when idle. Becomes its <see cref="WorldNPC.AnchorTile"/>.</summary>
    public Point Anchor { get; }

    /// <summary>
    /// When true the housing system spawns the owner at <see cref="Anchor"/> after
    /// worldgen and on world load if no NPC of that type is active.
    /// </summary>
    public bool SpawnsResident { get; }

    public NPCHome(int ownerType, Rectangle area, Point anchor, bool spawnsResident = true)
    {
        OwnerType = ownerType;
        Area = area;
        Anchor = anchor;
        SpawnsResident = spawnsResident;
    }

    public bool Contains(int tileX, int tileY) => Area.Contains(tileX, tileY);

    public bool Intersects(Rectangle other) => Area.Intersects(other);

    /// <summary>The active resident, or null if the owner isn't in the world right now.</summary>
    public NPC? Resident
    {
        get
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == OwnerType)
                    return npc;
            }

            return null;
        }
    }

    // Persistence. The owner is stored by identity rather than by runtime type id,
    // which shifts whenever the mod list changes.

    internal TagCompound Save() => new()
    {
        ["owner"] = TypeKey(OwnerType),
        ["x"] = Area.X,
        ["y"] = Area.Y,
        ["w"] = Area.Width,
        ["h"] = Area.Height,
        ["ax"] = Anchor.X,
        ["ay"] = Anchor.Y,
        ["spawns"] = SpawnsResident,
    };

    internal static NPCHome? Load(TagCompound tag)
    {
        if (!TryResolveType(tag.GetString("owner"), out int type))
            return null;

        return new NPCHome(
            type,
            new Rectangle(tag.GetInt("x"), tag.GetInt("y"), tag.GetInt("w"), tag.GetInt("h")),
            new Point(tag.GetInt("ax"), tag.GetInt("ay")),
            !tag.ContainsKey("spawns") || tag.GetBool("spawns"));
    }

    internal static string TypeKey(int type)
    {
        ModNPC? modded = NPCLoader.GetNPC(type);
        return modded == null ? $"Terraria:{type}" : $"{modded.Mod.Name}:{modded.Name}";
    }

    internal static bool TryResolveType(string key, out int type)
    {
        type = -1;

        int split = key.IndexOf(':');
        if (split < 0)
            return false;

        string owner = key[..split];
        string name = key[(split + 1)..];

        if (owner == "Terraria")
            return int.TryParse(name, out type);

        if (!ModLoader.TryGetMod(owner, out Mod mod) || !mod.TryFind(name, out ModNPC modded))
            return false;

        type = modded.Type;
        return true;
    }
}
