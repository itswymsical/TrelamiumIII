using System.Collections.Generic;
using System.IO;
using Terraria.Utilities;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Generation.Fortress;

/// <summary>
/// Hand-authored room interiors: <c>Assets/Structures/FortressRoom_{Kind}_{Name}.tstruct</c>, exactly
/// <see cref="FortressLayout.RoomWidth"/> x <see cref="FortressLayout.RoomHeight"/>, where Kind is
/// <see cref="FortressRoom.Kind"/> (e.g. <c>FortressRoom_LR_Crushers</c>). Doors and hatches are cut through the
/// surrounding wall by the builder; the template only has to keep its own sockets passable.
/// </summary>
public sealed class FortressRoomTemplates
{
    private const string Folder = "Assets/Structures/";
    private const string Prefix = "FortressRoom_";

    private readonly Dictionary<string, List<string>> byKind = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> unused = new(StringComparer.OrdinalIgnoreCase);

    private FortressRoomTemplates() { }

    /// <summary> Scans the mod's files; templates of the wrong size are logged and skipped. </summary>
    public static FortressRoomTemplates Load()
    {
        var templates = new FortressRoomTemplates();
        Mod mod = TrelamiumIII.Instance;

        foreach (string file in mod.GetFileNames() ?? [])
        {
            if (!file.StartsWith(Folder + Prefix, StringComparison.Ordinal) || !file.EndsWith(StructureFile.Extension, StringComparison.Ordinal))
                continue;

            string path = file[..^StructureFile.Extension.Length];
            string name = Path.GetFileName(path)[Prefix.Length..];
            int split = name.IndexOf('_');

            if (split <= 0)
            {
                mod.Logger.Warn($"{file} has no room kind; name it {Prefix}<Kind>_<Name>.");
                continue;
            }

            StructureData data = StructureLoader.Get(path);

            if (data.Width != FortressLayout.RoomWidth || data.Height != FortressLayout.RoomHeight)
            {
                mod.Logger.Warn($"{file} is {data.Width}x{data.Height}; fortress rooms must be {FortressLayout.RoomWidth}x{FortressLayout.RoomHeight}.");
                continue;
            }

            string kind = name[..split];

            if (!templates.byKind.TryGetValue(kind, out List<string>? paths))
                templates.byKind[kind] = paths = [];

            paths.Add(path);
        }

        return templates;
    }

    /// <summary> A template for <paramref name="kind"/>, preferring ones not picked yet; null when none exist. </summary>
    public string? Pick(string kind, UnifiedRandom random)
    {
        if (!byKind.TryGetValue(kind, out List<string>? all))
            return null;

        if (!unused.TryGetValue(kind, out List<string>? pool) || pool.Count == 0)
            unused[kind] = pool = [.. all];

        int index = random.Next(pool.Count);
        string path = pool[index];
        pool.RemoveAt(index);

        return path;
    }
}
