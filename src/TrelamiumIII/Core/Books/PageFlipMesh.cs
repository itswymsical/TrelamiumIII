using System.Collections.Generic;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     Draws a turning leaf as a deformed grid, textured front and back. Needs no sprite batch open and the target bound; triangles
///     are split by whether they've swung past a right angle, so both faces texture differently without a depth buffer.
/// </summary>
public sealed class PageFlipMesh : IDisposable
{
    private const int COLS = 64;
    private const int ROWS = 48;
    private const int VERT_COUNT = (COLS + 1) * (ROWS + 1);

    /// <summary>How far the top edge of the page runs ahead of the bottom edge, as a fraction.</summary>
    private const float LEAD = 0.3f;

    /// <summary>Darkening at the steepest part of the curl.</summary>
    private const float SHADE = 0.42f;

    private static readonly Vector3 ParchmentBack = new(200f / 255f, 190f / 255f, 165f / 255f);

    private readonly VertexPositionColorTexture[] frontVertices = new VertexPositionColorTexture[VERT_COUNT];
    private readonly VertexPositionColorTexture[] backVertices = new VertexPositionColorTexture[VERT_COUNT];
    private readonly List<short> frontIndices = new(COLS * ROWS * 6);
    private readonly List<short> backIndices = new(COLS * ROWS * 6);

    private BasicEffect? effect;
    private bool disposed;

    public void Load() =>
        Main.QueueMainThreadAction(() =>
        {
            effect = new BasicEffect(Main.graphics.GraphicsDevice)
            {
                LightingEnabled = false,
                TextureEnabled = true,
                VertexColorEnabled = true,
            };
        });

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        // Snapshot before clearing the field. The queued action runs later, by which point the field
        // is already null, so a lambda reading it would dispose nothing.
        BasicEffect? pending = effect;
        effect = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    /// <param name="back">What is printed on the reverse; null falls back to blank parchment.</param>
    /// <param name="spineX">Pivot, in the composite target's space.</param>
    /// <param name="progress">0 flat at the source, 1 flat at the destination.</param>
    /// <param name="backward">False turns right to left; true turns left to right.</param>
    public void Draw(
        GraphicsDevice device,
        Texture2D? front,
        Texture2D? back,
        float spineX,
        int pageWidth,
        int pageHeight,
        float progress,
        bool backward,
        Matrix projection)
    {
        if (progress <= 0f || disposed || effect is null)
            return;

        if (front is null && back is null)
            return;

        BuildVertices(spineX, pageWidth, pageHeight, progress, backward);
        BuildIndices(progress);

        effect.Projection = projection;
        effect.View = Matrix.Identity;
        effect.World = Matrix.Identity;

        BlendState previousBlend = device.BlendState;
        DepthStencilState previousDepth = device.DepthStencilState;
        RasterizerState previousRaster = device.RasterizerState;

        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        if (frontIndices.Count >= 3 && front is not null)
        {
            effect.Texture = front;
            effect.DiffuseColor = Vector3.One;
            DrawList(device, frontVertices, frontIndices);
        }

        if (backIndices.Count >= 3)
        {
            if (back is not null)
            {
                effect.Texture = back;
                effect.DiffuseColor = Vector3.One;
            }
            else
            {
                effect.Texture = TextureAssets.MagicPixel.Value;
                effect.DiffuseColor = ParchmentBack;
            }

            DrawList(device, backVertices, backIndices);
        }

        device.BlendState = previousBlend;
        device.DepthStencilState = previousDepth;
        device.RasterizerState = previousRaster;
    }

    private void BuildVertices(float spineX, int pageWidth, int pageHeight, float progress, bool backward)
    {
        float direction = backward ? -1f : 1f;

        for (int row = 0; row <= ROWS; row++)
        {
            float v = row / (float)ROWS;

            for (int col = 0; col <= COLS; col++)
            {
                int index = row * (COLS + 1) + col;
                float u = col / (float)COLS;

                float angle = CurlAngle(u, v, progress);
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);

                var position = new Vector3(spineX + direction * cos * u * pageWidth, v * pageHeight, 0f);

                // The steeper the leaf stands, the less light reaches it.
                float brightness = 1f - sin * SHADE;
                var tint = new Color(brightness, brightness, brightness, 1f);

                float frontU = backward ? 1f - u : u;

                frontVertices[index] = new VertexPositionColorTexture(position, tint, new Vector2(frontU, v));
                backVertices[index] = new VertexPositionColorTexture(position, tint, new Vector2(1f - frontU, v));
            }
        }
    }

    private void BuildIndices(float progress)
    {
        frontIndices.Clear();
        backIndices.Clear();

        for (int row = 0; row < ROWS; row++)
        {
            for (int col = 0; col < COLS; col++)
            {
                // Sorting on the quad's centre rather than a corner keeps the seam between the two
                // faces from zig-zagging along the fold.
                float angle = CurlAngle((col + 0.5f) / COLS, (row + 0.5f) / ROWS, progress);

                short topLeft = (short)(row * (COLS + 1) + col);
                short topRight = (short)(row * (COLS + 1) + col + 1);
                short bottomLeft = (short)((row + 1) * (COLS + 1) + col);
                short bottomRight = (short)((row + 1) * (COLS + 1) + col + 1);

                if (angle < MathHelper.PiOver2)
                {
                    frontIndices.Add(topLeft);
                    frontIndices.Add(topRight);
                    frontIndices.Add(bottomLeft);
                    frontIndices.Add(topRight);
                    frontIndices.Add(bottomRight);
                    frontIndices.Add(bottomLeft);
                }
                else
                {
                    // Reversed winding: this face is being seen from behind.
                    backIndices.Add(topLeft);
                    backIndices.Add(bottomLeft);
                    backIndices.Add(topRight);
                    backIndices.Add(topRight);
                    backIndices.Add(bottomLeft);
                    backIndices.Add(bottomRight);
                }
            }
        }
    }

    /// <summary>How far the leaf has swung at a point on it, in radians.</summary>
    private static float CurlAngle(float u, float v, float progress)
    {
        float lead = 1f - LEAD * (1f - v) * (1f - progress);
        float curve = MathHelper.Lerp((float)Math.Sqrt(u), 1f, progress);

        return progress * MathHelper.Pi * curve * lead;
    }

    private void DrawList(GraphicsDevice device, VertexPositionColorTexture[] vertices, List<short> indices)
    {
        short[] array = indices.ToArray();

        foreach (EffectPass pass in effect!.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, VERT_COUNT, array, 0, array.Length / 3);
        }
    }
}
