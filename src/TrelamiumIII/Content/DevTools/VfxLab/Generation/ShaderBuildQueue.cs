using System.Threading.Tasks;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

internal enum ShaderBuildStatus
{
    /// <summary> Nothing has been asked for yet. </summary>
    Idle,

    Compiling,

    /// <summary> The newest source is the one on screen. </summary>
    Compiled,

    /// <summary> The newest source didn't compile. The last one that did is still on screen. </summary>
    Failed,
}

/// <summary>
///     Turns a stream of edits into at most one compile at a time, always ending on the newest source. Compiles run in the
///     background and each result is handed over from <see cref="Update" />, on the main thread.
/// </summary>
/// <param name="compile"> Started from the main thread, finished wherever it likes. </param>
/// <param name="swap"> Puts a compiled effect on screen. May throw, which counts as a failed compile. </param>
internal sealed class ShaderBuildQueue(Func<string, Task<ShaderCompileResult>> compile, Action<byte[]> swap)
{
    private Task<ShaderCompileResult>? running;
    private string? runningSource;
    private string? nextSource;
    private string? appliedSource;

    public ShaderBuildStatus Status { get; private set; }

    /// <summary> What the compiler said about the newest finished source, warnings included. </summary>
    public string Log { get; private set; } = string.Empty;

    public void Submit(string source)
    {
        // Back to what's already compiling: forget anything queued behind it.
        if (source == runningSource)
        {
            nextSource = null;
            return;
        }

        if (running is null && source == appliedSource)
        {
            nextSource = null;
            Status = ShaderBuildStatus.Compiled;
            return;
        }

        nextSource = source;
        Status = ShaderBuildStatus.Compiling;
    }

    /// <summary> Call every tick, between draws, as a swap disposes the effect it replaces. </summary>
    public void Update()
    {
        if (running is { IsCompleted: true } finished)
            Finish(finished);

        if (running is not null || nextSource is not { } source)
            return;

        nextSource = null;

        if (source == appliedSource)
        {
            Status = ShaderBuildStatus.Compiled;
            return;
        }

        runningSource = source;
        running = compile(source);
        Status = ShaderBuildStatus.Compiling;
    }

    private void Finish(Task<ShaderCompileResult> finished)
    {
        string source = runningSource!;

        running = null;
        runningSource = null;

        // Superseded while it ran. Putting it on screen would only flash an edit that's already been replaced.
        if (nextSource is not null)
            return;

        if (finished.IsFaulted)
        {
            Fail(finished.Exception!.GetBaseException().Message);
            return;
        }

        ShaderCompileResult result = finished.Result;

        Log = result.Log;

        if (result.Code is not { } code)
        {
            Status = ShaderBuildStatus.Failed;
            return;
        }

        try
        {
            swap(code);
            appliedSource = source;
            Status = ShaderBuildStatus.Compiled;
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void Fail(string message)
    {
        Log = message;
        Status = ShaderBuildStatus.Failed;
    }
}
