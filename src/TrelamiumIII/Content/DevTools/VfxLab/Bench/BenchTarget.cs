using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues;

namespace TrelamiumIII.Content.DevTools.VfxLab.Bench;

/// <summary> A value the Bench has taken over, or what a uniform held before it did. </summary>
internal readonly record struct UniformState(float[]? Numbers, Texture2D? Texture, string? TextureName);

/// <summary>
///     Something on the Bench: a shader, and whichever draws of it the values apply to. Many layers share one effect, so a layer's
///     values can't simply be left on the effect; they're applied at the moment that layer draws, and taken back after.
/// </summary>
internal abstract class BenchTarget
{
    /// <summary> Taken over, by uniform name. </summary>
    public Dictionary<string, UniformState> Overrides { get; } = new(StringComparer.Ordinal);

    /// <summary> What each uniform held before the Bench touched it. </summary>
    public Dictionary<string, UniformState> Authored { get; } = new(StringComparer.Ordinal);

    public abstract string Name { get; }

    public abstract BenchGroup Group { get; }

    /// <summary> The effect as it is right now. Null until it loads, and a hot reload replaces it. </summary>
    public abstract Effect? Effect { get; }

    /// <summary> What has to be mid-draw for these values to apply. Null applies them to every draw of the effect. </summary>
    public virtual object? Scope => null;

    /// <summary> Whether a test cloud or patch can be made to see it. </summary>
    public virtual string? TestLabel => null;

    public virtual string? ClearTip => null;

    public virtual void Test() { }

    public virtual void ClearTest() { }

    /// <summary> Whether it's drawing at all, beyond whether its effect was used. </summary>
    public virtual bool HasContent => true;

    /// <summary> Puts this target's own values on the effect, so they can be read with nothing on screen. </summary>
    public virtual void Seed(Effect effect) { }

    /// <summary> The retuned values as C# to paste where they came from. </summary>
    public string CopyAsCSharp()
    {
        IEnumerable<ExportValue> values = Overrides
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new ExportValue(pair.Key, pair.Value.Numbers, pair.Value.TextureName));

        return Export(values);
    }

    protected virtual string Export(IEnumerable<ExportValue> values) => BenchExport.Parameters(values);

    public void Capture(Effect effect)
    {
        foreach (EffectParameter parameter in effect.Parameters)
        {
            if (Read(parameter) is { } state)
                Authored[parameter.Name] = state;
        }
    }

    /// <summary> Puts the overrides on the effect, keeping what they replace for <see cref="Restore" />. </summary>
    public void Apply(Effect effect)
    {
        foreach ((string name, UniformState state) in Overrides)
        {
            if (effect.Parameters[name] is { } parameter)
                Write(parameter, state);
        }
    }

    public void Restore(Effect effect)
    {
        foreach (string name in Overrides.Keys)
        {
            if (effect.Parameters[name] is { } parameter && Authored.TryGetValue(name, out UniformState state))
                Write(parameter, state);
        }
    }

    /// <summary> The uniform's value, or null for a type the Bench can't edit. </summary>
    public static UniformState? Read(EffectParameter parameter)
    {
        if (parameter.ParameterType is EffectParameterType.Texture or EffectParameterType.Texture2D)
            return new UniformState(null, parameter.GetValueTexture2D(), null);

        if (parameter.ParameterType != EffectParameterType.Single || parameter.RowCount > 1 || parameter.Elements is { Count: > 0 })
            return null;

        return parameter.ColumnCount switch
        {
            1 => new UniformState([parameter.GetValueSingle()], null, null),
            2 => new UniformState(ToArray(parameter.GetValueVector2()), null, null),
            3 => new UniformState(ToArray(parameter.GetValueVector3()), null, null),
            4 => new UniformState(ToArray(parameter.GetValueVector4()), null, null),
            _ => null,
        };
    }

    private static void Write(EffectParameter parameter, in UniformState state)
    {
        if (state.Numbers is not { } numbers)
        {
            if (state.Texture is { IsDisposed: false } texture)
                parameter.SetValue(texture);

            return;
        }

        switch (numbers.Length)
        {
            case 1:
                parameter.SetValue(numbers[0]);
                break;
            case 2:
                parameter.SetValue(new Vector2(numbers[0], numbers[1]));
                break;
            case 3:
                parameter.SetValue(new Vector3(numbers[0], numbers[1], numbers[2]));
                break;
            case 4:
                parameter.SetValue(new Vector4(numbers[0], numbers[1], numbers[2], numbers[3]));
                break;
        }
    }

    private static float[] ToArray(Vector2 value) => [value.X, value.Y];

    private static float[] ToArray(Vector3 value) => [value.X, value.Y, value.Z];

    private static float[] ToArray(Vector4 value) => [value.X, value.Y, value.Z, value.W];
}

internal enum BenchGroup
{
    Fields,
    Tiles,
    Shaders,
}

/// <summary> One <see cref="FieldLayer" />, however many others share its effect. </summary>
internal sealed class FieldTarget(FieldLayer layer) : BenchTarget
{
    private const int TestColumns = 8;
    private const int TestRows = 4;
    private const float TestSpacing = 56f;
    private const float TestRadius = 62f;

    /// <summary> So much that it never visibly fades. A cloud thinning out while it's being tuned would read as the tuning. </summary>
    private const float TestLife = 1e7f;

    private static readonly PropertyInfo ShaderProperty = typeof(FieldLayer).GetProperty("Shader", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo LookProperty = typeof(FieldLayer).GetProperty("Look", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary> Held, as this is read for every draw of every effect while the lab is open. </summary>
    private readonly Asset<Effect> shader = (Asset<Effect>)ShaderProperty.GetValue(layer)!;

    public FieldLayer Layer => layer;

    public override string Name => layer.GetType().Name;

    public override BenchGroup Group => BenchGroup.Fields;

    public override Effect? Effect => shader.Value;

    public override object Scope => layer;

    public override bool HasContent => layer.NodeCount > 0;

    public override string TestLabel => "Test cloud";

    public override string ClearTip => "Remove every node of this field";

    public override void Test()
    {
        Vector2 center = Main.LocalPlayer.Center + new Vector2(0f, -180f);

        for (int column = 0; column < TestColumns; column++)
        {
            for (int row = 0; row < TestRows; row++)
            {
                Vector2 position = center + new Vector2((column - TestColumns / 2f) * TestSpacing, (row - TestRows / 2f) * TestSpacing);

                layer.SpawnNode(position, new Vector2(MathF.Sin(column), MathF.Cos(row)), TestRadius, TestLife, pinned: true);
            }
        }
    }

    public override void ClearTest() => layer.ClearNodes();

    public override void Seed(Effect effect)
    {
        FieldLayer.ApplyScreenParameters(effect, new Vector2(2f));
        (LookProperty.GetValue(layer) as GasLook)?.Apply(effect);
    }

    protected override string Export(IEnumerable<ExportValue> values)
        => LookProperty.GetValue(layer) is GasLook ? BenchExport.GasLook(values) : BenchExport.Parameters(values);
}

/// <summary> One residue with a canvas, however many others share its effect. </summary>
internal sealed class ResidueTarget(IResidue residue) : BenchTarget
{
    private const float TestRadius = 96f;

    public IResidue Residue => residue;

    public override string Name => residue.Name;

    public override BenchGroup Group => BenchGroup.Tiles;

    public override Effect? Effect => residue.Shader?.Value;

    public override object Scope => residue;

    public override string TestLabel => "Test patch";

    public override string ClearTip => "Remove every residue in the world";

    public override void Test() => ResidueSystem.ApplyCircle(residue, Main.LocalPlayer.Bottom + new Vector2(0f, 8f), TestRadius, duration: 60f * 60f);

    public override void ClearTest() => ResidueSystem.ClearAll();

    public override void Seed(Effect effect) => residue.ApplyShaderParameters(effect);

    protected override string Export(IEnumerable<ExportValue> values)
        => residue is FilmResidue ? BenchExport.FilmLook(values) : BenchExport.Parameters(values);
}

/// <summary> A shader by itself, for every draw of it anywhere. What reaches screen filters, trails and sprite shaders. </summary>
internal sealed class ShaderTarget(string name, Asset<Effect> asset, bool needsItsHost) : BenchTarget
{
    /// <summary> It has its own vertex shader, whose inputs a sprite can't supply, so it can't be drawn on the preview plate. </summary>
    public bool NeedsItsHost => needsItsHost;

    public override string Name => name;

    public override BenchGroup Group => BenchGroup.Shaders;

    public override Effect? Effect => asset.Value;
}
