using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;
<<<<<<< HEAD
using TrelamiumIII.Core.Residues;
=======
using TrelamiumIII.Core.Tiles.Conditions;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

namespace TrelamiumIII.Core.Tiles.Seals;

/// <summary>
///     Owns the <see cref="SealType" /> registry and the per-tile <see cref="SealData" />: laying seals, reading them, and
///     carrying them through save and multiplayer as runs over the column-major tile index.
/// </summary>
public sealed class SealSystem : ModSystem
{
    private const int RunsPerPacket = 4000;

    private static readonly List<SealType> types = [null!];

    internal enum PacketKind : byte
    {
        Request,
        Runs,
        Impulse,
    }

    private readonly record struct Run(int Start, int Length, byte Index);

    public static IReadOnlyList<SealType> Types => types;

    /// <summary>Whether this world has any seal at all. Lets the per-tile hooks skip their lookups.</summary>
    public static bool Any { get; private set; }

    internal static void Register(SealType type)
    {
        if (types.Count > byte.MaxValue)
            throw new InvalidOperationException($"More than {byte.MaxValue} seal types.");

        type.Index = (byte)types.Count;
        types.Add(type);
    }

    public override void PostSetupContent()
    {
        for (int k = 1; k < types.Count; k++)
<<<<<<< HEAD
            ResidueSystem.AddOverlay(new SealCanvasSource(types[k]));
=======
            TileConditionSystem.AddOverlay(new SealCanvasSource(types[k]));
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }

    public override void Unload()
    {
        types.Clear();
        types.Add(null!);
    }

    public override void ClearWorld() => Any = false;

    /// <summary>The seal at (i, j), or null.</summary>
    public static SealType? TypeAt(int i, int j)
    {
        if (!Any || !WorldGen.InWorld(i, j))
            return null;

        byte index = Main.tile[i, j].Seal();

        return index != 0 && index < types.Count ? types[index] : null;
    }

    public static bool PreventsBreaking(int i, int j) => TypeAt(i, j)?.PreventsBreaking ?? false;

    public static bool PreventsBuilding(int i, int j) => TypeAt(i, j)?.PreventsBuilding ?? false;

    /// <summary>Lays <paramref name="type" /> over every tile position in <paramref name="area" />, or lifts seals when null.</summary>
    public static void Seal(Rectangle area, SealType? type)
    {
        int left = Math.Max(area.Left, 0);
        int top = Math.Max(area.Top, 0);
        int right = Math.Min(area.Right, Main.maxTilesX);
        int bottom = Math.Min(area.Bottom, Main.maxTilesY);
        byte index = type?.Index ?? 0;

        if (right <= left || bottom <= top)
            return;

        for (int x = left; x < right; x++)
        {
            for (int y = top; y < bottom; y++)
                Main.tile[x, y].Seal() = index;
        }

        Any |= index != 0;

        if (Main.netMode != NetmodeID.Server)
            return;

        var runs = new List<Run>(right - left);

        for (int x = left; x < right; x++)
            runs.Add(new Run(x * Main.maxTilesY + top, bottom - top, index));

        SendRuns(runs, -1);
    }

    /// <summary>Every sealed stretch of the column-major tile index.</summary>
    private static List<Run> CollectRuns()
    {
        var runs = new List<Run>();
        int height = Main.maxTilesY;
        int total = Main.maxTilesX * height;
        int start = 0;
        byte current = 0;

        for (int index = 0; index <= total; index++)
        {
            byte value = index < total ? Main.tile[index / height, index % height].Seal() : (byte)0;

            if (value == current)
                continue;

            if (current != 0)
                runs.Add(new Run(start, index - start, current));

            start = index;
            current = value;
        }

        return runs;
    }

    private static void ApplyRun(Run run, byte[]? remap = null)
    {
        int height = Main.maxTilesY;
        int total = Main.maxTilesX * height;
        byte index = remap is null ? run.Index : run.Index < remap.Length ? remap[run.Index] : (byte)0;
        int end = Math.Min(run.Start + run.Length, total);

        if (index >= types.Count)
            index = 0;

        for (int k = Math.Max(run.Start, 0); k < end; k++)
            Main.tile[k / height, k % height].Seal() = index;

        Any |= index != 0;
    }

    #region Save and load

    public override void SaveWorldData(TagCompound tag)
    {
        if (!Any)
            return;

        List<Run> runs = CollectRuns();

        if (runs.Count == 0)
            return;

        tag["types"] = types.Skip(1).Select(type => type.FullName).ToList();
        tag["starts"] = runs.Select(run => run.Start).ToArray();
        tag["lengths"] = runs.Select(run => run.Length).ToArray();
        tag["indices"] = runs.Select(run => run.Index).ToArray();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (!tag.ContainsKey("starts"))
            return;

        // Saved indices name types by position in the save's own list, so a type added or removed since still lines up.
        byte[] remap = [0, .. tag.GetList<string>("types").Select(name => ModContent.TryFind(name, out SealType type) ? type.Index : (byte)0)];
        int[] starts = tag.GetIntArray("starts");
        int[] lengths = tag.GetIntArray("lengths");
        byte[] indices = tag.GetByteArray("indices");

        for (int k = 0; k < starts.Length && k < lengths.Length && k < indices.Length; k++)
            ApplyRun(new Run(starts[k], lengths[k], indices[k]), remap);
    }

    #endregion

    #region Sync

    internal static ModPacket NewPacket(PacketKind kind)
    {
        ModPacket packet = TrelamiumIII.Instance.GetPacket();
        packet.Write((byte)TrelamiumIII.MessageType.Seals);
        packet.Write((byte)kind);
        return packet;
    }

    /// <summary>Client side: asks the server for every seal in the world.</summary>
    internal static void RequestAll()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NewPacket(PacketKind.Request).Send();
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        switch ((PacketKind)reader.ReadByte())
        {
            case PacketKind.Request when Main.netMode == NetmodeID.Server:
                SendRuns(CollectRuns(), whoAmI);
                break;

            case PacketKind.Runs:
                int count = reader.ReadUInt16();

                for (int k = 0; k < count; k++)
                    ApplyRun(new Run(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte()));

                break;

            case PacketKind.Impulse:
                SealField.ReceiveImpulse(reader, whoAmI);
                break;
        }
    }

    private static void SendRuns(List<Run> runs, int toClient)
    {
        foreach (Run[] chunk in runs.Chunk(RunsPerPacket))
        {
            ModPacket packet = NewPacket(PacketKind.Runs);
            packet.Write((ushort)chunk.Length);

            foreach (Run run in chunk)
            {
                packet.Write(run.Start);
                packet.Write(run.Length);
                packet.Write(run.Index);
            }

            packet.Send(toClient);
        }
    }

    #endregion
}

/// <summary>Requests the world's seals when this client's player enters.</summary>
public sealed class SealSyncPlayer : ModPlayer
{
    public override void OnEnterWorld() => SealSystem.RequestAll();
}
