using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Core.Graphics.Vfx;
using TrelamiumIII.Core.Residues;

namespace TrelamiumIII.Content.DevTools.VfxLab.Export;

/// <summary> Where exports land, and what the running game can tell an export about the mod. </summary>
internal static class LabExport
{
    public static bool HasSource => DeveloperConfig.SourceRoot() is not null;

    /// <summary> The source tree, or a folder laid out like it in the save directory, to copy over by hand. </summary>
    public static string Root => DeveloperConfig.SourceRoot() ?? Path.Combine(Main.SavePath, "TrelamiumIII", "VfxLab", "Export");

    public static ExportCatalog Catalog(Mod mod)
    {
        Dictionary<string, ModuleSchema> modules = [];

        foreach (Type type in ModuleCatalog.Types)
        {
            object sample = Activator.CreateInstance(type)!;

            modules[type.Name] = new ModuleSchema(type.Namespace!, ModuleCatalog.Settings(type).Select(field => (field.Name, (float)field.GetValue(sample)!)).ToList());
        }

        Dictionary<string, (string, string)> residues = [];

        foreach (IResidue residue in ResidueSystem.All)
        {
            Type type = residue.GetType();

            residues.TryAdd(residue.Name, (type.Namespace!, type.FullName![(type.Namespace!.Length + 1)..].Replace('+', '.')));
        }

        return new ExportCatalog
        {
            Modules = modules,
            Residues = residues,
            TakenNames = mod.Code.GetTypes().Where(static type => !type.IsNested && !IsExport(type)).Select(static type => type.Name).ToHashSet(StringComparer.Ordinal),
            HasTexture = static key => ModContent.HasAsset(key),
        };
    }

    private static bool IsExport(Type type) => type.IsSubclassOf(typeof(ModularField)) && !type.IsSubclassOf(typeof(LabFieldSlot));
}
