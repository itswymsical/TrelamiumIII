using System.Collections.Generic;
using System.Runtime.InteropServices;
using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Draws one source's tiles as a continuous surface: each tile is splatted into a screen-space canvas as a quadratic
///     B-spline, and the canvas is composited over the terrain through the source's shader.
/// </summary>
/// <remarks>
///     Target 0 holds coverage and the source's data, target 1 the kernel weight and world light, all as weighted sums.
///     Samples are copied into open neighbours so the surface runs past the terrain edge and the terrain mask cuts it.
/// </remarks>
internal sealed class TileCanvas(ITileCanvasSource source) : ARenderTargetContentByRequest
{
    /// <summary>Kernel half-width, in tiles.</summary>
    private const float KERNEL_RADIUS = 1.5f;

    /// <summary>Tiles past the viewport still stamped: the kernel plus one tile of spill.</summary>
    private const int CULL_PADDING = 3;

    private const int MAX_QUADS = 16384;

    private const int BOUNDS_MARGIN_TEXELS = 2;

    private const int IDLE_TICKS_BEFORE_RELEASE = 300;

    /// <summary>A removed tile's last sample, faded out over the condition's fade-out ticks.</summary>
    private struct Ghost
    {
        public Point Position;
        public TileCanvasSample Sample;
        public float Weight;
        public int Ticks;
    }

    /// <summary>Samples spilled into one open tile, averaged when it is stamped.</summary>
    private struct OpenCell
    {
        public Vector4 Sample;
        public float Weight;
        public int Count;
    }

    /// <summary>Plain additive: the stamp shader applies the kernel, so the targets hold straight sums.</summary>
    private static readonly BlendState Accumulate = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private static Asset<Effect>? stampShader;

    private readonly List<Ghost> ghosts = [];

    private readonly Dictionary<Point, OpenCell> openCells = [];

    private FieldStampVertex[] vertices = [];

    private int[] indices = [];

    private int vertexCount;

    private Point boundsMin;

    private Point boundsMax;

    private Matrix stampTransform;

    private RenderTarget2D? lightTarget;

    private int idleTicks;

    /// <summary>Quads written so far by the build in progress.</summary>
    private int quadCount;

    private TileCanvasWriter? writer;

    public ITileCanvasSource Source => source;

    private int PixelScale => Math.Max(source.CanvasPixelScale, 1);

    /// <summary>Tiles stamped by the last <see cref="Build" />, or empty.</summary>
    public Rectangle TileBounds => vertexCount == 0
        ? Rectangle.Empty
        : new Rectangle(boundsMin.X, boundsMin.Y, boundsMax.X - boundsMin.X + 1, boundsMax.Y - boundsMin.Y + 1);

    /// <summary>Keeps a removed tile's last sample on the canvas while it fades out over the source's fade-out ticks.</summary>
    public void AddGhost(Point tile, TileCanvasSample sample, float weight)
    {
        if (source.FadeOutTicks <= 0 || weight <= 0f || sample.Coverage <= 0.01f)
            return;

        ghosts.Add(new Ghost { Position = tile, Sample = sample, Weight = weight, Ticks = source.FadeOutTicks });
    }

    public void ClearGhosts() => ghosts.Clear();

    /// <summary>Ages the fading tiles and hands the targets back once there has been nothing to draw for a while.</summary>
    public void Update()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost ghost = ghosts[i];

            // A tile that took the condition again draws itself.
            if (--ghost.Ticks <= 0 || source.Contains(ghost.Position))
            {
                ghosts[i] = ghosts[^1];
                ghosts.RemoveAt(ghosts.Count - 1);

                continue;
            }

            ghosts[i] = ghost;
        }

        if (source.IsLive || ghosts.Count > 0)
        {
            idleTicks = 0;
            return;
        }

        if (_target is not null && ++idleTicks >= IDLE_TICKS_BEFORE_RELEASE)
        {
            Free();
            idleTicks = 0;
        }
    }

    /// <summary>Splats the visible tiles into the canvas. Call from <c>Main.CheckMonoliths</c>, before the scene draws.</summary>
    public void Build()
    {
        vertexCount = source.IsLive || ghosts.Count > 0 ? BuildGeometry() : 0;

        if (vertexCount == 0)
        {
            _wasPrepared = false;
            return;
        }

        // Read before any target is bound: the view matrix rebuilds lazily against the current viewport.
        stampTransform = Main.GameViewMatrix.TransformationMatrix * Matrix.CreateScale(1f / PixelScale, 1f / PixelScale, 1f);

        Request();
        PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);
    }

    /// <summary>Draws the canvas over the terrain. Call after the solid tile layer, before anything that stands on it.</summary>
    public void Composite(Texture2D mask, Vector2 maskOrigin)
    {
        if (!IsReady || _target is not { IsDisposed: false } canvas || lightTarget is null || source.Shader is not { Value: { } effect })
            return;

        Rectangle bounds = CanvasBounds(canvas.Width, canvas.Height);

        if (bounds.IsEmpty)
            return;

        ApplyFrameParameters(effect, canvas, mask, maskOrigin);
        source.ApplyShaderParameters(effect);

        int scale = PixelScale;
        SpriteBatch spriteBatch = Main.spriteBatch;

        spriteBatch.Begin(SpriteSortMode.Immediate, source.BlendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

        try
        {
            effect.CurrentTechnique.Passes[0].Apply();

            // Destination is the bounds scaled by exactly PixelScale, so the uv the shader reads stays the canvas uv.
            spriteBatch.Draw(canvas, new Rectangle(bounds.X * scale, bounds.Y * scale, bounds.Width * scale, bounds.Height * scale), bounds, Color.White);
        }
        finally
        {
            spriteBatch.End();

            // The effect outlives both textures, and FNA rebinds texture parameters on every pass Apply with no disposal check.
            effect.Parameters["uCanvasLight"]?.SetValue((Texture?)null);
            effect.Parameters["uMaskTex"]?.SetValue((Texture?)null);
        }
    }

    /// <summary>Forgets both targets and disposes them on the main thread.</summary>
    public void Free()
    {
        RenderTarget2D? canvas = _target;
        RenderTarget2D? light = lightTarget;

        Reset();
        lightTarget = null;

        if (canvas is not null)
            Main.QueueMainThreadAction(canvas.Dispose);

        if (light is not null)
            Main.QueueMainThreadAction(light.Dispose);
    }

    protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        stampShader ??= Assets.Shaders.TileCanvasStamp.Asset;

        if (stampShader is not { Value: { } stamp } || vertexCount == 0)
            return;

        (int width, int height) = FieldSurface.ScreenSize(PixelScale);

        // Half-float targets: eight-bit ones clamp the sums flat wherever kernels overlap.
        RenderTarget2D? canvas = _target;
        FieldSurface.Ensure(ref canvas, device, width, height);
        FieldSurface.Ensure(ref lightTarget, device, width, height);
        _target = canvas!;

        device.SetRenderTargets(_target, lightTarget);
        device.Clear(Color.Transparent);

        device.BlendState = Accumulate;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        stamp.Parameters["uWorldViewProjection"]?.SetValue(stampTransform * Matrix.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f));

        foreach (EffectPass pass in stamp.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount, indices, 0, vertexCount / 2);
        }

        device.SetRenderTargets(null);
        _wasPrepared = true;
    }

    private void ApplyFrameParameters(Effect effect, RenderTarget2D canvas, Texture2D mask, Vector2 maskOrigin)
    {
        // The tile layer is drawn from a whole-pixel origin, so world positions are offset by the same fraction to keep
        // the shading grid on the tile art.
        var origin = new Vector2(MathF.Floor(maskOrigin.X), MathF.Floor(maskOrigin.Y));
        Vector2 snap = maskOrigin - origin;

        Matrix worldToScreen = Matrix.CreateTranslation(snap.X - Main.screenPosition.X, snap.Y - Main.screenPosition.Y, 0f)
                               * Main.GameViewMatrix.TransformationMatrix;

        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["CanvasScreenSize"]?.SetValue(new Vector2(canvas.Width * PixelScale, canvas.Height * PixelScale));
        effect.Parameters["WorldToScreen"]?.SetValue(worldToScreen);
        effect.Parameters["ScreenToWorld"]?.SetValue(Matrix.Invert(worldToScreen));
        effect.Parameters["MaskOrigin"]?.SetValue(origin);
        effect.Parameters["MaskTexel"]?.SetValue(new Vector2(1f / mask.Width, 1f / mask.Height));
        effect.Parameters["CellSize"]?.SetValue(2f);
        effect.Parameters["SurfaceLip"]?.SetValue(0f);
        effect.Parameters["uCanvasLight"]?.SetValue(lightTarget);
        effect.Parameters["uMaskTex"]?.SetValue(mask);
    }

    private int BuildGeometry()
    {
        Rectangle area = VisibleTiles();
        quadCount = 0;

        openCells.Clear();

        source.Collect(area, writer ??= Stamp);

        int fadeOut = Math.Max(source.FadeOutTicks, 1);

        foreach (Ghost ghost in ghosts)
        {
            if (area.Contains(ghost.Position))
                Stamp(ghost.Position, ghost.Sample, ghost.Weight * ghost.Ticks / fadeOut);
        }

        foreach (KeyValuePair<Point, OpenCell> pair in openCells)
        {
            OpenCell cell = pair.Value;

            WriteQuad(pair.Key, cell.Sample / cell.Count, cell.Weight / cell.Count);
        }

        return quadCount * 4;
    }

    private void Stamp(Point tile, TileCanvasSample sample, float weight)
    {
        if (weight <= 0f)
            return;

        var premultiplied = new Vector4(sample.Coverage, sample.Data.X, sample.Data.Y, sample.Data.Z) * weight;

        WriteQuad(tile, premultiplied, weight);

        if (source.SpillsIntoOpen)
            Spill(tile, premultiplied, weight);
    }

    /// <summary>Copies a sample into its open neighbours, so the surface does not thin out toward the terrain edge.</summary>
    private void Spill(Point tile, Vector4 premultiplied, float weight)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var neighbour = new Point(tile.X + dx, tile.Y + dy);

                if (source.Contains(neighbour) || !IsOpen(neighbour))
                    continue;

                ref OpenCell cell = ref CollectionsMarshal.GetValueRefOrAddDefault(openCells, neighbour, out _);

                cell.Sample += premultiplied;
                cell.Weight += weight;
                cell.Count++;
            }
        }
    }

    private void WriteQuad(Point tile, Vector4 premultiplied, float weight)
    {
        ref int quads = ref quadCount;

        if (quads >= MAX_QUADS)
            return;

        EnsureCapacity(quads + 1);

        Color light = Lighting.GetColor(tile.X, tile.Y);
        var sample = new Color(premultiplied.X, premultiplied.Y, premultiplied.Z, premultiplied.W);
        var weighted = new Color(weight, light.R / 255f * weight, light.G / 255f * weight, light.B / 255f * weight);
        Vector2 centre = new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f) - Main.screenPosition;

        int v = quads * 4;

        vertices[v] = Corner(centre, -1f, -1f, sample, weighted);
        vertices[v + 1] = Corner(centre, 1f, -1f, sample, weighted);
        vertices[v + 2] = Corner(centre, 1f, 1f, sample, weighted);
        vertices[v + 3] = Corner(centre, -1f, 1f, sample, weighted);

        if (quads == 0)
        {
            boundsMin = tile;
            boundsMax = tile;
        }
        else
        {
            boundsMin = new Point(Math.Min(boundsMin.X, tile.X), Math.Min(boundsMin.Y, tile.Y));
            boundsMax = new Point(Math.Max(boundsMax.X, tile.X), Math.Max(boundsMax.Y, tile.Y));
        }

        quads++;
    }

    private static FieldStampVertex Corner(Vector2 centre, float x, float y, Color sample, Color weighted) => new()
    {
        Position = new Vector3(centre.X + x * KERNEL_RADIUS * 16f, centre.Y + y * KERNEL_RADIUS * 16f, 0f),
        Field = sample,
        Attributes = weighted,
        TextureCoordinate = new Vector2(x * KERNEL_RADIUS, y * KERNEL_RADIUS),
    };

    /// <summary>Grows the geometry buffers to hold <paramref name="quads" />, keeping what is already written.</summary>
    private void EnsureCapacity(int quads)
    {
        if (vertices.Length >= quads * 4)
            return;

        int capacity = Math.Max(64, vertices.Length / 4);

        while (capacity < quads)
            capacity *= 2;

        Array.Resize(ref vertices, capacity * 4);
        indices = new int[capacity * 6];

        for (int q = 0; q < capacity; q++)
        {
            int v = q * 4;
            int i = q * 6;

            indices[i] = v;
            indices[i + 1] = v + 1;
            indices[i + 2] = v + 2;
            indices[i + 3] = v;
            indices[i + 4] = v + 2;
            indices[i + 5] = v + 3;
        }
    }

    /// <summary>The canvas texels the stamped tiles can reach, so the composite only shades those.</summary>
    private Rectangle CanvasBounds(int width, int height)
    {
        if (vertexCount == 0)
            return Rectangle.Empty;

        // The kernel, plus a tile for a lip standing proud of the terrain.
        float pad = (KERNEL_RADIUS + 1f) * 16f;
        Matrix view = Main.GameViewMatrix.TransformationMatrix;

        Vector2 a = Vector2.Transform(new Vector2(boundsMin.X * 16f - pad, boundsMin.Y * 16f - pad) - Main.screenPosition, view);
        Vector2 b = Vector2.Transform(new Vector2((boundsMax.X + 1) * 16f + pad, (boundsMax.Y + 1) * 16f + pad) - Main.screenPosition, view);

        int scale = PixelScale;

        int left = Math.Max((int)MathF.Floor(MathF.Min(a.X, b.X) / scale) - BOUNDS_MARGIN_TEXELS, 0);
        int top = Math.Max((int)MathF.Floor(MathF.Min(a.Y, b.Y) / scale) - BOUNDS_MARGIN_TEXELS, 0);
        int right = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, b.X) / scale) + BOUNDS_MARGIN_TEXELS, width);
        int bottom = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, b.Y) / scale) + BOUNDS_MARGIN_TEXELS, height);

        return right <= left || bottom <= top ? Rectangle.Empty : new Rectangle(left, top, right - left, bottom - top);
    }

    private static bool IsOpen(Point tile)
    {
        if (!WorldGen.InWorld(tile.X, tile.Y))
            return false;

        Tile t = Main.tile[tile.X, tile.Y];

        return !t.HasTile || t.IsActuated || !Main.tileSolid[t.TileType];
    }

    /// <summary>The unzoomed viewport in tiles, padded. A superset of the zoomed one at every zoom Terraria allows.</summary>
    private static Rectangle VisibleTiles()
    {
        int left = Math.Max((int)(Main.screenPosition.X / 16f) - CULL_PADDING, 0);
        int top = Math.Max((int)(Main.screenPosition.Y / 16f) - CULL_PADDING, 0);
        int right = Math.Min((int)((Main.screenPosition.X + Main.screenWidth) / 16f) + CULL_PADDING, Main.maxTilesX - 1);
        int bottom = Math.Min((int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + CULL_PADDING, Main.maxTilesY - 1);

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }
}
