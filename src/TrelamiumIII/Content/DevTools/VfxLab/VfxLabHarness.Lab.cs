using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SubworldLibrary;
using Terraria.ID;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Content.DevTools.VfxLab.Room;
using TrelamiumIII.Content.DevTools.VfxLab.UI;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary>
///     The whole trip through the lab realm with scripted input: into its rooms, designing a field from nothing, changing how it moves, what
///     it leaves behind and how it looks, undoing, saving and loading, opening a real field, and back out.
/// </summary>
public sealed partial class VfxLabHarness
{
    private const int RainTicks = 600;
    private const int EmitTicks = 150;
    private const int TravelTimeoutTicks = 60 * 30;
    private const int BuildTimeoutTicks = 300;

    private static readonly FieldInfo NodesField = typeof(FieldLayer).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static List<FieldNode> NodesOf(FieldLayer layer) => (List<FieldNode>)NodesField.GetValue(layer)!;

    private static IEnumerable<bool> LabScript()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is { Length: > 0 } source)
            DeveloperConfig.Instance.SourceDirectory = source;

        waitTicks = SettleTicks;
        yield return true;

        CheckResidues();

        foreach (bool step in RainCheck()) yield return step;

        scripted.Clear();
        VfxLabUI.ScriptedFrames = scripted;
        VfxLabUI.Gui.Recorded = [];
        ModContent.GetInstance<VfxLabUI>().Toggle();
        waitTicks = 10;
        yield return true;

        // Into the realm, and it has to be the rooms we think it is.
        foreach (bool step in Click("enter")) yield return step;

        for (int i = 0; i < TravelTimeoutTicks && !LabRooms.InLab; i++)
            yield return true;

        Check("Enter lab goes to the lab realm", LabRooms.InLab);

        if (!LabRooms.InLab)
            yield break;

        waitTicks = SettleTicks;
        yield return true;

        Log($"arrived at tile {(Main.LocalPlayer.Bottom / 16f).ToPoint()}, room {LabRooms.Current}");
        Check("arrives in the Fields room", LabRooms.Current == LabRoom.Fields);
        foreach (bool step in Capture("lab_fields_room")) yield return step;

        foreach (bool step in DesignField()) yield return step;
        foreach (bool step in OpenRealField()) yield return step;
        foreach (bool step in ColourSwitching()) yield return step;
        foreach (bool step in LibraryReload()) yield return step;

        foreach (bool step in Click("clearall")) yield return step;
        waitTicks = 4;
        yield return true;
        Check("Clear all empties every field", FieldSystem.Layers.All(static layer => layer.NodeCount == 0), string.Join(",", FieldSystem.Layers.Where(static layer => layer.NodeCount > 0).Select(static layer => layer.GetType().Name)));
        Check("Clear all removes every source", LabTools.Emitters.Count == 0);

        foreach (bool step in TilesRoom()) yield return step;
        foreach (bool step in ShadersRoom()) yield return step;

        // And home.
        foreach (bool step in Click("tab0")) yield return step;
        waitTicks = 40;
        yield return true;
        foreach (bool step in Click("leave")) yield return step;

        for (int i = 0; i < TravelTimeoutTicks && LabRooms.InLab; i++)
            yield return true;

        Check("Leave lab comes back out", !LabRooms.InLab);

        VfxLabUI.ScriptedFrames = null;

        Log(benchFailures == 0 ? "PASS: everything in the lab works" : $"FAIL: {benchFailures} lab checks failed");
    }

    /// <summary> Everything that hops with the slime AI should leave gel, except the critters that only borrow it. </summary>
    private static void CheckResidues()
    {
        List<string> critters = [];

        foreach (NPC sample in ContentSamples.NpcsByNetId.Values)
        {
            if (sample.aiStyle == NPCAIStyleID.Slime && (NPCID.Sets.CountsAsCritter[sample.type] || sample.catchItem > 0) && NPCResidues.For(sample) is not null)
                critters.Add(sample.TypeName);
        }

        Check("no critter with the slime AI leaves a residue", critters.Count == 0, string.Join(", ", critters));

        var slime = new NPC();
        slime.SetDefaults(NPCID.BlueSlime);
        Check("slimes still do", NPCResidues.For(slime) is not null);
    }

    /// <summary> Rain coats the ground in the liquid residue's palette now. The value doesn't say how it looks, so this leaves a picture to judge. </summary>
    private static IEnumerable<bool> RainCheck()
    {
        rainHeld = true;
        Main.maxRaining = 0.35f;
        Main.cloudAlpha = 0.35f;
        Main.rainTime = 18000;

        waitTicks = RainTicks;
        yield return true;
        foreach (bool step in Capture("rain")) yield return step;

        Point below = (Main.LocalPlayer.Bottom / 16f).ToPoint();
        int wet = 0;

        for (int x = below.X - 50; x < below.X + 50; x++)
        {
            for (int y = below.Y - 10; y < below.Y + 10; y++)
            {
                if (TileConditionSystem.TryGet<DampCondition>(x, y, out _))
                    wet++;
            }
        }

        Check("rain dampens the ground", wet > 0, $"{wet} wet tiles");

        rainHeld = false;
        Main.maxRaining = 0f;
        Main.cloudAlpha = 0f;
        Main.rainTime = 0;
    }

    private static IEnumerable<bool> DesignField()
    {
        LabSlot slot = LabSession.Slots[0];
        LabFieldSlot layer = slot.Layer;

        foreach (bool step in Click("slot:A")) yield return step;
        Check("a slot can be selected", LabSession.Selected == slot);
        foreach (bool step in Capture("lab_empty_slot")) yield return step;

        foreach (bool step in Click("new")) yield return step;
        foreach (bool step in WaitForBuild(slot)) yield return step;
        Check("New field makes a recipe that builds", slot.Recipe is not null && slot.Live.Status == ShaderBuildStatus.Compiled, slot.Live.Log);

        if (slot.Recipe is null)
            yield break;

        // Releasing it, and moving the source.
        foreach (bool step in Click("tool:Emit")) yield return step;

        Rectangle room = LabRooms.AreaOf(LabRoom.Fields);
        Vector2 sourceAt = new Point(room.X + 38, room.Bottom - 10).ToWorldCoordinates();

        foreach (bool step in ClickAt(LabTools.ToUi(sourceAt).ToPoint())) yield return step;
        Check("the Emit tool places a source", LabTools.Emitters.Count == 1);

        waitTicks = EmitTicks;
        yield return true;
        Check("the source releases the field", layer.NodeCount > 0, $"{layer.NodeCount} nodes");
        foreach (bool step in Capture("lab_field_new")) yield return step;

        if (LabTools.Emitters.Count > 0)
        {
            LabEmitter emitter = LabTools.Emitters[0];
            Vector2 before = emitter.Position;

            foreach (bool step in Drag("emitter:0", 60)) yield return step;
            Check("a source can be dragged", Vector2.Distance(before, emitter.Position) > 40f, $"moved {Vector2.Distance(before, emitter.Position):F0}px");
        }

        // How it moves, through the real rows.
        float Rise() => NodesOf(layer).Count == 0 ? 0f : NodesOf(layer).Average(static node => node.Velocity.Y);

        Check("by default it rises", Rise() < 0f, $"mean velocity {Rise():F2}");

        foreach (bool step in Reveal("physics/density#0")) yield return step;
        foreach (bool step in Type("physics/density#0", "1")) yield return step;
        foreach (bool step in Reveal("physics/gravity#0")) yield return step;
        foreach (bool step in Type("physics/gravity#0", "0.4")) yield return step;
        foreach (bool step in Capture("lab_physics_rows")) yield return step;
        Check("a physics row sets the recipe", Math.Abs(slot.Recipe.Physics.Gravity - 0.4f) < 0.001f, slot.Recipe.Physics.Gravity.ToString("F3"));
        Check("and reaches the field at once", Math.Abs(layer.Spec.Gravity - 0.4f) < 0.001f);

        // A new release at the new density has to fall.
        layer.ClearNodes();
        waitTicks = 90;
        yield return true;
        Check("heavier and it falls", Rise() > 0f, $"mean velocity {Rise():F2}");
        foreach (bool step in Capture("lab_field_heavy")) yield return step;

        // The stack.
        foreach (bool step in Reveal("addstage")) yield return step;
        foreach (bool step in Click("addstage")) yield return step;
        Check("Add stage opens a list", VfxLabUI.PopupOpen);
        foreach (bool step in Capture("lab_add_stage")) yield return step;
        foreach (bool step in Click("list:Coords: DomainWarp")) yield return step;
        Check("picking one adds it", slot.Recipe.Stages.Any(static stage => stage.Type == "DomainWarp"));
        Check("and the list closes", !VfxLabUI.PopupOpen);
        foreach (bool step in WaitForBuild(slot)) yield return step;
        Check("and it recompiles", slot.Live.Status == ShaderBuildStatus.Compiled, slot.Live.Log);
        foreach (bool step in Capture("lab_field_warped")) yield return step;

        foreach (bool step in Reveal("stage:domainWarp:on")) yield return step;
        foreach (bool step in Click("stage:domainWarp:on")) yield return step;
        Check("a stage can be switched off", !slot.Recipe.Stages.First(static stage => stage.Type == "DomainWarp").Enabled);
        foreach (bool step in Reveal("stage:domainWarp:remove")) yield return step;
        foreach (bool step in Click("stage:domainWarp:remove")) yield return step;
        Check("and removed", slot.Recipe.Stages.All(static stage => stage.Type != "DomainWarp"));

        // Undo has to walk back through all of that, a step at a time.
        foreach (bool step in Click("undo")) yield return step;
        Check("Undo brings it back", slot.Recipe!.Stages.Any(static stage => stage.Type == "DomainWarp"));
        foreach (bool step in Click("redo")) yield return step;
        Check("Redo takes it away again", slot.Recipe!.Stages.All(static stage => stage.Type != "DomainWarp"));

        // A value dragged is one step of undo, however many frames the drag took.
        VfxRecipe recipe = slot.Recipe!;
        foreach (bool step in Reveal("stage:cut")) yield return step;
        foreach (bool step in Click("stage:cut")) yield return step;
        foreach (bool step in Reveal("cut/Level#0")) yield return step;
        foreach (bool step in Drag("cut/Level#0", 40)) yield return step;
        float dragged = ShaderGenerator.NumbersOf(slot.Recipe!.Stages.First(static stage => stage.Id == "cut"), LabSession.Library.Stages["Threshold"].Params[0])[0];
        foreach (bool step in Click("undo")) yield return step;
        float undone = ShaderGenerator.NumbersOf(slot.Recipe!.Stages.First(static stage => stage.Id == "cut"), LabSession.Library.Stages["Threshold"].Params[0])[0];
        Check("a drag is one step of undo", Math.Abs(dragged - undone) > 0.001f && Math.Abs(undone - 0.22f) < 0.001f, $"{dragged:F3} back to {undone:F3}");

        // Behaviour.
        foreach (bool step in Reveal("addmodule")) yield return step;
        foreach (bool step in Click("addmodule")) yield return step;
        foreach (bool step in Click("list:CurlFlow")) yield return step;
        Check("a behaviour can be added", layer.Spec.Modules.Length == 1, layer.Spec.Modules.Length.ToString());
        foreach (bool step in Reveal("CurlFlow/Strength#0")) yield return step;
        foreach (bool step in Type("CurlFlow/Strength#0", "0.2")) yield return step;
        Check("and tuned", slot.Recipe!.Modules.Any(static module => Math.Abs(module.Get("Strength", 0f) - 0.2f) < 0.001f));

        // What it leaves behind: heavy enough now to reach the floor.
        foreach (bool step in Reveal("residue/pick")) yield return step;
        foreach (bool step in Click("residue/pick")) yield return step;
        foreach (bool step in Click("list:SlimedCondition")) yield return step;
        Check("a residue can be chosen", layer.Spec.Residue?.Name == "SlimedCondition", layer.Spec.Residue?.Name ?? "none");

        foreach (bool step in Reveal("residue/threshold#0")) yield return step;
        foreach (bool step in Type("residue/threshold#0", "0.3")) yield return step;

        waitTicks = EmitTicks * 2;
        yield return true;

        int marked = 0;

        for (int x = room.Left; x < room.Right; x++)
        {
            for (int y = room.Bottom - 2; y < room.Bottom + 2; y++)
            {
                if (TileConditionSystem.TryGet<SlimedCondition>(x, y, out _))
                    marked++;
            }
        }

        Check("the field leaves its residue on the floor", marked > 0, $"{marked} tiles");
        foreach (bool step in Capture("lab_field_residue")) yield return step;

        // Looking inside.
        foreach (ShaderView view in Enum.GetValues<ShaderView>().Skip(1))
        {
            foreach (bool step in Click("view")) yield return step;
            foreach (bool step in WaitForBuild(slot)) yield return step;
            Check($"the {view} view builds", slot.Live.View == view && slot.Live.Status == ShaderBuildStatus.Compiled, slot.Live.Log);
            waitTicks = 6;
            yield return true;
            foreach (bool step in Capture($"lab_view_{view.ToString().ToLowerInvariant()}")) yield return step;
        }

        foreach (bool step in Click("view")) yield return step;
        foreach (bool step in WaitForBuild(slot)) yield return step;
        Check("and back to Final", slot.Live.View == ShaderView.Final);

        // Time.
        foreach (bool step in Click("pause")) yield return step;
        waitTicks = 4;
        yield return true;
        Vector2[] frozen = NodesOf(layer).Select(static node => node.Position).ToArray();
        waitTicks = 30;
        yield return true;
        Check("Pause stops it", frozen.Length > 0 && NodesOf(layer).Count == frozen.Length && NodesOf(layer).Select(static node => node.Position).SequenceEqual(frozen));
        foreach (bool step in Click("step")) yield return step;
        waitTicks = 4;
        yield return true;
        Check("Step moves it one tick", !NodesOf(layer).Select(static node => node.Position).SequenceEqual(frozen));
        foreach (bool step in Click("pause")) yield return step;

        foreach (bool step in Click("light")) yield return step;
        waitTicks = 30;
        yield return true;
        foreach (bool step in Capture("lab_dim")) yield return step;
        Check("the light can be turned down", LabRooms.LightLevel < 1f);
        foreach (bool step in Click("light")) yield return step;
        foreach (bool step in Click("light")) yield return step;
        waitTicks = 10;
        yield return true;

        // Cost.
        foreach (bool step in Click("measure")) yield return step;
        waitTicks = 4;
        yield return true;
        Check("Measure reports a cost", slot.Measured is not null, slot.Measured ?? "nothing");

        // A round trip through a file.
        string expected = RecipeFile.Serialize(slot.Recipe!);
        string name = slot.Recipe!.Name;

        foreach (bool step in Click("save")) yield return step;
        Check("Save writes the recipe", File.Exists(RecipeFile.PathOf(LabFiles.RecipeFolder, name)), RecipeFile.PathOf(LabFiles.RecipeFolder, name));

        foreach (bool step in Click("empty")) yield return step;
        waitTicks = 2;
        yield return true;
        Check("Empty clears the slot", slot.Recipe is null && layer.NodeCount == 0);

        foreach (bool step in Click("load")) yield return step;
        foreach (bool step in Click($"list:{name}")) yield return step;
        foreach (bool step in WaitForBuild(slot)) yield return step;
        Check("Load brings back exactly what was saved", slot.Recipe is not null && RecipeFile.Serialize(slot.Recipe) == expected);

        foreach (bool step in Click("clearall")) yield return step;
        foreach (bool step in Click("tool:Emit")) yield return step;
        Check("the tool can be put down", LabTools.Tool == LabTool.None);
    }

    /// <summary> A real field onto the bench: its physics have to come across, as that's what's being asked to be played with. </summary>
    private static IEnumerable<bool> OpenRealField()
    {
        Rectangle room = LabRooms.AreaOf(LabRoom.Fields);
        LabSlot slot = LabSession.Slots[1];

        foreach (bool step in Click("row:MudField")) yield return step;
        foreach (bool step in Click("open")) yield return step;
        foreach (bool step in WaitForBuild(slot)) yield return step;

        Check("a real field opens into a free slot", slot.Recipe?.Name == "MudField" && LabSession.Selected == slot, slot.Recipe?.Name ?? "nothing");

        if (slot.Recipe is null)
            yield break;

        Log($"MudField came across: {slot.Recipe.Physics.Kind}, density {slot.Recipe.Physics.Density}, viscosity {slot.Recipe.Physics.Viscosity}, gravity {slot.Recipe.Physics.Gravity}, residue {slot.Recipe.Links.Deposit?.Residue ?? "none"}");
        Check("with its physics", slot.Recipe.Physics.Kind == RecipePhysics.Liquid);
        Check("and its residue", slot.Recipe.Links.Deposit?.Residue == "MuddiedCondition");
        Check("and it builds", slot.Live.Status == ShaderBuildStatus.Compiled, slot.Live.Log);

        foreach (bool step in Click("tool:Spray")) yield return step;

        Point at = LabTools.ToUi(new Point(room.X + 34, room.Bottom - 14).ToWorldCoordinates()).ToPoint();

        foreach (bool step in Scripted(new LabInput { Mouse = at }, new LabInput { Mouse = at, LeftDown = true, LeftPressed = true })) yield return step;

        waitTicks = 90;
        yield return true;

        foreach (bool step in Scripted(new LabInput { Mouse = at, LeftReleased = true }, new LabInput { Mouse = at })) yield return step;

        Check("the Spray tool releases it", slot.Layer.NodeCount > 0, $"{slot.Layer.NodeCount} nodes");
        waitTicks = 30;
        yield return true;
        foreach (bool step in Capture("lab_mud_draft")) yield return step;

        foreach (bool step in Click("tool:Spray")) yield return step;
    }

    /// <summary> Your reported bug: going from one colour to the next shouldn't lose the picker. </summary>
    private static IEnumerable<bool> ColourSwitching()
    {
        LabSlot slot = LabSession.Slots[1];

        foreach (bool step in Click("slot:B")) yield return step;
        foreach (bool step in Reveal("stage:palette")) yield return step;
        foreach (bool step in Click("stage:palette")) yield return step;
        foreach (bool step in Reveal("palette/Body#swatch")) yield return step;
        foreach (bool step in Click("palette/Body#swatch")) yield return step;
        Check("a swatch opens the picker", VfxLabUI.PopupOpen);

        Rectangle first = VfxLabUI.PopupArea;

        foreach (bool step in Click("palette/Core#swatch")) yield return step;
        Check("the next swatch keeps it open", VfxLabUI.PopupOpen);
        Check("and moves it to the new colour", VfxLabUI.PopupArea != first);
        foreach (bool step in Capture("lab_color_switch")) yield return step;

        foreach (bool step in ClickPopup(0.3f, 0.3f)) yield return step;
        Check("and it sets the new colour", slot.Recipe!.Stages.First(static stage => stage.Id == "palette").Values.ContainsKey("Core"));

        foreach (bool step in ClickAt(new Point(LabGui.ScreenSize.X / 2, LabGui.ScreenSize.Y / 2))) yield return step;
        Check("clicking away closes it", !VfxLabUI.PopupOpen);
    }

    /// <summary> Saving a stage file rebuilds what's using it. Only ever edits the copy it's pointed at. </summary>
    private static IEnumerable<bool> LibraryReload()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is not { Length: > 0 } source || VfxLabUI.HotReload is not { } reload)
        {
            Log("not tested: reloading the stage library, as no source copy was given");
            yield break;
        }

        LabSlot slot = LabSession.Slots[1];
        string file = Path.Combine(source, "Assets", "Shaders", "Lab", "Stages", "Threshold.stage.fxh");
        string original = File.ReadAllText(file);

        const string Line = "if (px.depth < 0.0)";

        if (!original.Contains(Line))
        {
            Check("Threshold.stage.fxh still has the line this test edits", false);
            yield break;
        }

        StageLibrary before = LabSession.Library;

        File.WriteAllText(file, original.Replace(Line, "if (px.depth < 0.02)"));

        for (int i = 0; i < ReloadTimeoutTicks && ReferenceEquals(LabSession.Library, before); i++)
            yield return true;

        Check("saving a stage file reloads the library", !ReferenceEquals(LabSession.Library, before), reload.Status);
        foreach (bool step in WaitForBuild(slot)) yield return step;
        Check("and what's using it rebuilds", slot.Live.Status == ShaderBuildStatus.Compiled, slot.Live.Log);

        File.WriteAllText(file, original);
        waitTicks = 60;
        yield return true;
        foreach (bool step in WaitForBuild(slot)) yield return step;
    }

    private static IEnumerable<bool> TilesRoom()
    {
        foreach (bool step in Click("tab1")) yield return step;

        for (int i = 0; i < 60 && LabRooms.Current != LabRoom.Tiles; i++)
            yield return true;

        Check("the Tiles tab moves to its room", LabRooms.Current == LabRoom.Tiles);
        waitTicks = 30;
        yield return true;
        foreach (bool step in Capture("lab_tiles_room")) yield return step;

        foreach (bool step in Click("row:MuddiedCondition")) yield return step;
        foreach (bool step in Click("tool:Brush")) yield return step;

        Rectangle room = LabRooms.AreaOf(LabRoom.Tiles);
        Point at = LabTools.ToUi(new Point(room.X + 34, room.Bottom).ToWorldCoordinates()).ToPoint();

        foreach (bool step in Scripted(new LabInput { Mouse = at }, new LabInput { Mouse = at, LeftDown = true, LeftPressed = true })) yield return step;

        waitTicks = 20;
        yield return true;

        foreach (bool step in Scripted(new LabInput { Mouse = at, LeftReleased = true }, new LabInput { Mouse = at })) yield return step;

        Check("the Brush paints the condition", TileConditionSystem.TryGet<MuddiedCondition>(room.X + 34, room.Bottom, out _));
        waitTicks = 20;
        yield return true;
        foreach (bool step in Capture("lab_tiles_brushed")) yield return step;

        foreach (bool step in Click("tool:Brush")) yield return step;
    }

    private static IEnumerable<bool> ShadersRoom()
    {
        foreach (bool step in Click("tab2")) yield return step;

        for (int i = 0; i < 60 && LabRooms.Current != LabRoom.Shaders; i++)
            yield return true;

        Check("the Shaders tab moves to its room", LabRooms.Current == LabRoom.Shaders);
        waitTicks = 30;
        yield return true;

        foreach (bool step in Click("row:NoiseField")) yield return step;
        waitTicks = 6;
        yield return true;
        foreach (bool step in Capture("lab_shaders_room")) yield return step;

        // Every shader on the plate, off screen, where going through them quickly can't flash anything at anyone.
        int drawn = 0;
        List<string> skipped = [];

        foreach (bool step in RunOnDraw(() =>
        {
            GraphicsDevice device = Main.instance.GraphicsDevice;

            using var target = new RenderTarget2D(device, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);

            foreach (ShaderTarget shader in ShaderBench.Targets.OfType<ShaderTarget>())
            {
                if (shader.NeedsItsHost)
                {
                    skipped.Add(shader.Name);
                    continue;
                }

                if (shader.Effect is { } effect)
                {
                    ShaderPlate.Draw(Main.spriteBatch, effect, new Rectangle(0, 0, 320, 180), Matrix.Identity);
                    drawn++;
                }
            }

            device.SetRenderTarget(null);
        }))
            yield return step;

        Log($"on the plate: {drawn} shaders; need their real host: {string.Join(", ", skipped)}");
        Check("every shader that can draw on the plate does", drawn > 35 && skipped.Count > 0, $"{drawn} drawn, {skipped.Count} skipped");
    }

    private static IEnumerable<bool> WaitForBuild(LabSlot slot)
    {
        waitTicks = 2;
        yield return true;

        for (int i = 0; i < BuildTimeoutTicks && slot.Live.Status == ShaderBuildStatus.Compiling; i++)
            yield return true;

        waitTicks = 2;
        yield return true;
    }

    /// <summary> Scrolls the inspector until a row is on show, from the top so it can't be above the view. </summary>
    private static IEnumerable<bool> Reveal(string id)
    {
        VfxLabUI.ScrollInspectorToTop();

        for (int i = 0; i < 120; i++)
        {
            VfxLabUI.Gui.Recorded!.Clear();
            waitTicks = 2;
            yield return true;

            if (VfxLabUI.Gui.Recorded.ContainsKey(id))
                yield break;

            foreach (bool step in Scripted(new LabInput { Mouse = VfxLabUI.InspectorPanel.Center, Scroll = -1 }))
                yield return step;
        }

        throw new InvalidOperationException($"Couldn't find the row {id} in the inspector.");
    }
}
