using System.Collections.Generic;
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
public interface ISharedWorldData;

/// <summary>
/// Carries world progression across every world change: <see cref="ISharedWorldData"/> systems are saved in the world
/// being left and loaded over the destination's own data, and vanilla <c>downed</c> flags are OR-merged.
/// SubworldLibrary only copies at subserver start in multiplayer, so this is singleplayer-complete only.
/// </summary>
public sealed class SharedWorldData : ModSystem, ICopyWorldData
{
    private const string Key = "TrelamiumIII.SharedWorldData";
    private const string VanillaKey = "Vanilla";

    private static FieldInfo[] vanillaFlags = [];

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

    /// <summary>Leaving the main world, or leaving a realm for another realm (ignored then: the realm copied first).</summary>
    void ICopyWorldData.CopyMainWorldData() => Copy();

    /// <summary>Before a realm generates, or after a saved realm loads its own file.</summary>
    void ICopyWorldData.ReadCopiedMainWorldData() => Read();

    /// <summary>Only inside SubworldLibrary's copy hooks. The first copy under the key wins.</summary>
    internal static void Copy()
    {
        var tag = new TagCompound();
        var vanilla = new TagCompound();

        foreach (FieldInfo field in vanillaFlags)
        {
            if ((bool)field.GetValue(null)!)
                vanilla[field.Name] = true;
        }

        tag[VanillaKey] = vanilla;

        foreach (ModSystem system in Participants())
        {
            var data = new TagCompound();
            system.SaveWorldData(data);
            tag[system.FullName] = data;
        }

        SubworldSystem.CopyWorldData(Key, tag);
    }

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

        foreach (ModSystem system in Participants())
        {
            if (tag.TryGet(system.FullName, out TagCompound data))
                system.LoadWorldData(data);
        }
    }

    private static IEnumerable<ModSystem> Participants() => ModContent.GetContent<ModSystem>().Where(system => system is ISharedWorldData);
}
