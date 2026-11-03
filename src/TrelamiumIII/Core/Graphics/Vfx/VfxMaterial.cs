using System.Collections.Generic;
using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Vfx;

/// <summary>
///     Every value a generated shader takes, by the name it has in the shader. Set whole on the effect each time it draws, so
///     effects sharing nothing with each other can't inherit stale values, and a value the compiler stripped is simply skipped.
/// </summary>
internal sealed class VfxMaterial
{
    private readonly record struct Entry(string Name, int Size, Vector3 Value, Func<Asset<Texture2D>>? Texture);

    private readonly List<Entry> entries = [];

    public VfxMaterial Set(string name, float value)
    {
        entries.Add(new Entry(name, 1, new Vector3(value, 0f, 0f), null));
        return this;
    }

    public VfxMaterial Set(string name, Vector2 value)
    {
        entries.Add(new Entry(name, 2, new Vector3(value, 0f), null));
        return this;
    }

    public VfxMaterial Set(string name, Vector3 value)
    {
        entries.Add(new Entry(name, 3, value, null));
        return this;
    }

    public VfxMaterial Set(string name, Asset<Texture2D> texture) => Set(name, () => texture);

    /// <summary> Asked for each time it draws, so a field exported with this requests nothing until there's a screen to draw on. </summary>
    public VfxMaterial Set(string name, Func<Asset<Texture2D>> texture)
    {
        entries.Add(new Entry(name, 0, Vector3.Zero, texture));
        return this;
    }

    public void Apply(Effect effect)
    {
        foreach (Entry entry in entries)
        {
            if (effect.Parameters[entry.Name] is not { } parameter)
                continue;

            switch (entry.Size)
            {
                case 1:
                    parameter.SetValue(entry.Value.X);
                    break;
                case 2:
                    parameter.SetValue(new Vector2(entry.Value.X, entry.Value.Y));
                    break;
                case 3:
                    parameter.SetValue(entry.Value);
                    break;
                default:
                    // A sampler can't take a null texture, and the asset is null until it loads.
                    if (entry.Texture?.Invoke() is { Value: { } texture })
                        parameter.SetValue(texture);

                    break;
            }
        }
    }
}
