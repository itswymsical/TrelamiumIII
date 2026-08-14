using ReLogic.Content;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Graphics.Metaballs;

/// <summary>
///     A single family of metaballs: its own node pool, its own field target, its own shader.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is two steps. Every node is stamped as a soft radial blob into an
///         off-screen target at half resolution (<see cref="MetaballRenderTargetContent" />),
///         additively, so overlapping blobs sum into a continuous density field. That target is
///         then composited to the screen through <see cref="Shader" />, which cuts the field at a
///         threshold. Two blobs that merely sit near each other cross the threshold in the gap
///         between them and fuse into one silhouette -- which is the whole trick.
///     </para>
///     <para>
///         Layers are purely visual. Nodes do not damage, and nothing here reads or writes entity
///         state; gameplay drives the look by spawning nodes, never the reverse.
///     </para>
/// </remarks>
public abstract class MetaballLayer
{
    protected abstract Asset<Effect> Shader { get; }

    /// <summary>The soft radial falloff stamped once per node. Anything with a smooth edge works.</summary>
    protected abstract string TexturePath { get; }

    /// <summary>0.0 = buoyant, 1.0 = dense. Default for nodes that do not specify one.</summary>
    protected virtual float DefaultDensity => 0.25f;

    /// <summary>0.0 = flows freely, 1.0 = sluggish. Default for nodes that do not specify one.</summary>
    protected virtual float DefaultViscosity => 0.15f;

    /// <summary>Upward acceleration applied to buoyant nodes. Positive = force in -Y.</summary>
    protected virtual float BuoyancyForce => 0.055f;

    /// <summary>Downward acceleration applied to dense nodes. Positive = force in +Y.</summary>
    protected virtual float GravityScale => 0.07f;

    /// <summary>Per-tick velocity multiplier. Air resistance.</summary>
    protected virtual float Drag => 0.984f;

    /// <summary>Life subtracted per tick under normal physics.</summary>
    protected virtual float LifeDecay => 1f;

    /// <summary>Extra life subtracted per tick while the node is jammed inside geometry.</summary>
    protected virtual float EnclosedLifePenalty => 2f;

    /// <summary>
    ///     Ceiling on live nodes. Past it the oldest are dropped, so a runaway emitter costs a
    ///     thinner cloud rather than the frame rate.
    /// </summary>
    protected virtual int MaxNodes => 400;

    protected virtual BlendState CompositeBlendState => BlendState.AlphaBlend;

    protected readonly List<MetaballNode> nodes = [];

    public int NodeCount => nodes.Count;

    private MetaballRenderTargetContent? target;
    private Asset<Effect>? shader;
    private Asset<Texture2D>? radialTexture;

    public virtual void Load()
    {
        if (Main.dedServ)
            return;

        shader = Shader;
        radialTexture = ModContent.Request<Texture2D>(TexturePath);
        target = new MetaballRenderTargetContent(this) { PixelScale = 2 };
    }

    public virtual void Unload()
    {
        target?.Reset();
        target = null;
        shader = null;
        radialTexture = null;
        nodes.Clear();
    }

    /// <summary>
    ///     Spawns a node. Pass a negative density or viscosity to take the layer's default.
    /// </summary>
    public void SpawnNode(
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        float density = -1f,
        float viscosity = -1f)
    {
        if (Main.dedServ || life <= 0f)
            return;

        TrimToBudget();

        nodes.Add(new MetaballNode
        {
            Position = worldPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Density = density < 0f ? DefaultDensity : density,
            Viscosity = viscosity < 0f ? DefaultViscosity : viscosity,
            AnchorNPC = MetaballNode.NO_ANCHOR,
        });
    }

    /// <summary>
    ///     Spawns a node pinned to <paramref name="host" /> for <paramref name="anchorTicks" />,
    ///     after which it releases and drifts out the rest of its life.
    /// </summary>
    /// <remarks>
    ///     The offset is captured once, at spawn, so the cloud keeps its shape as the host moves
    ///     instead of being re-scattered every tick.
    /// </remarks>
    public void SpawnAnchoredNode(
        NPC host,
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        int anchorTicks,
        float density = -1f,
        float viscosity = -1f)
    {
        if (Main.dedServ || host is not { active: true } || life <= 0f)
            return;

        TrimToBudget();

        nodes.Add(new MetaballNode
        {
            Position = worldPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Density = density < 0f ? DefaultDensity : density,
            Viscosity = viscosity < 0f ? DefaultViscosity : viscosity,
            AnchorNPC = host.whoAmI,
            AnchorOffset = worldPosition - host.Center,
            AnchorTicks = anchorTicks,
        });
    }

    /// <summary>Drops the oldest nodes until there is room for one more.</summary>
    private void TrimToBudget()
    {
        int excess = nodes.Count - MaxNodes + 1;

        if (excess > 0)
            nodes.RemoveRange(0, Math.Min(excess, nodes.Count));
    }

    public void UpdateNodes()
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];

            UpdateNode(ref node, i);
            ApplyLighting(ref node);

            if (node.Life <= 0f)
            {
                nodes.RemoveAt(i);
                continue;
            }

            nodes[i] = node;
        }
    }

    protected virtual void UpdateNode(ref MetaballNode node, int index)
    {
        if (node.IsAnchored)
            FollowAnchor(ref node);
        else
        {
            ApplyPhysics(ref node);
            ResolveTileCollision(ref node);
        }

        node.Life -= LifeDecay;
    }

    /// <summary>
    ///     Rides the host: position is the captured offset from its centre, plus a small drift so
    ///     the cloud breathes instead of sitting welded in place.
    /// </summary>
    private static void FollowAnchor(ref MetaballNode node)
    {
        NPC host = Main.npc[node.AnchorNPC];

        if (host is not { active: true })
        {
            Release(ref node);
            return;
        }

        node.AnchorTicks--;
        node.AnchorOffset += node.Velocity;
        node.Velocity *= 0.9f;
        node.Position = host.Center + node.AnchorOffset;

        if (node.AnchorTicks <= 0)
            Release(ref node);
    }

    /// <summary>
    ///     Cuts a node loose from its host, handing it the host's momentum so it slides off along
    ///     the direction of travel rather than stopping dead in the air.
    /// </summary>
    private static void Release(ref MetaballNode node)
    {
        if (node.AnchorNPC != MetaballNode.NO_ANCHOR)
        {
            NPC host = Main.npc[node.AnchorNPC];

            if (host is { active: true })
                node.Velocity += host.velocity * 0.35f;
        }

        node.AnchorNPC = MetaballNode.NO_ANCHOR;
        node.AnchorTicks = 0;
    }

    protected virtual void ApplyLighting(ref MetaballNode node) { }

    private void ApplyPhysics(ref MetaballNode node)
    {
        float netVertical = MathHelper.Lerp(-BuoyancyForce, GravityScale, node.Density);
        node.Velocity.Y += netVertical;

        float drag = MathHelper.Lerp(Drag, Drag * 0.96f, node.Viscosity);
        node.Velocity *= drag;
    }

    /// <summary>
    ///     Resolves the node against its own tile and the four cardinal neighbours, applies lateral
    ///     spread on surface contact, then moves it by the corrected velocity.
    /// </summary>
    /// <remarks>
    ///     Spread is what stops a cloud from stacking into a column against a floor: nodes that
    ///     land shed their vertical momentum sideways and creep along the surface instead.
    /// </remarks>
    private void ResolveTileCollision(ref MetaballNode node)
    {
        const int TILE_SIZE = 16;
        const float RESTITUTION = 0.02f; // minimal bounce; gas should settle, not patter
        const float SPREAD_BASE = 0.40f;

        node.OnCeiling = false;
        node.OnFloor = false;
        node.OnWall = false;

        int cx = (int)(node.Position.X / TILE_SIZE);
        int cy = (int)(node.Position.Y / TILE_SIZE);

        if (IsSolidTile(cx, cy))
        {
            node.EnclosedTicks++;
            OnEnclosed(ref node);

            // Buoyant gas escapes upward, dense gas sinks; either way it leaves the wall.
            float ejectDir = node.Density < 0.5f ? -1f : 1f;
            node.Velocity.Y = ejectDir * 0.8f;
            node.Life -= EnclosedLifePenalty;

            node.Position += node.Velocity;
            return;
        }

        node.EnclosedTicks = 0;

        bool solidAbove = IsSolidTile(cx, cy - 1);
        bool solidBelow = IsSolidTile(cx, cy + 1);
        bool solidLeft = IsSolidTile(cx - 1, cy);
        bool solidRight = IsSolidTile(cx + 1, cy);

        if (solidAbove && node.Velocity.Y < 0f)
        {
            node.Velocity.Y = MathF.Abs(node.Velocity.Y) * RESTITUTION;
            node.OnCeiling = true;
            ApplyLateralSpread(ref node, solidLeft, solidRight, SPREAD_BASE);
            OnTileContact(ref node, cx, cy - 1, Framing.GetTileSafely(cx, cy - 1));
        }

        if (solidBelow && node.Velocity.Y > 0f)
        {
            node.Velocity.Y = -MathF.Abs(node.Velocity.Y) * RESTITUTION;
            node.Position.Y = (cy + 1) * TILE_SIZE;
            node.OnFloor = true;
            ApplyLateralSpread(ref node, solidLeft, solidRight, SPREAD_BASE);
            OnTileContact(ref node, cx, cy + 1, Framing.GetTileSafely(cx, cy + 1));
        }

        if (solidLeft && node.Velocity.X < 0f)
        {
            node.Velocity.X = MathF.Abs(node.Velocity.X) * RESTITUTION;
            node.OnWall = true;
            OnTileContact(ref node, cx + 1, cy, Framing.GetTileSafely(cx + 1, cy));
        }

        if (solidRight && node.Velocity.X > 0f)
        {
            node.Velocity.X = -MathF.Abs(node.Velocity.X) * RESTITUTION;
            node.OnWall = true;
            OnTileContact(ref node, cx - 1, cy, Framing.GetTileSafely(cx - 1, cy));
        }

        CheckLiquidContactAt(ref node, cx, cy);
        CheckLiquidContactAt(ref node, cx, cy - 1);
        CheckLiquidContactAt(ref node, cx, cy + 1);

        node.Position += node.Velocity;
    }

    private static void ApplyLateralSpread(ref MetaballNode node, bool solidLeft, bool solidRight, float force)
    {
        float spread = force * (1f - node.Viscosity);

        if (!solidLeft && !solidRight)
        {
            // Both sides open: keep the lateral momentum it already had, plus a little jitter.
            float momentum = MathHelper.Clamp(node.Velocity.X, -spread * 0.5f, spread * 0.5f);
            node.Velocity.X += momentum + (Main.rand.NextFloat() - 0.5f) * spread * 0.3f;
        }
        else if (!solidLeft)
            node.Velocity.X -= spread * Main.rand.NextFloat(0.6f, 1f);
        else if (!solidRight)
            node.Velocity.X += spread * Main.rand.NextFloat(0.6f, 1f);
        else
        {
            // Nowhere to go: it is in a one-tile pocket, so let it die off early.
            node.Velocity.X += (Main.rand.NextFloat() - 0.5f) * 0.07f;
            node.Life -= 1f;
        }
    }

    private void CheckLiquidContactAt(ref MetaballNode node, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
            return;

        Tile tile = Framing.GetTileSafely(tileX, tileY);

        if (tile.LiquidAmount > 0)
            OnLiquidContact(ref node, tileX, tileY, tile);
    }

    protected virtual void OnTileContact(ref MetaballNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnLiquidContact(ref MetaballNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnEnclosed(ref MetaballNode node) { }

    /// <summary>Stamps every node into the off-screen field target.</summary>
    internal void DrawNodes(SpriteBatch spriteBatch)
    {
        if (radialTexture is null)
            return;

        Texture2D texture = radialTexture.Value;
        Vector2 origin = texture.Size() / 2f;

        foreach (var node in nodes)
        {
            Vector2 screenPosition = node.Position - Main.screenPosition;

            // Overdriven past white on purpose: the shader thresholds the summed field, and a
            // node that only just reaches 1.0 at its centre never fuses with its neighbours.
            Color nodeColor = Color.White * node.Alpha * 1.25f;

            spriteBatch.Draw(
                texture,
                screenPosition,
                null,
                nodeColor,
                0f,
                origin,
                node.Radius * 2f / texture.Width,
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>Accumulates the node blobs into the off-screen field target.</summary>
    public void AccumulateField()
    {
        if (target is null || nodes.Count == 0 || Main.gameMenu || Main.dedServ)
            return;

        target.Request();
        target.PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);
    }

    /// <summary>Composites the field target to the screen through the layer's shader.</summary>
    public void CompositeToScreen()
    {
        if (target is null || shader is null || nodes.Count == 0)
            return;

        if (!target.IsReady || target.GetTarget() is not { } field)
            return;

        var spriteBatch = Main.spriteBatch;
        var effect = shader.Value;
        var device = Main.graphics.GraphicsDevice;

        ApplyShaderParameters(effect);

        // PointClamp: the field is deliberately half-resolution, and the shader snaps its decisions
        // to that grid. Filtering it back up would soften exactly the chunky edge we are after.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            CompositeBlendState,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        BindAdditionalTextures(device);
        effect.CurrentTechnique.Passes[0].Apply();

        spriteBatch.Draw(field, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, target.PixelScale, SpriteEffects.None, 0f);

        spriteBatch.End();
    }

    protected virtual void BindAdditionalTextures(GraphicsDevice device) { }

    protected virtual void ApplyShaderParameters(Effect effect) { }

    /// <summary>
    ///     Averages world light over a sample of the nodes.
    /// </summary>
    /// <remarks>
    ///     The composite is one full-screen draw, so the shader gets one light value for the whole
    ///     field rather than a per-node one. Sampling a fixed count keeps the cost flat no matter
    ///     how large the cloud grows.
    /// </remarks>
    protected Vector3 SampleAverageLight(int samples = 16)
    {
        if (nodes.Count == 0)
            return Vector3.One;

        int stride = Math.Max(1, nodes.Count / samples);
        Vector3 light = Vector3.Zero;
        int taken = 0;

        for (int i = 0; i < nodes.Count; i += stride)
        {
            light += Lighting.GetSubLight(nodes[i].Position);
            taken++;
        }

        return taken == 0 ? Vector3.One : light / taken;
    }

    protected static bool IsSolidTile(int tileX, int tileY)
    {
        // Out of bounds counts as solid so nodes cannot escape the world and simulate forever.
        if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
            return true;

        return WorldGen.SolidTile(tileX, tileY);
    }
}
