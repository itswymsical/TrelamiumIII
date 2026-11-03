using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Content.DevTools.VfxLab.UI;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Vfx;
using TrelamiumIII.Core.Residues;

namespace TrelamiumIII.Content.DevTools.VfxLab;

/// <summary> Every <see cref="FieldModule" /> there is, and the settings each one has. New modules are found by reflection, so adding one is one class. </summary>
internal static class ModuleCatalog
{
    private static List<Type>? types;

    public static IReadOnlyList<Type> Types => types ??= typeof(FieldModule).Assembly.GetTypes()
        .Where(static type => !type.IsAbstract && type.IsSubclassOf(typeof(FieldModule)))
        .OrderBy(static type => type.Name, StringComparer.Ordinal)
        .ToList();

    public static Type? Find(string name) => Types.FirstOrDefault(type => type.Name == name);

    /// <summary> A module's settings: its public float fields. </summary>
    public static IEnumerable<FieldInfo> Settings(Type type)
        => type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(static field => field.FieldType == typeof(float));

    /// <summary> A fresh module of the recipe's type, with the recipe's settings. Null for a type this build doesn't have. </summary>
    public static FieldModule? Create(RecipeModule recipe)
    {
        if (Find(recipe.Type) is not { } type)
            return null;

        var module = (FieldModule)Activator.CreateInstance(type)!;

        foreach (FieldInfo field in Settings(type))
            field.SetValue(module, recipe.Get(field.Name, (float)field.GetValue(module)!));

        return module;
    }
}

/// <summary> One of the lab's draft fields and the recipe it's showing, with everything that goes with editing one. </summary>
internal sealed class LabSlot(string letter, LabFieldSlot layer, StageLibrary library)
{
    private readonly RecipeHistory history = new();
    private bool dirty;

    public string Letter => letter;

    public LabFieldSlot Layer => layer;

    public VfxRecipe? Recipe { get; private set; }

    public LiveRecipe Live { get; } = new(layer.Live, library);

    /// <summary> Goes up when the recipe's structure changes, which is when the inspector's rows have to be rebuilt. </summary>
    public int Version { get; private set; }

    public string? Measured { get; set; }

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    public void Load(VfxRecipe recipe)
    {
        Recipe = recipe;
        history.Clear();
        history.Push(RecipeFile.Serialize(recipe));
        dirty = false;
        Measured = null;
        Version++;
        Rebuild();
    }

    public void Empty()
    {
        Recipe = null;
        history.Clear();
        dirty = false;
        Measured = null;
        Version++;
        LabTools.RemoveEmittersOf(Layer);
        Layer.ClearNodes();
    }

    /// <summary> After any edit. Regenerates, which only recompiles if the source came out different. </summary>
    /// <param name="structural"> Whether the inspector has different rows to show now. </param>
    public void Changed(bool structural = false)
    {
        dirty = true;
        Measured = null;

        if (structural)
            Version++;

        Rebuild();
    }

    /// <summary> Records the edit for undo, once it's finished: a drag is one step, however many frames it took. </summary>
    public void Commit()
    {
        if (!dirty || Recipe is null)
            return;

        dirty = false;

        string state = RecipeFile.Serialize(Recipe);

        if (!history.Push(state))
            return;

        try
        {
            Directory.CreateDirectory(LabFiles.AutosaveFolder);
            File.WriteAllText(RecipeFile.PathOf(LabFiles.AutosaveFolder, Recipe.Name), state);
        }
        catch (IOException exception)
        {
            TrelamiumIII.Instance.Logger.Warn($"Couldn't autosave the recipe {Recipe.Name}.", exception);
        }
    }

    public void Undo() => Restore(history.Undo());

    public void Redo() => Restore(history.Redo());

    public void SetView(ShaderView view)
    {
        Live.View = view;
        Rebuild();
    }

    public void SetLibrary(StageLibrary replacement)
    {
        Live.Library = replacement;
        Version++;
        Rebuild();
    }

    private void Restore(string? state)
    {
        if (state is null)
            return;

        Recipe = RecipeFile.Parse(state);
        dirty = false;
        Measured = null;
        Version++;
        Rebuild();
    }

    private void Rebuild()
    {
        if (Recipe is not { } recipe)
            return;

        Live.Set(recipe);

        VfxFieldSpec spec = Layer.Spec;
        RecipePhysics physics = recipe.Physics;

        spec.Kind = physics.Kind switch
        {
            RecipePhysics.Liquid => GasField.GasTypeID.Liquid,
            RecipePhysics.Plasma => GasField.GasTypeID.Plasma,
            _ => GasField.GasTypeID.Gas,
        };

        spec.Density = physics.Density;
        spec.Viscosity = physics.Viscosity;
        spec.Temperature = physics.Temperature;
        spec.Buoyancy = physics.Buoyancy;
        spec.Gravity = physics.Gravity;
        spec.Drag = physics.Drag;
        spec.LifeDecay = physics.LifeDecay;
        spec.EnclosedPenalty = physics.EnclosedPenalty;
        spec.MaxNodes = physics.MaxNodes;
        spec.FlowReference = physics.FlowReference;
        spec.StampStretch = physics.StampStretch;
        spec.TemperatureRange = new Vector2(physics.TemperatureMin, physics.TemperatureMax);
        spec.Material = Live.Material;
        spec.Modules = recipe.Modules.Select(ModuleCatalog.Create).OfType<FieldModule>().ToArray();

        RecipeDeposit? deposit = recipe.Links.Deposit;

        spec.Residue = deposit is null ? null : ResidueSystem.All.FirstOrDefault(residue => residue.Name == deposit.Residue)?.GetType();
        spec.ResidueThreshold = deposit?.Threshold ?? 0.9f;
        spec.ResidueInterval = deposit?.Interval ?? 5;
    }
}

/// <summary> What the lab's working on. All of it survives moving between worlds, so a draft comes along into the lab realm and back. </summary>
internal static class LabSession
{
    private static readonly List<LabSlot> slots = [];

    public static IReadOnlyList<LabSlot> Slots => slots;

    public static LabSlot? Selected { get; set; }

    public static StageLibrary Library { get; private set; } = new();

    /// <summary> Safe to call again. </summary>
    public static void Start(Mod mod)
    {
        if (slots.Count > 0)
            return;

        Library = LabFiles.LoadLibrary(mod);

        slots.Add(new LabSlot("A", FieldSystem.GetLayer<LabField0>()!, Library));
        slots.Add(new LabSlot("B", FieldSystem.GetLayer<LabField1>()!, Library));
        slots.Add(new LabSlot("C", FieldSystem.GetLayer<LabField2>()!, Library));
        slots.Add(new LabSlot("D", FieldSystem.GetLayer<LabField3>()!, Library));
    }

    public static void ReloadLibrary(Mod mod)
    {
        Library = LabFiles.LoadLibrary(mod);

        foreach (LabSlot slot in slots)
            slot.SetLibrary(Library);
    }

    public static void Stop()
    {
        slots.Clear();
        Selected = null;
    }

    /// <summary> The slot a new recipe should land in: the selected one if it's free, else the first that is, else the selected one anyway. </summary>
    public static LabSlot? FreeSlot()
        => Selected is { Recipe: null } selected ? selected : slots.FirstOrDefault(static slot => slot.Recipe is null) ?? Selected ?? slots.FirstOrDefault();
}
