using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <param name="Code"> The compiled effect, or null if it didn't compile. </param>
/// <param name="Log"> What fxc said, without the lines it prints for every effect. </param>
internal readonly record struct ShaderCompileResult(byte[]? Code, string Log, double Milliseconds, bool Cached);

/// <summary>
///     Compiles HLSL source while the game runs, with the fxc.exe and the arguments the SDK builds the mod's shaders with, so a
///     shader compiled here is byte-identical to the one the next build produces.
/// </summary>
/// <remarks> Only a machine with the SDK's NuGet package has the compiler; <see cref="FxcPath" /> is null anywhere else. </remarks>
internal static class ShaderCompiler
{
    private const int TimeoutMilliseconds = 30_000;

    private const string MissingCompiler = "fxc.exe wasn't found in the NuGet cache.";

    /// <summary> What fxc prints for every effect, whether anything is wrong or not. </summary>
    private static readonly string[] Noise = ["X4717", "compilation object save succeeded"];

    private static readonly Lazy<string?> fxc = new(Locate);

    public static string? FxcPath => fxc.Value;

    /// <summary> Runs on a worker thread, as a compile takes far longer than a frame. Hand the code to <see cref="LiveEffect" /> from the main thread. </summary>
    /// <param name="folder"> Where sources and results are kept by hash, so undoing an edit lands on a shader that's already compiled. </param>
    public static Task<ShaderCompileResult> CompileAsync(string source, string folder) => Task.Run(() => Compile(source, folder));

    /// <summary> Compiles a shader where it sits, so its <c>#include</c>s resolve. Never cached, as what it includes can change without it changing. </summary>
    /// <param name="folder"> Where the result is put while it's read back. </param>
    public static Task<ShaderCompileResult> CompileFileAsync(string path, string folder) => Task.Run(() => CompileFile(path, folder));

    /// <summary> The newest version of the SDK in the NuGet cache that has the compiler. </summary>
    private static string? Locate()
    {
        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        string sdk = Path.Combine(packages, "tomat.terraria.modloader.sdk");

        if (!Directory.Exists(sdk))
            return null;

        return Directory.GetDirectories(sdk)
            .OrderByDescending(static path => Version.TryParse(Path.GetFileName(path), out Version? version) ? version : new Version())
            .Select(static path => Path.Combine(path, "Sdk", "native", "fxc.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static ShaderCompileResult Compile(string source, string folder)
    {
        var watch = Stopwatch.StartNew();

        if (FxcPath is null)
            return new ShaderCompileResult(null, MissingCompiler, 0d, false);

        Directory.CreateDirectory(folder);

        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..16];
        string input = name + ".fx";
        string output = Path.Combine(folder, name + ".fxc");

        if (File.Exists(output))
            return new ShaderCompileResult(File.ReadAllBytes(output), string.Empty, watch.Elapsed.TotalMilliseconds, true);

        File.WriteAllText(Path.Combine(folder, input), source);

        return RunCompiler(folder, input, output, watch);
    }

    private static ShaderCompileResult CompileFile(string path, string folder)
    {
        var watch = Stopwatch.StartNew();

        if (FxcPath is null)
            return new ShaderCompileResult(null, MissingCompiler, 0d, false);

        Directory.CreateDirectory(folder);

        string output = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".fxc");

        try
        {
            return RunCompiler(Path.GetDirectoryName(path)!, Path.GetFileName(path), output, watch);
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static ShaderCompileResult RunCompiler(string workingDirectory, string input, string outputPath, Stopwatch watch)
    {
        var info = new ProcessStartInfo(FxcPath!, $"/T fx_2_0 \"{input}\" /Fo \"{outputPath}\" /D FX=1 /O3 /Op /nologo")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        using Process process = Process.Start(info)!;

        // Read together: either pipe filling up while the other is awaited would stall fxc.
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task<string> messages = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(TimeoutMilliseconds))
        {
            process.Kill();

            return new ShaderCompileResult(null, "fxc.exe timed out.", watch.Elapsed.TotalMilliseconds, false);
        }

        string log = string.Join('\n', (errors.Result + messages.Result)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !Noise.Any(line.Contains)));

        byte[]? code = process.ExitCode == 0 && File.Exists(outputPath) ? File.ReadAllBytes(outputPath) : null;

        return new ShaderCompileResult(code, log, watch.Elapsed.TotalMilliseconds, false);
    }
}
