using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;

namespace TrelamiumIII.Content.DevTools.VfxLab.Export;

/// <summary> A module as an export sees it: where it lives, and each of its settings with the default it has today. </summary>
internal sealed record ModuleSchema(string Namespace, IReadOnlyList<(string Name, float Default)> Settings);

/// <summary> What an export has to know about the mod it's going into. Handed in, so exporting is text in and text out and runs without the game. </summary>
internal sealed class ExportCatalog
{
    public IReadOnlyDictionary<string, ModuleSchema> Modules { get; init; } = new Dictionary<string, ModuleSchema>();

    /// <summary> Every tile condition, by the name recipes use for it. </summary>
    public IReadOnlyDictionary<string, (string Namespace, string Type)> Conditions { get; init; } = new Dictionary<string, (string, string)>();

    /// <summary> Classes the mod already has. A field exported earlier isn't one of these, or it could never be exported again. </summary>
    public IReadOnlySet<string> TakenNames { get; init; } = new HashSet<string>();

    public Func<string, bool> HasTexture { get; init; } = static _ => true;
}

/// <summary>
///     Writes a recipe as a real field: every value as a literal in a class the game finds like any other. It's the same
///     <c>ModularField</c> a lab draft is, so what was previewed is what ships.
/// </summary>
internal static class FieldExporter
{
    public const string DefaultFolder = "Content/Vfx";

    private const string ModAssets = "TrelamiumIII/Assets/";

    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary> Names the generated class reads, which a class of that name would hide. <c>Vfx</c> is the shaders' folder, and a nested type can't share its parent's name. </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "Asset", "Assets", "Effect", "Texture2D", "Vector2", "Vector3", "ModContent", "Shaders", "Textures", "Vfx" };

    /// <summary> A recipe's name only has to be letters and digits, which still allows these. </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary> What's wrong with exporting this. Errors stop it. Warnings say where it won't be what the recipe asks for, which is also where the preview isn't. </summary>
    public static IEnumerable<GenerationProblem> Check(VfxRecipe recipe, GeneratedShader shader, ExportCatalog catalog)
    {
        string name = recipe.Name;

        if (Keywords.Contains(name))
            yield return new GenerationProblem(name, "that's a C# keyword, so it can't name a class. Rename this recipe.", true);
        else if (Reserved.Contains(name) || catalog.TakenNames.Contains(name))
            yield return new GenerationProblem(name, "the mod already has a class with that name. Rename this recipe.", true);

        foreach (UniformValue uniform in shader.Uniforms)
        {
            if (uniform.Kind == ParamKind.Noise && (uniform.Texture is not { } texture || !catalog.HasTexture(RecipeFile.TextureKey(texture))))
                yield return new GenerationProblem(uniform.Name, $"there's no texture called '{uniform.Texture}'.", true);
        }

        foreach (RecipeModule module in recipe.Modules)
        {
            if (!catalog.Modules.TryGetValue(module.Type, out ModuleSchema? schema))
            {
                yield return new GenerationProblem(module.Type, "this build has no such behaviour, so it's left out, as it is in the preview.", false);
                continue;
            }

            foreach (string setting in module.Values.Keys.Where(key => schema.Settings.All(known => known.Name != key)))
                yield return new GenerationProblem(module.Type, $"it has no setting called '{setting}', so that's ignored.", false);
        }

        if (recipe.Links.Deposit is { } deposit && !catalog.Conditions.ContainsKey(deposit.Residue))
            yield return new GenerationProblem(deposit.Residue, "there's no tile condition with that name, so it leaves nothing, as it does in the preview.", false);
    }

    public static string ContentClass(VfxRecipe recipe, IReadOnlyList<UniformValue> uniforms, string namespaceName, ExportCatalog catalog)
    {
        SortedSet<string> usings = new(StringComparer.Ordinal) { "ReLogic.Content", "TrelamiumIII.Core.Gas", "TrelamiumIII.Core.Graphics.Vfx" };
        var spec = new StringBuilder();

        void Line(string text) => spec.Append("        ").Append(text).Append('\n');

        RecipePhysics physics = recipe.Physics;

        Line($"Kind = GasField.GasTypeID.{physics.Kind switch { RecipePhysics.Liquid => "Liquid", RecipePhysics.Plasma => "Plasma", _ => "Gas" }},");
        Line($"Density = {Number(physics.Density)},");
        Line($"Viscosity = {Number(physics.Viscosity)},");
        Line($"Temperature = {Number(physics.Temperature)},");
        Line($"Buoyancy = {Number(physics.Buoyancy)},");
        Line($"Gravity = {Number(physics.Gravity)},");
        Line($"Drag = {Number(physics.Drag)},");
        Line($"LifeDecay = {Number(physics.LifeDecay)},");
        Line($"EnclosedPenalty = {Number(physics.EnclosedPenalty)},");
        Line($"MaxNodes = {physics.MaxNodes.ToString(CultureInfo.InvariantCulture)},");
        Line($"FlowReference = {Number(physics.FlowReference)},");
        Line($"StampStretch = {Number(physics.StampStretch)},");
        Line($"TemperatureRange = {BenchExport.Literal([physics.TemperatureMin, physics.TemperatureMax])},");

        AppendMaterial(spec, uniforms);
        AppendModules(spec, recipe, catalog, usings);

        if (recipe.Links.Deposit is { } deposit && catalog.Conditions.TryGetValue(deposit.Residue, out (string Namespace, string Type) condition))
        {
            usings.Add(condition.Namespace);
            Line($"Residue = typeof({condition.Type}),");
            Line($"ResidueThreshold = {Number(deposit.Threshold)},");
            Line($"ResidueInterval = {deposit.Interval.ToString(CultureInfo.InvariantCulture)},");
        }

        var source = new StringBuilder();

        source.Append("// Generated by the VFX Lab from ").Append(ExportPlan.RecipePath(recipe.Name)).Append(". Re-export the recipe instead of editing this file.\n\n");

        foreach (string space in usings)
            source.Append("using ").Append(space).Append(";\n");

        source.Append("\nnamespace ").Append(namespaceName).Append(";\n\n");
        source.Append("internal sealed partial class ").Append(recipe.Name).Append(" : ModularField\n{\n");
        source.Append("    public override VfxFieldSpec Spec { get; } = new()\n    {\n");
        source.Append(spec);
        source.Append("    };\n\n");
        source.Append("    protected override Asset<Effect> Shader => Assets.Shaders.Vfx.").Append(recipe.Name).Append(".Asset;\n");
        source.Append("}\n");

        return source.ToString();
    }

    /// <summary> Written once and never again. It says how the lab released the field, as a recipe's emitter doesn't ship and it's the one tuned thing that would otherwise be lost. </summary>
    public static string HandWrittenHalf(VfxRecipe recipe, string namespaceName)
    {
        RecipeEmitter emitter = recipe.Emitter;
        string temperature = emitter.Temperature >= 0f ? $", temperature: {Number(emitter.Temperature)}" : string.Empty;

        return $$"""
            namespace {{namespaceName}};

            /// <summary>
            ///     The hand-written half of this field. The VFX Lab writes this file once and never touches it again, so anything the lab can't
            ///     express goes here: overrides, what it does to the world, drawing on top.
            /// </summary>
            /// <remarks>
            ///     The lab released it like this:
            ///     <code>
            ///     float angle = MathHelper.ToRadians({{Number(emitter.Angle)}} + Main.rand.NextFloat(-{{Number(emitter.Spread)}}, {{Number(emitter.Spread)}}));
            ///     FieldSystem.GetLayer&lt;{{recipe.Name}}&gt;()?.SpawnNode(position, angle.ToRotationVector2() * {{Number(emitter.Speed)}}, {{Number(emitter.Radius)}}, {{Number(emitter.Life)}}{{temperature}});
            ///     </code>
            /// </remarks>
            internal sealed partial class {{recipe.Name}}
            {
            }

            """.Replace("\r\n", "\n");
    }

    private static void AppendMaterial(StringBuilder spec, IReadOnlyList<UniformValue> uniforms)
    {
        spec.Append("        Material = new VfxMaterial()");

        foreach (UniformValue uniform in uniforms)
        {
            string value = uniform.Kind == ParamKind.Noise ? TextureLambda(uniform.Texture!) : BenchExport.Literal(uniform.Values);

            spec.Append("\n            .Set(\"").Append(uniform.Name).Append("\", ").Append(value).Append(')');
        }

        spec.Append(",\n");
    }

    private static void AppendModules(StringBuilder spec, VfxRecipe recipe, ExportCatalog catalog, SortedSet<string> usings)
    {
        List<string> modules = [];

        foreach (RecipeModule module in recipe.Modules)
        {
            if (!catalog.Modules.TryGetValue(module.Type, out ModuleSchema? schema))
                continue;

            usings.Add(schema.Namespace);

            // All of them, not just what was changed: like a flattened shader, this stays what was tuned if a default moves later.
            IEnumerable<string> settings = schema.Settings.Select(setting => $"{setting.Name} = {Number(module.Get(setting.Name, setting.Default))}");

            modules.Add($"new {module.Type} {{ {string.Join(", ", settings)} }}");
        }

        if (modules.Count == 0)
            return;

        spec.Append("        Modules =\n        [\n");

        foreach (string module in modules)
            spec.Append("            ").Append(module).Append(",\n");

        spec.Append("        ],\n");
    }

    /// <summary> Through the generated asset references where it can, so a renamed texture is a compile error and not a missing one at run time. </summary>
    private static string TextureLambda(string texture)
    {
        string key = RecipeFile.TextureKey(texture);

        if (key.StartsWith(ModAssets, StringComparison.Ordinal))
        {
            string[] parts = key[ModAssets.Length..].Split('/');

            if (parts.All(static part => Identifier.IsMatch(part)))
                return $"static () => Assets.{string.Join('.', parts)}.Asset";
        }

        return $"static () => ModContent.Request<Texture2D>(\"{key}\")";
    }

    private static string Number(float value) => BenchExport.Literal([value]);
}
