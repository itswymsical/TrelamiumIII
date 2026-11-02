using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools.VfxLab.Recipes;

/// <summary>
///     Rebuilds a <see cref="GasLook" /> as a stack of stages, which is how a recipe starts from a look that's already tuned.
///     The stack draws what <c>GasFbm.fx</c> draws; <c>VfxLabHarness.Parity</c> holds it to that, pixel by pixel.
/// </summary>
internal static class LookRecipe
{
    public static VfxRecipe From(string name, GasLook look)
    {
        var recipe = new VfxRecipe { Name = name, Surface = "Field" };

        recipe.Stages.Add(Stage("flow", "FlowAdvect").Set("Strength", look.FlowStrength));

        recipe.Stages.Add(Stage("noise", "NoiseLayer")
            .Set("Noise", LabFiles.TextureName(look.Noise().Name))
            .Set("Scale", look.NoiseScale)
            .Set("Strength", look.NoiseStrength)
            .Set("Drift", look.Drift.X, look.Drift.Y)
            .Set("Stretch", look.NoiseStretch.X, look.NoiseStretch.Y));

        recipe.Stages.Add(Stage("cut", "Threshold").Set("Level", look.Threshold));

        recipe.Stages.Add(Stage("shade", "DepthShade")
            .Set("Range", look.DepthRange)
            .Set("Bands", look.Bands));

        recipe.Stages.Add(Stage("palette", "Palette")
            .Set("Shadow", Rgb(look.Shadow))
            .Set("Body", Rgb(look.Body))
            .Set("Core", Rgb(look.Core))
            .Set("HotShadow", Rgb(look.HotShadow))
            .Set("HotBody", Rgb(look.HotBody))
            .Set("HotCore", Rgb(look.HotCore))
            .Set("TemperatureBlend", look.TemperatureBlend)
            .Set("BodyAlpha", look.BodyAlpha)
            .Set("AlphaFloor", look.BodyAlphaFloor));

        // A look with no floor of its own takes whatever its layer sets. Zero is what the shader reads if nothing does.
        recipe.Stages.Add(Stage("light", "LightFloor").Set("Floor", Rgb(look.WorldLight ?? Vector3.Zero)));

        recipe.Stages.Add(Stage("rim", "Rim")
            .Set("Outline", Rgb(look.Outline))
            .Set("OutlineAlpha", look.OutlineAlpha)
            .Set("HotOutline", Rgb(look.HotShadow))
            .Set("Highlight", Rgb(look.Highlight))
            .Set("HighlightAlpha", look.HighlightAlpha)
            .Set("HotHighlight", Rgb(look.HotCore)));

        return recipe;
    }

    private static RecipeStage Stage(string id, string type) => new() { Id = id, Type = type };

    private static float[] Rgb(Vector3 color) => [color.X, color.Y, color.Z];
}
