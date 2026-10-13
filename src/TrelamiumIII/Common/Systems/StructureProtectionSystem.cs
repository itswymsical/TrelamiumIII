using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Persists the tile-coordinate rectangles of story structures
/// across save/load and makes them indestructible.
/// </summary>
public class StructureProtectionSystem : ModSystem
{
    /// <summary>Story structures such as Argie's lab and Silvian's house, in tile coordinates. Drawn with the shield overlay.</summary>
    public static List<Rectangle> ShieldedAreas { get; } = [];

    /// <summary>
    /// Realm structures such as the Ligneous Fortress and Reverie's hall, in tile coordinates. Unlike
    /// <see cref="ShieldedAreas"/> they get no shield overlay: it restarts the sprite batch per tile, too costly at this size.
    /// </summary>
    public static List<Rectangle> ProtectedAreas { get; } = [];

    public override void ClearWorld() => Reset();

    public override void OnWorldLoad() => Reset();

    public override void OnWorldUnload() => Reset();

    private static void Reset()
    {
        ShieldedAreas.Clear();
        ProtectedAreas.Clear();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["shieldedAreas"] = Flatten(ShieldedAreas);
        tag["protectedAreas"] = Flatten(ProtectedAreas);
    }

    public override void LoadWorldData(TagCompound tag)
    {
        Unflatten(tag.GetList<int>("shieldedAreas"), ShieldedAreas);
        Unflatten(tag.GetList<int>("protectedAreas"), ProtectedAreas);
    }

    public override void NetSend(BinaryWriter writer)
    {
        Write(writer, ShieldedAreas);
        Write(writer, ProtectedAreas);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Read(reader, ShieldedAreas);
        Read(reader, ProtectedAreas);
    }

    public static bool IsProtected(int x, int y) => IsShielded(x, y) || AnyContains(ProtectedAreas, x, y);

    public static bool IsShielded(int x, int y) => AnyContains(ShieldedAreas, x, y);

    private static bool AnyContains(List<Rectangle> areas, int x, int y)
    {
        foreach (Rectangle area in areas)
        {
            if (area.Contains(x, y))
                return true;
        }

        return false;
    }

    private static List<int> Flatten(List<Rectangle> areas)
    {
        var values = new List<int>(areas.Count * 4);

        foreach (Rectangle area in areas)
            values.AddRange([area.X, area.Y, area.Width, area.Height]);

        return values;
    }

    private static void Unflatten(IList<int> values, List<Rectangle> areas)
    {
        areas.Clear();

        for (int k = 0; k + 3 < values.Count; k += 4)
            areas.Add(new Rectangle(values[k], values[k + 1], values[k + 2], values[k + 3]));
    }

    private static void Write(BinaryWriter writer, List<Rectangle> areas)
    {
        writer.Write(areas.Count);

        foreach (Rectangle area in areas)
        {
            writer.Write(area.X);
            writer.Write(area.Y);
            writer.Write(area.Width);
            writer.Write(area.Height);
        }
    }

    private static void Read(BinaryReader reader, List<Rectangle> areas)
    {
        areas.Clear();
        int count = reader.ReadInt32();

        for (int k = 0; k < count; k++)
            areas.Add(new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
    }
}
