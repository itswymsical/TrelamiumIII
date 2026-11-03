using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.UI;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     Drives the lab's real panels with scripted mouse input, the way someone would use them: picks a field, drags and types values,
///     chooses a colour and a texture, resets, compares and copies, then edits a shader file and watches it reload.
/// </summary>
public sealed partial class VfxLabHarness
{
    private const string SourceVariable = "TRELAMIUM_VFX_SOURCE";

    private const string Tuned = "IceMistField";

    /// <summary> Shares GasFbm with the field being tuned, to show values don't leak from one layer to another. </summary>
    private const string Neighbour = "BloodField";

    private const int ReloadTimeoutTicks = 300;

    private static readonly Queue<LabInput> scripted = new();

    private static int benchFailures;

    private static void Check(string name, bool ok, string detail = "")
    {
        benchFailures += ok ? 0 : 1;
        Log($"{(ok ? "PASS" : "FAIL")}: {name}{(detail.Length > 0 ? $" ({detail})" : string.Empty)}");
    }

    private static IEnumerable<bool> BenchScript()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is { Length: > 0 } source)
            DeveloperConfig.Instance.SourceDirectory = source;

        waitTicks = SettleTicks;
        yield return true;

        VfxLabUI.ScriptedFrames = scripted;
        VfxLabUI.Gui.Recorded = [];
        ModContent.GetInstance<VfxLabUI>().Toggle();
        Log($"opened; {ShaderBench.Targets.Count} targets, {ShaderBench.NoiseTextures.Count} noise textures");

        frozenTime = Main.timeForVisualEffects;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0f;

        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_open")) yield return step;

        foreach (bool step in Click($"row:{Tuned}")) yield return step;
        BenchTarget? selected = ShaderBench.Selected;
        Check("clicking a row selects it", selected?.Name == Tuned, selected?.Name ?? "nothing");

        if (selected is null)
            yield break;

        foreach (bool step in Click("test")) yield return step;
        SpawnNeighbourCloud();
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_selected")) yield return step;
        Color[] before = capturedPixels!;
        Check("the selected field shows as drawing", ShaderBench.IsDrawing(selected));
        Check("the authored value was read", selected.Authored.TryGetValue("Threshold", out UniformState authored) && Math.Abs(authored.Numbers![0] - 0.14f) < 0.001f);

        // Dragging a number.
        foreach (bool step in Drag($"{Tuned}/Threshold#0", 70)) yield return step;
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_dragged")) yield return step;
        Color[] dragged = capturedPixels!;
        bool hasThreshold = selected.Overrides.TryGetValue("Threshold", out UniformState threshold);
        Check("dragging a number overrides it", hasThreshold && threshold.Numbers![0] > 0.14f, hasThreshold ? threshold.Numbers![0].ToString("F3") : "no override");

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        Rectangle tunedArea = TunedArea(width, height);
        Rectangle neighbourArea = NeighbourArea(width, height);

        (double tunedChange, _) = Difference(before, dragged, width, tunedArea);
        (double neighbourChange, _) = Difference(before, dragged, width, neighbourArea);
        Check("the change reaches the screen", tunedChange > 1d, $"mean change {tunedChange:F2}/255");
        Check("a layer sharing the shader is untouched", neighbourChange < 0.5d, $"mean change {neighbourChange:F2}/255");

        // Comparing against the authored look.
        foreach (bool step in Click("bypass")) yield return step;
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_bypass")) yield return step;
        (double bypassChange, _) = Difference(before, capturedPixels!, width, tunedArea);
        Check("Compare draws the authored look", bypassChange < 0.5d, $"mean change {bypassChange:F2}/255");
        foreach (bool step in Click("bypass")) yield return step;

        // Clicking a number to type one.
        foreach (bool step in Type($"{Tuned}/BodyAlpha#0", "0.35")) yield return step;
        bool typed = selected.Overrides.TryGetValue("BodyAlpha", out UniformState bodyAlpha);
        Check("typing a number overrides it", typed && Math.Abs(bodyAlpha.Numbers![0] - 0.35f) < 0.001f, typed ? bodyAlpha.Numbers![0].ToString("F3") : "no override");

        // The colour popup.
        foreach (bool step in Click($"{Tuned}/BodyColor#swatch")) yield return step;
        foreach (bool step in Capture("bench_color_popup")) yield return step;
        foreach (bool step in ClickPopup(0.3f, 0.4f)) yield return step;
        Check("the colour popup sets a colour", selected.Overrides.ContainsKey("BodyColor"));
        foreach (bool step in ClickAt(new Point(width / 2, height - 200))) yield return step;

        // The texture popup.
        foreach (bool step in Click($"{Tuned}/uNoiseTex")) yield return step;
        foreach (bool step in Capture("bench_texture_popup")) yield return step;
        foreach (bool step in ClickPopup(0.5f, 0.5f)) yield return step;
        bool hasTexture = selected.Overrides.TryGetValue("uNoiseTex", out UniformState noiseTexture);
        Check("the texture popup sets a texture", hasTexture && noiseTexture.TextureName is not null, hasTexture ? noiseTexture.TextureName ?? "no name" : "no override");

        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_tuned")) yield return step;

        // Right-click resets one.
        foreach (bool step in RightClick($"{Tuned}/Threshold#0")) yield return step;
        Check("right-click resets a value", !selected.Overrides.ContainsKey("Threshold"));

        // Copy as C#.
        foreach (bool step in Click("copy")) yield return step;
        string copied = VfxLabUI.LastCopied ?? string.Empty;
        File.WriteAllText(Path.Combine(output!, "vfx_copied.txt"), copied);
        Check("Copy as C# writes a look", copied.StartsWith("Look with", StringComparison.Ordinal) && copied.Contains("BodyAlpha = 0.35f,") && copied.Contains("Noise = static () =>"), copied.Replace('\n', ' '));

        foreach (bool step in Click("reset")) yield return step;
        Check("Reset clears every value", selected.Overrides.Count == 0);

        foreach (bool step in Click("clear")) yield return step;
        ((FieldTarget)ShaderBench.Targets.First(static target => target.Name == Neighbour)).ClearTest();
        Check("Clear removes the test cloud", !ShaderBench.IsDrawing(selected) || !selected.HasContent);

        // A residue, through the same inspector.
        foreach (bool step in Click("tab1")) yield return step;
        waitTicks = 2;
        yield return true;
        BenchTarget slime = ShaderBench.Targets.First(static target => target.Name == "SlimedResidue");
        foreach (bool step in Click("row:SlimedResidue")) yield return step;
        Check("a residue can be selected", ShaderBench.Selected == slime);
        foreach (bool step in Click("test")) yield return step;
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Drag("SlimedResidue/Threshold#0", 40)) yield return step;
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_condition")) yield return step;
        Check("a residue's value can be overridden", slime.Overrides.ContainsKey("Threshold"));
        Check("a residue shows as drawing", ShaderBench.IsDrawing(slime));
        Check("and one sharing its shader doesn't", !ShaderBench.IsDrawing(ShaderBench.Targets.First(static target => target.Name == "MuddiedResidue")));

        // Typing into a field, then the row going away while it's focused, mustn't leave the keyboard held.
        foreach (bool step in Click("SlimedResidue/Bands#0")) yield return step;
        Check("clicking a number starts typing", VfxLabUI.Gui.IsEditing);
        VfxLabUI.Select(ShaderBench.Targets.First(static target => target.Name == "MuddiedResidue"));
        waitTicks = 2;
        yield return true;
        Check("changing target lets go of the keyboard", !VfxLabUI.Gui.IsEditing);
        VfxLabUI.Select(slime);
        waitTicks = 2;
        yield return true;
        foreach (bool step in Click("copy")) yield return step;
        Check("Copy as C# writes a film look", (VfxLabUI.LastCopied ?? string.Empty).Contains("Threshold ="), (VfxLabUI.LastCopied ?? string.Empty).Replace('\n', ' '));
        foreach (bool step in Click("reset")) yield return step;
        foreach (bool step in Click("clear")) yield return step;

        // Any shader at all, whether or not it's drawing right now.
        foreach (bool step in Click("tab2")) yield return step;
        waitTicks = 2;
        yield return true;
        foreach (bool step in Scripted(new LabInput { Mouse = RecordedArea("row:ActionLines").Center, Scroll = -1 })) yield return step;
        foreach (bool step in Capture("bench_shaders")) yield return step;

        int opened = 0;

        foreach (BenchTarget target in ShaderBench.Targets.Where(static target => target.Group == BenchGroup.Shaders && target.Effect is not null))
        {
            VfxLabUI.Select(target);
            waitTicks = 1;
            yield return true;
            opened++;
        }

        Check("every shader opens in the inspector", opened > 40, $"{opened} opened");

        foreach (bool step in HotReloadCheck()) yield return step;

        VfxLabUI.ScriptedFrames = null;
        frozenTime = null;

        Log(benchFailures == 0 ? "PASS: everything on the Bench works" : $"FAIL: {benchFailures} Bench checks failed");
    }

    /// <summary> Saves a real change to a shader file and watches it reach the screen with no rebuild. Only ever edits the copy it's pointed at. </summary>
    private static IEnumerable<bool> HotReloadCheck()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is not { Length: > 0 } source || VfxLabUI.HotReload is not { } reload)
        {
            Log("not tested: hot reload, as no source copy was given");
            yield break;
        }

        BenchTarget mist = ShaderBench.Targets.First(static target => target.Name == Tuned);
        VfxLabUI.Select(mist);
        mist.Test();
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_reload_before")) yield return step;
        Color[] before = capturedPixels!;

        string file = Path.Combine(source, "Assets", "Shaders", "GasFbm.fx");
        string original = File.ReadAllText(file);
        Effect? effectBefore = mist.Effect;

        const string BodyLine = "return float4(color * light * alpha, alpha);";

        if (!original.Contains(BodyLine))
        {
            Check("GasFbm.fx still has the line this test edits", false);
            yield break;
        }

        File.WriteAllText(file, original.Replace(BodyLine, "return float4(color * light * alpha * 0.55, alpha);"));

        for (int i = 0; i < ReloadTimeoutTicks && ReferenceEquals(mist.Effect, effectBefore); i++)
            yield return true;

        Check("saving a shader reloads it", !ReferenceEquals(mist.Effect, effectBefore), reload.Status);

        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("bench_reload_after")) yield return step;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        (double change, _) = Difference(before, capturedPixels!, width, TunedArea(width, height));
        Check("the reloaded shader reaches the screen", change > 1d, $"mean change {change:F2}/255");

        Effect? good = mist.Effect;
        File.WriteAllText(file, original.Replace(BodyLine, "return nope;"));

        for (int i = 0; i < ReloadTimeoutTicks && !reload.StatusIsError; i++)
            yield return true;

        foreach (bool step in Capture("bench_reload_error")) yield return step;
        Check("a broken save is reported", reload.StatusIsError && reload.Status.Contains("nope"), reload.Status);
        Check("and leaves the last good shader", ReferenceEquals(mist.Effect, good));

        File.WriteAllText(file, original);

        for (int i = 0; i < ReloadTimeoutTicks && reload.StatusIsError; i++)
            yield return true;

        Check("fixing it reloads again", !reload.StatusIsError, reload.Status);

        mist.ClearTest();
    }

    /// <summary> Over to the right of the test cloud, in another field that shares the same shader. </summary>
    private static void SpawnNeighbourCloud()
    {
        FieldLayer layer = FieldSystem.Layers.First(static layer => layer.GetType().Name == Neighbour);
        Vector2 center = Main.LocalPlayer.Center + new Vector2(-560f, -180f);

        for (int column = 0; column < 4; column++)
        {
            for (int row = 0; row < 4; row++)
                layer.SpawnNode(center + new Vector2(column * 50f, row * 50f - 100f), Vector2.Zero, 56f, 1e7f, pinned: true);
        }
    }

    /// <summary> The test cloud's left half. Story dialogue can pop up over its right half. </summary>
    private static Rectangle TunedArea(int width, int height) => new(width / 2 - 215, height / 2 - 260, 200, 160);

    private static Rectangle NeighbourArea(int width, int height) => new(width / 2 - 540, height / 2 - 270, 150, 140);

    private static Rectangle RecordedArea(string id)
        => VfxLabUI.Gui.Recorded!.TryGetValue(id, out Rectangle area) ? area : throw new InvalidOperationException($"No control called {id} has been drawn.");

    private static IEnumerable<bool> Scripted(params LabInput[] frames)
    {
        foreach (LabInput frame in frames)
            scripted.Enqueue(frame);

        while (scripted.Count > 0)
            yield return true;

        // One more, so whatever the last frame did has been drawn and read.
        waitTicks = 2;
        yield return true;
    }

    private static IEnumerable<bool> ClickAt(Point at) => Scripted(
        new LabInput { Mouse = at },
        new LabInput { Mouse = at, LeftDown = true, LeftPressed = true },
        new LabInput { Mouse = at, LeftReleased = true },
        new LabInput { Mouse = at });

    private static IEnumerable<bool> Click(string id) => ClickAt(RecordedArea(id).Center);

    private static IEnumerable<bool> RightClick(string id)
    {
        Point at = RecordedArea(id).Center;

        return Scripted(
            new LabInput { Mouse = at },
            new LabInput { Mouse = at, RightDown = true, RightPressed = true },
            new LabInput { Mouse = at });
    }

    private static IEnumerable<bool> Drag(string id, int pixels)
    {
        Point at = RecordedArea(id).Center;
        var end = new Point(at.X + pixels, at.Y);

        return Scripted(
            new LabInput { Mouse = at },
            new LabInput { Mouse = at, LeftDown = true, LeftPressed = true },
            new LabInput { Mouse = new Point(at.X + pixels / 2, at.Y), LeftDown = true },
            new LabInput { Mouse = end, LeftDown = true },
            new LabInput { Mouse = end, LeftReleased = true },
            new LabInput { Mouse = end });
    }

    /// <summary> Clicks a number, which starts typing, then enters the text. </summary>
    private static IEnumerable<bool> Type(string id, string text)
    {
        Point at = RecordedArea(id).Center;

        return Scripted(
            new LabInput { Mouse = at },
            new LabInput { Mouse = at, LeftDown = true, LeftPressed = true },
            new LabInput { Mouse = at, LeftReleased = true },
            new LabInput { Mouse = at, ScriptedText = text },
            new LabInput { Mouse = at, ScriptedText = text, EnterPressed = true },
            new LabInput { Mouse = at });
    }

    /// <summary> A click at a fraction of the open popup. </summary>
    private static IEnumerable<bool> ClickPopup(float x, float y)
    {
        Rectangle popup = VfxLabUI.PopupArea;

        return ClickAt(new Point(popup.X + (int)(popup.Width * x), popup.Y + (int)(popup.Height * y)));
    }
}
