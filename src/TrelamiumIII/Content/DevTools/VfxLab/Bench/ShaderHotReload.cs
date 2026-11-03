using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReLogic.Content;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;

namespace TrelamiumIII.Content.DevTools.VfxLab.Bench;

/// <summary>
///     Recompiles a shader the moment its file is saved and swaps it into the game, with no mod rebuild. Watches the source tree, so it
///     only works where there is one; the next build compiles the same file to the same bytes.
/// </summary>
internal sealed class ShaderHotReload : IDisposable
{
    /// <summary> Editors save in several steps. This long after the last change, the file has settled. </summary>
    private const int SettleMilliseconds = 150;

    private const string ShadersFolder = "Assets/Shaders";

    private static readonly Regex IncludePattern = new(@"^\s*#\s*include\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly string root;
    private readonly string modName;
    private readonly FileSystemWatcher watcher;
    private readonly ConcurrentDictionary<string, byte> changed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch sinceChange = Stopwatch.StartNew();
    private readonly List<(string Name, Task<ShaderCompileResult> Task)> compiling = [];

    /// <summary> Replaced effects. Something may have kept one past its asset, so they live until the mod unloads rather than risk a draw on a freed one. </summary>
    private readonly List<Effect> retired = [];

    private ShaderHotReload(string root, string modName)
    {
        this.root = root;
        this.modName = modName;

        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };

        watcher.Filters.Add("*.fx");
        watcher.Filters.Add("*.fxh");
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnChanged;
        watcher.EnableRaisingEvents = true;
    }

    /// <summary> The last thing that happened, for the status bar. </summary>
    public string Status { get; private set; } = "Watching shaders. Save an .fx to reload it.";

    public bool StatusIsError { get; private set; }

    /// <summary> Raised when anything in the stage library changes, so whatever's built from it can rebuild. </summary>
    public event Action? LibraryChanged;

    /// <summary> Null when there's no source tree to watch. </summary>
    public static ShaderHotReload? Start(Mod mod)
    {
        if (DeveloperConfig.SourceRoot() is not { } source)
            return null;

        string root = Path.Combine(source, ShadersFolder);

        return Directory.Exists(root) ? new ShaderHotReload(root, mod.Name) : null;
    }

    private void OnChanged(object sender, FileSystemEventArgs change)
    {
        changed[change.FullPath] = 0;
        sinceChange.Restart();
    }

    /// <summary> Call every tick. Swapping replaces an effect, which has to happen between draws. </summary>
    public void Update()
    {
        if (!changed.IsEmpty && sinceChange.ElapsedMilliseconds >= SettleMilliseconds)
            StartCompiles();

        for (int i = compiling.Count - 1; i >= 0; i--)
        {
            (string name, Task<ShaderCompileResult> task) = compiling[i];

            if (!task.IsCompleted)
                continue;

            compiling.RemoveAt(i);
            Finish(name, task);
        }
    }

    public void Dispose()
    {
        watcher.Dispose();

        foreach (Effect effect in retired)
            effect.Dispose();

        retired.Clear();
    }

    private void StartCompiles()
    {
        string[] paths = changed.Keys.ToArray();
        changed.Clear();

        HashSet<string> shaders = new(StringComparer.OrdinalIgnoreCase);
        bool library = false;

        foreach (string path in paths)
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

            if (relative.StartsWith(StageLibrary.Folder + "/", StringComparison.OrdinalIgnoreCase))
                library = true;
            else if (path.EndsWith(".fx", StringComparison.OrdinalIgnoreCase))
                shaders.Add(path);
            else
                shaders.UnionWith(Includers(relative));
        }

        if (library)
        {
            LibraryChanged?.Invoke();
            Report("Stage library changed.", false);
        }

        // By its path under the shaders folder, as that's its asset's name, and an exported shader is a folder down.
        foreach (string shader in shaders.Where(File.Exists))
            compiling.Add((Path.ChangeExtension(Path.GetRelativePath(root, shader), null).Replace('\\', '/'), ShaderCompiler.CompileFileAsync(shader, LabFiles.ShaderCache)));
    }

    /// <summary> The shaders whose source names this header. </summary>
    private IEnumerable<string> Includers(string header)
    {
        foreach (string path in Directory.EnumerateFiles(root, "*.fx"))
        {
            string text;

            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (IncludePattern.Matches(text).Any(match => string.Equals(match.Groups[1].Value, header, StringComparison.OrdinalIgnoreCase)))
                yield return path;
        }
    }

    private void Finish(string name, Task<ShaderCompileResult> task)
    {
        ShaderCompileResult result = task.Result;

        if (result.Code is not { } code)
        {
            // The first line says where; the shader that was running carries on.
            Report(WithoutFolder(result.Log.Split('\n')[0]), true);
            return;
        }

        string key = $"{modName}/{ShadersFolder}/{name}";

        if (!ModContent.RequestIfExists(key, out Asset<Effect> asset, AssetRequestMode.ImmediateLoad))
        {
            Report($"{name} compiled, but it's new: rebuild the mod to load it.", false);
            return;
        }

        try
        {
            var effect = new Effect(Main.instance.GraphicsDevice, code);

            if (asset.Value is { } old)
                retired.Add(old);

            asset.ownValue = effect;
            Report($"{name} reloaded in {result.Milliseconds:F0} ms.", false);
        }
        catch (Exception exception)
        {
            Report($"{name}: {exception.Message}", true);
        }
    }

    /// <summary> Everything fxc reports starts with the file's full path, which would push the error itself off the status bar. </summary>
    private static string WithoutFolder(string line)
    {
        int position = line.IndexOf('(');

        return position > 0 ? Path.GetFileName(line[..position]) + line[position..] : line;
    }

    private void Report(string status, bool isError)
    {
        Status = status;
        StatusIsError = isError;
    }
}
