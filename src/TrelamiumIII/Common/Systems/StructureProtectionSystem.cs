using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Persists the tile-coordinate rectangles of story structures
/// across save/load and makes them indestructible.
/// </summary>
public class StructureProtectionSystem : ModSystem
{
    /// <summary>
    /// Rectangle of Stumpy's cabin in tile coordinates.
    /// </summary>
    public static Rectangle ArgieLabArea { get; set; }

    public override void OnWorldLoad()
    {
        ArgieLabArea = Rectangle.Empty;
    }

    public override void OnWorldUnload()
    {
        ArgieLabArea = Rectangle.Empty;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["argieLabX"] = ArgieLabArea.X;
        tag["argieLabY"] = ArgieLabArea.Y;
        tag["argieLabWidth"] = ArgieLabArea.Width;
        tag["argieLabHeight"] = ArgieLabArea.Height;
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
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(ArgieLabArea.X);
        writer.Write(ArgieLabArea.Y);
        writer.Write(ArgieLabArea.Width);
        writer.Write(ArgieLabArea.Height);
    }

    public override void NetReceive(BinaryReader reader)
    {
        ArgieLabArea = new Rectangle(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32()
        );
    }

    /// <summary>
    /// Returns true if the given tile coordinate falls inside any protected structure area.
    /// </summary>
    public static bool IsProtected(int x, int y)
    {
        if (ArgieLabArea != Rectangle.Empty && ArgieLabArea.Contains(x, y))
            return true;

        // Add future protected areas here (temple, etc.)
        return false;
    }
}