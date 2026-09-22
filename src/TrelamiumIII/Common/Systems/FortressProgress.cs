using System.Collections.Generic;
using System.IO;
using Terraria.Audio;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Which Ligneous Fortress rooms are solved. Chests inside an unsolved room stay sealed; <see cref="Solve"/> is what a
/// room's puzzle calls once it is answered. Rooms are registered by the fortress generator, in route order.
/// </summary>
public sealed class FortressProgress : ModSystem
{
    private static readonly List<Rectangle> rooms = [];
    private static readonly List<bool> solved = [];

    public static int RoomCount => rooms.Count;

    public override void ClearWorld()
    {
        rooms.Clear();
        solved.Clear();
    }

    /// <summary> Generation only: replaces every room with <paramref name="interiors"/>, all unsolved. </summary>
    public static void Register(IEnumerable<Rectangle> interiors)
    {
        rooms.Clear();
        solved.Clear();

        foreach (Rectangle interior in interiors)
        {
            rooms.Add(interior);
            solved.Add(false);
        }
    }

    public static bool IsSolved(int room) => room >= 0 && room < solved.Count && solved[room];

    /// <summary> Inside an unsolved room. </summary>
    public static bool IsSealed(int i, int j)
    {
        for (int room = 0; room < rooms.Count; room++)
        {
            if (rooms[room].Contains(i, j))
                return !solved[room];
        }

        return false;
    }

    /// <summary>
    /// Server or singleplayer. Unseals the room's chests and syncs the change; the unseal effects play only where this
    /// runs, so multiplayer clients get the state without them.
    /// </summary>
    public static void Solve(int room, bool isSolved = true)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || room < 0 || room >= solved.Count || solved[room] == isSolved)
            return;

        solved[room] = isSolved;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
        else if (isSolved)
            PlayUnsealEffects(rooms[room]);
    }

    private static void PlayUnsealEffects(Rectangle room)
    {
        foreach (Chest? chest in Main.chest)
        {
            if (chest is null || !room.Contains(chest.x, chest.y))
                continue;

            var center = new Vector2(chest.x * 16 + 16, chest.y * 16 + 16);
            SoundEngine.PlaySound(SoundID.Unlock, center);

            for (int k = 0; k < 24; k++)
            {
                Dust dust = Dust.NewDustPerfect(center, DustID.TintableDustLighted, Main.rand.NextVector2Circular(3f, 3f), 0, Color.White, 1.2f);
                dust.noGravity = true;
            }
        }
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (rooms.Count == 0)
            return;

        var data = new List<int>(rooms.Count * 5);

        for (int room = 0; room < rooms.Count; room++)
        {
            Rectangle area = rooms[room];
            data.AddRange([area.X, area.Y, area.Width, area.Height, solved[room] ? 1 : 0]);
        }

        tag["fortressRooms"] = data;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        rooms.Clear();
        solved.Clear();

        IList<int> data = tag.GetList<int>("fortressRooms");

        for (int k = 0; k + 4 < data.Count; k += 5)
        {
            rooms.Add(new Rectangle(data[k], data[k + 1], data[k + 2], data[k + 3]));
            solved.Add(data[k + 4] != 0);
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(rooms.Count);

        for (int room = 0; room < rooms.Count; room++)
        {
            Rectangle area = rooms[room];
            writer.Write(area.X);
            writer.Write(area.Y);
            writer.Write(area.Width);
            writer.Write(area.Height);
            writer.Write(solved[room]);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        rooms.Clear();
        solved.Clear();

        int count = reader.ReadInt32();

        for (int room = 0; room < count; room++)
        {
            rooms.Add(new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
            solved.Add(reader.ReadBoolean());
        }
    }
}
