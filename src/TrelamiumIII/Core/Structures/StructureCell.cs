namespace TrelamiumIII.Core.Structures;

[Flags]
public enum CellFlags : ushort
{
    None = 0,

    /// <summary>The tile channel (block, liquid, wires, actuator) is outside the structure; placing it keeps the world's.</summary>
    TileVoid = 1 << 0,

    /// <summary>The wall channel is outside the structure; placing it keeps the world's wall.</summary>
    WallVoid = 1 << 1,

    HasTile = 1 << 2,
    Actuated = 1 << 3,
    HasActuator = 1 << 4,
    TileInvisible = 1 << 5,
    TileFullbright = 1 << 6,
    WallInvisible = 1 << 7,
    WallFullbright = 1 << 8,
    RedWire = 1 << 9,
    BlueWire = 1 << 10,
    GreenWire = 1 << 11,
    YellowWire = 1 << 12,

    TileChannel = HasTile | Actuated | HasActuator | TileInvisible | TileFullbright | RedWire | BlueWire | GreenWire | YellowWire,
    WallChannel = WallInvisible | WallFullbright
}

/// <summary>One tile of a <see cref="StructureData"/>. Types are runtime IDs; <see cref="StructureFile"/> maps them to names.</summary>
public record struct StructureCell
{
    public CellFlags Flags;

    public ushort TileType;
    public short TileFrameX;
    public short TileFrameY;
    public byte TileFrameNumber;
    public BlockType BlockType;
    public byte TileColor;

    /// <summary><see cref="WallID.None"/> when the cell has no wall.</summary>
    public ushort WallType;
    public byte WallColor;
    public byte WallFrameNumber;

    public byte LiquidAmount;
    public byte LiquidType;

    public readonly bool Has(CellFlags flag) => (Flags & flag) != 0;

    public static StructureCell Read(Tile tile)
    {
        var cell = new StructureCell();

        if (tile.HasTile)
        {
            cell.Flags |= CellFlags.HasTile;
            cell.TileType = tile.TileType;
            cell.TileFrameX = tile.TileFrameX;
            cell.TileFrameY = tile.TileFrameY;
            cell.TileFrameNumber = (byte)tile.TileFrameNumber;
            cell.BlockType = tile.BlockType;
            cell.TileColor = tile.TileColor;

            cell.Set(CellFlags.Actuated, tile.IsActuated);
            cell.Set(CellFlags.TileInvisible, tile.IsTileInvisible);
            cell.Set(CellFlags.TileFullbright, tile.IsTileFullbright);
        }

        cell.Set(CellFlags.HasActuator, tile.HasActuator);
        cell.Set(CellFlags.RedWire, tile.RedWire);
        cell.Set(CellFlags.BlueWire, tile.BlueWire);
        cell.Set(CellFlags.GreenWire, tile.GreenWire);
        cell.Set(CellFlags.YellowWire, tile.YellowWire);

        if (tile.WallType != WallID.None)
        {
            cell.WallType = tile.WallType;
            cell.WallColor = tile.WallColor;
            cell.WallFrameNumber = (byte)tile.WallFrameNumber;

            cell.Set(CellFlags.WallInvisible, tile.IsWallInvisible);
            cell.Set(CellFlags.WallFullbright, tile.IsWallFullbright);
        }

        if (tile.LiquidAmount > 0)
        {
            cell.LiquidAmount = tile.LiquidAmount;
            cell.LiquidType = (byte)tile.LiquidType;
        }

        return cell;
    }

    /// <summary>A cell whose tile channel is the given marker tile and nothing else.</summary>
    public static StructureCell Marker(ushort tileType) => new() { Flags = CellFlags.HasTile, TileType = tileType };

    /// <summary>Overwrites the block, actuator, wires and liquid of <paramref name="tile"/>.</summary>
    public readonly void WriteTile(Tile tile)
    {
        bool hasTile = Has(CellFlags.HasTile);

        tile.HasTile = hasTile;
        tile.TileType = hasTile ? TileType : (ushort)0;
        tile.TileFrameX = TileFrameX;
        tile.TileFrameY = TileFrameY;
        tile.TileFrameNumber = TileFrameNumber;
        tile.BlockType = hasTile ? BlockType : BlockType.Solid;
        tile.TileColor = TileColor;
        tile.IsActuated = Has(CellFlags.Actuated);
        tile.IsTileInvisible = Has(CellFlags.TileInvisible);
        tile.IsTileFullbright = Has(CellFlags.TileFullbright);

        tile.HasActuator = Has(CellFlags.HasActuator);
        tile.RedWire = Has(CellFlags.RedWire);
        tile.BlueWire = Has(CellFlags.BlueWire);
        tile.GreenWire = Has(CellFlags.GreenWire);
        tile.YellowWire = Has(CellFlags.YellowWire);

        tile.LiquidAmount = LiquidAmount;
        tile.LiquidType = LiquidAmount > 0 ? LiquidType : LiquidID.Water;
    }

    public readonly void WriteWall(Tile tile)
    {
        tile.WallType = WallType;
        tile.WallColor = WallColor;
        tile.WallFrameNumber = WallFrameNumber;
        tile.IsWallInvisible = Has(CellFlags.WallInvisible);
        tile.IsWallFullbright = Has(CellFlags.WallFullbright);
    }

    /// <summary>Drops the tile channel and marks it void.</summary>
    public void VoidTile()
    {
        Flags = (Flags & ~CellFlags.TileChannel) | CellFlags.TileVoid;
        TileType = 0;
        TileFrameX = TileFrameY = 0;
        TileFrameNumber = 0;
        BlockType = BlockType.Solid;
        TileColor = 0;
        LiquidAmount = LiquidType = 0;
    }

    /// <summary>Drops the wall channel and marks it void.</summary>
    public void VoidWall()
    {
        Flags = (Flags & ~CellFlags.WallChannel) | CellFlags.WallVoid;
        WallType = WallID.None;
        WallColor = WallFrameNumber = 0;
    }

    private void Set(CellFlags flag, bool value)
    {
        if (value)
            Flags |= flag;
        else
            Flags &= ~flag;
    }
}
