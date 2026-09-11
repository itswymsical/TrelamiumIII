using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs.Housing;

/// <summary>
/// Reserves places in the world for specific NPC types.
///
/// <para>
/// A reservation is by type alone. Vanilla housing still scores the room however it
/// likes, but every path that assigns a home - the housing query, the housing menu,
/// automatic move-in, <see cref="WorldGen.QuickFindHome"/> - runs through
/// <see cref="WorldGen.ScoreRoom"/>, and that is where a reserved room is refused to
/// anyone but its owner. The owner never uses vanilla housing at all: a
/// <see cref="WorldNPC"/> with a home simply anchors to it.
/// </para>
///
/// <para>
/// Reserve from worldgen with <see cref="Reserve{T}(Rectangle, Point?, bool)"/>, or
/// at runtime from a player-built house with <see cref="TryReserveRoomAt{T}"/>.
/// Reservations persist with the world and sync to clients.
/// </para>
/// </summary>
public class WorldNPCHousing : ModSystem
{
    private const string SaveKey = "npcHomes";

    private static readonly List<NPCHome> homes = [];

    public static IReadOnlyList<NPCHome> Homes => homes;

    public override void Load() => On_WorldGen.ScoreRoom += RefuseReservedRooms;

    public override void Unload() => On_WorldGen.ScoreRoom -= RefuseReservedRooms;

    public override void ClearWorld() => homes.Clear();

    // Reservation

    /// <summary>
    /// Reserves <paramref name="area"/> for <typeparamref name="T"/>. Any earlier home
    /// for that type is released. <paramref name="anchor"/> defaults to the row above
    /// the bottom-centre of the area, which for a structure is usually its floor.
    /// </summary>
    public static NPCHome Reserve<T>(Rectangle area, Point? anchor = null, bool spawnsResident = true) where T : ModNPC
        => Reserve(ModContent.NPCType<T>(), area, anchor, spawnsResident);

    public static NPCHome Reserve(int npcType, Rectangle area, Point? anchor = null, bool spawnsResident = true)
    {
        Release(npcType);

        var home = new NPCHome(npcType, area, anchor ?? DefaultAnchor(area), spawnsResident);
        homes.Add(home);
        return home;
    }

    /// <summary>
    /// Runs vanilla's room detection from a tile and reserves whatever room it finds
    /// for <typeparamref name="T"/>. Fails if the tile isn't inside a valid room, or
    /// if the room is already reserved for someone else. The resident stands on the
    /// tile the check started from.
    /// </summary>
    public static bool TryReserveRoomAt<T>(int tileX, int tileY, out NPCHome? home, bool spawnsResident = false) where T : ModNPC
    {
        home = null;

        if (!WorldGen.StartRoomCheck(tileX, tileY))
            return false;

        Rectangle room = TestedRoom();
        int type = ModContent.NPCType<T>();

        if (IsReservedAgainst(room, type))
            return false;

        home = Reserve(type, room, new Point(tileX, tileY), spawnsResident);
        return true;
    }

    public static bool Release(int npcType) => homes.RemoveAll(h => h.OwnerType == npcType) > 0;

    public static bool Release<T>() where T : ModNPC => Release(ModContent.NPCType<T>());

    // Lookup

    public static bool TryGetHome(int npcType, out NPCHome? home)
    {
        home = homes.Find(h => h.OwnerType == npcType);
        return home != null;
    }

    public static NPCHome? HomeAt(int tileX, int tileY) => homes.Find(h => h.Contains(tileX, tileY));

    public static NPCHome? HomeIntersecting(Rectangle area) => homes.Find(h => h.Intersects(area));

    /// <summary>True if <paramref name="area"/> touches a home belonging to someone other than <paramref name="npcType"/>.</summary>
    public static bool IsReservedAgainst(Rectangle area, int npcType)
    {
        NPCHome? home = HomeIntersecting(area);
        return home != null && home.OwnerType != npcType;
    }

    // Residents

    public override void PostWorldGen() => SpawnMissingResidents();

    public override void OnWorldLoad()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            SpawnMissingResidents();
    }

    /// <summary>Spawns the owner of every home that wants one and doesn't have it in the world.</summary>
    public static void SpawnMissingResidents()
    {
        foreach (NPCHome home in homes)
        {
            if (!home.SpawnsResident || home.Resident != null)
                continue;

            // NewNPC takes the bottom-centre of the hitbox, so the resident stands in its anchor tile.
            NPC.NewNPC(NPC.GetSource_None(), home.Anchor.X * 16 + 8, home.Anchor.Y * 16 + 16, home.OwnerType);
        }
    }

    // Vanilla denial

    private static void RefuseReservedRooms(On_WorldGen.orig_ScoreRoom orig, int ignoreNPC, int npcTypeAskingToScoreRoom)
    {
        orig(ignoreNPC, npcTypeAskingToScoreRoom);

        if (homes.Count == 0 || !IsReservedAgainst(TestedRoom(), npcTypeAskingToScoreRoom))
            return;

        // MoveTownNPC reads this as "This housing is already occupied", which is the
        // truth from the player's side: someone else lives here. EventSystem's probe
        // reads the same flags and reports HousingIssue.Occupied.
        WorldGen.hiScore = -1;
        WorldGen.roomOccupied = true;
    }

    /// <summary>The bounds vanilla's last <see cref="WorldGen.StartRoomCheck"/> found.</summary>
    private static Rectangle TestedRoom() => new(
        WorldGen.roomX1,
        WorldGen.roomY1,
        WorldGen.roomX2 - WorldGen.roomX1 + 1,
        WorldGen.roomY2 - WorldGen.roomY1 + 1);

    private static Point DefaultAnchor(Rectangle area) => new(area.Center.X, area.Bottom - 2);

    // Persistence

    public override void SaveWorldData(TagCompound tag)
    {
        var entries = new List<TagCompound>();
        foreach (NPCHome home in homes)
            entries.Add(home.Save());

        tag[SaveKey] = entries;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        homes.Clear();

        if (!tag.ContainsKey(SaveKey))
            return;

        foreach (TagCompound entry in tag.GetList<TagCompound>(SaveKey))
        {
            NPCHome? home = NPCHome.Load(entry);
            if (home != null)
                homes.Add(home);
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(homes.Count);
        foreach (NPCHome home in homes)
        {
            writer.Write(home.OwnerType);
            writer.Write(home.Area.X);
            writer.Write(home.Area.Y);
            writer.Write(home.Area.Width);
            writer.Write(home.Area.Height);
            writer.Write(home.Anchor.X);
            writer.Write(home.Anchor.Y);
            writer.Write(home.SpawnsResident);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        homes.Clear();

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int type = reader.ReadInt32();
            var area = new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            var anchor = new Point(reader.ReadInt32(), reader.ReadInt32());
            bool spawns = reader.ReadBoolean();
            homes.Add(new NPCHome(type, area, anchor, spawns));
        }
    }
}
