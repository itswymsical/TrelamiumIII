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
// VertexTrail
// A vertex strip that follows a list of world-space positions.
//
// The mesh is two rows of vertices, top and bottom, offset from each spine
// point by the perpendicular scaled by widthFn. Every adjacent pair of
// spine points forms two triangles.
//
// The staging arrays are allocated once at construction and the index list
// is built once with them -- it only ever depends on pointCount. Drawing
// goes through DrawUserIndexedPrimitives, so a trail owns no GPU resources
// of its own: nothing to allocate per instance, nothing to wait on before
// the first frame renders, and nothing to leak when a projectile dies
// without anyone disposing it.
//
// Usage:
//   trail = new VertexTrail(pointCount, widthFn, colorFn);
//   cache.Fill(positionBuffer);
//   trail.Positions = positionBuffer;
//   trail.Render(effect);
// -----------------------------------------------------------------------
public sealed class VertexTrail
{
    readonly int pointCount;
    readonly TrailWidthFunction widthFn;
    readonly TrailColorFunction colorFn;

    // Pre-allocated staging arrays written into every frame, never replaced
    readonly VertexPositionColorTexture[] stagingVertices;
    readonly short[] stagingIndices;

    // Positions[0] = tail, Positions[^1] = head.
    // Must match pointCount exactly.
    public Vector2[]? Positions { get; set; }

    public VertexTrail(int pointCount, TrailWidthFunction widthFn, TrailColorFunction colorFn)
    {
        this.pointCount = pointCount;
        this.widthFn = widthFn;
        this.colorFn = colorFn;

        // Two vertices per spine point (top + bottom row)
        stagingVertices = new VertexPositionColorTexture[pointCount * 2];

        // Two triangles between each adjacent pair of spine points,
        // each triangle defined by 3 indices
        stagingIndices = new short[6 * (pointCount - 1)];

        BuildIndices();
    }

    /// <summary>
    ///     Winding is a pure function of <see cref="pointCount" />, so it is written once here
    ///     rather than rebuilt and re-uploaded on every frame of every trail.
    /// </summary>
    void BuildIndices()
    {
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

    void BuildMesh()
    {
        var pos = Positions!;

        // Carried between iterations so a run of identical points -- a padded tail, or a
        // projectile that has stopped dead against a tile -- keeps the ribbon's last known
        // orientation instead of collapsing it and flipping the strip inside out as it recovers.
        var perp = Vector2.Zero;

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

            if (toNext != Vector2.Zero)
                perp = toNext.RotatedBy(MathHelper.PiOver2);

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
    }

    public void Render(Effect effect, Vector2? screenOffset = null)
    {
        if (Positions == null || effect == null || Positions.Length != pointCount)
            return;

        BuildMesh();

        var device = Main.graphics.GraphicsDevice;

        device.RasterizerState = RasterizerState.CullNone;
        device.DepthStencilState = DepthStencilState.None;

        var offset = screenOffset ?? -Main.screenPosition;
        var world = Matrix.CreateTranslation(new Vector3(offset, 0f));
        var view = Main.GameViewMatrix.TransformationMatrix;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        // Only shaders that take the transform themselves use this. pixelTrail wants its own
        // `transformMatrix`, which the caller sets alongside its other parameters.
        effect.Parameters["uWorldViewProjection"]?.SetValue(world * view * projection);

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                stagingVertices, 0, stagingVertices.Length,
                stagingIndices, 0, stagingIndices.Length / 3);
        }
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

    /// <summary>
    ///     Writes into an existing array, oldest position first, so
    ///     <c>destination[^1]</c> is always the newest.
    /// </summary>
    /// <remarks>
    ///     While the cache is still filling, the shortfall is padded onto the <i>tail</i> with the
    ///     oldest known point. Padding the far end instead left the newest position sitting in the
    ///     middle of the array and the head pinned to wherever the projectile spawned, which drew
    ///     the ribbon stretched back to the throw point for its first frames -- most visible on a
    ///     fast projectile, where those frames cover a lot of ground.
    /// </remarks>
    public void Fill(Vector2[] destination)
    {
        if (count == 0)
        {
            Array.Clear(destination);
            return;
        }

        var fillCount = Math.Min(count, destination.Length);
        var pad = destination.Length - fillCount;

        for (var i = 0; i < pad; i++)
            destination[i] = buffer[count - 1];

        for (var i = 0; i < fillCount; i++)
            destination[pad + i] = buffer[fillCount - 1 - i];
    }

    /// <summary>
    ///     Fills the whole history with one position, so a trail starts collapsed on its projectile
    ///     rather than growing out of a padded tail. Call on spawn.
    /// </summary>
    public void Prime(Vector2 position)
    {
        count = capacity;

        for (var i = 0; i < capacity; i++)
            buffer[i] = position;
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
