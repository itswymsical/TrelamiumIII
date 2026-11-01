using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>Which parts of a <see cref="Blueprint"/> a task asks for.</summary>
[Flags]
public enum BlueprintLayer
{
    None = 0,

    /// <summary>Blocks and platforms: every tile that frames itself, one per tile.</summary>
    Blocks = 1 << 0,

    /// <summary>Multitile objects such as doors, tables and torches, each counted once however many tiles it covers.</summary>
    Furniture = 1 << 1,

    /// <summary>Walls, except those behind a full solid block of the blueprint, which nobody would see.</summary>
    Walls = 1 << 2,

    All = Blocks | Furniture | Walls
}

/// <summary>How one tile of a blueprint compares with the world at its site.</summary>
public enum BlueprintCellState
{
    /// <summary>Nothing there yet.</summary>
    Missing,

    /// <summary>The right tile or wall.</summary>
    Built,

    /// <summary>Something else is in the way.</summary>
    Wrong
}

/// <summary>One countable part of a blueprint: a block, a wall, or a whole piece of furniture.</summary>
/// <param name="Type">Tile type (doors folded to closed) or wall type, per <paramref name="Layer"/>.</param>
/// <param name="Cells">Relative to the blueprint's top-left. One for a block or wall; every tile of the object for furniture.</param>
public sealed record BlueprintPiece(BlueprintLayer Layer, ushort Type, Point16[] Cells);

/// <summary>
///     A packaged structure laid out as a build order: where it goes, and which of its pieces are in place there. The
///     world is the only record of progress, so a broken block simply stops counting.
/// </summary>
/// <remarks>
///     <para>
///         Shared by every <see cref="BlueprintTask"/> that builds part of the same structure, the way
///         <see cref="TrelamiumIII.Content.Jobs.ValidHousingTask"/> siblings share a ledger: one site, staked once, for the whole job.
///     </para>
///     <para>
///         Only tiles and walls count. Matching is by type, with open and closed doors the same; paint, framing and
///         (unless <see cref="MatchShapes"/>) slopes are the builder's choice. Cells the structure leaves empty need not
///         be cleared.
///     </para>
///     <para>The structure is read on first use, since jobs are built while content is still loading.</para>
/// </remarks>
/// <param name="path">Mod-relative structure path without the extension, as <see cref="StructureLoader.Get"/> takes it.</param>
public sealed class Blueprint(string path)
{
    public string Path { get; } = path;

    /// <summary>Whether a block also has to be hammered to the blueprint's slope or half block.</summary>
    public bool MatchShapes { get; init; }

    /// <summary>Where the player may stake it. Null allows anywhere in the world.</summary>
    public BlueprintZone? Zone { get; init; }

    private StructureData? data;
    private BlueprintPiece[]? pieces;

    private Point origin;

    /// <summary>The world the site is in, or null before one is staked.</summary>
    private Guid? world;

    /// <summary>Set when the job chose the site itself, so the player can't move it.</summary>
    public bool IsFixed { get; private set; }

    public StructureData Data
    {
        get
        {
            Resolve();
            return data!;
        }
    }

    public IReadOnlyList<BlueprintPiece> Pieces
    {
        get
        {
            Resolve();
            return pieces!;
        }
    }

    /// <summary>World top-left of the site, or null when it isn't staked in the world that's loaded now.</summary>
    public Point? Site => world is { } id && id == Main.ActiveWorldFileData?.UniqueId ? origin : null;

    #region Site
    /// <summary>Puts the site's top-left at <paramref name="topLeft"/>, in the world that's loaded now.</summary>
    /// <param name="isFixed">The job chose it, so the player can't move it.</param>
    public void Stake(Point topLeft, bool isFixed = false)
    {
        origin = topLeft;
        world = Main.ActiveWorldFileData.UniqueId;
        IsFixed = isFixed;
    }

    public void ClearSite()
    {
        world = null;
        IsFixed = false;
    }

    /// <summary>The top-left that puts the bottom middle of the structure on <paramref name="tile"/>, the way it's held at the cursor.</summary>
    public Point OriginAt(Point tile) => new(tile.X - Data.Width / 2, tile.Y - Data.Height + 1);

    public Rectangle AreaAt(Point topLeft) => new(topLeft.X, topLeft.Y, Data.Width, Data.Height);

    public bool FitsAt(Point topLeft) => StructureWorld.InWorld(AreaAt(topLeft));

    /// <summary>Whether the player may stake it at <paramref name="topLeft"/>; if not, <paramref name="problemKey"/> says why.</summary>
    public bool CanStakeAt(Point topLeft, out string? problemKey)
    {
        if (!FitsAt(topLeft))
            problemKey = "Jobs.Blueprint.OffWorld";
        else if (Zone is not null && !Zone.Allows(AreaAt(topLeft)))
            problemKey = Zone.ProblemKey;
        else
            problemKey = null;

        return problemKey is null;
    }

    /// <summary>World tiles of the furniture that counts as a chair: one inside every room the structure has.</summary>
    public IEnumerable<Point> ChairsAt(Point site)
    {
        foreach (var piece in Pieces)
        {
            if (piece.Layer == BlueprintLayer.Furniture && Array.IndexOf(TileID.Sets.RoomNeeds.CountsAsChair, (int)piece.Type) >= 0)
                yield return new Point(site.X + piece.Cells[0].X, site.Y + piece.Cells[0].Y);
        }
    }
    #endregion

    #region Progress
    /// <summary>How many pieces of <paramref name="layers"/> the blueprint has.</summary>
    public int Required(BlueprintLayer layers)
    {
        var count = 0;

        foreach (var piece in Pieces)
        {
            if ((piece.Layer & layers) != 0)
                count++;
        }

        return count;
    }

    /// <summary>How many pieces of <paramref name="layers"/> are in place at the site; 0 when it isn't staked here.</summary>
    public int Built(BlueprintLayer layers)
    {
        if (Site is not { } site)
            return 0;

        var count = 0;

        foreach (var piece in Pieces)
        {
            if ((piece.Layer & layers) != 0 && IsBuilt(piece, site))
                count++;
        }

        return count;
    }

    /// <summary>A piece is built when every one of its tiles is, so a table one tile off doesn't count.</summary>
    public bool IsBuilt(BlueprintPiece piece, Point site)
    {
        foreach (var cell in piece.Cells)
        {
            if (StateOf(piece, cell, site) != BlueprintCellState.Built)
                return false;
        }

        return true;
    }

    public BlueprintCellState StateOf(BlueprintPiece piece, Point16 cell, Point site)
    {
        var tile = Framing.GetTileSafely(site.X + cell.X, site.Y + cell.Y);

        if (piece.Layer == BlueprintLayer.Walls)
        {
            if (tile.WallType == piece.Type)
                return BlueprintCellState.Built;

            return tile.WallType == WallID.None ? BlueprintCellState.Missing : BlueprintCellState.Wrong;
        }

        if (!tile.HasTile)
            return BlueprintCellState.Missing;

        if (Closed(tile.TileType) != piece.Type)
            return BlueprintCellState.Wrong;

        return !MatchShapes || tile.BlockType == Data[cell.X, cell.Y].BlockType ? BlueprintCellState.Built : BlueprintCellState.Wrong;
    }
    #endregion

    #region Persistence
    /// <summary>Writes the site, if one is staked. Every task sharing this blueprint writes the same thing.</summary>
    public void Save(TagCompound tag)
    {
        if (world is not { } id)
            return;

        tag["site"] = new TagCompound
        {
            ["x"] = origin.X,
            ["y"] = origin.Y,
            ["world"] = id.ToString(),
            ["fixed"] = IsFixed
        };
    }

    public void Load(TagCompound tag)
    {
        if (!tag.TryGet("site", out TagCompound site) || !Guid.TryParse(site.GetString("world"), out var id))
            return;

        origin = new Point(site.GetInt("x"), site.GetInt("y"));
        world = id;
        IsFixed = site.GetBool("fixed");
    }
    #endregion

    #region Survey
    /// <summary>Reads the structure the first time anything asks. One that won't load is logged and leaves the blueprint empty.</summary>
    private void Resolve()
    {
        if (pieces is not null)
            return;

        try
        {
            data = StructureLoader.Get(Path);
            pieces = Survey(data);
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Blueprint '{Path}' could not be read: {ex.Message}");

            data = StructureData.Void(1, 1);
            pieces = [];
        }
    }

    private static BlueprintPiece[] Survey(StructureData data)
    {
        var pieces = new List<BlueprintPiece>();

        for (var x = 0; x < data.Width; x++)
        {
            for (var y = 0; y < data.Height; y++)
            {
                ref readonly var cell = ref data[x, y];

                if (cell.Has(CellFlags.HasTile) && !cell.Has(CellFlags.TileVoid))
                {
                    if (IsBlock(cell.TileType))
                        pieces.Add(new BlueprintPiece(BlueprintLayer.Blocks, Closed(cell.TileType), [new Point16(x, y)]));
                    else if (IsObjectOrigin(cell))
                        pieces.Add(new BlueprintPiece(BlueprintLayer.Furniture, Closed(cell.TileType), ObjectCells(data, x, y)));
                }

                if (cell.WallType != WallID.None && !cell.Has(CellFlags.WallVoid) && !HidesWall(cell))
                    pieces.Add(new BlueprintPiece(BlueprintLayer.Walls, cell.WallType, [new Point16(x, y)]));
            }
        }

        return pieces.ToArray();
    }

    /// <summary>Tiles that frame themselves, and platforms, which keep their style in their frame but are building material all the same.</summary>
    private static bool IsBlock(int type) => !Main.tileFrameImportant[type] || TileID.Sets.Platforms[type];

    /// <summary>Whether <paramref name="cell"/> is the top-left tile of its object, which is where furniture is counted from.</summary>
    private static bool IsObjectOrigin(in StructureCell cell)
    {
        if (TileObjectData.GetTileData(cell.TileType, 0) is not { } objectData)
            return true;

        // Styles and alternates step by whole objects, so the top-left of any of them sits on a multiple.
        return cell.TileFrameX % objectData.CoordinateFullWidth == 0 && cell.TileFrameY % objectData.CoordinateFullHeight == 0;
    }

    /// <summary>The tiles of the object whose top-left is (<paramref name="x"/>, <paramref name="y"/>), as far as the structure holds them.</summary>
    private static Point16[] ObjectCells(StructureData data, int x, int y)
    {
        var type = data[x, y].TileType;
        var objectData = TileObjectData.GetTileData(type, 0);
        var width = objectData?.Width ?? 1;
        var height = objectData?.Height ?? 1;
        var cells = new List<Point16>(width * height);

        for (var i = x; i < x + width; i++)
        {
            for (var j = y; j < y + height; j++)
            {
                if (data.Contains(i, j) && data[i, j].Has(CellFlags.HasTile) && data[i, j].TileType == type)
                    cells.Add(new Point16(i, j));
            }
        }

        return cells.ToArray();
    }

    /// <summary>A full, unactuated solid block hides the wall behind it, so that wall isn't asked for.</summary>
    private static bool HidesWall(in StructureCell cell)
        => cell.Has(CellFlags.HasTile) && !cell.Has(CellFlags.TileVoid) && !cell.Has(CellFlags.Actuated)
        && cell.BlockType == BlockType.Solid && Main.tileSolid[cell.TileType] && !Main.tileSolidTop[cell.TileType];

    /// <summary>Doors, trapdoors and gates count open or shut; this folds each to its closed type.</summary>
    private static ushort Closed(int type) => (ushort)(type switch
    {
        TileID.OpenDoor => TileID.ClosedDoor,
        TileID.TrapdoorOpen => TileID.TrapdoorClosed,
        TileID.TallGateOpen => TileID.TallGateClosed,
        _ => TileID.Sets.CloseDoorID[type] >= 0 ? TileID.Sets.CloseDoorID[type] : type
    });
    #endregion
}
