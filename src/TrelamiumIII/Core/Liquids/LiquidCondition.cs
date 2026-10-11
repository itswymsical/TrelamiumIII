namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     One state a liquid's tiles can be in, stored per tile as its <see cref="Index" />.
///     Look and strength are data; the owning <see cref="LiquidConditionSet" /> decides what contact does with them.
/// </summary>
public class LiquidCondition(string name)
{
    /// <summary>Save key, unique within its liquid.</summary>
    public string Name => name;

    /// <summary>The per-tile byte. 0 is the liquid's default condition.</summary>
    public byte Index { get; internal set; }

    public LiquidConditionSet Liquid { get; internal set; } = null!;

    /// <summary>Multiplies the grayscale liquid texture, its slopes, falls and map colour.</summary>
    public virtual Color Tint { get; init; } = Color.White;

    public Vector3 Light { get; init; }

    /// <summary>Scales the liquid's contact effects, 0 to 1.</summary>
    public float Strength { get; init; } = 1f;

    public virtual string DisplayName => Liquid.ConditionDisplayName(this);

    public override string ToString() => $"{Liquid?.Name}/{Name}";
}
