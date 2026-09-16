using System.Runtime.InteropServices;

namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     One corner of a node's stamp quad, carrying both halves of that node's payload.
/// </summary>
/// <remarks>
///     <para>
///         The reason the stamp pass stopped going through <see cref="SpriteBatch" />. A sprite
///         vertex has exactly one colour, which is four channels, and coverage and brightness
///         already claimed two of them -- so anything else a layer wanted the shader to know had no
///         way through. A second colour doubles the budget, and because both channels ride the same
///         vertex they are guaranteed to reach the two targets under identical weight, which is
///         what makes dividing one by the other a valid recovery.
///     </para>
///     <para>
///         Eight bits per channel is enough for every quantity here. They are all 0..1 fractions
///         read back above the shader's threshold, where coverage is solid and the division that
///         recovers them is nowhere near the fringe that would amplify a quantisation step.
///     </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct FieldStampVertex : IVertexType
{
    /// <summary>Screen position, in the same pixels the old sprite path drew into.</summary>
    public Vector3 Position;

    /// <summary>Coverage, and the channels that ride the density target beside it.</summary>
    public Color Field;

    /// <summary>The second target's channels, under the same coverage weight.</summary>
    public Color Attributes;

    public Vector2 TextureCoordinate;

    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 1),
        new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
