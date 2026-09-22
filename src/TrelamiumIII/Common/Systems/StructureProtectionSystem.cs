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
    /// <summary>Argie's lab, in tile coordinates.</summary>
    public static Rectangle ArgieLabArea { get; set; }

    /// <summary>
    /// Realm structures such as the Ligneous Fortress and Reverie's hall, in tile coordinates. Unlike
    /// <see cref="ArgieLabArea"/> they get no shield overlay: it restarts the sprite batch per tile, too costly at this size.
    /// </summary>
    public static List<Rectangle> ProtectedAreas { get; } = [];

    public override void OnWorldLoad()
    {
        ArgieLabArea = Rectangle.Empty;
        ProtectedAreas.Clear();
    }

    public override void OnWorldUnload()
    {
        ArgieLabArea = Rectangle.Empty;
        ProtectedAreas.Clear();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["argieLabX"] = ArgieLabArea.X;
        tag["argieLabY"] = ArgieLabArea.Y;
        tag["argieLabWidth"] = ArgieLabArea.Width;
        tag["argieLabHeight"] = ArgieLabArea.Height;

        var fortress = new List<int>(ProtectedAreas.Count * 4);

        foreach (Rectangle area in ProtectedAreas)
            fortress.AddRange([area.X, area.Y, area.Width, area.Height]);

        tag["protectedAreas"] = fortress;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (tag.TryGet("argieLabX", out int x)
         && tag.TryGet("argieLabY", out int y)
         && tag.TryGet("argieLabWidth", out int w)
         && tag.TryGet("argieLabHeight", out int h))
        {
            ArgieLabArea = new Rectangle(x, y, w, h);
        }

        ProtectedAreas.Clear();
        IList<int> fortress = tag.GetList<int>("protectedAreas");

        for (int k = 0; k + 3 < fortress.Count; k += 4)
            ProtectedAreas.Add(new Rectangle(fortress[k], fortress[k + 1], fortress[k + 2], fortress[k + 3]));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(ArgieLabArea.X);
        writer.Write(ArgieLabArea.Y);
        writer.Write(ArgieLabArea.Width);
        writer.Write(ArgieLabArea.Height);

        writer.Write(ProtectedAreas.Count);

        foreach (Rectangle area in ProtectedAreas)
        {
            writer.Write(area.X);
            writer.Write(area.Y);
            writer.Write(area.Width);
            writer.Write(area.Height);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        ArgieLabArea = new Rectangle(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32()
        );

        ProtectedAreas.Clear();
        int count = reader.ReadInt32();

        for (int k = 0; k < count; k++)
            ProtectedAreas.Add(new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
    }

    public static bool IsProtected(int x, int y)
    {
        if (ArgieLabArea != Rectangle.Empty && ArgieLabArea.Contains(x, y))
            return true;

        foreach (Rectangle area in ProtectedAreas)
        {
            if (area.Contains(x, y))
                return true;
        }

        return false;
    }
}