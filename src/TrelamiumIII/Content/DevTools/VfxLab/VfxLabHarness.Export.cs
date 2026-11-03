using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ReLogic.Content;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Export;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Content.DevTools.VfxLab.UI;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Vfx;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     An export there and back. <c>export</c> drives the review with scripted input and writes two fields into a copy of the source. After a
///     rebuild of that copy, <c>exported</c> runs them with the lab unused, then holds them to the drafts they came from.
/// </summary>
public sealed partial class VfxLabHarness
{
    private const string ProbeField = "ExportProbe";
    private const string LiquidField = "ExportLiquid";
    private const string OddField = "lowerPlasma";
    private const string MovedFolder = "Content/VfxMoved";

    private const int ResidueTicks = 300;

    /// <summary>
    ///     Heavy enough to land, with modules that aren't the defaults, in muted colours that show on any sky. Every path through the shader
    ///     is switched on, or nudging a value behind a switched-off one couldn't change the picture.
    /// </summary>
    private static VfxRecipe ProbeRecipe()
    {
        VfxRecipe recipe = RecipeEdit.NewField(ProbeField);

        recipe.Physics.Density = 0.7f;
        recipe.Physics.Drag = 0.96f;
        recipe.Physics.MaxNodes = 300;
        recipe.Physics.TemperatureMax = 900f;
        recipe.Stages.First(static stage => stage.Id == "noise").Set("Noise", "Turbulence_06").Set("Scale", 0.8f).Set("Stretch", 1f, 0.6f).Set("ShadeShare", 0.7f);
        recipe.Stages.First(static stage => stage.Id == "cut").Set("Level", 0.3f);
        recipe.Stages.First(static stage => stage.Id == "shade").Set("Bands", 5f).Set("Range", 0.6f);
        recipe.Stages.First(static stage => stage.Id == "palette")
            .Set("TemperatureBlend", 1.5f)
            .Set("HotShadow", 0.4f, 0.1f, 0.05f)
            .Set("HotBody", 0.6f, 0.3f, 0.1f)
            .Set("HotCore", 0.7f, 0.6f, 0.3f)
            .Set("AlphaFloor", 0.4f);
        recipe.Stages.First(static stage => stage.Id == "light").Set("Floor", 0.3f, 0.2f, 0.1f);
        recipe.Stages.First(static stage => stage.Id == "rim")
            .Set("OutlineAlpha", 0.6f)
            .Set("HotOutline", 0.5f, 0.2f, 0.1f)
            .Set("Highlight", 0.7f, 0.7f, 0.6f)
            .Set("HighlightAlpha", 0.5f)
            .Set("HotHighlight", 0.7f, 0.5f, 0.3f);
        RecipeEdit.AddStage(recipe, "FlowAdvect").Set("Strength", 0.4f);
        RecipeEdit.AddStage(recipe, "DomainWarp").Set("Amount", 0.15f).Set("Octaves", 3).Set("Noise", "Milky_1").Set("Drift", 0.02f, 0.01f);
        recipe.Modules.Add(new RecipeModule { Type = "WindForce" }.Set("Pull", 0.03f));
        recipe.Modules.Add(new RecipeModule { Type = "CurlFlow" }.Set("Strength", 0.2f));
        recipe.Links.Deposit = new RecipeDeposit { Residue = "SlimedCondition", Threshold = 0.3f, Interval = 5 };
        recipe.Emitter.Radius = 36f;

        return recipe;
    }

    private static string ExportRows() => string.Join(",", VfxLabUI.Export.Rows.Select(static row => row.Status));

    private static string ExportProblems() => string.Join("; ", VfxLabUI.Export.Problems);

    private static IEnumerable<bool> OpenExport()
    {
        foreach (bool step in Click("export")) yield return step;

        for (int i = 0; i < BuildTimeoutTicks && VfxLabUI.Export.Compiling; i++)
            yield return true;

        waitTicks = 2;
        yield return true;
    }

    private static IEnumerable<bool> Rename(LabSlot draft, string name)
    {
        foreach (bool step in Type("name", name)) yield return step;

        if (draft.Recipe!.Name != name)
            Check($"the recipe is renamed {name}", false, draft.Recipe.Name);
    }

    private static IEnumerable<bool> ExportScript()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is not { Length: > 0 } source)
        {
            Check("export is given a copy of the source to write to", false);
            yield break;
        }

        DeveloperConfig.Instance.SourceDirectory = source;

        waitTicks = SettleTicks;
        yield return true;

        scripted.Clear();
        VfxLabUI.ScriptedFrames = scripted;
        VfxLabUI.Gui.Recorded = [];
        ModContent.GetInstance<VfxLabUI>().Toggle();
        waitTicks = 10;
        yield return true;

        string At(string relative) => Path.Combine(source, relative);

        string recipeFile = At($"Assets/Vfx/Recipes/{ProbeField}.vfx.json");
        string shaderFile = At($"Assets/Shaders/Vfx/{ProbeField}.fx");
        string compiledFile = At($"Assets/Shaders/Vfx/{ProbeField}.fxc");
        string classFile = At($"Content/Vfx/{ProbeField}.Vfx.g.cs");
        string handFile = At($"Content/Vfx/{ProbeField}.cs");

        LabSlot draft = LabSession.Slots[0];
        draft.Load(ProbeRecipe());
        VfxLabUI.SelectSlot(draft);
        foreach (bool step in WaitForBuild(draft)) yield return step;
        waitTicks = 6;
        yield return true;

        // The first one: everything's new, and nothing's written until it's accepted.
        foreach (bool step in OpenExport()) yield return step;
        Check("the review opens", VfxLabUI.Export.IsOpen);
        Check("a first export is all new", ExportRows() == "New,New,New,New,New", ExportRows());
        Check("with nothing to complain about", VfxLabUI.Export.Problems.Count == 0, ExportProblems());
        Check("the class goes where it should", VfxLabUI.Export.Namespace == "TrelamiumIII.Content.Vfx" && VfxLabUI.Export.Folder == "Content/Vfx", $"{VfxLabUI.Export.Namespace} in {VfxLabUI.Export.Folder}");
        Check("nothing's written yet", !File.Exists(recipeFile) && !File.Exists(shaderFile) && !File.Exists(classFile));
        foreach (bool step in Capture("export_review")) yield return step;

        foreach (bool step in Click("export:write")) yield return step;
        Check("accepting it closes the review", !VfxLabUI.Export.IsOpen);
        Check("and writes every file", new[] { recipeFile, shaderFile, compiledFile, classFile, handFile }.All(File.Exists));

        Task<ShaderCompileResult> inPlace = ShaderCompiler.CompileFileAsync(shaderFile, LabFiles.ShaderCache);

        while (!inPlace.IsCompleted)
            yield return true;

        Check("the compiled shader is what its source compiles to where it sits", inPlace.Result.Code?.SequenceEqual(File.ReadAllBytes(compiledFile)) == true, inPlace.Result.Log);
        Check("the shipped class knows nothing of the lab", !File.ReadAllText(classFile).Contains("DevTools"));

        foreach (bool step in OpenExport()) yield return step;
        Check("exporting it again changes nothing", ExportRows() == "Same,Same,Same,Same,Kept", ExportRows());
        foreach (bool step in Click("export:write")) yield return step;
        Check("and there's nothing to accept", VfxLabUI.Export.IsOpen);
        foreach (bool step in Click("export:cancel")) yield return step;
        Check("cancel closes it", !VfxLabUI.Export.IsOpen);

        // Looking inside a shader is only looking: the slot's showing something else, and what's exported hasn't changed.
        draft.SetView(ShaderView.Noise);
        foreach (bool step in WaitForBuild(draft)) yield return step;
        foreach (bool step in OpenExport()) yield return step;
        Check("a debug view doesn't reach the export", ExportRows() == "Same,Same,Same,Same,Kept", ExportRows());
        foreach (bool step in Click("export:cancel")) yield return step;
        draft.SetView(ShaderView.Final);
        foreach (bool step in WaitForBuild(draft)) yield return step;

        // A retuned value is a new literal in the class, and the same shader.
        draft.Recipe!.Stages.First(static stage => stage.Id == "palette").Set("BodyAlpha", 0.6f);
        draft.Changed();
        foreach (bool step in OpenExport()) yield return step;
        Check("a retuned value leaves the shader alone", ExportRows() == "Changed,Same,Same,Changed,Kept", ExportRows());
        foreach (bool step in Click("export:write")) yield return step;
        Check("and reaches the class", File.ReadAllText(classFile).Contains(".Set(\"palette_BodyAlpha\", 0.6f)"));

        // A new stage is a new shader. Cancelled, so what's written stays what's tested after the rebuild.
        RecipeStage added = RecipeEdit.AddStage(draft.Recipe, "FlowAdvect");
        draft.Changed(true);
        foreach (bool step in OpenExport()) yield return step;
        Check("a new stage changes the shader", ExportRows() == "Changed,Changed,Changed,Changed,Kept", ExportRows());
        foreach (bool step in Click("export:cancel")) yield return step;
        Check("a cancelled export writes nothing", !File.ReadAllText(classFile).Contains(added.Id + "_"));
        RecipeEdit.RemoveStage(draft.Recipe, added.Id);
        draft.Changed(true);
        waitTicks = 4;
        yield return true;

        // Edits by hand: one that's warned about before it's lost, and one that's never touched.
        string handText = File.ReadAllText(handFile) + "// mine\n";
        File.WriteAllText(handFile, handText);
        File.AppendAllText(classFile, "// by hand\n");
        foreach (bool step in OpenExport()) yield return step;
        Check("an edit to the class is called out", ExportRows() == "Same,Same,Same,Edited,Kept", ExportRows());
        foreach (bool step in Capture("export_edited")) yield return step;
        foreach (bool step in Click("export:write")) yield return step;
        Check("accepting it replaces the class", !File.ReadAllText(classFile).Contains("// by hand"));
        Check("and the hand-written half is left alone", File.ReadAllText(handFile) == handText);

        // What stops one.
        foreach ((string name, string why) in new[] { ("MudField", "a class the mod has"), ("event", "a keyword"), ("exportprobe", "other capitalisation") })
        {
            foreach (bool step in Rename(draft, name)) yield return step;
            foreach (bool step in OpenExport()) yield return step;

            Check($"{why} can't be exported", VfxLabUI.Export.Problems.Any(static problem => problem.IsError), ExportProblems());

            foreach (bool step in Click("export:write")) yield return step;
            Check($"and {why} writes nothing", VfxLabUI.Export.IsOpen && !File.Exists(At($"Content/Vfx/{name}.Vfx.g.cs").Replace("exportprobe", "nope")));
            foreach (bool step in Capture($"export_stopped_{name}")) yield return step;
            foreach (bool step in Click("export:cancel")) yield return step;
        }

        foreach (bool step in Rename(draft, ProbeField)) yield return step;

        // Folders.
        foreach (bool step in OpenExport()) yield return step;
        foreach (bool step in Type("export:folder", "../Outside")) yield return step;
        Check("a folder outside the mod is refused", VfxLabUI.Export.Rows.Count == 0 && VfxLabUI.Export.Problems.Any(static problem => problem.IsError), ExportProblems());
        foreach (bool step in Type("export:folder", "Content/Somewhere")) yield return step;
        Check("moving it warns that the old files stay", VfxLabUI.Export.Problems.Any(static problem => !problem.IsError && problem.Message.Contains("Content/Vfx")), ExportProblems());
        foreach (bool step in Click("export:cancel")) yield return step;

        Directory.CreateDirectory(At(MovedFolder));
        File.Move(classFile, At($"{MovedFolder}/{ProbeField}.Vfx.g.cs"));
        File.Move(handFile, At($"{MovedFolder}/{ProbeField}.cs"));
        foreach (bool step in OpenExport()) yield return step;
        Check("files moved by hand stay where they were put", VfxLabUI.Export.Folder == MovedFolder, VfxLabUI.Export.Folder);
        Check("in the namespace their hand-written half says", VfxLabUI.Export.Namespace == "TrelamiumIII.Content.Vfx", VfxLabUI.Export.Namespace);
        Check("and nothing needs writing", ExportRows() == "Same,Same,Same,Same,Kept", ExportRows());
        foreach (bool step in Click("export:cancel")) yield return step;

        // A real field's look and physics, as a liquid, with a residue it finds in its own code.
        VfxRecipe liquid = LayerRecipe.From(FieldSystem.GetLayer<MudField>()!);
        liquid.Name = LiquidField;

        LabSlot second = LabSession.Slots[1];
        second.Load(liquid);
        VfxLabUI.SelectSlot(second);
        foreach (bool step in WaitForBuild(second)) yield return step;
        waitTicks = 6;
        yield return true;

        foreach (bool step in OpenExport()) yield return step;
        Check("a real field's recipe exports too", ExportRows() == "New,New,New,New,New", ExportRows() + " " + ExportProblems());
        foreach (bool step in Click("export:write")) yield return step;
        Check("as a liquid with its residue", File.ReadAllText(At($"Content/Vfx/{LiquidField}.Vfx.g.cs")) is var text && text.Contains("GasTypeID.Liquid") && text.Contains("typeof(MuddiedCondition)"));

        // What the generated asset references have to cope with, which only the rebuild can prove: a lowercase name, and a texture that isn't noise.
        VfxRecipe odd = RecipeEdit.NewField(OddField);
        odd.Physics.Kind = RecipePhysics.Plasma;
        odd.Stages.First(static stage => stage.Id == "noise").Set("Noise", Assets.Textures.VFX.RadialGradient.KEY);

        LabSlot third = LabSession.Slots[2];
        third.Load(odd);
        VfxLabUI.SelectSlot(third);
        foreach (bool step in WaitForBuild(third)) yield return step;
        waitTicks = 6;
        yield return true;
        foreach (bool step in OpenExport()) yield return step;
        Check("a lowercase plasma exports", ExportRows() == "New,New,New,New,New", ExportRows() + " " + ExportProblems());
        foreach (bool step in Click("export:write")) yield return step;

        // Every recipe that had already been saved, as the rebuild will compile whatever these turn into.
        foreach (string saved in RecipeFile.Names(LabFiles.RecipeFolder).Except([ProbeField, LiquidField, OddField]).ToList())
        {
            third.Load(RecipeFile.Load(LabFiles.RecipeFolder, saved));
            VfxLabUI.SelectSlot(third);
            foreach (bool step in WaitForBuild(third)) yield return step;
            waitTicks = 6;
            yield return true;
            foreach (bool step in OpenExport()) yield return step;
            Check($"the saved recipe {saved} exports", !VfxLabUI.Export.Problems.Any(static problem => problem.IsError) && VfxLabUI.Export.Rows.Count == 5, ExportRows() + " " + ExportProblems());
            foreach (bool step in Capture($"export_saved_{saved}")) yield return step;
            foreach (bool step in Click(VfxLabUI.Export.Rows.Any(static row => row.WillWrite) ? "export:write" : "export:cancel")) yield return step;
        }

        Check("saving a new shader asks for a rebuild, and isn't an error", VfxLabUI.HotReload is { StatusIsError: false }, VfxLabUI.HotReload?.Status ?? "not watching");

        // No source folder: the same files, laid out the same way, somewhere they can be copied from.
        DeveloperConfig.Instance.SourceDirectory = string.Empty;
        Check("this client can't find a source folder", !LabExport.HasSource, LabExport.Root);

        if (Directory.Exists(LabExport.Root))
            Directory.Delete(LabExport.Root, true);

        LabSlot fourth = LabSession.Slots[3];
        fourth.Load(RecipeEdit.NewField("NoSourceField"));
        VfxLabUI.SelectSlot(fourth);
        foreach (bool step in WaitForBuild(fourth)) yield return step;
        waitTicks = 6;
        yield return true;
        foreach (bool step in OpenExport()) yield return step;
        Check("with no source folder it says where things will go", VfxLabUI.Export.Problems.Any(problem => !problem.IsError && problem.Message.Contains(LabExport.Root)), ExportProblems());
        foreach (bool step in Capture("export_no_source")) yield return step;
        foreach (bool step in Click("export:write")) yield return step;
        Check("and writes them there", File.Exists(Path.Combine(LabExport.Root, "Content/Vfx/NoSourceField.Vfx.g.cs")) && File.Exists(Path.Combine(LabExport.Root, "Assets/Shaders/Vfx/NoSourceField.fxc")));

        VfxLabUI.ScriptedFrames = null;

        Log(benchFailures == 0 ? "PASS: the export's review and files all work" : $"FAIL: {benchFailures} export checks failed");
    }

    private static IEnumerable<bool> ExportedScript()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is not { Length: > 0 } source)
        {
            Check("exported is given the copy of the source it was exported to", false);
            yield break;
        }

        DeveloperConfig.Instance.SourceDirectory = source;

        waitTicks = SettleTicks;
        yield return true;

        FieldLayer? foundProbe = FieldSystem.Layers.FirstOrDefault(static layer => layer.GetType().Name == ProbeField);
        FieldLayer? foundLiquid = FieldSystem.Layers.FirstOrDefault(static layer => layer.GetType().Name == LiquidField);

        Check("the rebuilt mod has the exported fields", foundProbe is ModularField and not LabFieldSlot && foundLiquid is ModularField and not LabFieldSlot);

        if (foundProbe is not ModularField probe || foundLiquid is not ModularField liquid)
            yield break;

        Check("in the namespace they were exported to", probe.GetType().Namespace == "TrelamiumIII.Content.Vfx", probe.GetType().FullName ?? string.Empty);

        List<string> classes = Directory.EnumerateFiles(source, "*.Vfx.g.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(static path => Path.GetFileName(path)[..^".Vfx.g.cs".Length])
            .ToList();

        Check("every exported class became a field", classes.Count >= 3 && classes.All(static name => FieldSystem.Layers.Any(layer => layer.GetType().Name == name && layer is ModularField)), string.Join(", ", classes));

        foreach ((ModularField layer, string name) in new[] { (probe, ProbeField), (liquid, LiquidField) })
        {
            var shader = (Asset<Effect>)ShaderProperty.GetValue(layer)!;
            byte[] packed = harnessMod!.GetFileBytes($"Assets/Shaders/Vfx/{name}.fxc");

            Check($"{name} draws with its own shader", shader.Name.Replace('\\', '/').EndsWith($"Assets/Shaders/Vfx/{name}"), shader.Name);
            Check($"and that's what the export wrote", packed.SequenceEqual(File.ReadAllBytes(Path.Combine(source, $"Assets/Shaders/Vfx/{name}.fxc"))));
        }

        foreach (bool step in RunWithoutLab(probe, liquid)) yield return step;

        // Now the lab starts, for the first time in this run, to compare with.
        LabSession.Start(harnessMod!);

        foreach (bool step in AgainstDraft(probe, ProbeField, LabSession.Slots[0], source, true)) yield return step;
        foreach (bool step in AgainstDraft(liquid, LiquidField, LabSession.Slots[1], source, false)) yield return step;

        foreach (bool step in ReopenFromTheGame(probe, source)) yield return step;

        Log(benchFailures == 0 ? "PASS: exported fields run from a clean build, the same as their drafts" : $"FAIL: {benchFailures} exported checks failed");
    }

    /// <summary> Everything an exported field is for, before anything has touched the lab. </summary>
    private static IEnumerable<bool> RunWithoutLab(ModularField probe, ModularField liquid)
    {
        Check("the lab hasn't started", !VfxLabUI.Open && LabSession.Slots.Count == 0);

        frozenTime = Main.timeForVisualEffects;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0f;

        Vector2 center = Main.LocalPlayer.Center;

        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("exported_empty")) yield return step;
        Color[] empty = capturedPixels!;

        SpawnSkyGrid(probe, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("exported_probe")) yield return step;
        Color[] drawn = capturedPixels!;
        probe.ClearNodes();

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        var region = new Rectangle(width / 2 - 600, height / 2 - 400, 1200, 270);
        (double mean, _) = Difference(empty, drawn, width, region);

        Check("an exported field draws", mean > 2d, $"mean {mean:F2} of 255 from an empty sky");

        frozenTime = null;

        // It has to land and mark the ground, with no lab behind it.
        foreach (ModularField layer in new[] { probe, liquid })
        {
            for (int column = 0; column < 12; column++)
            {
                for (int row = 0; row < 3; row++)
                    layer.SpawnNode(center + new Vector2((column - 6) * 28f, -40f - row * 28f), Vector2.Zero, 32f, 600f);
            }
        }

        waitTicks = ResidueTicks;
        yield return true;
        foreach (bool step in Capture("exported_residue")) yield return step;

        Point below = (center / 16f).ToPoint();
        int slimed = 0;
        int muddied = 0;

        for (int x = below.X - 40; x < below.X + 40; x++)
        {
            for (int y = below.Y - 10; y < below.Y + 30; y++)
            {
                slimed += TileConditionSystem.TryGet<SlimedCondition>(x, y, out _) ? 1 : 0;
                muddied += TileConditionSystem.TryGet<MuddiedCondition>(x, y, out _) ? 1 : 0;
            }
        }

        Check("it leaves the residue its recipe names", slimed > 0, $"{slimed} tiles");
        Check("so does the one from a real field", muddied > 0, $"{muddied} tiles");

        probe.ClearNodes();
        liquid.ClearNodes();
        TileConditionSystem.ClearAll();

        Check("all that with the lab unused", !VfxLabUI.Open && LabSession.Slots.Count == 0);
    }

    /// <summary> A draft and an export are meant to be the same class fed different data: the same numbers, the same shader, and the same pixels. </summary>
    /// <param name="nudge"> Whether every path through its shader is on, so that every value in it has to show. </param>
    private static IEnumerable<bool> AgainstDraft(ModularField exported, string name, LabSlot draft, string source, bool nudge)
    {
        VfxRecipe recipe = RecipeFile.Load(Path.Combine(source, "Assets/Vfx/Recipes"), name);

        draft.Load(recipe);

        while (draft.Live.Status is ShaderBuildStatus.Compiling)
        {
            draft.Live.Update();
            yield return true;
        }

        Check($"{name}'s recipe builds as a draft", draft.Live.Status == ShaderBuildStatus.Compiled, draft.Live.Log);

        CompareSpecs(name, exported.Spec, draft.Layer.Spec);

        GeneratedShader generated = ShaderGenerator.Generate(recipe, LabSession.Library);

        ShaderCompileResult compiled = default;
        foreach (bool step in Compile(generated.Source!, result => compiled = result)) yield return step;
        Check($"{name}'s draft compiles to the shader that shipped", compiled.Code?.SequenceEqual(harnessMod!.GetFileBytes($"Assets/Shaders/Vfx/{name}.fxc")) == true, compiled.Log);

        var shipped = (Asset<Effect>)ShaderProperty.GetValue(exported)!;

        foreach (bool step in RunOnDraw(() => CompareWithDraft(name, shipped.Value, exported.Spec.Material!, draft.Layer.Live.Asset.Value, generated, nudge))) yield return step;

        frozenTime = Main.timeForVisualEffects;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0f;

        Vector2 center = Main.LocalPlayer.Center;

        SpawnSkyGrid(exported, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture($"exported_{name}_real")) yield return step;
        Color[] real = capturedPixels!;
        exported.ClearNodes();

        SpawnSkyGrid(draft.Layer, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture($"exported_{name}_draft")) yield return step;
        Color[] previewed = capturedPixels!;
        draft.Layer.ClearNodes();

        SpawnSkyGrid(exported, center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture($"exported_{name}_control")) yield return step;
        Color[] control = capturedPixels!;
        exported.ClearNodes();

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        var region = new Rectangle(width / 2 - 600, height / 2 - 400, 1200, 270);

        (double testMean, int testOver) = Difference(real, previewed, width, region);
        (double controlMean, int controlOver) = Difference(real, control, width, region);

        Log($"{name} on screen, draft vs export: mean {testMean:F3} of 255, {testOver} of {region.Width * region.Height} pixels over {ScreenTolerance}");
        Log($"{name} on screen, export vs export (control): mean {controlMean:F3} of 255, {controlOver} pixels over {ScreenTolerance}");
        Check($"{name} looks the same as its draft", testOver <= controlOver + region.Width * region.Height / 1000, $"{testOver} pixels over, control {controlOver}");

        frozenTime = null;
    }

    /// <summary>
    ///     The exact version, with no world behind it: both through one synthetic field, and any difference at all is real. Then each value
    ///     on the draft's side is nudged in turn and has to show, so the comparison can fail, and a wrong literal in the class couldn't hide.
    /// </summary>
    private static void CompareWithDraft(string name, Effect shipped, VfxMaterial material, Effect previewed, GeneratedShader generated, bool nudge)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;

        using var target = new RenderTarget2D(device, ParityWidth, ParityHeight, false, SurfaceFormat.Color, DepthFormat.None);
        (Texture2D field, Texture2D attributes) = SyntheticField(device);
        using Texture2D fieldTexture = field;
        using Texture2D attributeTexture = attributes;

        Color[] Draw(Effect effect, VfxMaterial values)
        {
            ParityFrame(effect, attributeTexture, 12.34f, Vector2.One, new Vector2(3.3f, 1.7f));
            values.Apply(effect);

            return Render(device, target, fieldTexture, effect);
        }

        static int Worst(Color[] first, Color[] second)
        {
            int worst = 0;

            for (int i = 0; i < first.Length; i++)
            {
                Color a = first[i];
                Color b = second[i];

                worst = Math.Max(worst, Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A))));
            }

            return worst;
        }

        Color[] real = Draw(shipped, material);
        Color[] preview = Draw(previewed, LiveRecipe.BuildMaterial(generated));
        int worst = Worst(real, preview);
        int drawn = real.Count(static pixel => pixel.A > 0);

        Check($"{name} is its draft to the pixel", worst == 0 && drawn > 0, $"worst channel difference {worst}/255 across {drawn} drawn pixels");

        if (nudge)
        {
            List<string> missed = [];

            for (int i = 0; i < generated.Uniforms.Count; i++)
            {
                UniformValue original = generated.Uniforms[i];

                UniformValue nudged = original.Kind == ParamKind.Noise
                    ? original with { Texture = original.Texture == "Perlin" ? "Milky_2" : "Perlin" }
                    : original with { Values = original.Values.Select(static value => value + 0.05f).ToArray() };

                List<UniformValue> uniforms = [.. generated.Uniforms];
                uniforms[i] = nudged;

                if (Worst(real, Draw(previewed, LiveRecipe.BuildMaterial(generated with { Uniforms = uniforms }))) <= ParityTolerance)
                    missed.Add(original.Name);
            }

            Check($"all {generated.Uniforms.Count} of its values have to match for that to pass", missed.Count == 0, string.Join(", ", missed));
        }

        shipped.Parameters["uAttrTex"]?.SetValue((Texture?)null);
        previewed.Parameters["uAttrTex"]?.SetValue((Texture?)null);
    }

    /// <summary> Every property of the spec, by reflection, so a new one can't be left out of the export unnoticed. </summary>
    private static void CompareSpecs(string name, VfxFieldSpec exported, VfxFieldSpec draft)
    {
        List<string> different = [];

        foreach (PropertyInfo property in typeof(VfxFieldSpec).GetProperties())
        {
            object? a = property.GetValue(exported);
            object? b = property.GetValue(draft);

            switch (a)
            {
                case VfxMaterial:
                    // Compared by what it draws.
                    break;
                case FieldModule[] modules when b is FieldModule[] others:
                    if (DescribeModules(modules) != DescribeModules(others))
                        different.Add($"{property.Name}: {DescribeModules(modules)} vs {DescribeModules(others)}");

                    break;
                default:
                    if (!Equals(a, b))
                        different.Add($"{property.Name}: {a} vs {b}");

                    break;
            }
        }

        Check($"{name} has its draft's physics, behaviour and residue", different.Count == 0, string.Join("; ", different));
    }

    private static string DescribeModules(IEnumerable<FieldModule> modules)
        => string.Join(", ", modules.Select(static module => $"{module.GetType().Name}({string.Join(" ", ModuleCatalog.Settings(module.GetType()).Select(field => $"{field.Name}={field.GetValue(module)}"))})"));

    /// <summary>
    ///     The way back in: find the field in the game, open its recipe, and export it again. First with nothing to change, then with a new
    ///     shader, which hot reload puts straight into the running field ahead of the class that sets its values. That has to survive drawing.
    /// </summary>
    private static IEnumerable<bool> ReopenFromTheGame(ModularField probe, string source)
    {
        showPanels = true;

        scripted.Clear();
        VfxLabUI.ScriptedFrames = scripted;
        VfxLabUI.Gui.Recorded = [];
        ModContent.GetInstance<VfxLabUI>().Toggle();

        foreach (LabSlot occupied in LabSession.Slots)
            occupied.Empty();

        BenchTarget target = ShaderBench.Targets.First(static target => target.Name == ProbeField);
        VfxLabUI.Select(target);
        waitTicks = 10;
        yield return true;

        foreach (bool step in Capture("exported_in_the_list")) yield return step;
        foreach (bool step in Click("openrecipe")) yield return step;

        LabSlot? opened = LabSession.Selected;

        Check("an exported field opens from its recipe", opened?.Recipe?.Name == ProbeField, opened?.Recipe?.Name ?? "nothing");

        if (opened is null)
            yield break;

        foreach (bool step in WaitForBuild(opened)) yield return step;
        waitTicks = 6;
        yield return true;

        foreach (bool step in OpenExport()) yield return step;
        Check("once built, its own class doesn't count as taken", !VfxLabUI.Export.Problems.Any(static problem => problem.IsError), ExportProblems());
        Check("and nothing needs writing", ExportRows() == "Same,Same,Same,Same,Kept", ExportRows());
        Check("in the folder it was moved to", VfxLabUI.Export.Folder == MovedFolder, VfxLabUI.Export.Folder);
        foreach (bool step in Click("export:cancel")) yield return step;

        // Put back at the end, so this script can run again on the same build.
        Dictionary<string, byte[]> originals = new[]
            {
                $"Assets/Vfx/Recipes/{ProbeField}.vfx.json",
                $"Assets/Shaders/Vfx/{ProbeField}.fx",
                $"Assets/Shaders/Vfx/{ProbeField}.fxc",
                $"{MovedFolder}/{ProbeField}.Vfx.g.cs",
            }.ToDictionary(static path => path, path => File.ReadAllBytes(Path.Combine(source, path)));

        var shipped = (Asset<Effect>)ShaderProperty.GetValue(probe)!;
        Effect before = shipped.Value;

        RecipeEdit.AddStage(opened.Recipe!, "DomainWarp").Set("Noise", "Milky_3").Set("Amount", 0.2f);
        opened.Changed(true);
        foreach (bool step in WaitForBuild(opened)) yield return step;
        waitTicks = 6;
        yield return true;

        foreach (bool step in OpenExport()) yield return step;
        Check("a new stage on an exported field changes its files", ExportRows() == "Changed,Changed,Changed,Changed,Kept", ExportRows() + " " + ExportProblems());
        foreach (bool step in Click("export:write")) yield return step;

        for (int i = 0; i < BuildTimeoutTicks && ReferenceEquals(shipped.Value, before); i++)
            yield return true;

        Check("hot reload puts the new shader into the running field", !ReferenceEquals(shipped.Value, before), VfxLabUI.HotReload?.Status ?? "not watching");

        SpawnSkyGrid(probe, Main.LocalPlayer.Center);
        waitTicks = ScreenSettleTicks;
        yield return true;
        foreach (bool step in Capture("exported_new_shader_old_class")) yield return step;
        probe.ClearNodes();
        Check("and it draws with values its class doesn't set yet", VfxLabUI.HotReload is { StatusIsError: false }, VfxLabUI.HotReload?.Status ?? "not watching");

        foreach ((string path, byte[] bytes) in originals)
            File.WriteAllBytes(Path.Combine(source, path), bytes);

        VfxLabUI.ScriptedFrames = null;
    }
}
