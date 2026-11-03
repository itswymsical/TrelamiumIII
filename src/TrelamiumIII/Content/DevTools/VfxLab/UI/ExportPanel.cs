using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TrelamiumIII.Content.DevTools.VfxLab.Export;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

/// <summary>
///     The review before an export: every file it would write and how that compares with what's there, with nothing touched until it's
///     accepted. It takes the whole screen's input while it's open, and works from a copy of the recipe, so what's reviewed is what gets written.
/// </summary>
internal sealed class ExportPanel
{
    private const int Width = 620;
    private const int Padding = 8;
    private const int LineHeight = 20;
    private const int ButtonHeight = 24;
    private const int StatusWidth = 66;
    private const int ProblemsShown = 6;

    private VfxRecipe? recipe;
    private GeneratedShader? shader;
    private ExportCatalog? catalog;
    private Task<ShaderCompileResult>? compiling;
    private string shaderText = string.Empty;
    private byte[]? compiled;
    private string compilerLog = string.Empty;
    private string? existingFolder;
    private string folder = string.Empty;
    private string namespaceName = string.Empty;
    private List<ExportRow> rows = [];
    private List<GenerationProblem> problems = [];

    public bool IsOpen => recipe is not null;

    public bool Compiling => compiling is not null;

    public IReadOnlyList<ExportRow> Rows => rows;

    public IReadOnlyList<GenerationProblem> Problems => problems;

    public string Folder => folder;

    public string Namespace => namespaceName;

    private bool HasErrors => problems.Any(static problem => problem.IsError);

    private int Pending => rows.Count(static row => row.WillWrite);

    public void Open(VfxRecipe source, StageLibrary library, Mod mod)
    {
        recipe = RecipeFile.Parse(RecipeFile.Serialize(source));
        catalog = LabExport.Catalog(mod);
        existingFolder = ExportPlan.FindExisting(LabExport.Root, recipe.Name);
        folder = existingFolder ?? FieldExporter.DefaultFolder;
        shader = ShaderGenerator.Generate(recipe, library);
        compiled = null;
        compilerLog = string.Empty;
        compiling = null;

        // From the recipe, not what the slot is showing: a debug view is only for looking.
        if (shader.Source is { } generated)
        {
            shaderText = ExportStamp.Stamp(generated);
            compiling = ShaderCompiler.CompileAsync(shaderText, LabFiles.ShaderCache);
        }

        Refresh();
    }

    public void Close()
    {
        recipe = null;
        shader = null;
        catalog = null;
        compiling = null;
        rows = [];
        problems = [];
    }

    private void Refresh()
    {
        rows = [];
        problems = [.. shader!.Problems, .. FieldExporter.Check(recipe!, shader, catalog!)];

        if (!LabExport.HasSource)
            problems.Add(new GenerationProblem(recipe!.Name, $"no source folder found, so this goes to {LabExport.Root}, to copy across by hand.", false));

        if (compiled is null && compiling is null && shader.Source is not null)
            problems.Add(new GenerationProblem(recipe!.Name, compilerLog.Split('\n')[0], true));
        else if (compilerLog.Length > 0)
            problems.Add(new GenerationProblem(recipe!.Name, $"the compiler said: {compilerLog.Split('\n')[0]}", false));

        string clean = ExportPlan.CleanFolder(folder);

        if (ExportPlan.FolderProblem(clean) is { } wrongFolder)
        {
            problems.Add(new GenerationProblem("folder", wrongFolder, true));
            return;
        }

        namespaceName = ExportPlan.NamespaceOf(LabExport.Root, clean, recipe!.Name);

        if (existingFolder is not null && existingFolder != clean)
            problems.Add(new GenerationProblem("folder", $"it's already in {existingFolder}, and those files stay there. Delete them, or there'll be two of this field.", false));

        if (compiled is not { } code || HasErrors)
            return;

        rows = ExportPlan.Review(LabExport.Root, ExportPlan.Files(recipe, shader, shaderText, code, clean, namespaceName, catalog!));

        if (rows.Any(static row => row.Status == ExportStatus.Misspelled))
            problems.Add(new GenerationProblem(recipe.Name, "those files are there with other capitalisation. Rename this recipe to match them, or delete them.", true));
    }

    public void Draw(LabGui gui, Action<string> notify)
    {
        if (recipe is null)
            return;

        if (compiling is { IsCompleted: true } task)
        {
            compiling = null;
            compiled = task.Result.Code;
            compilerLog = task.Result.Log;
            Refresh();
        }

        Point screen = LabGui.ScreenSize;
        int shownProblems = Math.Min(problems.Count, ProblemsShown);
        int height = Padding * 2 + LineHeight + ButtonHeight + LineHeight + 5 * LineHeight + shownProblems * LineHeight + ButtonHeight + 22;
        var area = new Rectangle((screen.X - Width) / 2, (screen.Y - height) / 2, Width, height);

        gui.Fill(new Rectangle(0, 0, screen.X, screen.Y), Color.Black * 0.35f);
        gui.Panel(area);

        int x = area.X + Padding;
        int y = area.Y + Padding;
        int inner = area.Width - Padding * 2;

        gui.Text($"Export {recipe.Name}", x + 2, y + LineHeight / 2, Color.White);
        y += LineHeight;

        gui.Text("Folder", x + 2, y + ButtonHeight / 2, LabTheme.Muted);

        if (gui.TextBox("export:folder", new Rectangle(x + 50, y, inner - 50, ButtonHeight), ref folder, FieldExporter.DefaultFolder))
            Refresh();

        y += ButtonHeight + 2;

        string description = HasErrors ? "Fix what's listed below to export this." : $"The class is {namespaceName}.{recipe.Name}";

        gui.Text(LabGui.Fit(description, inner - 4, LabGui.SmallScale), x + 2, y + LineHeight / 2, LabTheme.Muted, LabGui.SmallScale);
        y += LineHeight + 4;

        if (Compiling)
            gui.Text("Compiling..", x + 2, y + LineHeight / 2, LabTheme.Muted);

        for (int i = 0; i < rows.Count; i++)
            DrawRow(gui, new Rectangle(x, y + i * LineHeight, inner, LineHeight), rows[i]);

        y += 5 * LineHeight + 4;

        for (int i = 0; i < shownProblems; i++)
        {
            GenerationProblem problem = problems[i];
            var line = new Rectangle(x, y + i * LineHeight, inner, LineHeight);
            string text = problem.ToString();

            gui.Text(LabGui.Fit(text, inner - 4, LabGui.SmallScale), x + 2, line.Center.Y, problem.IsError ? LabTheme.Failure : LabTheme.Changed, LabGui.SmallScale);
            gui.Tooltip(line, text);
        }

        y = area.Bottom - Padding - ButtonHeight;

        if (!HasErrors)
            gui.Text("Takes effect at the next rebuild. Until then the slot keeps previewing it.", x + 2, y + ButtonHeight / 2, LabTheme.Muted, LabGui.SmallScale);

        if (gui.Button("export:cancel", new Rectangle(area.Right - Padding - 214, y, 70, ButtonHeight), "Cancel"))
        {
            Close();
            return;
        }

        DrawWrite(gui, new Rectangle(area.Right - Padding - 140, y, 140, ButtonHeight), notify);
    }

    private static void DrawRow(LabGui gui, Rectangle line, ExportRow row)
    {
        (string label, Color color, string tip) = row.Status switch
        {
            ExportStatus.New => ("New", LabTheme.Live, "Doesn't exist yet"),
            ExportStatus.Changed => ("Changed", LabTheme.Changed, "Replaces what's there, which came from an earlier export"),
            ExportStatus.Kept => ("Kept", LabTheme.Muted, "Yours: written once, and never again"),
            ExportStatus.Edited => ("Edited", LabTheme.Failure, "Changed by hand since the last export, and writing would lose that"),
            ExportStatus.Misspelled => ("Misspelled", LabTheme.Failure, "There under other capitalisation"),
            _ => ("Same", LabTheme.Muted, "Already exactly this, so it's left alone"),
        };

        gui.Record($"export:row:{Path.GetFileName(row.Output.RelativePath)}", line);
        gui.Text(label, line.X + 2, line.Center.Y, color, LabGui.SmallScale);
        gui.Text(LabGui.Fit(row.Output.RelativePath, line.Width - StatusWidth - 4), line.X + StatusWidth, line.Center.Y, row.WillWrite ? LabTheme.Text : LabTheme.Muted);
        gui.Tooltip(line, tip);
    }

    private void DrawWrite(LabGui gui, Rectangle area, Action<string> notify)
    {
        int edited = rows.Count(static row => row.Status == ExportStatus.Edited);
        int pending = Pending;

        string label = Compiling ? "Compiling.."
            : HasErrors ? "Can't export"
            : pending == 0 ? "Nothing to write"
            : edited > 0 ? $"Overwrite {edited} edited"
            : $"Write {Files(pending)}";

        bool usable = !Compiling && !HasErrors && pending > 0;

        gui.Enabled = usable;
        bool pressed = gui.Button("export:write", area, label);
        gui.Enabled = true;

        if (!pressed)
            return;

        // Looked at again at the last moment, in case something changed the files while this sat open.
        try
        {
            rows = ExportPlan.Review(LabExport.Root, rows.Select(static row => row.Output));

            int written = ExportPlan.Write(LabExport.Root, rows);

            notify($"Wrote {Files(written)} for {recipe!.Name}. It all takes effect at the next rebuild.");
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(new GenerationProblem(recipe!.Name, $"couldn't write: {exception.Message}", true));
        }
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count} files";
}
