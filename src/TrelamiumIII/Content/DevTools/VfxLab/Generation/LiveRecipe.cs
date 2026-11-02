using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ReLogic.Content;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Core.Graphics.Vfx;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary> Where the lab keeps its files, and how the library gets out of the mod. </summary>
internal static class LabFiles
{
    private const string ShadersFolder = "Assets/Shaders/";

    /// <summary> Noise textures are named in recipes by file. Any other texture is named by its full key. </summary>
    private const string NoiseFolder = "TrelamiumIII/Assets/Textures/Noise/";

    public static string ShaderCache => Path.Combine(Main.SavePath, "TrelamiumIII", "VfxLab", "Cache");

    /// <summary> In the source tree where there is one, so saved recipes are part of the project. </summary>
    public static string RecipeFolder => DeveloperConfig.SourceRoot() is { } source
        ? Path.Combine(source, "Assets", "Vfx", "Recipes")
        : Path.Combine(Main.SavePath, "TrelamiumIII", "VfxLab", "Recipes");

    public static string AutosaveFolder => Path.Combine(Main.SavePath, "TrelamiumIII", "VfxLab", "Autosave");

    /// <summary> The stage files in the source tree where there is one, so edits to them count, and otherwise what the mod was built with. </summary>
    public static StageLibrary LoadLibrary(Mod mod)
    {
        if (DeveloperConfig.SourceRoot() is { } source && Directory.Exists(Path.Combine(source, ShadersFolder, StageLibrary.Folder)))
            return StageLibrary.FromFolder(Path.Combine(source, ShadersFolder));

        return LoadBuiltLibrary(mod);
    }

    /// <summary> Reads the library out of the mod, as built. </summary>
    public static StageLibrary LoadBuiltLibrary(Mod mod) => StageLibrary.From(mod.GetFileNames()
        .Where(static path => path.StartsWith(ShadersFolder + StageLibrary.Folder + "/", StringComparison.Ordinal))
        .Select(path => (path[ShadersFolder.Length..], Encoding.UTF8.GetString(mod.GetFileBytes(path)))));

    public static string TextureKey(string texture) => texture.Contains('/') ? texture : NoiseFolder + texture;

    /// <summary> The name a recipe uses for a texture asset, or its whole key if it's not a noise texture. </summary>
    public static string TextureName(string key)
    {
        string normalized = key.Replace('\\', '/');
        int folder = normalized.IndexOf("Textures/Noise/", StringComparison.Ordinal);

        return folder >= 0 ? normalized[(folder + "Textures/Noise/".Length)..] : normalized;
    }
}

/// <summary>
///     A recipe kept on screen as it's edited: regenerates its shader, recompiles only when the source changed, and swaps the
///     result in. Values that aren't structural change at once. A shader that won't build leaves the last good one running.
/// </summary>
internal sealed class LiveRecipe
{
    private readonly ShaderBuildQueue queue;

    public LiveRecipe(LiveEffect effect, StageLibrary library)
    {
        Library = library;
        queue = new ShaderBuildQueue(source => ShaderCompiler.CompileAsync(source, LabFiles.ShaderCache), effect.Swap);
    }

    /// <summary> Replaced when a stage file changes on disk; the next <see cref="Set" /> builds from the new one. </summary>
    public StageLibrary Library { get; set; }

    /// <summary> Which way into the shader to look. Takes effect on the next <see cref="Set" />. </summary>
    public ShaderView View { get; set; }

    public VfxMaterial Material { get; private set; } = new();

    public IReadOnlyList<GenerationProblem> Problems { get; private set; } = [];

    public int SamplerCount { get; private set; }

    public ShaderBuildStatus Status => queue.Status;

    public string Log => queue.Log;

    public void Set(VfxRecipe recipe)
    {
        GeneratedShader generated = ShaderGenerator.Generate(ShaderViews.Apply(recipe, View, Library), Library);

        Problems = generated.Problems;
        SamplerCount = generated.SamplerCount;

        if (generated.Source is not { } source)
            return;

        Material = BuildMaterial(generated);
        queue.Submit(source);
    }

    /// <summary> Call every tick. </summary>
    public void Update() => queue.Update();

    public static VfxMaterial BuildMaterial(GeneratedShader generated)
    {
        var material = new VfxMaterial();

        foreach (UniformValue uniform in generated.Uniforms)
        {
            switch (uniform.Kind)
            {
                case ParamKind.Float:
                    material.Set(uniform.Name, uniform.Values[0]);
                    break;
                case ParamKind.Float2:
                    material.Set(uniform.Name, new Vector2(uniform.Values[0], uniform.Values[1]));
                    break;
                case ParamKind.Color:
                    material.Set(uniform.Name, new Vector3(uniform.Values[0], uniform.Values[1], uniform.Values[2]));
                    break;
                case ParamKind.Noise when uniform.Texture is { } texture:
                    // Off the main thread nothing here runs, and a lab that has to wait a frame for a texture would show it popping in.
                    material.Set(uniform.Name, ModContent.Request<Texture2D>(LabFiles.TextureKey(texture), AssetRequestMode.ImmediateLoad));
                    break;
            }
        }

        return material;
    }
}
