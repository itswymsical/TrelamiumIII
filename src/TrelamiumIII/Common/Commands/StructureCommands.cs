using SubworldLibrary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TrelamiumIII.Content.DevTools.StructureLab;
using TrelamiumIII.Content.DevTools.StructureLab.Editor;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Commands;

/// <summary><c>/sedit</c>: the structure lab. Structures are <c>Assets/Structures/{name}.tstruct</c> in the mod source.</summary>
public partial class StructureCommands : ModCommand
{
    /// <summary>Top-left of the area the self-test borrows.</summary>
    private static readonly Point SelfTestOrigin = new(100, 100);

    public override string Command => "sedit";

    public override CommandType Type => CommandType.Chat | CommandType.Console;

    /// <summary>Resize anchors, in reading order of a 3x3 grid.</summary>
    private static readonly string[] Anchors = ["tl", "t", "tr", "l", "c", "r", "bl", "b", "br"];

    public override string Usage => "/sedit new <name> <width> <height> | open <name> | save [name] | resize <width> <height> [anchor] | edit"
                                    + " | voidout | kit | exit | place <name> | import [name] | selftest [name]";

    public override string Description => "Structure lab: edit structures on a canvas and save them into the mod source.";

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex NamePattern();

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        try
        {
            switch (verb)
            {
                case "import":
                    Import(caller, args);
                    return;

                case "selftest":
                    SelfTest(caller, args);
                    return;
            }

            if (caller.CommandType == CommandType.Console)
            {
                caller.Reply("Only import and selftest run from the console.", Color.Red);
                return;
            }

            switch (verb)
            {
                case "new":
                    New(caller, args);
                    break;

                case "open":
                    Open(caller, args);
                    break;

                case "save":
                    Save(caller, args);
                    break;

                case "resize":
                    Resize(caller, args);
                    break;

                case "voidout":
                    caller.Reply($"Voided {StructureEditor.VoidOutside()} cells around the structure.", Color.LightGreen);
                    break;

                case "kit":
                    caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("StructureLab"), ModContent.ItemType<StructureVoidTileItem>(), 999);
                    caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("StructureLab"), ModContent.ItemType<StructureVoidWallItem>(), 999);
                    break;

                case "edit":
                    StructureEditorMode.Enter();
                    break;

                case "exit":
                    if (!StructureEditor.InLab)
                        break;

                    StructureEditorMode.Exit();
                    SubworldSystem.Exit();
                    break;

                case "place":
                    Place(caller, args);
                    break;

                default:
                    caller.Reply(Usage, Color.Red);
                    break;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException
                                              or UnauthorizedAccessException)
        {
            caller.Reply(exception.Message, Color.Red);
        }
    }

    private static void New(CommandCaller caller, string[] args)
    {
        if (args.Length < 4 || !int.TryParse(args[2], out int width) || !int.TryParse(args[3], out int height) || width < 1 || height < 1)
            throw new ArgumentException("Usage: /sedit new <name> <width> <height>");

        string name = ValidName(args[1]);

        if (File.Exists(StructureEditor.PathOf(name)))
            throw new ArgumentException($"'{name}' already exists; use /sedit open {name}.");

        StructureEditor.Open(name, new StructureData(width, height));
    }

    private static void Open(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("Usage: /sedit open <name>");

        string name = ValidName(args[1]);
        StructureData data = StructureEditor.Load(name, out List<string> warnings);

        if (warnings.Count > 0)
            caller.Reply($"Converted from StructureHelper without: {string.Join(", ", warnings)}", Color.Orange);

        StructureEditor.Open(name, data);
    }

    private static void Save(CommandCaller caller, string[] args)
    {
        string path = StructureEditor.Save(args.Length > 1 ? ValidName(args[1]) : null);
        caller.Reply($"Saved {path}", Color.LightGreen);

        if (!StructureEditor.SavesToSource)
            caller.Reply("The mod source was not found, so this is outside the mod. Set the source directory in the Authoring config.", Color.Orange);
    }

    /// <summary>Resizes the open canvas, keeping the anchored part (tl, t, tr, l, c, r, bl, b or br; c by default) in place.</summary>
    private static void Resize(CommandCaller caller, string[] args)
    {
        if (args.Length < 3 || !int.TryParse(args[1], out int width) || !int.TryParse(args[2], out int height) || width < 1 || height < 1)
            throw new ArgumentException("Usage: /sedit resize <width> <height> [tl|t|tr|l|c|r|bl|b|br]");

        int anchor = args.Length > 3 ? Array.IndexOf(Anchors, args[3].ToLowerInvariant()) : 4;

        if (anchor < 0)
            throw new ArgumentException($"'{args[3]}' is not an anchor; use one of {string.Join(", ", Anchors)}.");

        if (!StructureEditor.InLab || StructureEditor.Canvas.IsEmpty)
            throw new InvalidOperationException("No canvas is open. Use /sedit new or /sedit open.");

        if (width > StructureEditor.MaxWidth || height > StructureEditor.MaxHeight)
            throw new ArgumentException($"The lab holds canvases up to {StructureEditor.MaxWidth}x{StructureEditor.MaxHeight}.");

        StructureEditorMode.ResizeCanvas(StructureEditor.Anchored(width, height, new Point(anchor % 3, anchor / 3)));
        caller.Reply($"The canvas is {width}x{height}.", Color.LightGreen);
    }

    /// <summary>Places a structure in the current world with its bottom-centre on the cursor, as world generation would.</summary>
    private static void Place(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("Usage: /sedit place <name>");

        StructureData data = StructureEditor.Load(ValidName(args[1]), out _);
        Point cursor = Main.MouseWorld.ToTileCoordinates();
        var origin = new Point(cursor.X - data.Width / 2, cursor.Y - data.Height + 1);

        StructureWorld.Place(data, origin);
        caller.Reply($"Placed {args[1]} at {origin}.", Color.LightGreen);
    }

    /// <summary>Converts StructureHelper files in the structure folder to <c>.tstruct</c> beside them.</summary>
    private static void Import(CommandCaller caller, string[] args)
    {
        IEnumerable<string> paths = args.Length > 1
            ? [Path.Combine(StructureEditor.Folder, ValidName(args[1]) + StructureHelperReader.Extension)]
            : Directory.EnumerateFiles(StructureEditor.Folder, "*" + StructureHelperReader.Extension);

        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            StructureData data = StructureHelperReader.Read(path, out List<string> warnings);

            StructureFile.Write(data, StructureEditor.PathOf(name));

            int voidCells = data.Cells.Count(cell => cell.Has(CellFlags.TileVoid));
            int lootRules = data.Chests.Sum(chest => chest.Loot.Count);

            Report(caller, $"{name}: {data.Width}x{data.Height}, {voidCells} void cells, {data.Chests.Count} chests ({lootRules} loot rules), "
                           + $"{data.Signs.Count} signs, {data.Entities.Count} tile entities", Color.LightGreen);

            if (warnings.Count > 0)
                Report(caller, $"{name} lost: {string.Join(", ", warnings)}", Color.Orange);
        }
    }

    private static void SelfTest(CommandCaller caller, string[] args)
    {
        IEnumerable<string> names = args.Length > 1
            ? [ValidName(args[1])]
            : Directory.EnumerateFiles(StructureEditor.Folder, "*" + StructureFile.Extension).Select(path => Path.GetFileNameWithoutExtension(path)!);

        foreach (string name in names)
        {
            StructureData data = StructureFile.Read(StructureEditor.PathOf(name));

            if (StructureEditor.InLab && StructureEditor.Canvas.Intersects(new Rectangle(SelfTestOrigin.X, SelfTestOrigin.Y, data.Width, data.Height)))
                throw new InvalidOperationException("The self-test area overlaps the canvas.");

            string result = StructureSelfTest.Run(name, data, SelfTestOrigin);
            Report(caller, $"{name}: {result}", result == "OK" ? Color.LightGreen : Color.Orange);
        }
    }

    private static string ValidName(string name) => NamePattern().IsMatch(name)
        ? name
        : throw new ArgumentException($"'{name}' is not a valid structure name; use letters, digits and underscores.");

    private static void Report(CommandCaller caller, string text, Color color)
    {
        caller.Reply(text, color);
        TrelamiumIII.Instance.Logger.Info(text);
    }
}
