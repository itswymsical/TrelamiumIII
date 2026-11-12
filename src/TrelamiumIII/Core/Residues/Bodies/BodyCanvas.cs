using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>
///     Draws residues on players and NPCs with the shaders they wear on the ground: a body's silhouette takes the place of
///     the terrain mask, and a small grid of cells takes the place of tiles.
/// </summary>
/// <remarks>
///     <para>
///         Bodies share atlases rather than having targets of their own. A canvas is two half-float targets, which is fine
///         per residue and too much per slime in a swarm, and a slot where one screen-wide silhouette would do keeps
///         overlapping bodies from bleeding into each other. When the atlases are full, the bodies left over go uncoated.
///     </para>
///     <para>
///         A body is drawn into its slot three ways. A player goes through the player renderer, and so does any NPC with a
///         <see cref="TerrarianModel" />, off its backing player. Anything else goes through vanilla's own NPC draw.
///     </para>
///     <para>
///         To the shader, "world" here is body space: pixels from the top-left of the slot, offset per body. That pins its
///         noise and shading grid to the body, so they don't swim across it as it walks, and keeps two zombies from burning
///         in step.
///     </para>
/// </remarks>
internal sealed class BodyCanvas : ModSystem
{
    /// <summary>Pixels the mask atlases hold, and that the canvas atlases cover.</summary>
    private const int ATLAS_SIZE = 1024;

    /// <summary>Divisor on the canvas atlases. Shaders decide per 2 pixels, so this gives each decision a texel of its own.</summary>
    private const int CANVAS_SCALE = 2;

    /// <summary>Slot sizes round up to this, so a slot starts on the shading grid and a body's size doesn't flicker with its frames.</summary>
    private const int SLOT_ROUNDING = 16;

    /// <summary>Kernel half-width, in cells.</summary>
    private const float KERNEL_RADIUS = 1.5f;

    /// <summary>Cells stamped past each edge of the grid.</summary>
    private const int CELL_RING = 1;

    /// <summary>Less of a residue than this isn't worth a stamp.</summary>
    private const float FAINTEST_CELL = 0.01f;

    /// <summary>Times each copy of a silhouette is summed into a mask, which lifts translucent bodies like slimes past the shaders' cut.</summary>
    private const int MASK_LAYERS = 3;

    /// <summary>A body more see-through than this is hidden, and a coat would give it away.</summary>
    private const int MAX_ALPHA = 250;

    private const int IDLE_TICKS_BEFORE_RELEASE = 300;

    /// <summary>
    ///     Neither the real draw nor an afterimage. Everything that shouldn't be drawn twice asks for a shadow of exactly zero
    ///     (dust, the held item, its projectile), and only a positive one hides the skin and hair.
    /// </summary>
    private const float CAPTURE_SHADOW = -0.001f;

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

    /// <param name="Slot">Its silhouette, in the silhouette atlas.</param>
    /// <param name="Anchor">Where the middle of its slot lands on the screen, before zoom. Whole pixels, so a slot lays exactly over its sprite.</param>
    /// <param name="FirstCoat">Where its coats start in <see cref="placed" />.</param>
    private readonly record struct Body(Entity Entity, BodyBox Box, Rectangle Slot, Point Anchor, int FirstCoat, int CoatCount);

    /// <param name="Slot">Its mask and canvas, in those atlases' shared pixels.</param>
    private readonly record struct PlacedCoat(BodyCoat Coat, Rectangle Slot);

    /// <summary>Lays square slots into an atlas in rows, first come first served.</summary>
    private struct Shelves
    {
        private int x;

        private int y;

        private int rowHeight;

        public void Reset() => x = y = rowHeight = 0;

        public bool TryPlace(int size, out Rectangle slot)
        {
            if (x + size > ATLAS_SIZE)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            slot = new Rectangle(x, y, size, size);

            if (y + size > ATLAS_SIZE)
                return false;

            x += size;
            rowHeight = Math.Max(rowHeight, size);

            return true;
        }
    }

    /// <summary>Who has a slot this frame.</summary>
    private static readonly List<Body> bodies = [];

    private static readonly List<PlacedCoat> placed = [];

    private static Shelves silhouetteShelves;

    private static Shelves coatShelves;

    private static RenderTarget2D? silhouette;

    private static RenderTarget2D? mask;

    private static RenderTarget2D? canvas;

    private static RenderTarget2D? light;

    /// <summary>What <see cref="Main.GameViewMatrix" /> is during a capture: no zoom and no flip, for draws that restart their batch with it.</summary>
    private static SpriteViewMatrix? captureView;

    private static FieldStampVertex[] vertices = [];

    private static int[] indices = [];

    private static int vertexCount;

    private static int idleTicks;

    /// <summary>A capture runs the NPC draw hook below, which must not composite onto what's being captured.</summary>
    private static bool capturing;

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

            if (FindBody(player, out Body body))
                CompositeCoats(in body);
        };

        On_Main.DrawNPCDirect += static (orig, self, spriteBatch, npc, behindTiles, screenPos) =>
        {
            orig(self, spriteBatch, npc, behindTiles, screenPos);

            if (capturing || !FindBody(npc, out Body body))
                return;

            // In the middle of vanilla's batch, which has to be put back as the pass opened it.
            spriteBatch.End();
            CompositeCoats(in body);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        };
    }

    public override void OnModUnload()
    {
        bodies.Clear();
        placed.Clear();
        captureView = null;
        Free();
    }

    public override void PostUpdateEverything()
    {
        if (bodies.Count > 0 || silhouette is null)
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

    private static bool FindBody(Entity entity, out Body found)
    {
        foreach (Body body in bodies)
        {
            if (body.Entity == entity)
            {
                found = body;
                return true;
            }
        }

        found = default;
        return false;
    }

    /// <summary>Call from <c>Main.CheckMonoliths</c>, before the scene draws, like the tile canvases.</summary>
    private static void Build()
    {
        bodies.Clear();
        placed.Clear();

        if (Main.gameMenu)
            return;

        silhouetteShelves.Reset();
        coatShelves.Reset();

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (player.active && !player.dead && !player.ghost && !player.invis && !player.stoned && !player.ShouldNotDraw)
                TryAddBody(player, player.GetModPlayer<BodyCoatPlayer>().Coats);
        }

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || npc.type <= NPCID.None || npc.hide || npc.alpha > MAX_ALPHA)
                continue;

            TryAddBody(npc, npc.GetGlobalNPC<BodyCoatNPC>().Coats);
        }

        if (bodies.Count == 0)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        try
        {
            EnsureTargets(device);
            CaptureSilhouettes(device);
            SpreadMasks(device);
            StampCells(device);
        }
        catch (Exception exception)
        {
            bodies.Clear();
            placed.Clear();
            ResidueSystem.ReportFailure(exception);
        }
        finally
        {
            device.SetRenderTargets(null);
        }
    }

    private static void TryAddBody(Entity entity, BodyCoats coats)
    {
        if (!coats.IsCoated)
            return;

        BodyBox box = BodyBox.Of(entity);
        int size = (box.Slot + SLOT_ROUNDING - 1) / SLOT_ROUNDING * SLOT_ROUNDING;

        Vector2 onScreen = box.Middle - Main.screenPosition;

        // The unzoomed viewport, a superset of the zoomed one at every zoom Terraria allows.
        if (onScreen.X < -size || onScreen.Y < -size || onScreen.X > Main.screenWidth + size || onScreen.Y > Main.screenHeight + size)
            return;

        if (!silhouetteShelves.TryPlace(size, out Rectangle slot))
            return;

        int firstCoat = placed.Count;

        foreach (BodyCoat coat in coats.All)
        {
            if (coatShelves.TryPlace(size, out Rectangle coatSlot))
                placed.Add(new PlacedCoat(coat, coatSlot));
        }

        var anchor = new Point((int)MathF.Floor(onScreen.X), (int)MathF.Floor(onScreen.Y));

        bodies.Add(new Body(entity, box, slot, anchor, firstCoat, placed.Count - firstCoat));
    }

    private static void EnsureTargets(GraphicsDevice device)
    {
        EnsureMaskTarget(ref silhouette, device);
        EnsureMaskTarget(ref mask, device);

        // Half-float: eight-bit targets clamp the sums flat wherever kernels overlap.
        FieldSurface.Ensure(ref canvas, device, ATLAS_SIZE / CANVAS_SCALE, ATLAS_SIZE / CANVAS_SCALE);
        FieldSurface.Ensure(ref light, device, ATLAS_SIZE / CANVAS_SCALE, ATLAS_SIZE / CANVAS_SCALE);
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
    /// <remarks>
    ///     Moves the screen rather than the batch's matrix, because some draws restart the batch with their own, which comes
    ///     from <see cref="Main.GameViewMatrix" />. Everything they read that way lands in the slot too.
    /// </remarks>
    private static void CaptureSilhouettes(GraphicsDevice device)
    {
        device.SetRenderTarget(silhouette);
        device.Clear(Color.Transparent);

        Vector2 screen = Main.screenPosition;
        SpriteViewMatrix view = Main.GameViewMatrix;

        Main.GameViewMatrix = captureView ??= new SpriteViewMatrix(device);
        capturing = true;

        try
        {
            foreach (Body body in bodies)
            {
                Point middle = body.Slot.Center;

                Main.screenPosition = screen + new Vector2(body.Anchor.X - middle.X, body.Anchor.Y - middle.Y);
                device.ScissorRectangle = body.Slot;

                switch (body.Entity)
                {
                    case Player player:
                        CapturePlayer(player);
                        break;

                    case NPC npc:
                        CaptureNPC(npc);
                        break;
                }
            }
        }
        finally
        {
            capturing = false;
            Main.screenPosition = screen;
            Main.GameViewMatrix = view;
        }
    }

    private static void BeginCapture(SpriteSortMode mode)
        => Main.spriteBatch.Begin(mode, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, ClippedToSlot, null, Main.GameViewMatrix.TransformationMatrix);

    private static void CapturePlayer(Player player)
    {
        // The sprite blinks while immune. A coat shouldn't, as whatever laid it is likely what's hitting it.
        int immuneAlpha = player.immuneAlpha;
        player.immuneAlpha = 0;

        BeginCapture(SpriteSortMode.Immediate);

        try
        {
            Vector2 position = player.position + new Vector2(0f, player.gfxOffY);

            Main.PlayerRenderer.DrawPlayer(Main.Camera, player, position, player.fullRotation, player.fullRotationOrigin, CAPTURE_SHADOW);
        }
        finally
        {
            Main.spriteBatch.End();
            player.immuneAlpha = immuneAlpha;
        }
    }

    private static void CaptureNPC(NPC npc)
    {
        TerrarianModel? model = TerrarianModel.Of(npc);
        int alpha = npc.alpha;

        // The pass adds this around every NPC it draws, and so does this. Alpha only hides the body from itself.
        npc.position += npc.netOffset;
        npc.alpha = 0;

        BeginCapture(model is null ? SpriteSortMode.Deferred : SpriteSortMode.Immediate);

        try
        {
            if (model is not null)
            {
                model.DrawInto(CAPTURE_SHADOW);
            }
            else
            {
                Main.instance.DrawNPCCheckAlt(npc);
                Main.instance.DrawNPCDirect(Main.spriteBatch, npc, npc.behindTiles, Main.screenPosition);
            }
        }
        finally
        {
            Main.spriteBatch.End();
            npc.alpha = alpha;
            npc.position -= npc.netOffset;
        }
    }

    /// <summary>Copies each coat its body's silhouette, spread as far as that residue reaches.</summary>
    private static void SpreadMasks(GraphicsDevice device)
    {
        SpriteBatch spriteBatch = Main.spriteBatch;

        device.SetRenderTarget(mask);
        device.Clear(Color.Transparent);

        foreach (Body body in bodies)
        {
            for (int i = body.FirstCoat; i < body.FirstCoat + body.CoatCount; i++)
            {
                PlacedCoat coat = placed[i];

                device.ScissorRectangle = coat.Slot;
                spriteBatch.Begin(SpriteSortMode.Deferred, Accumulate, SamplerState.PointClamp, DepthStencilState.None, ClippedToSlot);

                foreach (Point offset in coat.Coat.Residue.BodyReach)
                {
                    for (int layer = 0; layer < MASK_LAYERS; layer++)
                        spriteBatch.Draw(silhouette, new Vector2(coat.Slot.X + offset.X, coat.Slot.Y + offset.Y * body.Box.GravDir), body.Slot, Color.White);
                }

                // What's come off it has no silhouette to be cut by. A square will do, as the canvas fades out well inside it.
                foreach (BodyNode node in coat.Coat.Nodes)
                {
                    Point middle = coat.Slot.Center;
                    int reach = (int)node.RadiusNow + 1;

                    spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(middle.X + (int)node.Offset.X - reach, middle.Y + (int)node.Offset.Y - reach, reach * 2, reach * 2), Color.White);
                }

                spriteBatch.End();
            }
        }
    }

    private static void StampCells(GraphicsDevice device)
    {
        if (Assets.Shaders.TileCanvasStamp.Asset is not { Value: { } stamp })
            return;

        vertexCount = 0;

        foreach (Body body in bodies)
        {
            for (int i = body.FirstCoat; i < body.FirstCoat + body.CoatCount; i++)
                WriteCells(in body, placed[i]);
        }

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

    /// <summary>
    ///     Stamps a coat's grid over its body, with a ring past the edge carrying what's at the edge, so that coverage holds up
    ///     across anything the sprite has outside its box and the silhouette is what cuts it.
    /// </summary>
    private static void WriteCells(in Body body, PlacedCoat placedCoat)
    {
        BodyCoat coat = placedCoat.Coat;
        BodyGrid grid = coat.Grid;
        BodyBox box = body.Box;
        var cell = new Vector2(box.Width / (float)BodyGrid.COLUMNS, box.Height / (float)BodyGrid.ROWS);

        Vector2 worldMiddle = body.Anchor.ToVector2() + Main.screenPosition;
        Vector2 slotMiddle = placedCoat.Slot.Center.ToVector2();

        for (int y = -CELL_RING; y < BodyGrid.ROWS + CELL_RING; y++)
        {
            for (int x = -CELL_RING; x < BodyGrid.COLUMNS + CELL_RING; x++)
            {
                float amount = grid.At(x, y);

                if (amount < FAINTEST_CELL)
                    continue;

                var offset = new Vector2((x + 0.5f) * cell.X - box.Width * 0.5f, (y + 0.5f) * cell.Y - box.Height * 0.5f);

                offset *= new Vector2(box.Direction < 0 ? -1f : 1f, box.GravDir);

                Vector2 world = worldMiddle + offset;
                Color lit = Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));

                WriteQuad(slotMiddle + offset, cell, coat.Residue.EncodeBody(amount, coat.Age, coat.Tint), lit);
            }
        }

        foreach (BodyNode node in coat.Nodes)
        {
            Vector2 world = worldMiddle + node.Offset;
            Color lit = Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));
            TileCanvasSample sample = coat.Residue.EncodeBody(node.Amount * node.Fade, coat.Age, coat.Tint);
            var size = new Vector2(node.RadiusNow / KERNEL_RADIUS);

            WriteQuad(slotMiddle + node.Offset, size, sample, lit);
            WriteQuad(slotMiddle + node.Offset, size, sample, lit);
        }
    }

    private static void WriteQuad(Vector2 centre, Vector2 cell, TileCanvasSample sample, Color lit)
    {
        EnsureCapacity(vertexCount / 4 + 1);

        var field = new Color(sample.Coverage, sample.Data.X, sample.Data.Y, sample.Data.Z);
        var weighted = new Color(1f, lit.R / 255f, lit.G / 255f, lit.B / 255f);

        vertices[vertexCount++] = Corner(centre, cell, -1f, -1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, cell, 1f, -1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, cell, 1f, 1f, field, weighted);
        vertices[vertexCount++] = Corner(centre, cell, -1f, 1f, field, weighted);
    }

    private static FieldStampVertex Corner(Vector2 centre, Vector2 cell, float x, float y, Color field, Color weighted) => new()
    {
        Position = new Vector3(centre.X + x * KERNEL_RADIUS * cell.X, centre.Y + y * KERNEL_RADIUS * cell.Y, 0f),
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

    /// <summary>Draws a body's coats over its sprite, oldest first. Runs with no batch open.</summary>
    private static void CompositeCoats(in Body body)
    {
        try
        {
            for (int i = body.FirstCoat; i < body.FirstCoat + body.CoatCount; i++)
                Composite(in body, placed[i]);
        }
        catch (Exception exception)
        {
            ResidueSystem.ReportFailure(exception);
        }
    }

    private static void Composite(in Body body, PlacedCoat placedCoat)
    {
        IResidue residue = placedCoat.Coat.Residue;

        if (mask is null || canvas is null || light is null || residue.Shader is not { Value: { } effect })
            return;

        Rectangle slot = placedCoat.Slot;

        // Even, so the shading grid stays on the same pixels.
        var noise = new Vector2(body.Entity.whoAmI * 58 % 512, body.Entity.whoAmI * 34 % 512);
        var slotOrigin = new Vector2(slot.X, slot.Y);
        Matrix bodyToAtlas = Matrix.CreateTranslation(slotOrigin.X - noise.X, slotOrigin.Y - noise.Y, 0f);

        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["CanvasScreenSize"]?.SetValue(new Vector2(ATLAS_SIZE));
        effect.Parameters["WorldToScreen"]?.SetValue(bodyToAtlas);
        effect.Parameters["ScreenToWorld"]?.SetValue(Matrix.Invert(bodyToAtlas));
        effect.Parameters["MaskOrigin"]?.SetValue(noise - slotOrigin);
        effect.Parameters["MaskTexel"]?.SetValue(new Vector2(1f / ATLAS_SIZE));
        effect.Parameters["CellSize"]?.SetValue(2f);
        effect.Parameters["SurfaceLip"]?.SetValue(0f);
        effect.Parameters["uCanvasLight"]?.SetValue(light);
        effect.Parameters["uMaskTex"]?.SetValue(mask);

        residue.ApplyBodyParameters(effect, body.Box.GravDir, body.Box.Height);

        var destination = new Rectangle(body.Anchor.X - slot.Width / 2, body.Anchor.Y - slot.Height / 2, slot.Width, slot.Height);
        var source = new Rectangle(slot.X / CANVAS_SCALE, slot.Y / CANVAS_SCALE, slot.Width / CANVAS_SCALE, slot.Height / CANVAS_SCALE);
        SpriteBatch spriteBatch = Main.spriteBatch;

        // The same matrix the body was just drawn under, which carries the zoom and any flip for reversed gravity.
        spriteBatch.Begin(SpriteSortMode.Immediate, residue.BlendState, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        try
        {
            effect.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(canvas, destination, source, Color.White);
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