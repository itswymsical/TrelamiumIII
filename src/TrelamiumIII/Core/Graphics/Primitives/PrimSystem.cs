using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Graphics.Primitives;

// -----------------------------------------------------------------------
// Delegates
// Factor: 0.0 = tail end, 1.0 = head (projectile center).
// -----------------------------------------------------------------------
public delegate float TrailWidthFunction(float factor);
public delegate Color TrailColorFunction(float factor);

// -----------------------------------------------------------------------
// VertexBuffer
// Owns DynamicVertexBuffer and DynamicIndexBuffer on the GPU.
// Pre-allocated at construction SetDataOptions.Discard lets the
// driver orphan the old buffer instead of stalling the pipeline.
// All GPU resource creation and disposal is queued to the main thread.
// -----------------------------------------------------------------------
public sealed class VertexBuffer : IDisposable
{
    readonly GraphicsDevice device;
    DynamicVertexBuffer? vertexBuffer;
    DynamicIndexBuffer? indexBuffer;

    public bool IsReady => vertexBuffer != null && indexBuffer != null;
    public bool IsDisposed { get; private set; }

    public VertexBuffer(GraphicsDevice device, int maxVertices, int maxIndices)
    {
        this.device = device;

        Main.QueueMainThreadAction(() =>
        {
            vertexBuffer = new DynamicVertexBuffer(device,
                typeof(VertexPositionColorTexture), maxVertices, BufferUsage.WriteOnly);
            indexBuffer = new DynamicIndexBuffer(device,
                IndexElementSize.SixteenBits, maxIndices, BufferUsage.WriteOnly);
        });
    }

    public void Upload(VertexPositionColorTexture[] vertices, int vertexCount,
                       short[] indices, int indexCount)
    {
        if (!IsReady || IsDisposed) return;

        vertexBuffer!.SetData(0, vertices, 0, vertexCount,
            VertexPositionColorTexture.VertexDeclaration.VertexStride,
            SetDataOptions.Discard);

        indexBuffer!.SetData(0, indices, 0, indexCount,
            SetDataOptions.Discard);
    }

    public void Draw(Effect effect, Matrix world)
    {
        if (!IsReady || IsDisposed) return;

        device.SetVertexBuffer(vertexBuffer);
        device.Indices = indexBuffer;
        device.RasterizerState = RasterizerState.CullNone;
        device.DepthStencilState = DepthStencilState.None;

        var view = Main.GameViewMatrix.TransformationMatrix;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["uWorldViewProjection"]
            ?.SetValue(world * view * projection);

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0, 0, vertexBuffer!.VertexCount,
                0, indexBuffer!.IndexCount / 3);
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);

        Main.QueueMainThreadAction(() =>
        {
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
        });
    }
}

// -----------------------------------------------------------------------
// VertexTrail
// A vertex strip that follows a list of world-space positions.
//
// The mesh is two rows of vertices top and bottom  offset from each
// spine point by the perpendicular scaled by widthFn. Every adjacent
// pair of spine points forms two triangles.
//
// Both the GPU buffers and the CPU staging arrays are allocated once
// at construction. GenerateMesh writes into those arrays every frame
// zero heap allocation per draw call.
//
// Usage:
//   trail = new VertexTrail(pointCount, widthFn, colorFn);
//   cache.Fill(positionBuffer);
//   trail.Positions = positionBuffer;
//   trail.Render(effect);
// -----------------------------------------------------------------------
public sealed class VertexTrail : IDisposable
{
    readonly int pointCount;
    readonly TrailWidthFunction widthFn;
    readonly TrailColorFunction colorFn;

    // Pre-allocated staging arrays written into every frame, never replaced
    readonly VertexPositionColorTexture[] stagingVertices;
    readonly short[] stagingIndices;

    readonly VertexBuffer buffer;

    public bool IsDisposed { get; private set; }

    // Positions[0] = tail, Positions[^1] = head.
    // Must match pointCount exactly.
    public Vector2[]? Positions { get; set; }

    public VertexTrail(int pointCount, TrailWidthFunction widthFn, TrailColorFunction colorFn)
    {
        this.pointCount = pointCount;
        this.widthFn = widthFn;
        this.colorFn = colorFn;

        // Two vertices per spine point (top + bottom row)
        var maxVertices = pointCount * 2;

        // Two triangles between each adjacent pair of spine points,
        // each triangle defined by 3 indices
        var maxIndices = 6 * (pointCount - 1);

        stagingVertices = new VertexPositionColorTexture[maxVertices];
        stagingIndices = new short[maxIndices];

        buffer = new VertexBuffer(Main.graphics.GraphicsDevice, maxVertices, maxIndices);
    }

    void BuildMesh()
    {
        var pos = Positions!;

        for (var k = 0; k < pos.Length; k++)
        {
            var factor = (float)k / (pos.Length - 1); // 0 = tail, 1 = head
            var width = widthFn?.Invoke(factor) ?? 16f;
            var color = colorFn?.Invoke(factor) ?? Color.White;

            var current = pos[k];

            // Direction toward the next point or extrapolated for the last
            var next = k < pos.Length - 1
                ? pos[k + 1]
                : pos[^1] + (pos[^1] - pos[^2]);

            var toNext = (next - current).SafeNormalize(Vector2.Zero);
            var perp = toNext.RotatedBy(MathHelper.PiOver2);

            // Top row: indices 0..n-1
            // Bottom row: indices n..2n-1
            stagingVertices[k] = new VertexPositionColorTexture(
                (current + perp * width).ToVector3(),
                color,
                new Vector2(factor, 0f));

            stagingVertices[k + pointCount] = new VertexPositionColorTexture(
                (current - perp * width).ToVector3(),
                color,
                new Vector2(factor, 1f));
        }

        // Two triangles per segment:
        // (k+n, k+n+1, k+1) and (k+1, k, k+n)
        for (short k = 0; k < pointCount - 1; k++)
        {
            var b = k * 6;
            stagingIndices[b] = (short)(k + pointCount);
            stagingIndices[b + 1] = (short)(k + pointCount + 1);
            stagingIndices[b + 2] = (short)(k + 1);
            stagingIndices[b + 3] = (short)(k + 1);
            stagingIndices[b + 4] = k;
            stagingIndices[b + 5] = (short)(k + pointCount);
        }
    }

    public void Render(Effect effect, Vector2? screenOffset = null)
    {
        if (Positions == null || IsDisposed || effect == null) return;

        BuildMesh();
        buffer.Upload(stagingVertices, stagingVertices.Length,
                      stagingIndices, stagingIndices.Length);

        var offset = screenOffset ?? -Main.screenPosition;
        buffer.Draw(effect, Matrix.CreateTranslation(new Vector3(offset, 0f)));
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);
        buffer?.Dispose();
    }
}

// -----------------------------------------------------------------------
// TrailPositionCache
// Manages position history for a trail with zero per-frame allocation.
// Call Update() from AI() each frame.
// Call Fill() to write into a pre-allocated destination array.
// -----------------------------------------------------------------------
public sealed class TrailPositionCache
{
    readonly Vector2[] buffer;
    readonly int capacity;
    int count;

    public int Count => count;

    public TrailPositionCache(int capacity)
    {
        this.capacity = capacity;
        buffer = new Vector2[capacity];
    }

    // Insert latest position at the head, shift everything back.
    public void Update(Vector2 position)
    {
        if (count < capacity) count++;
        for (var i = count - 1; i > 0; i--)
            buffer[i] = buffer[i - 1];
        buffer[0] = position;
    }

    // Write into an existing array, oldest position first (tail to head).
    // If the cache isn't full yet, the tail is padded with the oldest known point.
    public void Fill(Vector2[] destination)
    {
        var fillCount = Math.Min(count, destination.Length);

        // Write from oldest (tail) to newest (head)
        for (var i = 0; i < fillCount; i++)
            destination[i] = buffer[fillCount - 1 - i];

        // Pad with the oldest known point if the cache isn't full yet
        for (var i = fillCount; i < destination.Length; i++)
            destination[i] = count > 0 ? buffer[count - 1] : Vector2.Zero;
    }

    public void Reset() => count = 0;
}

// -----------------------------------------------------------------------
// PrimitiveRenderer
// Single ModSystem that drives the draw hooks.
// Projectiles and NPCs implement IDrawPrimitive to opt into rendering.
// -----------------------------------------------------------------------
public class PrimitiveRenderer : ModSystem
{
    public override void OnModLoad()
    {
        if (Main.dedServ) return;

        On_Main.DrawDust += (orig, self) =>
        {
            orig(self);
            if (Main.gameMenu) return;

            for (var k = 0; k < Main.maxProjectiles; k++)
            {
                var proj = Main.projectile[k];
                if (proj.active && proj.ModProjectile is IDrawPrimitive p)
                    p.DrawPrimitives();
            }

            for (var k = 0; k < Main.maxNPCs; k++)
            {
                var npc = Main.npc[k];
                if (npc.active && npc.ModNPC is IDrawPrimitive p)
                    p.DrawPrimitives();
            }
        };
    }
}
