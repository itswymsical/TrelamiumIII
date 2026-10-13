using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.Tiles.Seals;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Reads the rectangles worlds saved before seals were per tile, and lays them as seals. Saves nothing, so the old
/// tags drop on the next save. Kept under this name because the tags are filed under it.
/// </summary>
public sealed class StructureProtectionSystem : ModSystem
{
    public override void SaveWorldData(TagCompound tag) { }

    public override void LoadWorldData(TagCompound tag)
    {
        Migrate(tag.GetList<int>("shieldedAreas"), ModContent.GetInstance<WardSeal>());
        Migrate(tag.GetList<int>("protectedAreas"), ModContent.GetInstance<RealmSeal>());
    }

    private static void Migrate(IList<int> values, SealType type)
    {
        for (int k = 0; k + 3 < values.Count; k += 4)
            SealSystem.Seal(new Rectangle(values[k], values[k + 1], values[k + 2], values[k + 3]), type);
    }
}
