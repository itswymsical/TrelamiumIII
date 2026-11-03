using System.Collections.Generic;
using System.Reflection;
using ReLogic.Content;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Content.DevTools.VfxLab.Recipes;

/// <summary>
///     Reads a recipe off a field that's already in the game: its look, its physics and the residue it leaves. That's how a real field gets
///     onto the bench to be played with. Whatever it does in its own code, like the Highlands mist following the weather, isn't data and
///     doesn't come across.
/// </summary>
internal static class LayerRecipe
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary> Fields that lay their residue in their own code, where there's no property to read it from. </summary>
    private static readonly Dictionary<Type, Type> ResiduesInCode = new()
    {
        [typeof(MudField)] = typeof(MuddiedResidue),
        [typeof(GelField)] = typeof(SlimedResidue),
    };

    private static readonly PropertyInfo Look = typeof(FieldLayer).GetProperty("Look", Flags)!;
    private static readonly PropertyInfo Shader = typeof(FieldLayer).GetProperty("Shader", Flags)!;
    private static readonly PropertyInfo Kind = typeof(GasField).GetProperty("GasType", Flags)!;
    private static readonly PropertyInfo Threshold = typeof(GasField).GetProperty("TileFieldThreshold", Flags)!;
    private static readonly PropertyInfo Interval = typeof(GasField).GetProperty("TileFieldInterval", Flags)!;
    private static readonly PropertyInfo Residue = typeof(ChemicalGasField).GetProperty("Residue", Flags)!;

    /// <summary> Only a field drawn through <c>GasFbm</c> from a look can be rebuilt as stages. </summary>
    public static bool CanOpen(FieldLayer layer)
        => Shader.GetValue(layer) is Asset<Effect> shader && shader.Name == Assets.Shaders.GasFbm.Asset.Name && Look.GetValue(layer) is GasLook;

    public static VfxRecipe From(FieldLayer layer)
    {
        string name = layer.GetType().Name;
        VfxRecipe recipe = LookRecipe.From(name, (GasLook)Look.GetValue(layer)!);

        RecipePhysics physics = recipe.Physics;
        physics.Density = Read<float>(layer, "DefaultDensity");
        physics.Viscosity = Read<float>(layer, "DefaultViscosity");
        physics.Temperature = Read<float>(layer, "DefaultTemperature");
        physics.Buoyancy = Read<float>(layer, "BuoyancyForce");
        physics.Gravity = Read<float>(layer, "GravityScale");
        physics.Drag = Read<float>(layer, "Drag");
        physics.LifeDecay = Read<float>(layer, "LifeDecay");
        physics.EnclosedPenalty = Read<float>(layer, "EnclosedLifePenalty");
        physics.MaxNodes = Read<int>(layer, "BaseMaxNodes");
        physics.FlowReference = Read<float>(layer, "FlowReferenceSpeed");
        physics.StampStretch = Read<float>(layer, "StampStretch");

        Vector2 range = Read<Vector2>(layer, "TemperatureRange");
        physics.TemperatureMin = range.X;
        physics.TemperatureMax = range.Y;

        if (layer is not GasField gas)
            return recipe;

        physics.Kind = (int)Kind.GetValue(gas)! switch
        {
            0 => RecipePhysics.Liquid,
            2 => RecipePhysics.Plasma,
            _ => RecipePhysics.Gas,
        };

        string? residue = layer is ChemicalGasField && Residue.GetValue(layer) is IResidue declared
            ? declared.Name
            : ResiduesInCode.GetValueOrDefault(layer.GetType())?.Name;

        if (residue is not null)
        {
            recipe.Links.Deposit = new RecipeDeposit
            {
                Residue = residue,
                Threshold = (float)Threshold.GetValue(gas)!,
                Interval = (int)Interval.GetValue(gas)!,
            };
        }

        return recipe;
    }

    private static T Read<T>(FieldLayer layer, string property)
        => (T)typeof(FieldLayer).GetProperty(property, Flags)!.GetValue(layer)!;
}
