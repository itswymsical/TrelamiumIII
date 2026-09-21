using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace TrelamiumIII.Core.Structures;

/// <summary>
///     Holds a chest's loot rules while its structure is placed for editing; <see cref="StructureWorld.Capture" /> folds
///     them back into the chest. Sits on the chest's top-left tile.
/// </summary>
public sealed class ChestLootEntity : ModTileEntity
{
    public List<LootRule> Rules = [];

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && Main.tileContainer[tile.TileType] && TileObjectData.IsTopLeft(x, y);
    }

    public override void SaveData(TagCompound tag) => tag["rules"] = StructureFile.SaveRules(Rules);

    public override void LoadData(TagCompound tag) => Rules = StructureFile.LoadRules(tag.GetList<TagCompound>("rules"), nameof(ChestLootEntity));

    internal static void Attach(int x, int y, IEnumerable<LootRule> rules)
    {
        int id = ModContent.GetInstance<ChestLootEntity>().Place(x, y);

        if (id >= 0 && TileEntity.ByID[id] is ChestLootEntity entity)
            entity.Rules = [.. rules.Select(rule => rule.Clone())];
    }
}
