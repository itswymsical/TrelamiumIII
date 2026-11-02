using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;

namespace TrelamiumIII.Content.DevTools.VfxLab.Recipes;

/// <summary> Edits to a recipe's structure, the ones the lab's stack makes. Pure, so they run without the game. </summary>
internal static class RecipeEdit
{
    /// <summary> A plain cloud to start from: every stage a field needs, in a colour that shows up against the lab's walls. </summary>
    public static VfxRecipe NewField(string name) => new()
    {
        Name = name,
        Surface = "Field",
        Stages =
        [
            new RecipeStage { Id = "noise", Type = "NoiseLayer" },
            new RecipeStage { Id = "cut", Type = "Threshold" },
            new RecipeStage { Id = "shade", Type = "DepthShade" },
            new RecipeStage { Id = "palette", Type = "Palette" }
                .Set("Shadow", 0.18f, 0.32f, 0.36f)
                .Set("Body", 0.35f, 0.62f, 0.66f)
                .Set("Core", 0.72f, 0.92f, 0.9f),
            new RecipeStage { Id = "light", Type = "LightFloor" },
            new RecipeStage { Id = "rim", Type = "Rim" }
                .Set("Outline", 0.72f, 0.92f, 0.9f),
        ],
    };

    /// <summary> Adds a stage, with an id made from its type that nothing else in the recipe has. </summary>
    public static RecipeStage AddStage(VfxRecipe recipe, string type)
    {
        string stem = new(type.Where(char.IsLetterOrDigit).ToArray());
        stem = char.ToLowerInvariant(stem[0]) + stem[1..];

        string id = stem;

        for (int n = 2; recipe.Stages.Any(stage => stage.Id == id); n++)
            id = $"{stem}{n}";

        var added = new RecipeStage { Id = id, Type = type };
        recipe.Stages.Add(added);

        return added;
    }

    public static bool RemoveStage(VfxRecipe recipe, string id) => recipe.Stages.RemoveAll(stage => stage.Id == id) > 0;

    /// <summary>
    ///     Swaps a stage with its neighbour in the same phase. Stages of other phases can sit between them in the list, and they aren't
    ///     what the move means, since a phase always runs in its place whatever the list's order.
    /// </summary>
    /// <param name="direction"> Negative for earlier, positive for later. </param>
    /// <returns> False when it's already at that end of its phase. </returns>
    public static bool MoveStage(VfxRecipe recipe, string id, int direction, StageLibrary library)
    {
        int index = recipe.Stages.FindIndex(stage => stage.Id == id);

        if (index < 0 || !library.Stages.TryGetValue(recipe.Stages[index].Type, out StageDefinition? definition))
            return false;

        int step = Math.Sign(direction);

        for (int other = index + step; other >= 0 && other < recipe.Stages.Count; other += step)
        {
            if (!library.Stages.TryGetValue(recipe.Stages[other].Type, out StageDefinition? neighbour) || neighbour.Phase != definition.Phase)
                continue;

            (recipe.Stages[index], recipe.Stages[other]) = (recipe.Stages[other], recipe.Stages[index]);
            return true;
        }

        return false;
    }

    /// <summary> The stages in the order they'll run, which is how the stack shows them. </summary>
    public static IEnumerable<RecipeStage> InRunOrder(VfxRecipe recipe, StageLibrary library)
        => recipe.Stages.OrderBy(stage => library.Stages.TryGetValue(stage.Type, out StageDefinition? definition) ? definition.Phase : StagePhase.Finish);
}

/// <summary> Undo and redo, as whole recipes. A recipe is plain data, so a stack of snapshots is all it takes. </summary>
internal sealed class RecipeHistory
{
    private const int Limit = 200;

    private readonly List<string> states = [];
    private int current = -1;

    public bool CanUndo => current > 0;

    public bool CanRedo => current < states.Count - 1;

    /// <summary> Records a state, dropping anything that had been undone. The same state twice in a row is one state. </summary>
    /// <returns> False if nothing changed. </returns>
    public bool Push(string state)
    {
        if (current >= 0 && states[current] == state)
            return false;

        states.RemoveRange(current + 1, states.Count - current - 1);
        states.Add(state);

        if (states.Count > Limit)
            states.RemoveAt(0);

        current = states.Count - 1;
        return true;
    }

    public string? Undo() => CanUndo ? states[--current] : null;

    public string? Redo() => CanRedo ? states[++current] : null;

    public void Clear()
    {
        states.Clear();
        current = -1;
    }
}
