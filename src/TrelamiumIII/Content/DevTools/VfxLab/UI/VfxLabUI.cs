using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using ReLogic.OS;
using SubworldLibrary;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Content.DevTools.VfxLab.Room;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Vfx;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

/// <summary>
///     The lab's panels, over the running game. The world underneath keeps simulating and the player can still move, because what's being
///     tuned is out there. <c>/vfx</c> opens and closes them, anywhere; the lab realm has a room for each tab.
/// </summary>
public sealed class VfxLabUI : ModSystem
{
    private const int Margin = 10;

    /// <summary> Below the hotbar. </summary>
    private const int TopMargin = 74;

    private const int ListWidth = 216;
    private const int InspectorWidth = 348;
    private const int StatusHeight = 26;
    private const int HeaderHeight = 26;
    private const int ButtonHeight = 24;
    private const int ListRowHeight = 20;
    private const int Padding = 6;
    private const int NoticeFrames = 300;
    private const string ScrollLock = "TrelamiumIII/VfxLab";

    private static readonly float[] Speeds = [1f, 2f, 4f, 0.25f];
    private static readonly float[] LightLevels = [1f, 0.45f, 0.1f];
    private static readonly string[] LightNames = ["Bright", "Dim", "Dark"];

    private static readonly Inspector inspector = new();
    private static readonly SlotInspector slotInspector = new(inspector);
    private static readonly ExportPanel exportPanel = new();

    private static LabInput lastInput;
    private static Rectangle listPanel;
    private static Rectangle inspectorPanel;
    private static Rectangle statusBar;

    private static BenchGroup group = BenchGroup.Fields;
    private static string search = string.Empty;
    private static int listScroll;
    private static int lightIndex;

    private static List<InspectorField> fields = [];
    private static BenchTarget? builtTarget;
    private static Effect? builtEffect;
    private static LabSlot? builtSlot;
    private static int builtVersion;
    private static string nameText = string.Empty;

    private static ShaderHotReload? hotReload;
    private static string notice = string.Empty;
    private static int noticeFrames;
    private static bool wasEditing;
    private static bool travelRequested;
    private static LabSlot? measureRequest;

    internal static LabGui Gui { get; } = new();

    public static bool Open { get; private set; }

    /// <summary> Frames of input from a harness, one per drawn frame. While this is set the real mouse and keys are ignored. </summary>
    internal static Queue<LabInput>? ScriptedFrames { get; set; }

    /// <summary> Whatever was last put on the clipboard, for a harness to read back. </summary>
    internal static string? LastCopied { get; private set; }

    internal static ShaderHotReload? HotReload => hotReload;

    internal static ExportPanel Export => exportPanel;

    internal static Rectangle PopupArea => inspector.PopupArea;

    internal static bool PopupOpen => inspector.PopupOpen;

    internal static Rectangle InspectorPanel => inspectorPanel;

    internal static void ScrollInspectorToTop() => inspector.ScrollToTop();

    private static bool MouseOverPanels
    {
        get
        {
            Point mouse = ScriptedFrames is null ? LabInput.Read(lastInput).Mouse : lastInput.Mouse;

            return exportPanel.IsOpen || listPanel.Contains(mouse) || inspectorPanel.Contains(mouse) || statusBar.Contains(mouse) || (inspector.PopupOpen && inspector.PopupArea.Contains(mouse));
        }
    }

    public void Toggle()
    {
        if (Open)
        {
            Open = false;
            Gui.CancelEdit();
            inspector.ClosePopups();
            exportPanel.Close();
            return;
        }

        ShaderBench.Install(Mod);
        LabSession.Start(Mod);
        inspector.Textures = ShaderBench.NoiseTextures;

        if (hotReload is null && ShaderHotReload.Start(Mod) is { } started)
        {
            hotReload = started;
            hotReload.LibraryChanged += () => LabSession.ReloadLibrary(Mod);
        }

        Open = true;

        if (ShaderCompiler.FxcPath is null)
            Notify("No fxc.exe here, so shaders can't be recompiled.");
    }

    /// <summary> From anywhere, with the panels open: into the lab realm, where each tab has a room. </summary>
    public void EnterLab()
    {
        if (!Open)
            Toggle();

        travelRequested = !LabRooms.InLab;
    }

    public override void Unload()
    {
        hotReload?.Dispose();
        hotReload = null;
        ShaderBench.Unload();
        LabSession.Stop();
        fields = [];
        builtTarget = null;
        builtEffect = null;
        builtSlot = null;
        exportPanel.Close();
        Open = false;
    }

    public override void ClearWorld()
    {
        LabTools.ClearWorld();
        LabRooms.Reset();

        foreach (LabSlot slot in LabSession.Slots)
            slot.Layer.ClearNodes();
    }

    public override void PostUpdateEverything()
    {
        LabClock.Tick();

        if (Open)
            LabTools.Tick();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!Open)
        {
            ReleaseKeyboard();
            return;
        }

        if (travelRequested)
        {
            travelRequested = false;

            if (LabRooms.InLab)
                SubworldSystem.Exit();
            else
                SubworldSystem.Enter<VfxLabRealm>();

            return;
        }

        hotReload?.Update();
        LabRooms.Update();

        foreach (LabSlot slot in LabSession.Slots)
            slot.Live.Update();

        if (MouseOverPanels)
        {
            // Without this a click on a panel also swings whatever's held, and the scroll wheel turns the hotbar.
            Main.LocalPlayer.mouseInterface = true;
            PlayerInput.LockVanillaMouseScroll(ScrollLock);
        }

        if (Gui.IsEditing)
        {
            PlayerInput.WritingText = true;
            Main.blockInput = true;
            wasEditing = true;
        }
        else
        {
            ReleaseKeyboard();
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!Open)
            return;

        int cursor = layers.FindIndex(static layer => layer.Name == "Vanilla: Cursor");

        layers.Insert(cursor < 0 ? layers.Count : cursor, new LegacyGameInterfaceLayer("TrelamiumIII: VFX Lab", static () =>
        {
            DrawFrame(Main.spriteBatch);
            return true;
        }, InterfaceScaleType.UI));
    }

    /// <summary> Left set, the player can't move once the field is closed. </summary>
    private static void ReleaseKeyboard()
    {
        if (!wasEditing)
            return;

        wasEditing = false;
        Main.blockInput = false;
    }

    private static void DrawFrame(SpriteBatch batch)
    {
        ShaderBench.NextFrame();

        LabInput input = NextInput();
        lastInput = input;

        LayOut();

        bool overPanels = MouseOverPanels;

        if (overPanels)
        {
            Main.LocalPlayer.mouseInterface = true;

            if (input.Scroll != 0)
                PlayerInput.ScrollWheelDeltaForUI = 0;
        }

        Gui.Begin(batch, input);

        if (LabRooms.Fade > 0f)
        {
            Point screen = LabGui.ScreenSize;
            Gui.Fill(new Rectangle(0, 0, screen.X, screen.Y), Color.Black * LabRooms.Fade);
        }

        // A popup only keeps the mouse while it's over the popup, so going from one colour to the next is one click. An export's review keeps it wherever it is.
        Gui.Enabled = !exportPanel.IsOpen && !(inspector.PopupOpen && inspector.PopupArea.Contains(input.Mouse));

        LabTools.Frame(Gui, overPanels);

        DrawList();
        DrawInspector();
        DrawStatus();

        Gui.Enabled = true;
        inspector.DrawPopups(Gui);
        exportPanel.Draw(Gui, Notify);

        Gui.End();

        // A drag is one step of undo, however many frames it took.
        if (!input.LeftDown)
        {
            foreach (LabSlot slot in LabSession.Slots)
                slot.Commit();
        }

        if (LabSession.Selected is { } selected && !Gui.IsEditing && !exportPanel.IsOpen)
        {
            if (input.UndoPressed)
                selected.Undo();
            else if (input.RedoPressed)
                selected.Redo();
        }
    }

    private static LabInput NextInput()
    {
        if (ScriptedFrames is not { } frames)
            return LabInput.Read(lastInput);

        // With nothing queued the mouse just rests where it was, holding whatever it held.
        return frames.TryDequeue(out LabInput frame) ? frame : new LabInput { Mouse = lastInput.Mouse, LeftDown = lastInput.LeftDown };
    }

    private static void LayOut()
    {
        Point screen = LabGui.ScreenSize;

        statusBar = new Rectangle(Margin, screen.Y - StatusHeight - Margin, screen.X - Margin * 2, StatusHeight);
        listPanel = new Rectangle(Margin, TopMargin, ListWidth, statusBar.Y - TopMargin - Margin);
        inspectorPanel = new Rectangle(screen.X - InspectorWidth - Margin, Margin, InspectorWidth, statusBar.Y - Margin * 2);
    }

    #region The list

    private static void DrawList()
    {
        Gui.Panel(listPanel);

        int x = listPanel.X + Padding;
        int y = listPanel.Y + Padding;
        int inner = listPanel.Width - Padding * 2;
        BenchGroup[] groups = Enum.GetValues<BenchGroup>();
        int tabWidth = (inner - 2 * (groups.Length - 1)) / groups.Length;

        // In the lab realm each tab is a room, so the world follows.
        if (LabRooms.InLab && !LabRooms.Leaving && (int)LabRooms.Current != (int)group)
            group = (BenchGroup)LabRooms.Current;

        for (int i = 0; i < groups.Length; i++)
        {
            if (Gui.Button($"tab{i}", new Rectangle(x + i * (tabWidth + 2), y, tabWidth, ButtonHeight), groups[i].ToString(), group == groups[i]))
            {
                group = groups[i];
                listScroll = 0;
                LabRooms.Go((LabRoom)i);
            }
        }

        y += ButtonHeight + 4;

        if (Gui.TextBox("search", new Rectangle(x, y, inner, ButtonHeight), ref search, "Search"))
            listScroll = 0;

        y += ButtonHeight + 4;

        List<BenchTarget> shown = ShaderBench.Targets
            .Where(static target => target.Group == group && (search.Length == 0 || target.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // The drafts head the list, with the real fields they can be started from below them.
        int drafts = group == BenchGroup.Fields ? LabSession.Slots.Count + 2 : 0;
        int total = shown.Count + drafts;

        var list = new Rectangle(x, y, inner, listPanel.Bottom - y - Padding);
        int rows = Math.Max(list.Height / ListRowHeight, 1);
        int maxScroll = Math.Max(total - rows, 0);

        if (Gui.Hovered(list))
            listScroll -= Gui.Input.Scroll;

        listScroll = Math.Clamp(listScroll, 0, maxScroll);

        for (int i = 0; i < rows && i + listScroll < total; i++)
        {
            int index = i + listScroll;
            var row = new Rectangle(list.X, list.Y + i * ListRowHeight, list.Width, ListRowHeight);

            if (index < drafts)
                DrawDraftRow(row, index);
            else
                DrawTargetRow(row, shown[index - drafts]);
        }
    }

    private static void DrawDraftRow(Rectangle row, int index)
    {
        int slots = LabSession.Slots.Count;

        if (index == 0)
        {
            Gui.Text("Your fields", row.X + 4, row.Center.Y, LabTheme.Muted, LabGui.SmallScale);
            return;
        }

        if (index == slots + 1)
        {
            Gui.Text("In the game", row.X + 4, row.Bottom - 8, LabTheme.Muted, LabGui.SmallScale);
            return;
        }

        LabSlot slot = LabSession.Slots[index - 1];
        bool drawing = slot.Layer.NodeCount > 0;

        Gui.Record($"slot:{slot.Letter}", row);

        if (Gui.ListRow(row, $"{slot.Letter}  {slot.Recipe?.Name ?? "(empty)"}", slot == LabSession.Selected, drawing ? LabTheme.Live : null))
            SelectSlot(slot);
    }

    private static void DrawTargetRow(Rectangle row, BenchTarget target)
    {
        string? changes = target.Overrides.Count > 0 ? target.Overrides.Count.ToString() : null;

        if (Gui.ListRow(row, target.Name, target == ShaderBench.Selected, ShaderBench.IsDrawing(target) ? LabTheme.Live : null, changes, target.Name))
            Select(target);
    }

    internal static void Select(BenchTarget target)
    {
        ShaderBench.Selected = target;
        LabSession.Selected = null;
        inspector.ClosePopups();
        inspector.ScrollToTop();
        builtTarget = null;
        builtSlot = null;
    }

    internal static void SelectSlot(LabSlot slot)
    {
        LabSession.Selected = slot;
        ShaderBench.Selected = null;
        inspector.ClosePopups();
        inspector.ScrollToTop();
        builtTarget = null;
        builtSlot = null;
        nameText = slot.Recipe?.Name ?? string.Empty;
    }

    #endregion

    #region The inspector

    private static void DrawInspector()
    {
        Gui.Panel(inspectorPanel);

        if (LabSession.Selected is { } slot)
            DrawSlot(slot);
        else if (ShaderBench.Selected is { } target)
            DrawTarget(target);
        else
            Gui.Text("Choose something to tune.", inspectorPanel.X + Padding + 2, inspectorPanel.Y + Padding + HeaderHeight / 2, LabTheme.Muted);
    }

    private static void DrawSlot(LabSlot slot)
    {
        int x = inspectorPanel.X + Padding;
        int y = inspectorPanel.Y + Padding;
        int inner = inspectorPanel.Width - Padding * 2;

        if (slot.Recipe is not { } recipe)
        {
            Gui.Text($"Slot {slot.Letter} is empty.", x + 2, y + HeaderHeight / 2, Color.White);
            y += HeaderHeight;

            int half = (inner - 2) / 2;

            if (Gui.Button("new", new Rectangle(x, y, half, ButtonHeight), "New field", tip: "Start from a plain smoke"))
                LoadInto(slot, RecipeEdit.NewField(UnusedName("Field")));

            if (Gui.Button("load", new Rectangle(x + half + 2, y, half, ButtonHeight), "Load", tip: "Open a saved recipe"))
                OpenLoadList(slot, new Rectangle(x, y, half, ButtonHeight));

            y += ButtonHeight + 8;
            Gui.Text("Or pick a field in the list, and open it from there.", x + 2, y + 8, LabTheme.Muted, LabGui.SmallScale);
            return;
        }

        if (Gui.TextBox("name", new Rectangle(x, y, inner - 16, ButtonHeight), ref nameText, "Name") && VfxRecipe.IsValidName(nameText))
        {
            recipe.Name = nameText;
            slot.Changed();
        }

        Color state = slot.Live.Status switch
        {
            ShaderBuildStatus.Compiled => LabTheme.Live,
            ShaderBuildStatus.Failed => LabTheme.Failure,
            _ => LabTheme.Changed,
        };

        Gui.Fill(new Rectangle(x + inner - 10, y + ButtonHeight / 2 - 3, 6, 6), state);

        y += ButtonHeight + 3;

        int width = (inner - 2 * 5) / 6;
        string[] labels = ["Save", "Export", "Load", "Undo", "Redo", "Empty"];

        for (int i = 0; i < labels.Length; i++)
        {
            var area = new Rectangle(x + i * (width + 2), y, width, ButtonHeight);

            if (!Gui.Button(labels[i].ToLowerInvariant(), area, labels[i]))
                continue;

            switch (i)
            {
                case 0:
                    SaveRecipe(recipe);
                    break;
                case 1:
                    inspector.ClosePopups();
                    exportPanel.Open(recipe, LabSession.Library, ModContent.GetInstance<VfxLabUI>().Mod);
                    break;
                case 2:
                    OpenLoadList(slot, area);
                    break;
                case 3:
                    slot.Undo();
                    break;
                case 4:
                    slot.Redo();
                    break;
                case 5:
                    slot.Empty();
                    break;
            }
        }

        // Emptying the slot from the row above leaves nothing further to draw.
        if (slot.Recipe is not { } current)
            return;

        y += ButtonHeight + 3;

        int budgetWidth = inner - 74;
        string budget = BudgetLine(slot);

        Gui.Text(LabGui.Fit(budget, budgetWidth), x + 2, y + ButtonHeight / 2, slot.Live.Status == ShaderBuildStatus.Failed ? LabTheme.Failure : LabTheme.Muted, LabGui.SmallScale);
        Gui.Tooltip(new Rectangle(x, y, budgetWidth, ButtonHeight), budget);

        if (Gui.Button("measure", new Rectangle(x + inner - 70, y, 70, ButtonHeight), "Measure", tip: "How long the GPU takes to draw this over the whole screen"))
            measureRequest = slot;

        y += ButtonHeight + 3;

        if (slot != builtSlot || slot.Version != builtVersion)
        {
            fields = slotInspector.Build(slot);
            builtSlot = slot;
            builtVersion = slot.Version;
            nameText = current.Name;
        }

        inspector.Draw(Gui, new Rectangle(x, y, inner, inspectorPanel.Bottom - y - Padding), fields);
    }

    private static string BudgetLine(LabSlot slot)
    {
        LiveRecipe live = slot.Live;

        if (live.Status == ShaderBuildStatus.Failed)
            return live.Log.Split('\n')[0];

        if (live.Problems.FirstOrDefault(static problem => problem.IsError) is { Message: not null } error)
            return error.ToString();

        string cost = slot.Measured is { } measured ? $"  {measured}" : string.Empty;

        return $"{slot.Layer.NodeCount} nodes  {live.SamplerCount} of 16 textures{cost}";
    }

    private static void DrawTarget(BenchTarget target)
    {
        int x = inspectorPanel.X + Padding;
        int y = inspectorPanel.Y + Padding;
        int inner = inspectorPanel.Width - Padding * 2;

        Gui.Text(LabGui.Fit(target.Name, inner - 16), x + 2, y + HeaderHeight / 2, Color.White);

        if (ShaderBench.IsDrawing(target))
            Gui.Fill(new Rectangle(x + inner - 8, y + HeaderHeight / 2 - 3, 6, 6), LabTheme.Live);

        y += HeaderHeight;

        int buttonWidth = (inner - 2 * 2) / 3;

        if (Gui.Button("reset", new Rectangle(x, y, buttonWidth, ButtonHeight), "Reset", tip: "Put every value back"))
            target.Overrides.Clear();

        if (Gui.Button("bypass", new Rectangle(x + buttonWidth + 2, y, buttonWidth, ButtonHeight), "Compare", ShaderBench.Bypass, "Draw as authored, without your changes"))
            ShaderBench.Bypass = !ShaderBench.Bypass;

        if (Gui.Button("copy", new Rectangle(x + (buttonWidth + 2) * 2, y, buttonWidth, ButtonHeight), "Copy as C#", tip: "Copy your changes as code to paste where they came from"))
            CopyChanges(target);

        y += ButtonHeight + 3;

        if (target.TestLabel is { } label)
        {
            int half = (inner - 2) / 2;

            if (Gui.Button("test", new Rectangle(x, y, half, ButtonHeight), label, tip: "Make some near you, to see what you're tuning"))
                target.Test();

            if (Gui.Button("clear", new Rectangle(x + half + 2, y, half, ButtonHeight), "Clear", tip: target.ClearTip))
                target.ClearTest();

            y += ButtonHeight + 3;
        }

        if (target is FieldTarget { Layer: var layer } && LayerRecipe.CanOpen(layer))
        {
            if (Gui.Button("open", new Rectangle(x, y, inner, ButtonHeight), "Open as a draft, to change its physics", tip: "Copies its look, physics and residue into one of your slots"))
                OpenLayer(layer);

            y += ButtonHeight + 3;
        }
        else if (target is FieldTarget { Layer: ModularField and not LabFieldSlot } && File.Exists(RecipeFile.PathOf(LabFiles.RecipeFolder, target.Name)))
        {
            if (Gui.Button("openrecipe", new Rectangle(x, y, inner, ButtonHeight), "Open its recipe", tip: "This was exported from the lab, so change it there and export it again"))
            {
                if (LabSession.FreeSlot() is { } free)
                    LoadSaved(free, target.Name);
            }

            y += ButtonHeight + 3;
        }

        // The effect only appears once it's loaded, and a hot reload swaps it for one that may have other uniforms.
        Effect? effect = target.Effect;

        if (effect is null)
        {
            Gui.Text("Loading..", x + 2, y + 12, LabTheme.Muted);
            return;
        }

        if (target != builtTarget || effect != builtEffect)
        {
            fields = ShaderBench.BuildFields(target, effect);
            AppendToolRows(target);
            builtTarget = target;
            builtEffect = effect;
        }

        inspector.Draw(Gui, new Rectangle(x, y, inner, inspectorPanel.Bottom - y - Padding), fields);
    }

    /// <summary> The settings of whichever tool works on this kind of target. </summary>
    private static void AppendToolRows(BenchTarget target)
    {
        if (target is FieldTarget)
        {
            fields.Add(new InspectorField { Id = "emitter", Label = "Release", Kind = FieldKind.Header, Tip = "How the Emit and Spray tools let this field out" });
            fields.AddRange(SlotInspector.EmitterRows(LabTools.BenchEmitter, static () => { }));
        }
        else if (target is ConditionTarget)
        {
            fields.Add(new InspectorField { Id = "brush", Label = "Brush", Kind = FieldKind.Header, Tip = "How the Brush tool paints this condition" });
            fields.Add(new InspectorField
            {
                Id = "brush/radius",
                Label = "Radius",
                Kind = FieldKind.Numbers,
                Tip = "Pixels",
                Min = 8f,
                Max = 240f,
                Get = static () => [LabTools.BrushRadius],
                Set = static values => LabTools.BrushRadius = values[0],
            });
        }
    }

    private static void LoadInto(LabSlot slot, VfxRecipe recipe)
    {
        slot.Load(recipe);
        SelectSlot(slot);
    }

    private static void OpenLayer(FieldLayer layer)
    {
        if (LabSession.FreeSlot() is not { } slot)
            return;

        VfxRecipe recipe = LayerRecipe.From(layer);

        recipe.Emitter = new RecipeEmitter
        {
            Rate = LabTools.BenchEmitter.Rate,
            Radius = LabTools.BenchEmitter.Radius,
            Life = LabTools.BenchEmitter.Life,
            Speed = LabTools.BenchEmitter.Speed,
            Angle = LabTools.BenchEmitter.Angle,
            Spread = LabTools.BenchEmitter.Spread,
            Temperature = LabTools.BenchEmitter.Temperature,
        };

        LoadInto(slot, recipe);
        Notify($"{layer.GetType().Name} is in slot {slot.Letter}. What it does in its own code, beyond look, physics and residue, doesn't come across.");
    }

    private static string UnusedName(string stem)
    {
        HashSet<string> taken = [.. RecipeFile.Names(LabFiles.RecipeFolder), .. LabSession.Slots.Select(static slot => slot.Recipe?.Name ?? string.Empty)];
        string name = stem;

        for (int n = 2; taken.Contains(name); n++)
            name = $"{stem}{n}";

        return name;
    }

    private static void SaveRecipe(VfxRecipe recipe)
    {
        try
        {
            RecipeFile.Save(LabFiles.RecipeFolder, recipe);
            Notify($"Saved {recipe.Name} to {LabFiles.RecipeFolder}");
        }
        catch (IOException exception)
        {
            Notify($"Couldn't save: {exception.Message}");
        }
    }

    private static void OpenLoadList(LabSlot slot, Rectangle anchor)
    {
        List<(string Label, Action Pick)> items = RecipeFile.Names(LabFiles.RecipeFolder)
            .Select(name => (name, (Action)(() => LoadSaved(slot, name))))
            .ToList();

        if (items.Count == 0)
        {
            Notify("Nothing has been saved yet.");
            return;
        }

        inspector.OpenList("Load a recipe", items, anchor);
    }

    private static void LoadSaved(LabSlot slot, string name)
    {
        try
        {
            LoadInto(slot, RecipeFile.Load(LabFiles.RecipeFolder, name));
            Notify($"Loaded {name}.");
        }
        catch (Exception exception) when (exception is RecipeFormatException or IOException)
        {
            Notify($"Couldn't load {name}: {exception.Message}");
        }
    }

    private static void CopyChanges(BenchTarget target)
    {
        if (target.Overrides.Count == 0)
        {
            Notify("Nothing to copy: no values have been changed.");
            return;
        }

        LastCopied = target.CopyAsCSharp();
        Platform.Get<IClipboard>().Value = LastCopied;
        Notify($"Copied {target.Overrides.Count} changed values of {target.Name}.");
    }

    private static void Notify(string text)
    {
        notice = text;
        noticeFrames = NoticeFrames;
    }

    #endregion

    #region The status bar

    private static void DrawStatus()
    {
        Gui.Panel(statusBar);

        int x = statusBar.X + 3;
        int y = statusBar.Y + 2;
        int height = statusBar.Height - 4;

        Rectangle Next(int width)
        {
            var area = new Rectangle(x, y, width, height);
            x += width + 2;
            return area;
        }

        void Gap() => x += 8;

        // Worlds change between frames, not halfway through drawing one.
        if (LabRooms.InLab)
            travelRequested |= Gui.Button("leave", Next(76), "Leave lab", tip: "Back to the world you came from");
        else
            travelRequested |= Gui.Button("enter", Next(76), "Enter lab", tip: "A room for fields, one for tiles, one for shaders");

        Gap();

        foreach ((LabTool tool, string label, string tip) in (ReadOnlySpan<(LabTool, string, string)>)
        [
            (LabTool.Emit, "Emit", "Click to place a source. Drag one to move it, right-click to remove it"),
            (LabTool.Spray, "Spray", "Hold to release at the pointer"),
            (LabTool.Brush, "Brush", "Hold to paint the selected tile condition"),
        ])
        {
            if (Gui.Button($"tool:{label}", Next(48), label, LabTools.Tool == tool, tip))
                LabTools.Tool = LabTools.Tool == tool ? LabTool.None : tool;
        }

        if (Gui.Button("clearall", Next(62), "Clear all", tip: "Remove every cloud, source and tile condition in the world"))
            LabTools.ClearAll();

        Gap();

        if (Gui.Button("pause", Next(44), LabClock.Paused ? "Play" : "Pause", LabClock.Paused, "Stops your fields. Real ones keep the game's time"))
            LabClock.Paused = !LabClock.Paused;

        if (Gui.Button("step", Next(38), "Step", tip: "One tick, while paused"))
            LabClock.Step();

        if (Gui.Button("speed", Next(38), $"{LabClock.Speed:0.##}x", LabClock.Speed != 1f, "How fast your fields run"))
            LabClock.Speed = Speeds[(Array.IndexOf(Speeds, LabClock.Speed) + 1) % Speeds.Length];

        if (LabRooms.InLab)
        {
            if (Gui.Button("light", Next(52), LightNames[lightIndex], lightIndex != 0, "How brightly the lab is lit"))
            {
                lightIndex = (lightIndex + 1) % LightLevels.Length;
                LabRooms.LightLevel = LightLevels[lightIndex];
            }
        }

        if (LabSession.Selected is { Recipe: not null } slot)
        {
            Gap();

            if (Gui.Button("view", Next(84), $"View: {slot.Live.View}", slot.Live.View != ShaderView.Final, "Look inside the shader. Your field keeps running underneath"))
            {
                ShaderView[] views = Enum.GetValues<ShaderView>();
                slot.SetView(views[((int)slot.Live.View + 1) % views.Length]);
            }
        }

        Gap();

        // A notice has its moment and then gives way, or a shader error could sit behind one unseen.
        if (noticeFrames > 0 && --noticeFrames == 0)
            notice = string.Empty;

        bool isError = hotReload?.StatusIsError == true;
        string text = notice.Length > 0 ? notice : hotReload?.Status ?? "No source folder found, so saved shaders won't reload. Set it under Authoring in the mod's config.";

        var close = new Rectangle(statusBar.Right - 62, y, 60, height);

        Gui.Text(LabGui.Fit(text, close.X - x - 6), x, statusBar.Center.Y, isError && notice.Length == 0 ? LabTheme.Failure : LabTheme.Muted);

        if (Gui.Button("close", close, "Close", tip: "/vfx opens this again"))
            ModContent.GetInstance<VfxLabUI>().Toggle();
    }

    #endregion

    #region Measuring

    public override void PostSetupContent()
    {
        if (!Main.dedServ)
            Main.OnPostDraw += RunMeasure;
    }

    /// <summary> After the frame, the one place it's safe to bind a target. </summary>
    private static void RunMeasure(GameTime gameTime)
    {
        if (measureRequest is not { } slot)
            return;

        measureRequest = null;

        if (slot.Layer.Live.Asset.Value is not { } effect)
            return;

        slot.Measured = $"{LabBudget.Measure(effect, slot.Live.Material.Apply):F2} ms full screen";
    }

    #endregion
}

/// <summary> What a shader really costs: how long the GPU takes to fill a field target with it, every pixel in the body of the material. </summary>
internal static class LabBudget
{
    private const int Draws = 30;

    /// <summary> Reading a pixel back makes the CPU wait for the draws, which a frame time wouldn't show under vsync. </summary>
    public static double Measure(Effect effect, Action<Effect> apply)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        SpriteBatch batch = Main.spriteBatch;

        int width = PlayerInput.RealScreenWidth / 2;
        int height = PlayerInput.RealScreenHeight / 2;

        using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        using var white = new Texture2D(device, 1, 1);
        white.SetData([Color.White]);

        var readback = new Color[1];
        var corner = new Rectangle(0, 0, 1, 1);

        void Flush() => target.GetData(0, corner, readback, 0, 1);

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);
        Flush();

        FieldLayer.ApplyScreenParameters(effect, new Vector2(2f));
        effect.Parameters["uAttrTex"]?.SetValue(white);
        apply(effect);

        double Time()
        {
            var watch = Stopwatch.StartNew();

            batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            effect.CurrentTechnique.Passes[0].Apply();

            for (int i = 0; i < Draws; i++)
                batch.Draw(white, new Rectangle(0, 0, width, height), Color.White);

            batch.End();
            Flush();

            return watch.Elapsed.TotalMilliseconds / Draws;
        }

        // Twice, as a new shader finishes compiling the first time it's bound, and that's not what it costs to draw.
        Time();
        double milliseconds = Time();

        effect.Parameters["uAttrTex"]?.SetValue((Texture?)null);
        device.SetRenderTarget(null);

        return milliseconds;
    }
}
