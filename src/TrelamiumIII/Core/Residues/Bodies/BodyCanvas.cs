using System.Collections.Generic;
using ReLogic.Content;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>
///     Draws a residue on a player with the shader it wears on the ground: the player's silhouette takes the place of the
///     terrain mask, and a small grid of body cells takes the place of tiles.
/// </summary>
/// <remarks>
///     <para>
///         Each body gets a slot in atlases shared by all of them, rather than targets of its own. A canvas is two half-float
///         targets, which is fine per residue and too much per slime in a swarm, and a slot where one screen-wide silhouette
///         would do keeps overlapping bodies from bleeding into each other.
///     </para>
///     <para>
///         To the shader, "world" here is body space: pixels from the top-left of the slot. That pins its noise and shading
///         grid to the body, so they don't swim across it as it walks.
///     </para>
///     <para>
///         The fire is hardcoded for now, see <see cref="BodyFireTest" />. Only the local player can have it, and it does
///         nothing but draw.
///     </para>
/// </remarks>
internal sealed class BodyCanvas : ModSystem
{
    private const int SLOT_SIZE = 256;

    private const int SLOTS_PER_SIDE = 4;

    private const int ATLAS_SIZE = SLOT_SIZE * SLOTS_PER_SIDE;

    private const int MAX_BODIES = SLOTS_PER_SIDE * SLOTS_PER_SIDE;

    /// <summary>Body pixels per cell. Tiles are 16, far too coarse for a body three tiles tall.</summary>
    private const int CELL_SIZE = 6;

    /// <summary>Cells each side of the body's centre. Past any player sprite: the silhouette is what cuts it.</summary>
    private const int CELL_REACH = 8;

    /// <summary>Kernel half-width, in cells.</summary>
    private const float KERNEL_RADIUS = 1.5f;

    private const int IDLE_TICKS_BEFORE_RELEASE = 300;

    /// <summary>
    ///     Neither the real draw nor an afterimage. Everything that shouldn't be drawn twice asks for a shadow of exactly zero
    ///     (dust, the held item, its projectile), and only a positive one hides the skin and hair.
    /// </summary>
    private const float CAPTURE_SHADOW = -0.001f;

    /// <summary>
    ///     Where the mask is copied to, in whole sprite pixels, so flames stand a little proud of the outline and more so
    ///     upward. Never down, where they'd sit on the ground under the feet.
    /// </summary>
    private static readonly Point[] MaskReach =
    [
        new(0, 0),
        new(-2, 0),
        new(2, 0),
        new(0, -2),
        new(-2, -2),
        new(2, -2),
        new(0, -4),
    ];

    /// <summary>Plain additive, as <see cref="TileCanvas" /> stamps: the targets hold straight sums.</summary>
    private static readonly BlendState Accumulate = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private static readonly RasterizerState ClippedToSlot = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
    };

    /// <param name="Anchor">Where the middle of its slot lands on the screen, before zoom. Whole pixels, like the sprite's own.</param>
    private readonly record struct Body(Player Player, Rectangle Slot, Point Anchor);

    /// <summary>Who has a slot this frame.</summary>
    private static readonly List<Body> bodies = [];

    private static RenderTarget2D? silhouette;

    private static RenderTarget2D? mask;

    private static RenderTarget2D? canvas;

    private static RenderTarget2D? light;

    private static FieldStampVertex[] vertices = [];

    private static int[] indices = [];

    private static int vertexCount;

    private static int idleTicks;

    public override void OnModLoad()
    {
        if (Main.dedServ)
            return;

        On_Main.CheckMonoliths += static orig =>
        {
            orig();
            Build();
        };

        On_LegacyPlayerRenderer.DrawPlayerFull += static (orig, self, camera, player) =>
        {
            orig(self, camera, player);
            CompositeOver(player);
        };
    }

    public override void OnModUnload()
    {
        bodies.Clear();
        Free();
    }

    public override void ClearWorld() => BodyFireTest.Stop();

    public override void PostUpdateEverything()
    {
        BodyFireTest.Update();

        if (BodyFireTest.IsBurning || silhouette is null)
        {
            idleTicks = 0;
            return;
        }

        if (++idleTicks >= IDLE_TICKS_BEFORE_RELEASE)
            Free();
    }

    /// <summary>Forgets every target and disposes them on the main thread.</summary>
    private static void Free()
    {
        foreach (RenderTarget2D? target in (ReadOnlySpan<RenderTarget2D?>)[silhouette, mask, canvas, light])
        {
            if (target is not null)
                Main.QueueMainThreadAction(target.Dispose);
        }

        silhouette = mask = canvas = light = null;
        idleTicks = 0;
    }

    /// <summary>Call from <c>Main.CheckMonoliths</c>, before the scene draws, like the tile canvases.</summary>
    private static void Build()
    {
        bodies.Clear();

        if (Main.gameMenu || !BodyFireTest.IsBurning)
            return;

        Player player = Main.LocalPlayer;

        if (player.active && !player.dead && !player.ghost && !player.invis && !player.ShouldNotDraw && !player.stoned)
            bodies.Add(new Body(player, SlotAt(bodies.Count), AnchorOf(player)));

        if (bodies.Count == 0)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        try
        {
            EnsureTargets(device);
            CaptureSilhouettes(device);
            SpreadMask(device);
            StampCells(device);
        }
        catch (Exception exception)
        {
            bodies.Clear();
            ResidueSystem.ReportFailure(exception);
        }
        finally
        {
            device.SetRenderTargets(null);
        }
    }

    private static Rectangle SlotAt(int index)
        => new(index % SLOTS_PER_SIDE * SLOT_SIZE, index / SLOTS_PER_SIDE * SLOT_SIZE, SLOT_SIZE, SLOT_SIZE);

    /// <summary>Follows the sprite: <c>gfxOffY</c> is how a player eases up a step, and the draw adds it to the position.</summary>
    private static Point AnchorOf(Player player)
    {
        Vector2 onScreen = player.Center + new Vector2(0f, player.gfxOffY) - Main.screenPosition;

        return new Point((int)MathF.Floor(onScreen.X), (int)MathF.Floor(onScreen.Y));
    }

    private static void EnsureTargets(GraphicsDevice device)
    {
        EnsureMaskTarget(ref silhouette, device);
        EnsureMaskTarget(ref mask, device);

        // Half-float: eight-bit targets clamp the sums flat wherever kernels overlap.
        FieldSurface.Ensure(ref canvas, device, ATLAS_SIZE, ATLAS_SIZE);
        FieldSurface.Ensure(ref light, device, ATLAS_SIZE, ATLAS_SIZE);
    }

    private static void EnsureMaskTarget(ref RenderTarget2D? target, GraphicsDevice device)
    {
        if (target is { IsDisposed: false })
            return;

        target = new RenderTarget2D(device, ATLAS_SIZE, ATLAS_SIZE, mipMap: false, SurfaceFormat.Color, DepthFormat.None, preferredMultiSampleCount: 0, RenderTargetUsage.PreserveContents);
    }

    /// <summary>
    ///     Draws each body into its slot the way the game is about to draw it on the screen, moved by whole pixels and with no
    ///     zoom, so the composite can lay a slot exactly over the real sprite.
    /// </summary>
    private static void CaptureSilhouettes(GraphicsDevice device)
    {
        SpriteBatch spriteBatch = Main.spriteBatch;

        device.SetRenderTarget(silhouette);
        device.Clear(Color.Transparent);

        foreach (Body body in bodies)
        {
            Player player = body.Player;
            Point middle = body.Slot.Center;
            Matrix toSlot = Matrix.CreateTranslation(middle.X - body.Anchor.X, middle.Y - body.Anchor.Y, 0f);

            // The sprite blinks while immune. The fire on it shouldn't, as whatever lit it is likely what's hitting it.
            int immuneAlpha = player.immuneAlpha;
            player.immuneAlpha = 0;

            device.ScissorRectangle = body.Slot;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, ClippedToSlot, null, toSlot);

            try
            {
                Vector2 position = player.position + new Vector2(0f, player.gfxOffY);

                Main.PlayerRenderer.DrawPlayer(Main.Camera, player, position, player.fullRotation, player.fullRotationOrigin, CAPTURE_SHADOW);
            }
            finally
            {
                spriteBatch.End();
                player.immuneAlpha = immuneAlpha;
            }
        }
    }

    private static void SpreadMask(GraphicsDevice device)
    {
        SpriteBatch spriteBatch = Main.spriteBatch;

        device.SetRenderTarget(mask);
        device.Clear(Color.Transparent);

        foreach (Body body in bodies)
        {
            device.ScissorRectangle = body.Slot;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, ClippedToSlot);

            foreach (Point offset in MaskReach)
                spriteBatch.Draw(silhouette, new Vector2(body.Slot.X + offset.X, body.Slot.Y + offset.Y * body.Player.gravDir), body.Slot, Color.White);

            spriteBatch.End();
        }
    }

    private static void StampCells(GraphicsDevice device)
    {
        if (Assets.Shaders.TileCanvasStamp.Asset is not { Value: { } stamp })
            return;

        vertexCount = 0;

        foreach (Body body in bodies)
            WriteCells(in body);

        device.SetRenderTargets(canvas, light);
        device.Clear(Color.Transparent);

        if (vertexCount == 0)
            return;

        device.BlendState = Accumulate;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        stamp.Parameters["uWorldViewProjection"]?.SetValue(Matrix.CreateOrthographicOffCenter(0f, ATLAS_SIZE, ATLAS_SIZE, 0f, -1f, 1f));

        foreach (EffectPass pass in stamp.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount, indices, 0, vertexCount / 2);
        }
    }

    private static void WriteCells(in Body body)
    {
        if (ResidueSystem.Get<BurnedResidue>() is not { } burn)
            return;

        Vector2 worldMiddle = body.Anchor.ToVector2() + Main.screenPosition;
        Vector2 slotMiddle = body.Slot.Center.ToVector2();

        for (int x = -CELL_REACH; x < CELL_REACH; x++)
        {
            for (int y = -CELL_REACH; y < CELL_REACH; y++)
            {
                var offset = new Vector2((x + 0.5f) * CELL_SIZE, (y + 0.5f) * CELL_SIZE);

                // Measured from its feet, wherever gravity has put them.
                Vector2 fromCentre = offset * new Vector2(1f, body.Player.gravDir);

                if (BodyFireTest.CoverageAt(fromCentre) is not (> 0f and var coverage))
                    continue;

                // The ground's own encoding, so the shader reads the same channels off it.
                var instance = new ResidueInstance
                {
                    Life = 1f,
                    MaxLife = 1f,
                    Intensity = coverage,
                    Level = BodyFireTest.HeatAt(fromCentre),
                    Variant = BurnProfile.Standard.Id,
                };

                Vector2 world = worldMiddle + offset;
                Color lit = Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));

                WriteQuad(slotMiddle + offset, burn.EncodeCanvas(in instance), lit);
            }
        }
    }

    private static void WriteQuad(Vector2 centre, TileCanvasSample sample, Color lit)
    {
        EnsureCapacity(vertexCount / 4 + 1);

        var field = new Color(sample.Coverage, sample.Data.X, sample.Data.Y, sample.Data.Z);
        var weighted = new Color(1f, lit.R / 255f, lit.G / 255f, lit.B / 255f);

        vertices[vertexCount++] = Corner(centre, -1f, -1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, 1f, -1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, 1f, 1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, -1f, 1f, field, weighted);
    }

    private static FieldStampVertex Corner(Vector2 centre, float x, float y, Color field, Color weighted) => new()
    {
        Position = new Vector3(centre.X + x * KERNEL_RADIUS * CELL_SIZE, centre.Y + y * KERNEL_RADIUS * CELL_SIZE, 0f),
        Field = field,
        Attributes = weighted,
        TextureCoordinate = new Vector2(x * KERNEL_RADIUS, y * KERNEL_RADIUS),
    };

    private static void EnsureCapacity(int quads)
    {
        if (vertices.Length >= quads * 4)
            return;

        int capacity = Math.Max(256, vertices.Length / 4);

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

    private static void CompositeOver(Player player)
    {
        foreach (Body body in bodies)
        {
            if (body.Player != player)
                continue;

            try
            {
                Composite(in body);
            }
            catch (Exception exception)
            {
                ResidueSystem.ReportFailure(exception);
            }

            return;
        }
    }

    /// <summary>Draws a body's slot over its sprite. Runs after the player's own batch has ended.</summary>
    private static void Composite(in Body body)
    {
        if (mask is null || canvas is null || light is null
            || ResidueSystem.Get<BurnedResidue>() is not { } burn
            || burn.Shader is not { Value: { } effect })
            return;

        var slotOrigin = new Vector2(body.Slot.X, body.Slot.Y);
        Matrix bodyToAtlas = Matrix.CreateTranslation(slotOrigin.X, slotOrigin.Y, 0f);

        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["CanvasScreenSize"]?.SetValue(new Vector2(ATLAS_SIZE));
        effect.Parameters["WorldToScreen"]?.SetValue(bodyToAtlas);
        effect.Parameters["ScreenToWorld"]?.SetValue(Matrix.Invert(bodyToAtlas));
        effect.Parameters["MaskOrigin"]?.SetValue(-slotOrigin);
        effect.Parameters["MaskTexel"]?.SetValue(new Vector2(1f / ATLAS_SIZE));
        effect.Parameters["CellSize"]?.SetValue(2f);
        effect.Parameters["uCanvasLight"]?.SetValue(light);
        effect.Parameters["uMaskTex"]?.SetValue(mask);

        burn.ApplyShaderParameters(effect);
        BodyFireTest.ApplyLook(effect, body.Player);

        var destination = new Rectangle(body.Anchor.X - SLOT_SIZE / 2, body.Anchor.Y - SLOT_SIZE / 2, SLOT_SIZE, SLOT_SIZE);
        SpriteBatch spriteBatch = Main.spriteBatch;

        // The same matrix the player was just drawn under, which carries the zoom and any flip for reversed gravity.
        spriteBatch.Begin(SpriteSortMode.Immediate, burn.BlendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        try
        {
            effect.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(canvas, destination, body.Slot, Color.White);
        }
        finally
        {
            spriteBatch.End();

            // The effect outlives both textures, and FNA rebinds texture parameters on every pass Apply with no disposal check.
            effect.Parameters["uCanvasLight"]?.SetValue((Texture?)null);
            effect.Parameters["uMaskTex"]?.SetValue((Texture?)null);
        }
    }
}
