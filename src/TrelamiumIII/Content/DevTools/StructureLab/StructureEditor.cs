using SubworldLibrary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.DevTools.StructureLab.Editor;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Content.DevTools.StructureLab;

/// <summary>
///     The structure open in the <see cref="StructureLabRealm" />: a canvas of lab tiles that is the structure itself.
///     Saved to <c>Assets/Structures</c> of the mod source.
/// </summary>
public static class StructureEditor
{
    /// <summary>Tiles kept clear between the canvas and the lab's world edge.</summary>
    private const int Margin = 60;

    /// <summary>Tiles between the bottom of the canvas and where the player arrives.</summary>
    private const int ArrivalGap = 4;

    public static int MaxWidth => StructureLabRealm.WorldWidth - 2 * Margin;

    public static int MaxHeight => StructureLabRealm.WorldHeight - 2 * Margin;

    /// <summary>Name of the open structure; null when no canvas is up.</summary>
    public static string? DocumentName { get; private set; }

    /// <summary>The canvas in lab tile coordinates; empty when none is up.</summary>
    public static Rectangle Canvas { get; private set; }

    public static bool InLab => SubworldSystem.IsActive<StructureLabRealm>();

    /// <summary>Handed to the lab's generation pass, which runs after <see cref="SubworldSystem.Enter{T}" /> returns.</summary>
    private static (string Name, StructureData Data)? pending;

    #region Files
    /// <summary><c>Assets/Structures</c> of the source tree, or a folder in the save directory when it cannot be found.</summary>
    public static string Folder => SourceRoot() is { } root
        ? Path.Combine(root, "Assets", "Structures")
        : Path.Combine(Main.SavePath, "TrelamiumIII", "Structures");

    public static bool SavesToSource => SourceRoot() is not null;

    public static string PathOf(string name) => Path.Combine(Folder, name + StructureFile.Extension);

    private static string AutosavePath(string name) => Path.Combine(Main.SavePath, "TrelamiumIII", "Structures", "Autosave", name + StructureFile.Extension);

    /// <summary>The configured source tree, else the one under ModSources that holds <c>build.txt</c>.</summary>
    private static string? SourceRoot()
    {
        DeveloperConfig config = DeveloperConfig.Instance;

        if (config.HasSourceDirectory && Directory.Exists(config.SourceDirectory))
            return config.SourceDirectory;

        string sources = Path.Combine(Main.SavePath, "ModSources", "TrelamiumIII");
        string[] candidates = [Path.Combine(sources, "src", "TrelamiumIII"), sources];

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "build.txt")));
    }

    /// <summary>The named structure from <see cref="Folder" />, converting a <c>.shstruct</c> when there is no <c>.tstruct</c>.</summary>
    /// <param name="warnings">Content a conversion could not carry over.</param>
    public static StructureData Load(string name, out List<string> warnings)
    {
        warnings = [];
        string path = PathOf(name);

        if (File.Exists(path))
            return StructureFile.Read(path);

        string legacy = Path.Combine(Folder, name + StructureHelperReader.Extension);

        if (File.Exists(legacy))
            return StructureHelperReader.Read(legacy, out warnings);

        throw new FileNotFoundException($"No structure named '{name}' in {Folder}.");
    }

    /// <summary>Writes the canvas to <see cref="Folder" />, as <paramref name="name" /> when given.</summary>
    public static string Save(string? name = null)
    {
        RequireCanvas();

        name ??= DocumentName!;
        string path = PathOf(name);

        StructureFile.Write(StructureWorld.Capture(Canvas), path);
        DocumentName = name;

        return path;
    }
    #endregion

    #region Canvas
    /// <summary>Puts <paramref name="data" /> on the canvas, entering the lab first when needed.</summary>
    public static void Open(string name, StructureData data)
    {
        if (data.Width > MaxWidth || data.Height > MaxHeight)
            throw new ArgumentException($"The lab holds structures up to {MaxWidth}x{MaxHeight}; this one is {data.Width}x{data.Height}.");

        if (!InLab)
        {
            pending = (name, data);
            SubworldSystem.Enter<StructureLabRealm>();
            return;
        }

        StructureWorld.Clear(Canvas);
        Show(name, data);

        Main.LocalPlayer.Bottom = ArrivalPoint;
        Main.LocalPlayer.velocity = Vector2.Zero;
    }

    /// <summary>Run by the lab's generation pass: shows the structure <see cref="Open" /> was given, if any.</summary>
    internal static void BuildPendingCanvas()
    {
        Canvas = Rectangle.Empty;
        DocumentName = null;

        if (pending is not { } document)
            return;

        pending = null;
        Show(document.Name, document.Data);

        // Below the canvas: spawning inside it would clear the tiles around the player.
        Main.spawnTileX = Canvas.Center.X;
        Main.spawnTileY = Canvas.Bottom + ArrivalGap;
    }

    internal static void OnLeaveLab()
    {
        try
        {
            if (DocumentName is not null && !Canvas.IsEmpty)
                StructureFile.Write(StructureWorld.Capture(Canvas), AutosavePath(DocumentName));
        }
        catch (Exception exception)
        {
            TrelamiumIII.Instance.Logger.Error($"Could not autosave structure '{DocumentName}'.", exception);
        }

        Canvas = Rectangle.Empty;
        DocumentName = null;
    }

    private static Vector2 ArrivalPoint => new Vector2(Canvas.Center.X, Canvas.Bottom + ArrivalGap) * 16f;

    private static void Show(string name, StructureData data)
    {
        Canvas = new Rectangle((Main.maxTilesX - data.Width) / 2, (Main.maxTilesY - data.Height) / 2, data.Width, data.Height);
        StructureWorld.Place(data, Canvas.Location, editable: true);
        DocumentName = name;

        StructureEditorMode.OnDocumentOpened();
    }

    private static void RequireCanvas()
    {
        if (!InLab || Canvas.IsEmpty || DocumentName is null)
            throw new InvalidOperationException("No canvas is open. Use /sedit new or /sedit open.");
    }
    #endregion

    #region Tools
    /// <summary>
    ///     Voids every empty cell connected to the canvas edge, stopping at tiles, walls and liquid, so the structure keeps
    ///     the terrain around its outline. Returns how many cells changed.
    /// </summary>
    public static int VoidOutside()
    {
        RequireCanvas();

        Rectangle area = Canvas;
        int voidTile = ModContent.TileType<StructureVoidTile>();
        int voidWall = ModContent.WallType<StructureVoidWall>();
        StructureCell tileMarker = StructureCell.Marker((ushort)voidTile);
        var wallMarker = new StructureCell { WallType = (ushort)voidWall };

        var visited = new bool[area.Width, area.Height];
        var queue = new Queue<Point>();

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= area.Width || y >= area.Height || visited[x, y])
                return;

            Tile tile = Main.tile[area.X + x, area.Y + y];

            if ((tile.HasTile && tile.TileType != voidTile) || (tile.WallType != WallID.None && tile.WallType != voidWall) || tile.LiquidAmount > 0)
                return;

            visited[x, y] = true;
            queue.Enqueue(new Point(x, y));
        }

        for (int x = 0; x < area.Width; x++)
        {
            Visit(x, 0);
            Visit(x, area.Height - 1);
        }

        for (int y = 0; y < area.Height; y++)
        {
            Visit(0, y);
            Visit(area.Width - 1, y);
        }

        int changed = 0;

        while (queue.TryDequeue(out Point cell))
        {
            Tile tile = Main.tile[area.X + cell.X, area.Y + cell.Y];

            if (!tile.HasTile || tile.WallType == WallID.None)
                changed++;

            if (!tile.HasTile)
                tileMarker.WriteTile(tile);

            if (tile.WallType == WallID.None)
                wallMarker.WriteWall(tile);

            Visit(cell.X - 1, cell.Y);
            Visit(cell.X + 1, cell.Y);
            Visit(cell.X, cell.Y - 1);
            Visit(cell.X, cell.Y + 1);
        }

        StructureWorld.Frame(area);
        return changed;
    }
    #endregion
}
