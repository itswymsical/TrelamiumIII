namespace TrelamiumIII.Core.Graphics.Primitives;

/// <summary>
///     A triangle strip between two rows of world-space points. Row one takes texture V 0, row two V 1.
/// </summary>
/// <remarks>Draws from user arrays, like <see cref="VertexTrail" />, so it owns no GPU resources.</remarks>
public sealed class VertexStrip
{
    private readonly VertexPositionColorTexture[] vertices;
    private readonly short[] indices;

    public VertexStrip(int pointCount)
    {
        PointCount = Math.Max(pointCount, 2);

        vertices = new VertexPositionColorTexture[PointCount * 2];
        indices = new short[(PointCount - 1) * 6];

        for (int i = 0; i < PointCount - 1; i++)
        {
            int k = i * 6;

            indices[k] = (short)i;
            indices[k + 1] = (short)(i + 1);
            indices[k + 2] = (short)(i + PointCount);

            indices[k + 3] = (short)(i + 1);
            indices[k + 4] = (short)(i + PointCount + 1);
            indices[k + 5] = (short)(i + PointCount);
        }
    }

    public int PointCount { get; }

    public void SetPoint(int index, Vector2 first, Vector2 second, float u, Color color)
    {
        vertices[index] = new VertexPositionColorTexture(new Vector3(first, 0f), color, new Vector2(u, 0f));
        vertices[index + PointCount] = new VertexPositionColorTexture(new Vector3(second, 0f), color, new Vector2(u, 1f));
    }

    /// <summary>Draws every pass of <paramref name="effect" />. The caller sets its transform and parameters.</summary>
    public void Render(Effect effect, BlendState blendState)
    {
        GraphicsDevice device = Main.graphics.GraphicsDevice;

        device.BlendState = blendState;
        device.RasterizerState = RasterizerState.CullNone;
        device.DepthStencilState = DepthStencilState.None;

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                vertices, 0, vertices.Length,
                indices, 0, indices.Length / 3);
        }
    }
}
