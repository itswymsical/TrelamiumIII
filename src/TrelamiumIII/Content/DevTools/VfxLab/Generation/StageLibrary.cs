using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary> Everything the generator builds from: the shared code, and every host and stage file that parsed. </summary>
/// <remarks> A bad file is a problem for that file alone. The rest of the library still works, so one typo can't take the lab down. </remarks>
internal sealed class StageLibrary
{
    public const string HostExtension = ".host.fxh";
    public const string StageExtension = ".stage.fxh";
    public const string CommonFile = "Common.fxh";

    /// <summary> The folder under <c>Assets/Shaders</c> that holds the library. </summary>
    public const string Folder = "Lab";

    private readonly Dictionary<string, StageDefinition> stages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HostDefinition> hosts = new(StringComparer.Ordinal);
    private readonly List<string> problems = [];

    public IReadOnlyDictionary<string, StageDefinition> Stages => stages;

    public IReadOnlyDictionary<string, HostDefinition> Hosts => hosts;

    public IReadOnlyList<string> Problems => problems;

    public string Common { get; private set; } = string.Empty;

    public string CommonSource { get; private set; } = CommonFile;

    /// <param name="files"> Each file's path, as compile errors should name it, and its text. Other kinds of file are ignored. </param>
    public static StageLibrary From(IEnumerable<(string Path, string Text)> files)
    {
        var library = new StageLibrary();

        foreach ((string path, string text) in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
            library.Add(path, text);

        if (library.Common.Length == 0)
            library.problems.Add($"No {CommonFile} in the library.");

        return library;
    }

    /// <param name="shadersRoot"> The folder holding <see cref="Folder" />, so paths match on every machine. </param>
    public static StageLibrary FromFolder(string shadersRoot)
    {
        string root = Path.Combine(shadersRoot, Folder);

        return From(Directory.EnumerateFiles(root, "*.fxh", SearchOption.AllDirectories)
            .Select(path => (Path.GetRelativePath(shadersRoot, path).Replace('\\', '/'), File.ReadAllText(path))));
    }

    /// <summary> The stages <paramref name="host" /> can run, for the inspector's list. </summary>
    public IEnumerable<StageDefinition> CompatibleStages(HostDefinition host)
        => stages.Values.Where(stage => stage.Needs.All(host.Provides.Contains));

    private void Add(string path, string text)
    {
        try
        {
            if (path.EndsWith(HostExtension, StringComparison.Ordinal))
            {
                HostDefinition host = StageParser.ParseHost(path, text);

                if (!hosts.TryAdd(host.Name, host))
                    problems.Add($"{path}: another file already defines the host {host.Name}.");
            }
            else if (path.EndsWith(StageExtension, StringComparison.Ordinal))
            {
                StageDefinition stage = StageParser.ParseStage(path, text);

                if (!stages.TryAdd(stage.Name, stage))
                    problems.Add($"{path}: another file already defines the stage {stage.Name}.");
            }
            else if (Path.GetFileName(path) == CommonFile)
            {
                Common = StageParser.Normalize(text);
                CommonSource = path;
            }
        }
        catch (StageFormatException exception)
        {
            problems.Add(exception.Message);
        }
    }
}
