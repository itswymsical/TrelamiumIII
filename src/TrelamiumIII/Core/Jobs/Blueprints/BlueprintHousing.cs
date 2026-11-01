using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Localization;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Gives a house built from a <see cref="Blueprint"/> its tenant: houses a town NPC in one of its rooms, bringing them
///     into the world first if they aren't here yet. How a job lands "and then the Merchant moved in".
/// </summary>
/// <remarks>
///     <para>
///         Rooms are found from the blueprint's chairs (<see cref="Blueprint.ChairsAt"/>), since every room that passes
///         a housing check has one, and scored the way vanilla scores housing. A room that isn't fit to live in yet is
///         skipped rather than forced, and if none is, nobody moves.
///     </para>
///     <para>
///         Not <see cref="WorldGen.SpawnTownNPC"/>: that picks whichever NPC vanilla would rather spawn. The server, or
///         singleplayer, does the moving; a client sends the rooms and asks.
///     </para>
/// </remarks>
public static class BlueprintHousing
{
    /// <summary>Houses <paramref name="npcType"/> in the first livable room of <paramref name="blueprint"/>'s site.</summary>
    public static void MoveIn(int npcType, Blueprint blueprint)
    {
        if (blueprint.Site is not { } site)
            return;

        Point[] rooms = blueprint.ChairsAt(site).ToArray();

        if (rooms.Length == 0)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = TrelamiumIII.Instance.GetPacket();
            packet.Write((byte)TrelamiumIII.MessageType.BlueprintMoveIn);
            packet.Write7BitEncodedInt(npcType);
            packet.Write((byte)Math.Min(rooms.Length, byte.MaxValue));

            foreach (Point room in rooms.Take(byte.MaxValue))
            {
                packet.Write((short)room.X);
                packet.Write((short)room.Y);
            }

            packet.Send();
            return;
        }

        Settle(npcType, rooms);
    }

    /// <summary>Server: a client finished a house. Only town NPCs are moved in, or brought in.</summary>
    internal static void Receive(BinaryReader reader)
    {
        int npcType = reader.Read7BitEncodedInt();
        var rooms = new Point[reader.ReadByte()];

        for (int i = 0; i < rooms.Length; i++)
            rooms[i] = new Point(reader.ReadInt16(), reader.ReadInt16());

        if (Main.netMode == NetmodeID.Server && npcType > NPCID.None && npcType < NPCLoader.NPCCount
            && ContentSamples.NpcsByNetId[npcType].townNPC)
        {
            Settle(npcType, rooms);
        }
    }

    private static void Settle(int npcType, IEnumerable<Point> rooms)
    {
        int tenant = NPC.FindFirstNPC(npcType);

        foreach (Point room in rooms)
        {
            if (!WorldGen.InWorld(room.X, room.Y, 10) || !WorldGen.StartRoomCheck(room.X, room.Y) || !WorldGen.RoomNeeds(npcType))
                continue;

            // Ignoring the tenant-to-be, so a room they already live in still scores.
            WorldGen.ScoreRoom(tenant, npcType);

            if (WorldGen.hiScore <= 0)
                continue;

            House(npcType, tenant, WorldGen.bestX, WorldGen.bestY);
            return;
        }
    }

    private static void House(int npcType, int tenant, int x, int y)
    {
        if (tenant < 0)
        {
            tenant = NPC.NewNPC(new EntitySource_SpawnNPC(), x * 16 + 8, y * 16, npcType, 1);

            if (tenant >= Main.maxNPCs)
                return;

            Announce(Main.npc[tenant]);
        }

        Main.npc[tenant].UpdateHomeTileState(homeless: false, x, y);
        Main.npc[tenant].netUpdate = true;
    }

    /// <summary>Vanilla's arrival line, so a job's tenant arrives like any other.</summary>
    private static void Announce(NPC npc)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            Main.NewText(Language.GetTextValue("Announcement.HasArrived", npc.FullName), 50, 125);
        else if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasArrived", npc.GetFullNetName()), new Color(50, 125, 255));
    }
}
