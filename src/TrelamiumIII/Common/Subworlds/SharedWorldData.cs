using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SubworldLibrary;
using Terraria.GameContent.Events;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Subworlds;

/// <summary>
/// Marks a <see cref="ModSystem"/> whose <c>SaveWorldData</c>/<c>LoadWorldData</c> state travels with the player between
/// the main world and every realm. Only for world-wide progression; tile positions belong to one world.
/// </summary>
public interface ISharedWorldData
{
    /// <summary>
    /// Folds another server's <c>SaveWorldData</c> output into this world's state in multiplayer, where both may have
    /// moved on since they last agreed. Keep it monotonic (OR flags, max counters, union sets) or progress flips back.
    /// </summary>
    void MergeWorldData(TagCompound tag);
}

/// <summary>
/// Carries world progression across every world change: <see cref="ISharedWorldData"/> systems are saved in the world
/// being left and loaded over the destination's own data, and vanilla <c>downed</c> flags are OR-merged.
/// In multiplayer, where SubworldLibrary copies only at sub-server start, servers also exchange snapshots whenever the
/// data changes and merge them through <see cref="ISharedWorldData.MergeWorldData"/>.
/// </summary>
public sealed class SharedWorldData : ModSystem, ICopyWorldData
{
    private const string Key = "TrelamiumIII.SharedWorldData";
    private const string VanillaKey = "Vanilla";

    /// <summary>
    /// The main world's evil. Not progression, but every realm has to agree with it: Ingress grows a patch of the same
    /// one, and a realm generates before it could otherwise know which.
    /// </summary>
    private const string CrimsonKey = "Crimson";

    /// <summary>Ticks between change checks on a server.</summary>
    private const int SyncInterval = 60;

    /// <summary>SubworldLibrary frames link packets with a 16-bit length.</summary>
    private const int MaxPacketBytes = ushort.MaxValue - 16;

    private static FieldInfo[] vanillaFlags = [];

    /// <summary>The last snapshot this server sent across the sub-server link, compressed.</summary>
    private static byte[]? lastSent;

    public override void Load()
    {
        vanillaFlags =
        [
            .. typeof(NPC).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("downed", StringComparison.Ordinal)),
            .. typeof(DD2Event).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("DownedInvasion", StringComparison.Ordinal))
        ];
    }

    public override void Unload() => vanillaFlags = [];

    public override void ClearWorld() => lastSent = null;

    /// <summary>Leaving the main world, or leaving a realm for another realm (ignored then: the realm copied first).</summary>
    void ICopyWorldData.CopyMainWorldData() => Copy();

    /// <summary>Before a realm generates, or after a saved realm loads its own file.</summary>
    void ICopyWorldData.ReadCopiedMainWorldData() => Read();

    /// <summary>Only inside SubworldLibrary's copy hooks. The first copy under the key wins.</summary>
    internal static void Copy() => SubworldSystem.CopyWorldData(Key, BuildTag());

    /// <summary>Only inside SubworldLibrary's read hooks, after the destination loaded or cleared its own data.</summary>
    internal static void Read()
    {
        TagCompound? tag = SubworldSystem.ReadCopiedWorldData<TagCompound>(Key);

        if (tag is null || !tag.ContainsKey(VanillaKey))
            return;

        TagCompound vanilla = tag.GetCompound(VanillaKey);

        foreach (FieldInfo field in vanillaFlags)
        {
            if (vanilla.ContainsKey(field.Name))
                field.SetValue(null, true);
        }

        if (tag.TryGet(CrimsonKey, out bool crimson))
            WorldGen.crimson = crimson;

        foreach (ModSystem system in Participants())
        {
            if (tag.TryGet(system.FullName, out TagCompound data))
                system.LoadWorldData(data);
        }
    }

    private static TagCompound BuildTag()
    {
        var tag = new TagCompound();
        var vanilla = new TagCompound();

        foreach (FieldInfo field in vanillaFlags)
        {
            if ((bool)field.GetValue(null)!)
                vanilla[field.Name] = true;
        }

        tag[VanillaKey] = vanilla;
        tag[CrimsonKey] = WorldGen.crimson;

        foreach (ModSystem system in Participants())
        {
            var data = new TagCompound();
            system.SaveWorldData(data);
            tag[system.FullName] = data;
        }

        return tag;
    }

    /// <summary>A sub-server sends to the main server; the main server sends to every running sub-server.</summary>
    public override void PostUpdateEverything()
    {
        if (Main.netMode != NetmodeID.Server || Main.GameUpdateCount % SyncInterval != 0)
            return;

        using var body = new MemoryStream();
        TagIO.ToStream(BuildTag(), body);
        byte[] snapshot = body.ToArray();

        if (lastSent is not null && snapshot.AsSpan().SequenceEqual(lastSent))
            return;

        if (snapshot.Length > MaxPacketBytes)
        {
            Mod.Logger.Warn($"Shared world data is {snapshot.Length} bytes, over the sub-server link limit; not synced.");
            return;
        }

        lastSent = snapshot;

        // Length-prefixed: the gzip reader buffers ahead and must not run past this packet.
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)TrelamiumIII.MessageType.SharedWorldData);
        writer.Write(snapshot.Length);
        writer.Write(snapshot);

        byte[] packet = stream.ToArray();

        if (SubworldSystem.AnyActive())
            SubworldSystem.SendToMainServer(Mod, packet);
        else
            SubworldSystem.SendToAllSubservers(Mod, packet);
    }

    /// <summary>Merges a snapshot from the other side of the sub-server link and syncs the result to this server's clients.</summary>
    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        byte[] snapshot = reader.ReadBytes(reader.ReadInt32());

        // SubworldLibrary delivers link packets as 256; anything else came from a client.
        if (Main.netMode != NetmodeID.Server || whoAmI != 256)
            return;

        TagCompound tag = TagIO.FromStream(new MemoryStream(snapshot));
        TagCompound vanilla = tag.GetCompound(VanillaKey);

        foreach (FieldInfo field in vanillaFlags)
        {
            if (vanilla.ContainsKey(field.Name))
                field.SetValue(null, true);
        }

        foreach (ModSystem system in Participants())
        {
            if (tag.TryGet(system.FullName, out TagCompound data))
                ((ISharedWorldData)system).MergeWorldData(data);
        }

        NetMessage.SendData(MessageID.WorldData);
    }

    private static IEnumerable<ModSystem> Participants() => ModContent.GetContent<ModSystem>().Where(system => system is ISharedWorldData);
}
