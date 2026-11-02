using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary> Ways to look inside a recipe's shader: what its field holds, before any of it becomes a look. </summary>
internal enum ShaderView
{
    /// <summary> As it ships. </summary>
    Final,

    /// <summary> The field's density, with no cut. </summary>
    Cover,

    /// <summary> The noise that shades the body. </summary>
    Noise,

    /// <summary> Which way it's moving. </summary>
    Flow,

    /// <summary> How hot it is. </summary>
    Heat,
}

/// <summary> Debug views are ordinary shaders, generated like any other: the recipe's noise and coordinates, and a stage that shows one thing. </summary>
internal static class ShaderViews
{
    /// <summary> The recipe to generate for <paramref name="view" />. Shares its stages with the one given, and never edits them. </summary>
    public static VfxRecipe Apply(VfxRecipe recipe, ShaderView view, StageLibrary library)
    {
        if (view == ShaderView.Final)
            return recipe;

        List<RecipeStage> stages = recipe.Stages
            .Where(stage => library.Stages.TryGetValue(stage.Type, out StageDefinition? definition) && definition.Phase is StagePhase.Coords or StagePhase.Shape)
            .ToList();

        stages.Add(new RecipeStage { Id = "view", Type = $"View{view}" });

        return new VfxRecipe { Name = recipe.Name, Surface = recipe.Surface, Stages = stages };
    }
}
