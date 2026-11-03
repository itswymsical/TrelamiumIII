using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Terraria.IO;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     Dev tool. Runs a script against the lab inside a real game client, writes screenshots and vfx_report.txt, and quits without
///     saving. Runs only when <see cref="OutputVariable" /> names an output folder; tools/VfxLabHarness/Run-Harness.ps1 launches
///     a client into a throwaway world.
/// </summary>
/// <remarks>
///     Someone may be watching the window, so a script must never strobe or flash it: dim test colours, shown steadily, and stress
///     tests that swap between things that look the same.
/// </remarks>
public sealed partial class VfxLabHarness : ModSystem
{
    private const string OutputVariable = "TRELAMIUM_VFX_HARNESS";
    private const string ScriptVariable = "TRELAMIUM_VFX_SCRIPT";
    private const string PlayerName = "Harness";

    private const int SettleTicks = 180;
    private const int GiveUpTicks = 60 * 60 * 4;

    private static readonly List<double> frames = [];
    private static readonly Stopwatch frameClock = new();

    private static string? output;
    private static string? scriptName;
    private static Mod? harnessMod;
    private static LabFieldSlot? slot;
    private static IEnumerator<bool>? script;
    private static int waitTicks;
    private static int totalTicks;
    private static bool finished;

    /// <summary> Holds the shaders' clock still, so two captures can be compared. </summary>
    private static double? frozenTime;

    /// <summary> Otherwise it's always dry, to keep captures comparable. </summary>
    private static bool rainHeld;

    /// <summary> For a script that compares screenshots first and drives the panels after. </summary>
    private static bool showPanels;

    private static string? captureName;
    private static Color[]? capturedPixels;
    private static Vector3 capturedMean;

    /// <summary> Run once after the next frame is drawn, the one place a script may bind render targets. </summary>
    private static Action? drawJob;

    public override void PostSetupContent()
    {
        if (Main.dedServ || Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } path)
            return;

        Directory.CreateDirectory(path);
        File.Delete(Path.Combine(path, "vfx_report.txt"));
        output = path;
        scriptName = Environment.GetEnvironmentVariable(ScriptVariable);
        harnessMod = Mod;

        try
        {
            Directory.CreateDirectory(Main.PlayerPath);

            if (Directory.GetFiles(Main.PlayerPath, "*.plr").Length == 0)
                PlayerFileData.CreateAndSave(new Player { name = PlayerName, difficulty = PlayerDifficultyID.SoftCore });

            slot = FieldSystem.GetLayer<LabFieldSlot>() ?? throw new InvalidOperationException("LabFieldSlot wasn't registered.");
        }
        catch (Exception exception)
        {
            Fail(exception);
        }

        Main.OnPostDraw += OnFrame;
    }

    public override void Unload() => Main.OnPostDraw -= OnFrame;

    public override void PostUpdateEverything()
    {
        if (output is null || slot is null || Main.gameMenu || finished)
            return;

        // Behind another window the game would sleep between frames and stop being a fair measure of anything.
        Main.instance.IsActive = true;
        Main.instance.InactiveSleepTime = TimeSpan.Zero;

        if (++totalTicks > GiveUpTicks)
            Finish("gave up: ran out of time");

        Player player = Main.LocalPlayer;
        player.immune = true;
        player.immuneTime = Math.Max(player.immuneTime, 2);
        player.statLife = player.statLifeMax2;
        Main.hideUI = !showPanels && scriptName is not ("bench" or "lab" or "export");
        Main.dayTime = true;
        Main.time = 27000;
        Main.raining = rainHeld;

        if (frozenTime is { } time)
            Main.timeForVisualEffects = time;

        if (waitTicks > 0)
        {
            waitTicks--;
            return;
        }

        try
        {
            script ??= (scriptName switch
            {
                "parity" => ParityScript(),
                "bench" => BenchScript(),
                "lab" => LabScript(),
                "export" => ExportScript(),
                "exported" => ExportedScript(),
                _ => SpikeScript(),
            }).GetEnumerator();

            if (!script.MoveNext())
                Finish("done");
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static IEnumerable<bool> Compile(string source, Action<ShaderCompileResult> done)
    {
        Task<ShaderCompileResult> task = ShaderCompiler.CompileAsync(source, LabFiles.ShaderCache);

        while (!task.IsCompleted)
            yield return true;

        done(task.Result);
    }

    private static IEnumerable<bool> Capture(string name)
    {
        captureName = name;

        while (captureName is not null)
            yield return true;
    }

    private static IEnumerable<bool> RunOnDraw(Action job)
    {
        drawJob = job;

        while (drawJob is not null)
            yield return true;
    }

    /// <summary> After the frame is drawn and before it is presented: the backbuffer holds exactly what the player sees. </summary>
    private static void OnFrame(GameTime gameTime)
    {
        if (output is null || finished || Main.gameMenu)
            return;

        if (frameClock.IsRunning)
            frames.Add(frameClock.Elapsed.TotalMilliseconds);

        try
        {
            if (captureName is { } name)
            {
                SaveCapture(name);
                captureName = null;
            }

            if (drawJob is { } job)
            {
                job();
                drawJob = null;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }

        // After the capture, or the time spent encoding a screenshot reads as a slow frame.
        frameClock.Restart();
    }

    private static void SaveCapture(string name)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        device.GetBackBufferData(pixels);

        PngWriter.Write(Path.Combine(output!, $"vfx_{name}.png"), width, height, (x, y) => pixels[y * width + x]);

        capturedPixels = pixels;

        // The middle of the screen, which a node grid covers; a little of it is the player.
        Vector3 sum = Vector3.Zero;
        int count = 0;

        for (int y = height / 2 - 150; y < height / 2 + 150; y += 3)
        {
            for (int x = width / 2 - 300; x < width / 2 + 300; x += 3)
            {
                sum += pixels[y * width + x].ToVector3();
                count++;
            }
        }

        capturedMean = sum / Math.Max(count, 1);
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.Order()];

        return sorted.Length == 0 ? 0d : sorted[sorted.Length / 2];
    }

    private static string Summary(IReadOnlyCollection<double> values)
        => values.Count == 0 ? "no samples" : $"min {values.Min():F2} / median {Median(values):F2} / max {values.Max():F2} over {values.Count}";

    /// <summary> Written as it goes, so a native crash still leaves everything up to that point. </summary>
    private static void Log(string line)
    {
        if (output is not null)
            File.AppendAllText(Path.Combine(output, "vfx_report.txt"), line + Environment.NewLine);
    }

    private static void Fail(Exception exception)
    {
        Log($"EXCEPTION: {exception}");
        Finish("failed");
    }

    private static void Finish(string reason)
    {
        finished = true;
        Log(reason);

        // Straight out: nothing in this throwaway world is worth saving, and Environment.Exit hangs from inside the game loop.
        Process.GetCurrentProcess().Kill();
    }
}
