using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.UI;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Tiles.Conditions;

namespace TrelamiumIII.Content.DevTools.VfxLab.Bench;

/// <summary>
///     Retunes any shader in the mod while it draws, with nothing in the shader or its host knowing. Every draw of every effect goes
///     through <see cref="EffectPass.Apply" />, so that's the one place values are swapped in. Nothing is hooked until the lab first opens.
/// </summary>
internal static class ShaderBench
{
    /// <summary> Draw frames an effect still counts as on screen after its last use. </summary>
    private const int LiveFrames = 30;

    private const string ShadersFolder = "Assets/Shaders/";
    private const string NoiseFolder = "Assets/Textures/Noise/";

    /// <summary> Uniforms the game sets every frame from the camera and the clock. Overriding them is rarely what's wanted, so they're folded away. </summary>
    private static readonly HashSet<string> FrameUniforms = new(StringComparer.Ordinal)
    {
        "Time", "uTime", "Aspect", "WorldOffset", "FieldTexel", "Zoom", "FieldGain",
        "CanvasScreenSize", "MaskOrigin", "MaskTexel", "CellSize", "uWorldViewProjection", "transformMatrix",
    };

    /// <summary> Textures the host binds itself: its own render targets, which a picker has no business replacing. </summary>
    private static readonly HashSet<string> FrameTextures = new(StringComparer.Ordinal) { "uAttrTex", "uMaskTex", "uCanvasLight" };

    private static readonly List<BenchTarget> targets = [];
    /// <summary> The frame each effect, layer and condition was last drawn on. </summary>
    private static readonly Dictionary<object, ulong> lastDrawn = [];

    private static readonly List<(string Name, Asset<Texture2D> Asset)> noise = [];

    private static bool installed;
    private static ulong frame;

    /// <summary> Whose draw is in progress. Set by the hosts' hooks, so a shared effect's draws can be told apart. </summary>
    private static object? scope;

    private static bool scopeIsOneShot;

    public static IReadOnlyList<BenchTarget> Targets => targets;

    public static IReadOnlyList<(string Name, Asset<Texture2D> Asset)> NoiseTextures => noise;

    public static BenchTarget? Selected { get; set; }

    /// <summary> Draws with everything authored, to compare against what's been changed. </summary>
    public static bool Bypass { get; set; }

    /// <summary> Finds what can be tuned and hooks the draws. Safe to call again. </summary>
    public static void Install(Mod mod)
    {
        if (installed)
            return;

        installed = true;

        foreach (FieldLayer layer in FieldSystem.Layers.Where(static layer => layer is not LabFieldSlot).OrderBy(static layer => layer.GetType().Name, StringComparer.Ordinal))
            targets.Add(new FieldTarget(layer));

        foreach (ITileCondition condition in TileConditionSystem.All.Where(static condition => condition.Shader is not null).OrderBy(static condition => condition.Name, StringComparer.Ordinal))
            targets.Add(new ConditionTarget(condition));

        List<string> files = mod.GetFileNames() ?? [];

        foreach (string path in files.Where(static path => path.StartsWith(ShadersFolder, StringComparison.Ordinal) && path.EndsWith(".fxc", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            string name = path[ShadersFolder.Length..^".fxc".Length];

            if (!name.Contains('/'))
                targets.Add(new ShaderTarget(name, ModContent.Request<Effect>($"{mod.Name}/{ShadersFolder}{name}"), HasVertexShader(mod.GetFileBytes(path))));
        }

        foreach (string path in files.Where(static path => path.StartsWith(NoiseFolder, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);

            noise.Add((name, ModContent.Request<Texture2D>($"{mod.Name}/{NoiseFolder}{name}")));
        }

        MonoModHooks.Add(typeof(EffectPass).GetMethod(nameof(EffectPass.Apply), BindingFlags.Instance | BindingFlags.Public)!, OnPassApply);
        MonoModHooks.Add(typeof(FieldLayer).GetMethod(nameof(FieldLayer.CompositeToScreen), BindingFlags.Instance | BindingFlags.Public)!, OnFieldComposite);
        MonoModHooks.Add(typeof(ConditionCanvasSource).GetMethod(nameof(ConditionCanvasSource.ApplyShaderParameters), BindingFlags.Instance | BindingFlags.Public)!, OnConditionParameters);
    }

    /// <summary> Whether a compiled effect holds vertex shader bytecode, which starts with its version token: vs_2_0 or vs_3_0. </summary>
    private static bool HasVertexShader(byte[] code)
    {
        ReadOnlySpan<byte> vs2 = [0x00, 0x02, 0xFE, 0xFF];
        ReadOnlySpan<byte> vs3 = [0x00, 0x03, 0xFE, 0xFF];

        return code.AsSpan().IndexOf(vs2) >= 0 || code.AsSpan().IndexOf(vs3) >= 0;
    }

    public static void Unload()
    {
        targets.Clear();
        lastDrawn.Clear();
        noise.Clear();
        Selected = null;
        scope = null;
        installed = false;
    }

    /// <summary> Call once per drawn frame. </summary>
    public static void NextFrame() => frame++;

    /// <summary> Whether the effect has been used in the last half second, which is what the live dot shows. </summary>
    public static bool IsDrawing(BenchTarget target)
        => target.HasContent && (target.Scope ?? target.Effect) is { } key && lastDrawn.TryGetValue(key, out ulong last) && frame - last <= LiveFrames;

    private static void OnFieldComposite(Action<FieldLayer> orig, FieldLayer self)
    {
        scope = self;

        if (self.NodeCount > 0)
            lastDrawn[self] = frame;

        orig(self);
        scope = null;
    }

    private static void OnConditionParameters(Action<ConditionCanvasSource, Effect> orig, ConditionCanvasSource self, Effect effect)
    {
        orig(self, effect);

        // Its canvas applies the pass straight after this, and that one pass is all this scope is for.
        scope = self.Condition;
        scopeIsOneShot = true;
        lastDrawn[self.Condition] = frame;
    }

    private static void OnPassApply(Action<EffectPass> orig, EffectPass self)
    {
        Effect effect = self.parentEffect;

        lastDrawn[effect] = frame;
        ApplyOverrides(orig, self, effect);

        if (scopeIsOneShot)
        {
            scope = null;
            scopeIsOneShot = false;
        }
    }

    private static void ApplyOverrides(Action<EffectPass> orig, EffectPass self, Effect effect)
    {
        BenchTarget? wide = null;
        BenchTarget? scoped = null;

        foreach (BenchTarget target in targets)
        {
            if ((target.Overrides.Count == 0 && target != Selected) || target.Effect != effect)
                continue;

            if (target.Scope is null)
                wide = target;
            else if (ReferenceEquals(target.Scope, scope))
                scoped = target;
        }

        // Snapshotted first, so what's shown as the authored value is what the host set this draw and not an override left behind.
        wide?.Capture(effect);
        scoped?.Capture(effect);

        if (!Bypass)
        {
            wide?.Apply(effect);
            scoped?.Apply(effect);
        }

        orig(self);

        if (!Bypass)
        {
            scoped?.Restore(effect);
            wide?.Restore(effect);
        }
    }

    /// <summary> The inspector rows for whatever uniforms the target's effect has. </summary>
    public static List<InspectorField> BuildFields(BenchTarget target, Effect effect)
    {
        target.Seed(effect);
        target.Capture(effect);

        List<InspectorField> look = [];
        List<InspectorField> frameRows = [];

        foreach (EffectParameter parameter in effect.Parameters)
        {
            string name = parameter.Name;

            if (FrameTextures.Contains(name) || BenchTarget.Read(parameter) is not { } state)
                continue;

            (FrameUniforms.Contains(name) ? frameRows : look).Add(state.Numbers is { } numbers ? NumberField(target, name, numbers.Length) : TextureField(target, name));
        }

        // Numbers, then colours, then textures, and otherwise the order the shader declares them, which usually has some sense to it.
        List<InspectorField> fields = [new InspectorField { Id = "look", Label = "Look", Kind = FieldKind.Header }, .. look.OrderBy(static field => field.Kind)];

        if (frameRows.Count > 0)
        {
            fields.Add(new InspectorField { Id = "frame", Label = "Set by the game each frame", Kind = FieldKind.Header, Folded = true });
            fields.AddRange(frameRows);
        }

        return fields;
    }

    private static bool IsColorName(string name)
        => name.Contains("Color", StringComparison.OrdinalIgnoreCase) || name.Contains("Colour", StringComparison.OrdinalIgnoreCase) || name.Contains("Tint", StringComparison.OrdinalIgnoreCase) || name.Contains("Light", StringComparison.Ordinal);

    private static InspectorField NumberField(BenchTarget target, string name, int count)
    {
        float[] Current() => (float[])((target.Overrides.TryGetValue(name, out UniformState state) ? state.Numbers : target.Authored.GetValueOrDefault(name).Numbers) ?? new float[count]).Clone();

        return new InspectorField
        {
            Id = $"{target.Name}/{name}",
            Label = name,
            Kind = count == 3 && IsColorName(name) ? FieldKind.Color : FieldKind.Numbers,
            Tip = $"{name}: drag, or click to type. Shift is finer, Ctrl coarser. Right-click resets.",
            Get = Current,
            Set = values => target.Overrides[name] = new UniformState(values, null, null),
            IsChanged = () => target.Overrides.ContainsKey(name),
            Reset = () => target.Overrides.Remove(name),
        };
    }

    private static InspectorField TextureField(BenchTarget target, string name)
    {
        string? NameOf(Texture2D? texture)
        {
            foreach ((string candidate, Asset<Texture2D> asset) in noise)
            {
                if (asset.IsLoaded && ReferenceEquals(asset.Value, texture))
                    return candidate;
            }

            return null;
        }

        return new InspectorField
        {
            Id = $"{target.Name}/{name}",
            Label = name,
            Kind = FieldKind.Texture,
            Tip = $"{name}: click to choose a noise texture. Right-click resets.",
            GetTexture = () => target.Overrides.TryGetValue(name, out UniformState state) ? state.TextureName : NameOf(target.Authored.GetValueOrDefault(name).Texture),
            SetTexture = chosen =>
            {
                foreach ((string candidate, Asset<Texture2D> asset) in noise)
                {
                    if (candidate == chosen)
                        target.Overrides[name] = new UniformState(null, asset.Value, chosen);
                }
            },
            IsChanged = () => target.Overrides.ContainsKey(name),
            Reset = () => target.Overrides.Remove(name),
        };
    }
}
