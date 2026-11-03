using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

internal enum LabTool
{
    None,

    /// <summary> Click to place a source that keeps releasing. Drag one to move it, and right-click to remove it. </summary>
    Emit,

    /// <summary> Hold to release at the pointer. </summary>
    Spray,

    /// <summary> Hold to paint a residue. </summary>
    Brush,
}

/// <summary> A source left in the world, releasing a field until it's removed. </summary>
/// <param name="settings"> Asked each time, as an undo or a load replaces the recipe it came from. </param>
internal sealed class LabEmitter(FieldLayer layer, Func<RecipeEmitter> settings, Vector2 position)
{
    private float owed;

    public FieldLayer Layer => layer;

    public Vector2 Position { get; set; } = position;

    public void Tick(int steps)
    {
        RecipeEmitter current = settings();

        owed += current.Rate / 60f * steps;

        while (owed >= 1f)
        {
            owed -= 1f;
            LabTools.Release(layer, current, Position);
        }
    }
}

/// <summary> What the mouse does out in the world while the lab's open, and everything it's left there. </summary>
internal static class LabTools
{
    /// <summary> UI pixels from an emitter's marker that still count as grabbing it. </summary>
    private const int GrabReach = 12;

    private const int BrushInterval = 6;

    private static readonly List<LabEmitter> emitters = [];

    private static LabEmitter? dragging;
    private static bool spraying;
    private static bool painting;
    private static Vector2 pointer;
    private static float sprayOwed;
    private static int brushTimer;

    public static LabTool Tool { get; set; }

    public static IReadOnlyList<LabEmitter> Emitters => emitters;

    /// <summary> How a field already in the game is released, as those have no recipe to hold this. </summary>
    public static RecipeEmitter BenchEmitter { get; } = new();

    public static float BrushRadius { get; set; } = 40f;

    /// <summary> The field the mouse would release: the selected draft, or a real one picked on the Bench. </summary>
    private static (FieldLayer Layer, Func<RecipeEmitter> Settings)? Source
    {
        get
        {
            if (LabSession.Selected is { Recipe: not null } slot)
                return (slot.Layer, () => slot.Recipe?.Emitter ?? BenchEmitter);

            return ShaderBench.Selected is FieldTarget target ? (target.Layer, static () => BenchEmitter) : null;
        }
    }

    public static void Release(FieldLayer layer, RecipeEmitter settings, Vector2 position)
    {
        float angle = MathHelper.ToRadians(settings.Angle + Main.rand.NextFloat(-settings.Spread, settings.Spread));

        layer.SpawnNode(position, angle.ToRotationVector2() * settings.Speed, settings.Radius, settings.Life, temperature: settings.Temperature);
    }

    /// <summary> Every tick. Releases on the game's clock, but only a draft's own emitters stop when the lab's is paused. </summary>
    public static void Tick()
    {
        foreach (LabEmitter emitter in emitters)
            emitter.Tick(emitter.Layer is LabFieldSlot ? LabClock.StepsThisTick : 1);

        if (spraying && Source is { } source)
        {
            RecipeEmitter settings = source.Settings();

            sprayOwed += settings.Rate / 60f;

            while (sprayOwed >= 1f)
            {
                sprayOwed -= 1f;
                Release(source.Layer, settings, pointer);
            }
        }

        if (painting && ShaderBench.Selected is ResidueTarget target && ++brushTimer >= BrushInterval)
        {
            brushTimer = 0;
            ResidueSystem.ApplyCircle(target.Residue, pointer, BrushRadius, duration: 60f * 60f, origin: ResidueOrigin.Owned);
        }
    }

    /// <summary> Everything the lab has put out in the world, gone. So nobody has to go back through the lists to tidy up. </summary>
    public static void ClearAll()
    {
        emitters.Clear();
        dragging = null;

        foreach (FieldLayer layer in FieldSystem.Layers)
            layer.ClearNodes();

        ResidueSystem.ClearAll();
    }

    public static void RemoveEmittersOf(FieldLayer layer)
    {
        if (dragging?.Layer == layer)
            dragging = null;

        emitters.RemoveAll(emitter => emitter.Layer == layer);
    }

    /// <summary> The world is about to change, and nothing out in it is coming along. </summary>
    public static void ClearWorld()
    {
        emitters.Clear();
        dragging = null;
        spraying = false;
        painting = false;
    }

    /// <summary> Each drawn frame, with the panels already drawn. </summary>
    /// <param name="overPanels"> Whether the mouse is on a panel, so what it does isn't for the world. </param>
    public static void Frame(LabGui gui, bool overPanels)
    {
        LabInput input = gui.Input;

        pointer = ToWorld(input.Mouse);

        if (!input.LeftDown)
        {
            spraying = false;
            painting = false;
            dragging = null;
        }

        DrawMarkers(gui);

        if (Tool == LabTool.None)
            return;

        if (!overPanels)
            Main.LocalPlayer.mouseInterface = true;

        if (dragging is { } held)
            held.Position = pointer;

        if (overPanels || gui.IsEditing || !gui.Enabled)
            return;

        switch (Tool)
        {
            case LabTool.Emit:
                UseEmitterTool(input);
                break;
            case LabTool.Spray:
                spraying |= input.LeftPressed && Source is not null;
                break;
            case LabTool.Brush:
                painting |= input.LeftPressed && ShaderBench.Selected is ResidueTarget;
                break;
        }
    }

    private static void UseEmitterTool(in LabInput input)
    {
        LabEmitter? nearest = NearestTo(input.Mouse);

        if (input.RightPressed && nearest is not null)
        {
            emitters.Remove(nearest);
            return;
        }

        if (!input.LeftPressed)
            return;

        if (nearest is not null)
        {
            dragging = nearest;
            return;
        }

        if (Source is { } source)
        {
            var placed = new LabEmitter(source.Layer, source.Settings, pointer);

            emitters.Add(placed);
            dragging = placed;
        }
    }

    private static LabEmitter? NearestTo(Point mouse)
        => emitters.FirstOrDefault(emitter => Vector2.DistanceSquared(ToUi(emitter.Position), mouse.ToVector2()) <= GrabReach * GrabReach);

    private static void DrawMarkers(LabGui gui)
    {
        foreach (LabEmitter emitter in emitters)
        {
            Vector2 at = ToUi(emitter.Position);
            var center = new Rectangle((int)at.X - 3, (int)at.Y - 3, 6, 6);
            var ring = new Rectangle((int)at.X - 7, (int)at.Y - 7, 14, 14);

            gui.Record($"emitter:{emitters.IndexOf(emitter)}", ring);
            gui.Fill(center, emitter == dragging ? Color.White : LabTheme.Accent);
            gui.Outline(ring, Color.Black * 0.7f);
        }
    }

    /// <summary> Works in any drawing context, unlike <see cref="Main.MouseWorld" />, whose inputs change meaning while interface layers draw. </summary>
    public static Vector2 ToWorld(Point ui)
    {
        Vector2 size = new(Terraria.GameInput.PlayerInput.RealScreenWidth, Terraria.GameInput.PlayerInput.RealScreenHeight);
        Vector2 pixel = ui.ToVector2() * Main.UIScale;
        float zoom = Main.GameViewMatrix.Zoom.X;

        return Main.screenPosition + size / 2f + (pixel - size / 2f) / zoom;
    }

    public static Vector2 ToUi(Vector2 world)
    {
        Vector2 size = new(Terraria.GameInput.PlayerInput.RealScreenWidth, Terraria.GameInput.PlayerInput.RealScreenHeight);
        float zoom = Main.GameViewMatrix.Zoom.X;

        return (size / 2f + (world - Main.screenPosition - size / 2f) * zoom) / Main.UIScale;
    }
}
