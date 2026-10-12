namespace TrelamiumIII.Core.Tiles;

/// <summary>
///     One piece of a material's furniture set, registered by <see cref="RegisterSet" />. Named <c>{Material}{Piece}Tile</c>
///     with its texture in the registering folder.
/// </summary>
public sealed class FurnitureSetTile : FurnitureActor
{
    private readonly string name;
    private readonly string texture;
    private readonly Func<int> dust;
    private readonly Color? light;

    private FurnitureSetTile? closedDoor;
    private FurnitureSetTile? openDoor;

    private FurnitureSetTile(string name, string directory, FurnitureType type, Func<int> dust, Color? light)
    {
        this.name = name;
        texture = $"{directory}/{name}";
        this.dust = dust;
        this.light = light;

        FurnitureType = type;
    }

    public override string Name => name;

    public override string Texture => texture;

    public override int ClosedDoorType => closedDoor?.Type ?? 0;

    public override int OpenDoorType => openDoor?.Type ?? 0;

    /// <summary>
    ///     Adds a tile and item per piece of <paramref name="types" /> for <paramref name="material" />. A
    ///     <see cref="FurnitureType.ClosedDoor" /> also adds its open tile.
    /// </summary>
    /// <param name="directory">Texture folder, e.g. the registering type's namespace as a path.</param>
    /// <param name="dust">Resolved at SetStaticDefaults, since dust types may load later.</param>
    /// <param name="materialItem">The wood or block the recipes consume.</param>
    /// <param name="lightColor">Light of the lit pieces, or null for <see cref="FurnitureActor.LightColor" />'s default.</param>
    public static void RegisterSet(Mod mod, string material, string directory, Func<int> dust, Func<int> materialItem,
        Color? lightColor, params FurnitureType[] types)
    {
        foreach (FurnitureType type in types)
        {
            if (type == FurnitureType.ClosedDoor)
            {
                var closed = new FurnitureSetTile($"{material}DoorClosedTile", directory, FurnitureType.ClosedDoor, dust, lightColor);
                var open = new FurnitureSetTile($"{material}DoorOpenTile", directory, FurnitureType.OpenDoor, dust, lightColor);

                closed.closedDoor = open.closedDoor = closed;
                closed.openDoor = open.openDoor = open;

                mod.AddContent(open);
                mod.AddContent(closed);
                mod.AddContent(new FurnitureSetItem($"{material}DoorItem", directory, type, closed, materialItem));

                continue;
            }

            string piece = type == FurnitureType.Workbench ? "WorkBench" : type.ToString();

            var tile = new FurnitureSetTile($"{material}{piece}Tile", directory, type, dust, lightColor);

            mod.AddContent(tile);
            mod.AddContent(new FurnitureSetItem($"{material}{piece}Item", directory, type, tile, materialItem));
        }
    }

    public override void SetStaticDefaults()
    {
        DustID = dust();

        if (light is { } color)
            LightColor = color;

        base.SetStaticDefaults();
    }
}

/// <summary>Places a <see cref="FurnitureSetTile" />. Registered alongside it.</summary>
public sealed class FurnitureSetItem : FurnitureItem
{
    private readonly string name;
    private readonly string texture;
    private readonly FurnitureType type;

    [CloneByReference]
    private readonly FurnitureSetTile tile;

    [CloneByReference]
    private readonly Func<int> material;

    internal FurnitureSetItem(string name, string directory, FurnitureType type, FurnitureSetTile tile, Func<int> material)
    {
        this.name = name;
        texture = $"{directory}/{name}";
        this.type = type;
        this.tile = tile;
        this.material = material;
    }

    protected override bool CloneNewInstances => true;

    public override string Name => name;

    public override string Texture => texture;

    protected override FurnitureType FurnitureType => type;

    protected override int TileType => tile.Type;

    protected override int MaterialType => material();
}
