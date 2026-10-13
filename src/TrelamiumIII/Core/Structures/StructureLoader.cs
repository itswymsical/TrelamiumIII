using System.Collections.Generic;
using System.IO;

namespace TrelamiumIII.Core.Structures;

/// <summary>
///     Structures packaged with the mod, parsed once per load. Paths are relative to the mod root without the
///     extension, e.g. <c>"Assets/Structures/SilviansHouseV2"</c>; a <c>.shstruct</c> is read when no <c>.tstruct</c> exists.
/// </summary>
public sealed class StructureLoader : ModSystem
{
    private static readonly Dictionary<string, StructureData> Cache = [];

    /// <summary>Shared between callers; never modify the result.</summary>
    public static StructureData Get(string path)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(path, out StructureData? data))
                Cache[path] = data = Load(path);

            return data;
        }
    }

    public override void Unload()
    {
        lock (Cache)
            Cache.Clear();
    }

    private static StructureData Load(string path)
    {
        Mod mod = TrelamiumIII.Instance;
        string file = path + StructureFile.Extension;

        if (mod.FileExists(file))
        {
            using Stream stream = mod.GetFileStream(file);
            return StructureFile.Read(stream, file);
        }

        string legacy = path + StructureHelperReader.Extension;

        if (!mod.FileExists(legacy))
            throw new FileNotFoundException($"The mod has no structure at {file}.", file);

        using Stream legacyStream = mod.GetFileStream(legacy);
        StructureData data = StructureHelperReader.Read(legacyStream, legacy, out List<string> warnings);

        if (warnings.Count > 0)
            mod.Logger.Warn($"{legacy} lost content in conversion: {string.Join(", ", warnings)}");

        return data;
    }
}
