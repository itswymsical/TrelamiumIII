using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;

namespace TrelamiumIII.Content.DevTools.VfxLab.Export;

internal enum ExportFileKind
{
    /// <summary> Replaced by every export. Being different from what's on disk is normal. </summary>
    Recipe,

    /// <summary> Replaced by every export, and carries a hash of itself, so an edit by hand can be told from an older export. </summary>
    Stamped,

    /// <summary> Bytes, compared as bytes. </summary>
    Compiled,

    /// <summary> Written if it isn't there, and otherwise never touched. </summary>
    WriteOnce,
}

internal enum ExportStatus
{
    New,
    Changed,
    Same,

    /// <summary> A hand-written half that's already there. </summary>
    Kept,

    /// <summary> Changed by hand since the last export, and writing would lose that. </summary>
    Edited,

    /// <summary> There, under other capitalisation. Stops the export. </summary>
    Misspelled,
}

/// <param name="RelativePath"> From the root of the mod's source, with forward slashes. </param>
internal sealed record ExportFile(string RelativePath, byte[] Content, ExportFileKind Kind);

internal readonly record struct ExportRow(ExportFile Output, ExportStatus Status)
{
    public bool WillWrite => Status is ExportStatus.New or ExportStatus.Changed or ExportStatus.Edited;
}

/// <summary> The first line of a generated file: a hash of the rest of it. A comment, so it doesn't reach the compiled shader. </summary>
internal static class ExportStamp
{
    private const string Prefix = "// Export hash: ";

    public static string Stamp(string text)
    {
        string normalized = Normalize(text);

        return $"{Prefix}{Hash(normalized)}\n{normalized}";
    }

    /// <summary> Whether this is still exactly what an export wrote. </summary>
    public static bool IsIntact(string text)
    {
        string normalized = Normalize(text);
        int lineEnd = normalized.IndexOf('\n');

        if (lineEnd < 0 || !normalized.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        return normalized[Prefix.Length..lineEnd] == Hash(normalized[(lineEnd + 1)..]);
    }

    /// <summary> Git can hand a file back with other line endings, and some editors add a byte order mark. Neither is an edit. </summary>
    public static string Normalize(string text) => text.TrimStart('﻿').Replace("\r\n", "\n");

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}

/// <summary> Which files an export makes, how each compares with what's on disk, and writing them. Takes the root as an argument, so it runs without the game. </summary>
internal static class ExportPlan
{
    private const string ClassSuffix = ".Vfx.g.cs";
    private const string RootNamespace = "TrelamiumIII";

    private static readonly EnumerationOptions AnyCapitalisation = new() { MatchCasing = MatchCasing.CaseInsensitive };

    private static readonly EnumerationOptions Everywhere = new() { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true, IgnoreInaccessible = true };

    private static readonly Regex FolderPattern = new("^[A-Za-z_][A-Za-z0-9_]*(/[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

    private static readonly Regex NamespacePattern = new(@"^\s*namespace\s+([\w.]+)", RegexOptions.Compiled | RegexOptions.Multiline);

    public static string RecipePath(string name) => $"Assets/Vfx/Recipes/{name}{RecipeFile.Extension}";

    public static string CleanFolder(string folder) => folder.Replace('\\', '/').Trim().Trim('/');

    /// <summary> Null if the folder can hold the class: inside the mod, and with names that work in a namespace. </summary>
    public static string? FolderProblem(string folder)
        => FolderPattern.IsMatch(CleanFolder(folder)) ? null : $"'{folder}' has to be a folder inside the mod, named so it works as a namespace.";

    /// <summary> Where this was last exported to, wherever its files have been moved since. </summary>
    public static string? FindExisting(string root, string name)
    {
        if (!Directory.Exists(root))
            return null;

        foreach (string path in Directory.EnumerateFiles(root, name + ClassSuffix, Everywhere))
        {
            string folder = Path.GetRelativePath(root, Path.GetDirectoryName(path)!).Replace('\\', '/');
            string[] parts = folder.Split('/');

            if (folder != "." && !parts.Contains("bin") && !parts.Contains("obj"))
                return folder;
        }

        return null;
    }

    /// <summary> The folder's, unless a hand-written half is already there saying otherwise: two halves in two namespaces would quietly be two classes. </summary>
    public static string NamespaceOf(string root, string folder, string name)
    {
        string clean = CleanFolder(folder);
        string handWritten = Path.Combine(root, clean, name + ".cs");

        if (File.Exists(handWritten) && NamespacePattern.Match(File.ReadAllText(handWritten)) is { Success: true } match)
            return match.Groups[1].Value;

        return $"{RootNamespace}.{clean.Replace('/', '.')}";
    }

    /// <summary> In the order they get written: a shader before its compiled form, which the build then takes as up to date. </summary>
    /// <param name="shaderText"> The stamped source that <paramref name="compiled" /> came from. </param>
    public static List<ExportFile> Files(VfxRecipe recipe, GeneratedShader shader, string shaderText, byte[] compiled, string folder, string namespaceName, ExportCatalog catalog)
    {
        string name = recipe.Name;
        string clean = CleanFolder(folder);

        return
        [
            new ExportFile(RecipePath(name), Encoding.UTF8.GetBytes(RecipeFile.Serialize(recipe)), ExportFileKind.Recipe),
            new ExportFile($"Assets/Shaders/Vfx/{name}.fx", Encoding.UTF8.GetBytes(shaderText), ExportFileKind.Stamped),
            new ExportFile($"Assets/Shaders/Vfx/{name}.fxc", compiled, ExportFileKind.Compiled),
            new ExportFile($"{clean}/{name}{ClassSuffix}", Encoding.UTF8.GetBytes(ExportStamp.Stamp(FieldExporter.ContentClass(recipe, shader.Uniforms, namespaceName, catalog))), ExportFileKind.Stamped),
            new ExportFile($"{clean}/{name}.cs", Encoding.UTF8.GetBytes(FieldExporter.HandWrittenHalf(recipe, namespaceName)), ExportFileKind.WriteOnce),
        ];
    }

    public static List<ExportRow> Review(string root, IEnumerable<ExportFile> files)
        => files.Select(file => new ExportRow(file, StatusOf(root, file))).ToList();

    /// <returns> How many files were written. </returns>
    public static int Write(string root, IEnumerable<ExportRow> rows)
    {
        int written = 0;

        foreach (ExportRow row in rows.Where(static row => row.WillWrite))
        {
            string path = Path.Combine(root, row.Output.RelativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, row.Output.Content);
            written++;
        }

        return written;
    }

    private static ExportStatus StatusOf(string root, ExportFile file)
    {
        string path = Path.Combine(root, file.RelativePath);
        string folder = Path.GetDirectoryName(path)!;
        string name = Path.GetFileName(path);
        string? existing = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, name, AnyCapitalisation).FirstOrDefault() : null;

        if (existing is null)
            return ExportStatus.New;

        // Written through another spelling the file would keep its old name, and the asset reference the build generates from it wouldn't match the class.
        if (Path.GetFileName(existing) != name)
            return ExportStatus.Misspelled;

        if (file.Kind == ExportFileKind.WriteOnce)
            return ExportStatus.Kept;

        byte[] bytes = File.ReadAllBytes(existing);

        if (file.Kind == ExportFileKind.Compiled)
            return bytes.SequenceEqual(file.Content) ? ExportStatus.Same : ExportStatus.Changed;

        string before = ExportStamp.Normalize(Encoding.UTF8.GetString(bytes));

        if (before == ExportStamp.Normalize(Encoding.UTF8.GetString(file.Content)))
            return ExportStatus.Same;

        return file.Kind == ExportFileKind.Stamped && !ExportStamp.IsIntact(before) ? ExportStatus.Edited : ExportStatus.Changed;
    }
}
