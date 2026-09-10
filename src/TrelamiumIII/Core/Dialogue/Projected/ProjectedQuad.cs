namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>Per-corner offsets, in panel-local pixels, that take a rectangle off true.</summary>
public readonly struct QuadWarp(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft)
{
    public readonly Vector2 TopLeft = topLeft;
    public readonly Vector2 TopRight = topRight;
    public readonly Vector2 BottomRight = bottomRight;
    public readonly Vector2 BottomLeft = bottomLeft;

    public static QuadWarp None => default;

    /// <summary>The same crooked rectangle every time for a given <paramref name="seed" />.</summary>
    public static QuadWarp Handmade(int seed, float amount) =>
        new(new Vector2(Hash(seed, 0), Hash(seed, 1)) * amount,
            new Vector2(Hash(seed, 2), Hash(seed, 3)) * amount,
            new Vector2(Hash(seed, 4), Hash(seed, 5)) * amount,
            new Vector2(Hash(seed, 6), Hash(seed, 7)) * amount);

    public static QuadWarp operator *(QuadWarp warp, float scale) =>
        new(warp.TopLeft * scale, warp.TopRight * scale, warp.BottomRight * scale, warp.BottomLeft * scale);

    private static float Hash(int seed, int index)
    {
        float value = MathF.Sin(seed * 12.9898f + index * 78.233f) * 43758.5453f;
        return (value - MathF.Floor(value)) * 2f - 1f;
    }
}

/// <summary>Where a quad sits in the projected scene.</summary>
public struct QuadPlacement()
{
    /// <summary>Centre on the z = 0 plane</summary>
    public Vector2 Center = Vector2.Zero;

    /// <summary>Size on the z = 0 plane</summary>
    public Vector2 Size = Vector2.Zero;

    /// <summary>Turn about the vertical.</summary>
    public float Yaw = 0f;

    /// <summary>Tilt about the horizontal.</summary>
    public float Pitch = 0f;

    /// <summary>Lean in the picture plane.</summary>
    public float Roll = 0f;

    /// <summary>Push away from the camera.</summary>
    public float Depth = 0f;

    public QuadWarp Warp = QuadWarp.None;

    /// <summary>How much darker the receding edge goes, 0 to 1.</summary>
    public float FarShade = 0f;
}

/// <summary>
///     The camera every projected panel is seen through.
/// </summary>
/// <remarks>
///     <para>
///         Set up so that one world unit on the z = 0 plane is exactly one screen pixel: the distance
///         is solved from the field of view rather than picked. The field of view is then the one dial on how hard the illusion reads.
///     </para>
/// </remarks>
public static class PanelCamera
{
    public static readonly float FieldOfView = MathHelper.ToRadians(78f);

    /// <summary>Camera distance that makes the z = 0 plane measure one unit per screen pixel.</summary>
    public static float Distance => Main.screenHeight / (2f * MathF.Tan(FieldOfView * 0.5f));

    /// <summary>
    ///     Looks down +Z from behind the plane, with up inverted.
    /// </summary>
    public static Matrix View =>
        Matrix.CreateLookAt(new Vector3(0f, 0f, -Distance), Vector3.Zero, new Vector3(0f, -1f, 0f));

    public static Matrix Projection
    {
        get
        {
            float distance = Distance;

            return Matrix.CreatePerspectiveFieldOfView(
                FieldOfView,
                Main.screenWidth / (float)Main.screenHeight,
                distance * 0.1f,
                distance * 4f);
        }
    }

    /// <summary>Panel space is centred on the screen</summary>
    public static Vector3 ToPanelSpace(Vector2 screenPoint, float depth) =>
        new(screenPoint.X - Main.screenWidth * 0.5f, screenPoint.Y - Main.screenHeight * 0.5f, depth);

    public static Matrix World(in QuadPlacement placement) =>
        Matrix.CreateRotationX(placement.Pitch)
        * Matrix.CreateRotationY(placement.Yaw)
        * Matrix.CreateRotationZ(placement.Roll)
        * Matrix.CreateTranslation(ToPanelSpace(placement.Center, placement.Depth));

    /// <summary>The quad's four corners in panel-local space: clockwise from the top left.</summary>
    public static void LocalCorners(in QuadPlacement placement, Span<Vector3> corners)
    {
        Vector2 half = placement.Size * 0.5f;
        QuadWarp warp = placement.Warp;

        corners[0] = new Vector3(-half.X + warp.TopLeft.X, -half.Y + warp.TopLeft.Y, 0f);
        corners[1] = new Vector3(half.X + warp.TopRight.X, -half.Y + warp.TopRight.Y, 0f);
        corners[2] = new Vector3(half.X + warp.BottomRight.X, half.Y + warp.BottomRight.Y, 0f);
        corners[3] = new Vector3(-half.X + warp.BottomLeft.X, half.Y + warp.BottomLeft.Y, 0f);
    }

    /// <summary>Where the quad's corners land on screen, for hit testing against the mouse.</summary>
    /// <returns>False when the quad is behind the camera and has no screen footprint.</returns>
    public static bool ScreenCorners(in QuadPlacement placement, Span<Vector2> screen)
    {
        Span<Vector3> local = stackalloc Vector3[4];
        LocalCorners(placement, local);

        Matrix transform = World(placement) * View * Projection;

        for (int i = 0; i < 4; i++)
        {
            Vector4 clip = Vector4.Transform(new Vector4(local[i], 1f), transform);

            if (clip.W <= 0.0001f)
                return false;

            // The perspective divide the GPU is about to do, repeated here so the mouse is tested
            // against the shape the player can see rather than the untilted rectangle behind it.
            screen[i] = new Vector2(
                (clip.X / clip.W * 0.5f + 0.5f) * Main.screenWidth,
                (0.5f - clip.Y / clip.W * 0.5f) * Main.screenHeight);
        }

        return true;
    }

    /// <param name="padding">Grows the quad before testing, in its own local pixels.</param>
    public static bool Contains(in QuadPlacement placement, Vector2 point, float padding = 0f)
    {
        if (placement.Size.X <= 0f || placement.Size.Y <= 0f)
            return false;

        QuadPlacement grown = placement;
        grown.Size += new Vector2(padding * 2f);

        Span<Vector2> screen = stackalloc Vector2[4];

        if (!ScreenCorners(grown, screen))
            return false;

        return InTriangle(point, screen[0], screen[1], screen[2])
            || InTriangle(point, screen[0], screen[2], screen[3]);
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Side(point, a, b);
        float d2 = Side(point, b, c);
        float d3 = Side(point, c, a);

        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;

        // Consistent sign on all three edges, whichever way the triangle happens to be wound.
        return !(negative && positive);
    }

    private static float Side(Vector2 point, Vector2 a, Vector2 b) =>
        (point.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (point.Y - b.Y);
}

/// <summary>
///     Draws panel surfaces as flat quads standing in the projected scene.
/// </summary>
public sealed class ProjectedQuad : IDisposable
{
    private const int COLUMNS = 12;
    private const int ROWS = 8;
    private const int VERTEX_COUNT = (COLUMNS + 1) * (ROWS + 1);

    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[VERTEX_COUNT];
    private readonly short[] indices = new short[COLUMNS * ROWS * 6];

    private BasicEffect? effect;
    private bool disposed;

    private BlendState? previousBlend;
    private DepthStencilState? previousDepth;
    private RasterizerState? previousRasterizer;
    private SamplerState? previousSampler;

    public ProjectedQuad() => BuildIndices();

    public void Load() =>
        Main.QueueMainThreadAction(() =>
        {
            if (disposed)
                return;

            effect = new BasicEffect(Main.graphics.GraphicsDevice)
            {
                LightingEnabled = false,
                TextureEnabled = true,
                VertexColorEnabled = true,
                World = Matrix.Identity,
            };
        });

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        // Snapshot before clearing the field
        BasicEffect? pending = effect;
        effect = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    public bool Ready => !disposed && effect is not null;

    public void Begin(GraphicsDevice device)
    {
        if (!Ready)
            return;

        previousBlend = device.BlendState;
        previousDepth = device.DepthStencilState;
        previousRasterizer = device.RasterizerState;
        previousSampler = device.SamplerStates[0];

        // Premultiplied, because that is what a sprite batch left in the surface.
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.PointClamp;

        effect!.View = PanelCamera.View;
        effect.Projection = PanelCamera.Projection;
    }

    public void End(GraphicsDevice device)
    {
        if (previousBlend is null)
            return;

        device.BlendState = previousBlend;
        device.DepthStencilState = previousDepth;
        device.RasterizerState = previousRasterizer;
        device.SamplerStates[0] = previousSampler;

        previousBlend = null;
    }

    /// <param name="source">Region of <paramref name="texture" /> the quad is cut from.</param>
    /// <param name="tint">Premultiplied so multiplying it through fades the quad as one object.</param>
    public void Draw(GraphicsDevice device, Texture2D texture, Rectangle source, in QuadPlacement placement, Color tint)
    {
        if (!Ready || placement.Size.X <= 0f || placement.Size.Y <= 0f)
            return;

        BuildVertices(texture, source, placement, tint);

        effect!.Texture = texture;

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList, vertices, 0, VERTEX_COUNT, indices, 0, indices.Length / 3);
        }
    }

    private void BuildVertices(Texture2D texture, Rectangle source, in QuadPlacement placement, Color tint)
    {
        Span<Vector3> corners = stackalloc Vector3[4];
        PanelCamera.LocalCorners(placement, corners);

        Matrix world = PanelCamera.World(placement);

        for (int i = 0; i < 4; i++)
            corners[i] = Vector3.Transform(corners[i], world);

        // Shading runs off the spread of depth across this quad rather than an absolute distance, so
        // a barely-turned panel still gets the full gradient and the cue does not fade out with the
        // angle it is meant to be reporting.
        float nearest = MathF.Min(MathF.Min(corners[0].Z, corners[1].Z), MathF.Min(corners[2].Z, corners[3].Z));
        float furthest = MathF.Max(MathF.Max(corners[0].Z, corners[1].Z), MathF.Max(corners[2].Z, corners[3].Z));
        float spread = MathF.Max(furthest - nearest, 0.0001f);

        Vector2 uvMin = new(source.X / (float)texture.Width, source.Y / (float)texture.Height);
        Vector2 uvMax = new(source.Right / (float)texture.Width, source.Bottom / (float)texture.Height);

        for (int row = 0; row <= ROWS; row++)
        {
            float v = row / (float)ROWS;

            for (int column = 0; column <= COLUMNS; column++)
            {
                float u = column / (float)COLUMNS;

                Vector3 top = Vector3.Lerp(corners[0], corners[1], u);
                Vector3 bottom = Vector3.Lerp(corners[3], corners[2], u);
                Vector3 position = Vector3.Lerp(top, bottom, v);

                float depth = (position.Z - nearest) / spread;
                float brightness = 1f - depth * placement.FarShade;

                Color shaded = new(
                    (byte)(tint.R * brightness),
                    (byte)(tint.G * brightness),
                    (byte)(tint.B * brightness),
                    tint.A);

                vertices[row * (COLUMNS + 1) + column] = new VertexPositionColorTexture(
                    position,
                    shaded,
                    new Vector2(MathHelper.Lerp(uvMin.X, uvMax.X, u), MathHelper.Lerp(uvMin.Y, uvMax.Y, v)));
            }
        }
    }

    private void BuildIndices()
    {
        int cursor = 0;

        for (int row = 0; row < ROWS; row++)
        {
            for (int column = 0; column < COLUMNS; column++)
            {
                short topLeft = (short)(row * (COLUMNS + 1) + column);
                short topRight = (short)(topLeft + 1);
                short bottomLeft = (short)((row + 1) * (COLUMNS + 1) + column);
                short bottomRight = (short)(bottomLeft + 1);

                indices[cursor++] = topLeft;
                indices[cursor++] = topRight;
                indices[cursor++] = bottomLeft;
                indices[cursor++] = topRight;
                indices[cursor++] = bottomRight;
                indices[cursor++] = bottomLeft;
            }
        }
    }
}
