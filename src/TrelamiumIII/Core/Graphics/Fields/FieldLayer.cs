using ReLogic.Content;
using System.Collections.Generic;
using TrelamiumIII.Common.Configs;

namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     A single family of blobs: its own node pool, its own field target, its own shader.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is two steps. Every node is stamped as a soft radial blob into an off-screen
///         target at half resolution, additively, so overlapping blobs sum into a continuous density
///         field. That target is then composited to the screen through <see cref="Shader" />, which
///         cuts the field at a threshold. Two blobs that merely sit near each other cross the
///         threshold in the gap between them and fuse into one silhouette, which is the whole trick.
///     </para>
///     <para>
///         This layer is the <b>visual</b> half. Its nodes drift, collide with terrain and are drawn;
///         they do not damage anything and nothing here reads or writes entity state. A layer that
///         should act on the world extends <c>GasField</c>, which adds emitters, entity contact
///         sweeps, ownership and the accumulated tile field on top of everything here.
///     </para>
/// </remarks>
public abstract class FieldLayer
{
    /// <summary>Composite order, low first. Layers with the same value fall back to discovery order.</summary>
    /// <remarks>
    ///     The composite is one full-screen draw per layer over the finished scene, so this is
    ///     literally what sits on top of what. It used to be unanswerable: the two systems this
    ///     merges each hooked the frame separately, so whether gas drew over metaballs came down to
    ///     which <see cref="ModSystem" /> loaded first, and within each system the order was
    ///     whatever reflection happened to return.
    /// </remarks>
    public virtual int LoadOrder => 100;

    protected abstract Asset<Effect> Shader { get; }

    /// <summary>The soft radial falloff stamped once per node. Anything with a smooth edge works.</summary>
    protected abstract string TexturePath { get; }

    /// <summary>0.0 = buoyant, 1.0 = dense. Default for nodes that do not specify one.</summary>
    protected virtual float DefaultDensity => 0.25f;

    /// <summary>0.0 = flows freely, 1.0 = sluggish. Default for nodes that do not specify one.</summary>
    protected virtual float DefaultViscosity => 0.15f;

    /// <summary>Default node temperature in Celsius.</summary>
    protected virtual float DefaultTemperature => 20f;

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
    ///     Whether landing on a floor costs <see cref="EnclosedLifePenalty" /> as well.
    /// </summary>
    /// <remarks>
    ///     What separates something that pools from something that creeps. A liquid that has reached
    ///     the ground has arrived and should start going away; a gas that has reached the ground
    ///     spreads along it and carries on. Set on the layer rather than read off the node's own
    ///     density, because it is a statement about the material, not about one blob of it.
    /// </remarks>
    protected virtual bool PoolsOnFloor => false;

    /// <summary>
    ///     Ceiling on live nodes. Past it the oldest are dropped, so a runaway emitter costs a
    ///     thinner cloud rather than the frame rate.
    /// </summary>
    protected virtual int BaseMaxNodes => 400;

    protected int MaxNodes => (int)(BaseMaxNodes * ModContent.GetInstance<VFXConfig>().MetaballCountMultiplier);

    protected virtual BlendState CompositeBlendState => BlendState.AlphaBlend;

    /// <summary>
    ///     Filtering applied to the stamp textures as they are summed into the field.
    /// </summary>
    /// <remarks>
    ///     Point keeps the field on the same chunky grid the shader quantises its decisions to;
    ///     linear softens the blobs before they are ever thresholded, which reads as a smoother,
    ///     less pixel-art silhouette. The two systems this merges disagreed, so it stays a per-layer
    ///     choice rather than becoming a silent change to half the layers.
    /// </remarks>
    protected internal virtual SamplerState StampSampler => SamplerState.PointClamp;

    /// <summary>
    ///     Whether this layer still simulates with no screen to draw to.
    /// </summary>
    /// <remarks>
    ///     A purely visual layer has nothing to do on a dedicated server and refuses to spawn nodes
    ///     there at all. A layer that acts on the world does: the accumulated tile field leaves
    ///     residues, converts liquid and scorches terrain, and that is world state rather than
    ///     decoration.
    /// </remarks>
    protected virtual bool SimulatesHeadless => false;

    protected readonly List<FieldNode> nodes = [];

    public int NodeCount => nodes.Count;

    /// <summary>Drops every live node at once, for an emitter that has ended abruptly.</summary>
    /// <remarks>
    ///     Nodes normally expire on their own. 
    /// </remarks>
    public void ClearNodes() => nodes.Clear();

    private FieldTargetContent? target;
    private Asset<Effect>? shader;
    private Asset<Texture2D>? radialTexture;

    public virtual void Load()
    {
        if (Main.dedServ && !SimulatesHeadless)
            return;

        shader = Shader;
        radialTexture = ModContent.Request<Texture2D>(TexturePath);

        if (!Main.dedServ)
            target = new FieldTargetContent(this) { PixelScale = 2 };
    }

    public virtual void Unload()
    {
        target?.Free();
        target = null;
        shader = null;
        radialTexture = null;
        nodes.Clear();
    }

    /// <summary>
    ///     Spawns a node. Pass a negative density, viscosity or temperature to take the layer's
    ///     default.
    /// </summary>
    /// <param name="owner">
    ///     The player whose weapon put this node here, or <see cref="FieldNode.NoOwner" /> for a node
    ///     the world produced by itself. Ignored by layers that do not act on anything.
    /// </param>
    public void SpawnNode(
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        float density = -1f,
        float viscosity = -1f,
        float temperature = -1f,
        int owner = FieldNode.NoOwner)
    {
        if (life <= 0f || (Main.dedServ && !SimulatesHeadless))
            return;

        TrimToBudget();

        var node = new FieldNode
        {
            Position = worldPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Density = density < 0f ? DefaultDensity : density,
            Viscosity = viscosity < 0f ? DefaultViscosity : viscosity,
            Temperature = temperature < 0f ? DefaultTemperature : temperature,
            Owner = owner,
            AnchorHost = null,
        };

        OnNodeSpawn(ref node);
        nodes.Add(node);
    }

    /// <summary>
    ///     Spawns a node pinned to <paramref name="host" /> for <paramref name="anchorTicks" />,
    ///     after which it releases and drifts out the rest of its life.
    /// </summary>
    /// <remarks>
    ///     The offset is captured once, at spawn, so the cloud keeps its shape as the host moves
    ///     instead of being re-scattered every tick.
    ///     <para>
    ///         Takes an <see cref="Entity" /> rather than one overload per host kind: everything the
    ///         anchor needs is on the base, and a second copy of this body is exactly how a player
    ///         host ended up being looked up in the NPC array.
    ///     </para>
    /// </remarks>
    public void SpawnAnchoredNode(
        Entity host,
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        int anchorTicks,
        float density = -1f,
        float viscosity = -1f,
        float temperature = -1f,
        int owner = FieldNode.NoOwner)
    {
        if (host is not { active: true } || life <= 0f || (Main.dedServ && !SimulatesHeadless))
            return;

        TrimToBudget();

        var node = new FieldNode
        {
            Position = worldPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Density = density < 0f ? DefaultDensity : density,
            Viscosity = viscosity < 0f ? DefaultViscosity : viscosity,
            Temperature = temperature < 0f ? DefaultTemperature : temperature,
            Owner = owner,
            AnchorHost = host,

            // Captured against the same anchor FollowAnchor rides, or the first frame would jump by
            // whatever the host's graphical offset happened to be.
            AnchorOffset = worldPosition - VisualCenter(host),
            AnchorTicks = anchorTicks,
        };

        OnNodeSpawn(ref node);
        nodes.Add(node);
    }

    /// <summary>Called on a fully built node before it enters the list, so it can be adjusted.</summary>
    protected virtual void OnNodeSpawn(ref FieldNode node) { }

    /// <summary>Drops the oldest nodes until there is room for one more.</summary>
    private void TrimToBudget()
    {
        int excess = nodes.Count - MaxNodes + 1;

        if (excess > 0)
            nodes.RemoveRange(0, Math.Min(excess, nodes.Count));
    }

    /// <summary>Ticks with no nodes before the field target is handed back.</summary>
    /// <remarks>
    ///     Long enough that a weapon being fired in bursts does not churn a screen-sized surface
    ///     between every shot, short enough that a layer used once in a boss fight is not still
    ///     holding one an hour later. Both systems this merges held theirs for the rest of the
    ///     session once a layer had fired a single node.
    /// </remarks>
    private const int IDLE_TICKS_BEFORE_RELEASE = 300;

    private int idleTicks;

    /// <summary>Advances the whole layer for one tick.</summary>
    public virtual void Update()
    {
        UpdateNodes();
        ReleaseTargetIfIdle();
    }

    private void ReleaseTargetIfIdle()
    {
        if (nodes.Count > 0)
        {
            idleTicks = 0;
            return;
        }

        if (target is not null && ++idleTicks >= IDLE_TICKS_BEFORE_RELEASE)
        {
            target.Free();
            idleTicks = 0;
        }
    }

    protected void UpdateNodes()
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

    protected virtual void UpdateNode(ref FieldNode node, int index)
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
    private static void FollowAnchor(ref FieldNode node)
    {
        if (node.AnchorHost is not { active: true } host)
        {
            Release(ref node);
            return;
        }

        node.AnchorTicks--;
        node.AnchorOffset += node.Velocity;
        node.Velocity *= 0.9f;
        node.Position = VisualCenter(host) + node.AnchorOffset;

        if (node.AnchorTicks <= 0)
            Release(ref node);
    }

    /// <summary>
    ///     Where a host is actually drawn, which is not always where it is.
    /// </summary>
    /// <remarks>
    ///     <c>Center</c> is the hitbox centre, and both players and NPCs are drawn offset from it by
    ///     <c>gfxOffY</c> -- the smoothing applied while stepping up half blocks, slopes and stairs.
    ///     It is zero standing still and up to a tile while moving over uneven ground, so a cloud
    ///     anchored to <c>Center</c> alone sits correctly until its host starts walking and then
    ///     visibly detaches, which is exactly the sort of bug that looks like a bad spawn offset.
    /// </remarks>
    private static Vector2 VisualCenter(Entity host) => host switch
    {
        Player player => player.Center + new Vector2(0f, player.gfxOffY),
        NPC npc => npc.Center + new Vector2(0f, npc.gfxOffY),
        _ => host.Center,
    };

    /// <summary>
    ///     Cuts a node loose from its host, handing it the host's momentum so it slides off along
    ///     the direction of travel rather than stopping dead in the air.
    /// </summary>
    private static void Release(ref FieldNode node)
    {
        if (node.AnchorHost is { active: true } host)
            node.Velocity += host.velocity * 0.35f;

        node.AnchorHost = null;
        node.AnchorTicks = 0;
    }

    protected virtual void ApplyLighting(ref FieldNode node) { }

    private void ApplyPhysics(ref FieldNode node)
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
    private void ResolveTileCollision(ref FieldNode node)
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

            if (PoolsOnFloor)
                node.Life -= EnclosedLifePenalty;

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

    private static void ApplyLateralSpread(ref FieldNode node, bool solidLeft, bool solidRight, float force)
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

    private void CheckLiquidContactAt(ref FieldNode node, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
            return;

        Tile tile = Framing.GetTileSafely(tileX, tileY);

        if (tile.LiquidAmount > 0)
            OnLiquidContact(ref node, tileX, tileY, tile);
    }

    protected virtual void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnEnclosed(ref FieldNode node) { }

    /// <summary>The soft falloff every node is stamped with, or null before it has loaded.</summary>
    internal Texture2D? StampTexture => radialTexture is { IsLoaded: true } asset ? asset.Value : null;

    internal FieldStampVertex[] StampVertices = [];

    internal short[] StampIndices = [];

    /// <summary>
    ///     The layer's temperature in Celsius at each end of its 0..1 encoding.
    /// </summary>
    /// <remarks>
    ///     A single channel cannot hold a temperature, only a position between two of them, and the
    ///     useful span is not the same for frost and for thermite. Each layer says what its own
    ///     scale means so the shader can read one number and still be answering a question about
    ///     that material.
    /// </remarks>
    protected virtual Vector2 TemperatureRange => new(0f, 1200f);

    /// <summary>
    ///     Speed, in pixels per tick, that encodes as a full-strength flow.
    /// </summary>
    /// <remarks>
    ///     Flow rides a bias-encoded channel and so has to arrive in -1..1. Normalising against a
    ///     per-layer reference rather than a global one keeps the encoding usable across materials
    ///     that move at very different speeds: a drifting mist saturating the channel would read as
    ///     identical to a burst arm, and the advection it drives would lose all of its contrast.
    /// </remarks>
    protected virtual float FlowReferenceSpeed => 6f;

    /// <summary>How tightly a node is held to something, 0..1. Free-drifting nodes are 0.</summary>
    protected virtual float NodeBind(in FieldNode node) => node.IsAnchored ? 1f : 0f;

    /// <summary>A layer-defined sub-material, 0..1. Lets one field carry more than one look.</summary>
    protected virtual float NodeVariant(in FieldNode node) => 0f;

    /// <summary>
    ///     Packs one node into the eight channels the two targets carry.
    /// </summary>
    /// <remarks>
    ///     Everything is scaled by <see cref="FieldStamp.Weight" /> so the shader can recover it by
    ///     dividing against coverage: see <see cref="FieldStamp" /> for why that division is the
    ///     only correct way to read a value back out of an additive sum. Override to put something
    ///     else in a channel; the weighting is the part that must not change.
    /// </remarks>
    protected virtual void EncodeNode(in FieldNode node, out Color field, out Color attributes)
    {
        float weight = FieldStamp.Weight(node.Alpha);

        Vector2 span = TemperatureRange;
        float temperature = span.Y - span.X <= 0f ? 0f : MathHelper.Clamp((node.Temperature - span.X) / (span.Y - span.X), 0f, 1f);

        float reference = MathF.Max(FlowReferenceSpeed, 0.0001f);
        var flow = new Vector2(
            MathHelper.Clamp(node.Velocity.X / reference, -1f, 1f),
            MathHelper.Clamp(node.Velocity.Y / reference, -1f, 1f));

        field = FieldStamp.Field(weight, FieldStamp.Brightness(node.Position), temperature, 1f - node.Alpha);
        attributes = FieldStamp.Attributes(weight, flow, NodeBind(in node), NodeVariant(in node));
    }

    /// <summary>
    ///     Builds one quad per node into <see cref="StampVertices" />, returning the vertex count.
    /// </summary>
    /// <remarks>
    ///     This replaced a <see cref="SpriteBatch" /> draw per node. The saving in draw calls is
    ///     real but incidental; the reason is that a sprite vertex carries one colour and the field
    ///     needs two. See <see cref="FieldStampVertex" />.
    /// </remarks>
    internal int BuildStampGeometry()
    {
        if (StampTexture is null || nodes.Count == 0)
            return 0;

        EnsureGeometryCapacity(nodes.Count);

        int v = 0;

        foreach (var node in nodes)
        {
            EncodeNode(in node, out Color field, out Color attributes);

            Vector2 centre = node.Position - Main.screenPosition;
            float radius = node.Radius;

            Write(ref v, centre.X - radius, centre.Y - radius, 0f, 0f, field, attributes);
            Write(ref v, centre.X + radius, centre.Y - radius, 1f, 0f, field, attributes);
            Write(ref v, centre.X + radius, centre.Y + radius, 1f, 1f, field, attributes);
            Write(ref v, centre.X - radius, centre.Y + radius, 0f, 1f, field, attributes);
        }

        return v;
    }

    private void Write(ref int index, float x, float y, float u, float w, Color field, Color attributes)
    {
        StampVertices[index++] = new FieldStampVertex
        {
            Position = new Vector3(x, y, 0f),
            Field = field,
            Attributes = attributes,
            TextureCoordinate = new Vector2(u, w),
        };
    }

    /// <summary>Grows the geometry buffers to hold <paramref name="quads" />, in powers of two.</summary>
    /// <remarks>
    ///     The index pattern never changes, so it is written once per growth rather than per frame.
    ///     Doubling means a layer that ramps up to its budget reallocates a handful of times over
    ///     its whole life instead of on the frames its cloud happens to be growing.
    /// </remarks>
    private void EnsureGeometryCapacity(int quads)
    {
        if (StampVertices.Length >= quads * 4)
            return;

        int capacity = Math.Max(64, StampVertices.Length / 4);

        while (capacity < quads)
            capacity *= 2;

        StampVertices = new FieldStampVertex[capacity * 4];
        StampIndices = new short[capacity * 6];

        for (int q = 0; q < capacity; q++)
        {
            int v = q * 4;
            int i = q * 6;

            StampIndices[i + 0] = (short)(v + 0);
            StampIndices[i + 1] = (short)(v + 1);
            StampIndices[i + 2] = (short)(v + 2);
            StampIndices[i + 3] = (short)(v + 0);
            StampIndices[i + 4] = (short)(v + 2);
            StampIndices[i + 5] = (short)(v + 3);
        }
    }

    /// <summary>
    ///     Slack added around the node bounds, in field texels.
    /// </summary>
    /// <remarks>
    ///     The shader reads one texel out from each cell for its outline test, and the FBM erosion
    ///     moves the cut line by a fraction of a texel on top of that. Two would do; four costs
    ///     almost nothing and means a tuning change to <c>NoiseStrength</c> cannot clip the rim.
    /// </remarks>
    private const int BOUNDS_MARGIN_TEXELS = 4;

    /// <summary>
    ///     The region of the field target the live nodes can possibly cover, in target texels, or
    ///     <see cref="Rectangle.Empty" /> when they cover nothing on screen.
    /// </summary>
    /// <remarks>
    ///     The composite used to be a full-screen quad whatever the cloud was doing, so a wisp
    ///     twenty pixels across paid a screen-wide four-octave FBM plus five field taps per pixel.
    ///     <c>discard</c> ends the shader early but does not refund the texture fetches that led to
    ///     it, so the only way not to pay was not to rasterise. A cloud entirely off screen now
    ///     costs nothing at all rather than a full pass that discards every pixel.
    /// </remarks>
    private Rectangle FieldBounds()
    {
        if (target is null || nodes.Count == 0)
            return Rectangle.Empty;

        Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
        Vector2 zoom = Main.GameViewMatrix.Zoom;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var node in nodes)
        {
            // The same transform the target was built under: unzoomed screen offset, then scaled
            // about the screen centre. A radius has to travel through the zoom as well.
            Vector2 centre = (node.Position - Main.screenPosition - half) * zoom + half;
            Vector2 extent = new Vector2(node.Radius) * zoom;

            minX = MathF.Min(minX, centre.X - extent.X);
            minY = MathF.Min(minY, centre.Y - extent.Y);
            maxX = MathF.Max(maxX, centre.X + extent.X);
            maxY = MathF.Max(maxY, centre.Y + extent.Y);
        }

        int scale = target.PixelScale;

        // Snapped outward to whole texels so the sub-rectangle stays on the grid the shader
        // quantises to; a half-texel offset here would shift every cell decision by half a cell.
        int left = (int)MathF.Floor(minX / scale) - BOUNDS_MARGIN_TEXELS;
        int top = (int)MathF.Floor(minY / scale) - BOUNDS_MARGIN_TEXELS;
        int right = (int)MathF.Ceiling(maxX / scale) + BOUNDS_MARGIN_TEXELS;
        int bottom = (int)MathF.Ceiling(maxY / scale) + BOUNDS_MARGIN_TEXELS;

        (int width, int height) = FieldSurface.ScreenSize(scale);

        left = Math.Max(left, 0);
        top = Math.Max(top, 0);
        right = Math.Min(right, width);
        bottom = Math.Min(bottom, height);

        return right <= left || bottom <= top
            ? Rectangle.Empty
            : new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>Accumulates the node blobs into the off-screen field target.</summary>
    public void AccumulateField()
    {
        if (target is null || nodes.Count == 0 || Main.gameMenu || Main.dedServ)
            return;

        if (FieldBounds().IsEmpty)
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

        Rectangle source = FieldBounds();

        if (source.IsEmpty)
            return;

        var spriteBatch = Main.spriteBatch;
        var effect = shader.Value;
        var device = Main.graphics.GraphicsDevice;

        ApplyScreenParameters(effect, target.PixelScale);

        // The second half of the field. A layer that never raises FlowStrength or TemperatureBlend
        // leaves this sampler untouched, which is what keeps the cost off layers that want none of
        // it -- and what lets the interface-space field, which has no attribute target at all, go
        // on using the same shader.
        if (target.Attributes is { } attributes)
            effect.Parameters["uAttrTex"]?.SetValue(attributes);

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

        // Destination is the source scaled by exactly PixelScale, so the sub-rectangle lands on the
        // same pixels the full-screen quad used to. SpriteBatch builds texcoords from the source
        // rectangle against the whole texture, so the uv the shader reads is still absolute -- its
        // world reconstruction and its neighbour taps need no adjusting for the smaller draw.
        var destination = new Rectangle(
            source.X * target.PixelScale,
            source.Y * target.PixelScale,
            source.Width * target.PixelScale,
            source.Height * target.PixelScale);

        spriteBatch.Draw(field, destination, source, Color.White);

        spriteBatch.End();
    }

    protected virtual void BindAdditionalTextures(GraphicsDevice device) { }

    protected virtual void ApplyShaderParameters(Effect effect) { }

    /// <summary>
    ///     Everything the shader needs that is a fact about the screen rather than a choice about
    ///     the material, plus safe defaults for the parameters a layer can leave unset.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Run ahead of <see cref="ApplyShaderParameters" /> so a layer can still override any
    ///         of it. These were copied verbatim into every layer's own block -- thirteen identical
    ///         copies of four lines that cannot differ between layers, because they describe the
    ///         frame and not the effect.
    ///     </para>
    ///     <para>
    ///         The defaults matter more than the tidying. An unset shader parameter is zero, and
    ///         zero is catastrophic for three of these: no <c>FieldGain</c> discards every pixel,
    ///         no <c>NoiseStretch</c> collapses the FBM lookup to a constant, and no
    ///         <c>DepthRange</c> flattens the body. Setting them here means forgetting one costs
    ///         nothing rather than costing the whole layer.
    ///     </para>
    /// </remarks>
    internal static void ApplyScreenParameters(Effect effect, int pixelScale)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(pixelScale / (float)Main.screenWidth, pixelScale / (float)Main.screenHeight));

        // The field target bakes the zoom into its own transform, so a shader reading world
        // position back out of a screen uv has to undo it -- without this it silently assumes 1 and
        // its noise slides across the world as the camera pans.
        effect.Parameters["Zoom"]?.SetValue(Main.GameViewMatrix.Zoom);

        effect.Parameters["FieldGain"]?.SetValue(FieldStamp.Gain);
        effect.Parameters["DepthRange"]?.SetValue(FieldStamp.DepthRange);

        // Isotropic noise and no surface line: gas. A liquid layer sets both.
        effect.Parameters["NoiseStretch"]?.SetValue(Vector2.One);
        effect.Parameters["HighlightAlpha"]?.SetValue(0f);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.75f);

        // Both attribute consumers default off, which is what makes them safe to add. An unbound
        // attribute sampler reads zero, and zero recovers as a flow of (-1, -1) rather than none --
        // so a layer that has not opted in must not reach the sampler at all.
        effect.Parameters["FlowStrength"]?.SetValue(0f);
        effect.Parameters["TemperatureBlend"]?.SetValue(0f);
    }

    /// <summary>
    ///     Averages world light over a sample of the nodes.
    /// </summary>
    /// <remarks>
    ///     The composite is one full-screen draw, so the shader gets one light value for the whole
    ///     field rather than a per-node one. Sampling a fixed count keeps the cost flat no matter
    ///     how large the cloud grows.
    /// </remarks>
    protected Vector3 SampleAverageLight(int samples = 32)
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

    protected static bool HasLiquid(int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
            return false;

        return Framing.GetTileSafely(tileX, tileY).LiquidAmount > 0;
    }
}
