namespace TrelamiumIII.Core.Rarities.Text;

/// <summary>Offset copies of the text drawn under the body. <see cref="Vanilla" /> is the tooltip shadow: black, 2px, four directions.</summary>
public sealed record TextOutline
{
    /// <summary>Most offsets one outline can produce: 16 directions times 4 rings.</summary>
    internal const int MAX_OFFSETS = 64;

    private static readonly Vector2[] Cardinal = [-Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY];
    private static readonly Vector2[] Diagonal = [new(-1f, -1f), new(1f, -1f), new(-1f, 1f), new(1f, 1f)];

    /// <summary>Multiplied by the text's alpha.</summary>
    public Color Color { get; init; } = Color.Black;

    /// <summary>Distance of the outer ring in the batch's pixels, not multiplied by text scale.</summary>
    public float Spread { get; init; } = 2f;

    /// <summary>Copies per ring, 1..16. 4 is vanilla's cardinal set, 8 adds pixel-aligned diagonals, other counts space evenly.</summary>
    public int Directions { get; init; } = 4;

    /// <summary>Rings evenly spaced out to <see cref="Spread" />, 1..4. Above 1 fills the gaps a wide spread leaves.</summary>
    public int Rings { get; init; } = 1;

    public static TextOutline Vanilla { get; } = new();

    /// <summary>Writes every copy's offset into <paramref name="destination" /> and returns the count.</summary>
    internal int WriteOffsets(Span<Vector2> destination)
    {
        int directions = Math.Clamp(Directions, 1, 16);
        int rings = Math.Clamp(Rings, 1, 4);
        int count = 0;

        for (int ring = 1; ring <= rings; ring++)
        {
            float distance = Spread * ring / rings;

            for (int i = 0; i < directions; i++)
                destination[count++] = Direction(i, directions) * distance;
        }

        return count;
    }

    private static Vector2 Direction(int index, int count) => count switch
    {
        4 => Cardinal[index],
        8 => index < 4 ? Cardinal[index] : Diagonal[index - 4],
        _ => (index / (float)count * MathHelper.TwoPi).ToRotationVector2(),
    };
}

/// <summary>How text is layered: an optional outline pass under the body pass, each with its own blend.</summary>
public sealed record TextStyle
{
    /// <summary>Null draws no outline.</summary>
    public TextOutline? Outline { get; init; } = TextOutline.Vanilla;

    /// <summary>Blend for both passes, unless <see cref="OutlineBlend" /> or <see cref="BodyBlend" /> overrides one.</summary>
    public BlendState Blend { get; init; } = BlendState.AlphaBlend;

    public BlendState? OutlineBlend { get; init; }

    public BlendState? BodyBlend { get; init; }

    public static TextStyle Vanilla { get; } = new();

    internal BlendState OutlinePassBlend => OutlineBlend ?? Blend;

    internal BlendState BodyPassBlend => BodyBlend ?? Blend;

    /// <summary>True when this draws exactly like a vanilla tooltip line.</summary>
    internal bool IsVanilla => Equals(Vanilla);
}
