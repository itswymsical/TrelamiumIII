using TrelamiumIII.Content.Dusts;
using TrelamiumIII.Core.Tiles;

// Some code structure adapted from Spirit Reforged: https://github.com/GabeHasWon/SpiritReforged/tree/master/Common/TileCommon/PresetTiles/Furniture
namespace TrelamiumIII.Content.Construction.Spruce;

/// <summary>Registers the spruce furniture set.</summary>
public sealed class SpruceFurniture : ICustomAutoload
{
    public static void Autoload(Mod mod) => FurnitureSetTile.RegisterSet(
        mod,
        "Spruce",
        typeof(SpruceFurniture).Namespace!.Replace('.', '/'),
        dust: static () => ModContent.DustType<SpruceDust>(),
        materialItem: static () => ModContent.ItemType<SpruceWoodItem>(),
        lightColor: new Color(255, 150, 70),
        FurnitureType.Chair,
        FurnitureType.Table,
        FurnitureType.Workbench,
        FurnitureType.Dresser,
        FurnitureType.Bookcase,
        FurnitureType.Sofa,
        FurnitureType.Bed,
        FurnitureType.Sink,
        FurnitureType.Bathtub,
        FurnitureType.Lamp,
        FurnitureType.Chandelier,
        FurnitureType.Candelabra,
        FurnitureType.Lantern,
        FurnitureType.Piano,
        FurnitureType.Chest,
        FurnitureType.ClosedDoor,
        FurnitureType.Platform);
}
