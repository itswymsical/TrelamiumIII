using ReLogic.Content;
using System.Collections.Generic;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Gas;


/// <summary>
/// Abstract base for all gas-type metaball layers.
/// Handles physics, tile collision, surface spreading, entity contact sweeps,
/// and exposes override hooks for alchemy reactions.
/// </summary>
public abstract class GasLayer
{
    internal class GasTypeID
    {
        public static int Liquid = 0;
        public static int Gas = 1;
        public static int Plasma = 2;
    }
    public virtual int LoadOrder => 100;

    protected abstract Asset<Effect> Shader { get; }
    protected abstract string TexturePath { get; }
    /// <summary>
    /// 0 = liquid, 1 = gas, 2 = plasma. Used for alchemy reaction checks.
    /// </summary>
    protected abstract int GasType { get; }

    /// <summary>0.0 = buoyant, 1.0 = dense. Sets node default.</summary>
    protected virtual float DefaultDensity => 0.25f;

    /// <summary>0.0 = flows freely, 1.0 = sluggish. Sets node default.</summary>
    protected virtual float DefaultViscosity => 0.15f;

    /// <summary>Default node temperature in Celsius.</summary>
    protected virtual float DefaultTemperature => 20f;

    /// <summary>
    /// Upward acceleration applied to buoyant nodes.
    /// Positive value = force in -Y direction.
    /// </summary>
    protected virtual float BuoyancyForce => 0.055f;

    /// <summary>
    /// Downward acceleration applied to dense nodes.
    /// Positive value = force in +Y direction.
    /// </summary>
    protected virtual float GravityScale => 0.07f;

    /// <summary>Per-tick velocity multiplier. Controls drag / air resistance.</summary>
    protected virtual float Drag => 0.984f;

    /// <summary>Life subtracted per tick during normal physics.</summary>
    protected virtual float LifeDecay => 1f;

    /// <summary>
    /// Additional life subtracted per tick while in contact with geometry.
    /// <para>
    /// For liquid types (0), this applies <see cref="OnTileContact(ref GasNode, int, int, Tile)"></see>
    /// </para>
    /// </summary>
    protected virtual float EnclosedLifePenalty => 2f;

    protected virtual BlendState CompositeBlendState => BlendState.AlphaBlend;

    protected List<GasNode> gasNodes = new();

    GasRenderTargetContent _gasTarget;
    Asset<Effect> shader;
    Asset<Texture2D> radialTexture;

    public virtual void Load()
    {
        shader = Shader;
        radialTexture = ModContent.Request<Texture2D>(TexturePath);
        _gasTarget = new GasRenderTargetContent(this) { PixelScale = 2 };
    }

    public virtual void Unload()
    {
        _gasTarget?.Reset();
        _gasTarget = null;
        gasNodes.Clear();
    }

    /// <summary>
    /// Stamps every node into the field target, carrying the light it is standing in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stamp is not white any more; it follows <see cref="FieldStamp" />, which is where the
    /// encoding and the reasoning behind it live. The short version: red still sums to exactly the
    /// coverage it always held, and green now carries that coverage weighted by how lit the node
    /// is, so the shader can recover brightness per pixel instead of being handed one number for
    /// the whole screen.
    /// </para>
    /// <para>
    /// The sample costs nothing new -- it was already being taken here and then discarded -- and
    /// this is the last point in the pipeline that still knows where each blob is.
    /// </para>
    /// </remarks>
    internal void DrawNodes(SpriteBatch sb)
    {
        foreach (var node in gasNodes)
        {
            Vector2 screenPos = node.Position - Main.screenPosition;
            float size = node.Radius * 2f;

            // Overdriven past white on purpose: the shader thresholds the summed field, and a node
            // that only just reaches 1.0 at its centre never fuses with its neighbours.
            Color nodeColor = FieldStamp.Lit(node.Position, node.Alpha * 1.25f);

            sb.Draw(
                radialTexture.Value,
                screenPos,
                null,
                nodeColor,
                0f,
                radialTexture.Value.Size() / 2f,
                size / radialTexture.Value.Width,
                SpriteEffects.None,
                0f
            );
        }
    }


    /// <summary>
    /// Spawn a gas node at a world position with explicit physics parameters.
    /// Pass negative density/viscosity to use the layer's defaults.
    /// </summary>
    /// <param name="owner">
    /// The player whose weapon put this node here, or <see cref="GasNode.NoOwner" /> for gas the
    /// world produced by itself.
    /// </param>
    /// <remarks>
    /// Ownership is not about damage credit -- these nodes deal none directly. It is about what the
    /// gas is allowed to do to the person who fired it, and it survives the node: a cloud that
    /// pools into a residue hands its owner to the tile, and the residue hands it back to whatever
    /// it exhales later. Without that chain a thrown flask ends up afflicting the thrower two
    /// seconds after they walked over their own puddle.
    /// </remarks>
    public void SpawnNode(
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        float density = -1f,
        float viscosity = -1f,
        float temperature = -1f,
        int owner = GasNode.NoOwner)
    {

        gasNodes.Add(new GasNode
        {
            Position = worldPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Density = density < 0f ? DefaultDensity : density,
            Viscosity = viscosity < 0f ? DefaultViscosity : viscosity,
            Temperature = temperature < 0f ? DefaultTemperature : temperature,
            GasTypeId = GasType,
            Owner = owner,
            ContactCooldown = Main.rand.Next(0, 20), // stagger initial contact sweeps
        });
    }

    public void UpdateGasNodes()
    {
        for (int i = gasNodes.Count - 1; i >= 0; i--)
        {
            var node = gasNodes[i];
                UpdateGasNode(ref node, i);
                ApplyLighting(ref node);
                gasNodes[i] = node;

                if (node.Life <= 0f)
                    gasNodes.RemoveAt(i);
        }

        UpdateTileField();
    }

    /// <summary>
    /// Set true on layers that change the world, to pay for the tile field. Off by default.
    /// </summary>
    protected virtual bool UsesTileField => false;

    /// <summary>
    /// Coverage a tile must accumulate before <see cref="OnTileField"/> and
    /// <see cref="OnLiquidField"/> fire on it.
    /// </summary>
    /// <remarks>
    /// In units of nodes: 1.0 is one node at full life sitting dead centre on the tile, and partial
    /// overlaps add up toward it. So a value under 1 means a single passing wisp is enough, and a
    /// value above it means the layer has to actually pool before the world notices.
    /// </remarks>
    protected virtual float TileFieldThreshold => 0.9f;

    /// <summary>Ticks between field rebuilds.</summary>
    /// <remarks>
    /// The world does not need re-deciding at 60Hz, and the interval is also what turns a drain
    /// into something the player can watch happen rather than a single frame of state change.
    /// </remarks>
    protected virtual int TileFieldInterval => 5;

    /// <summary>
    /// One tile's worth of accumulated field: how much of the layer is sitting on it, and whose.
    /// </summary>
    /// <remarks>
    /// The owner is the one with the single largest contribution rather than a majority or an
    /// average, because ownership is not a quantity that can be blended -- a tile is either safe
    /// for a given player to stand on or it is not. Weighting by contribution means the answer is
    /// whoever actually put the bulk of the pool there, which is the only reading of "whose puddle
    /// is this" a player would recognise.
    /// </remarks>
    private struct FieldCell
    {
        public float Coverage;

        public int Owner;

        /// <summary>The largest single contribution <see cref="Owner" /> has made, for comparison.</summary>
        public float OwnerWeight;
    }

    /// <summary>Per-tile accumulated node coverage. Rebuilt from scratch each pass.</summary>
    private readonly Dictionary<Point, FieldCell> tileField = [];

    private int tileFieldTimer;

    /// <summary>
    /// A tile the layer has genuinely pooled on, with the coverage that got it over the threshold.
    /// </summary>
    /// <remarks>
    /// This is the hook for anything that changes the world, and it is deliberately not
    /// <see cref="OnTileContact"/>. Per-node contact answers "did this blob touch geometry", which
    /// is the right question for the blob's own physics and the wrong one for the terrain: one node
    /// clipping a corner at the tail of its life is invisible on screen and yet was enough to char
    /// a tile, boil a pool dry, or convert the block underneath. Thresholding an accumulated field
    /// asks the question the renderer asks -- is there visibly something here -- so what burns is
    /// what the player can see burning.
    /// </remarks>
    /// <param name="owner">
    /// The player who put most of this pool here, or <see cref="GasNode.NoOwner" />. Pass it on to
    /// anything left behind, so the residue knows who is allowed to walk through it.
    /// </param>
    protected virtual void OnTileField(int tileX, int tileY, Tile tile, float density, int owner) { }

    /// <inheritdoc cref="OnTileField"/>
    protected virtual void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner) { }

    private void UpdateTileField()
    {
        if (!UsesTileField || gasNodes.Count == 0)
            return;

        if (++tileFieldTimer < TileFieldInterval)
            return;

        tileFieldTimer = 0;

        AccumulateTileField();

        foreach (KeyValuePair<Point, FieldCell> pair in tileField)
        {
            FieldCell cell = pair.Value;

            if (cell.Coverage < TileFieldThreshold)
                continue;

            Tile tile = Framing.GetTileSafely(pair.Key.X, pair.Key.Y);

            if (tile.LiquidAmount > 0)
                OnLiquidField(pair.Key.X, pair.Key.Y, tile, cell.Coverage, cell.Owner);

            if (tile.HasTile && Main.tileSolid[tile.TileType])
                OnTileField(pair.Key.X, pair.Key.Y, tile, cell.Coverage, cell.Owner);
        }
    }

    /// <summary>
    /// Sums every node's coverage into the tiles it overlaps.
    /// </summary>
    /// <remarks>
    /// The falloff is squared so a node contributes its full weight only at the tile it is centred
    /// on and tails off quickly across its radius, which is roughly the shape of the radial gradient
    /// the renderer stamps. It is an approximation of the field the shader thresholds rather than
    /// the same number -- the shader samples an actual texture and squares the sum afterwards -- so
    /// <see cref="TileFieldThreshold"/> is tuned against this, not copied from the shader.
    /// </remarks>
    private void AccumulateTileField()
    {
        tileField.Clear();

        foreach (GasNode node in gasNodes)
        {
            float radius = node.Radius;

            if (radius <= 0f || node.Life <= 0f)
                continue;

            float alpha = MathHelper.Clamp(node.Alpha, 0f, 1f);

            int minX = (int)((node.Position.X - radius) / 16f);
            int maxX = (int)((node.Position.X + radius) / 16f);
            int minY = (int)((node.Position.Y - radius) / 16f);
            int maxY = (int)((node.Position.Y + radius) / 16f);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    float distance = Vector2.Distance(node.Position, new Vector2(x * 16f + 8f, y * 16f + 8f));

                    if (distance >= radius)
                        continue;

                    float falloff = 1f - distance / radius;
                    float contribution = alpha * falloff * falloff;
                    var key = new Point(x, y);

                    tileField.TryGetValue(key, out FieldCell cell);

                    cell.Coverage += contribution;

                    // The <= 0 half is not redundant: a default cell reads as owned by player 0,
                    // so the first contribution has to claim it even if it weighs nothing.
                    if (cell.OwnerWeight <= 0f || contribution > cell.OwnerWeight)
                    {
                        cell.Owner = node.Owner;
                        cell.OwnerWeight = contribution;
                    }

                    tileField[key] = cell;
                }
            }
        }
    }
    protected Vector3 SampleAverageLight(int samples = 32)
    {
        if (gasNodes.Count == 0)
            return Vector3.One;

        int stride = Math.Max(1, gasNodes.Count / samples);
        Vector3 light = Vector3.Zero;
        int taken = 0;

        for (int i = 0; i < gasNodes.Count; i += stride)
        {
            light += Lighting.GetSubLight(gasNodes[i].Position);
            taken++;
        }

        return taken == 0 ? Vector3.One : light / taken;
    }

    protected virtual void UpdateGasNode(ref GasNode node, int index)
    {
        ApplyPhysics(ref node);
        ResolveTileCollision(ref node);

        if (node.ContactCooldown <= 0)
        {
            CheckEntityContacts(ref node);
            node.ContactCooldown = 15;
        }
        else
            node.ContactCooldown--;

        node.Life -= LifeDecay;
    }
    protected virtual void ApplyLighting(ref GasNode gas) { }

    private void ApplyPhysics(ref GasNode node)
    {
        float netVertical = MathHelper.Lerp(-BuoyancyForce, GravityScale, node.Density);
        node.Velocity.Y += netVertical;

        float drag = MathHelper.Lerp(Drag, Drag * 0.96f, node.Viscosity);
        node.Velocity *= drag;
    }

    /// <summary>
    /// Resolves tile collisions against the node's current tile and its
    /// four cardinal neighbours. Applies lateral spreading on surface contact,
    /// then moves the node by its (possibly corrected) velocity.
    ///
    /// Collision flags (OnCeiling, OnFloor, OnWall) are refreshed each call.
    /// </summary>
    private void ResolveTileCollision(ref GasNode node)
    {
        const int TileSize = 16;
        const float Restitution = 0.02f;   // minimal bounce on tile contact
        const float SpreadBase = 0.40f;

        node.OnCeiling = false;
        node.OnFloor = false;
        node.OnWall = false;

        int cx = (int)(node.Position.X / TileSize);
        int cy = (int)(node.Position.Y / TileSize);

        if (IsSolidTile(cx, cy))
        {
            node.EnclosedTicks++;
            OnGasEnclosed(ref node);

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
            node.Velocity.Y = MathF.Abs(node.Velocity.Y) * Restitution;
            node.OnCeiling = true;
            ApplyLateralSpread(ref node, solidLeft, solidRight, SpreadBase);
            OnTileContact(ref node, cx, cy - 1, Framing.GetTileSafely(cx, cy - 1));
        }

        if (solidBelow && node.Velocity.Y > 0f)
        {
            node.Velocity.Y = -MathF.Abs(node.Velocity.Y) * Restitution;
            node.Position.Y = (cy + 1) * TileSize;
            node.OnFloor = true;
            if (GasType == 0)
            {
                node.Life -= EnclosedLifePenalty;
            }
            ApplyLateralSpread(ref node, solidLeft, solidRight, SpreadBase);
            OnTileContact(ref node, cx, cy + 1, Framing.GetTileSafely(cx, cy + 1));
        }

        if (solidLeft && node.Velocity.X < 0f)
        {
            node.Velocity.X = MathF.Abs(node.Velocity.X) * Restitution;
            node.OnWall = true;
            OnTileContact(ref node, cx + 1, cy, Framing.GetTileSafely(cx + 1, cy));
        }

        if (solidRight && node.Velocity.X > 0f)
        {
            node.Velocity.X = -MathF.Abs(node.Velocity.X) * Restitution;
            node.OnWall = true;
            OnTileContact(ref node, cx - 1, cy, Framing.GetTileSafely(cx - 1, cy));
        }

        CheckLiquidContactAt(ref node, cx, cy);
        CheckLiquidContactAt(ref node, cx, cy - 1);
        CheckLiquidContactAt(ref node, cx, cy + 1);

        node.Position += node.Velocity;
    }

    private void ApplyLateralSpread(ref GasNode node, bool solidLeft, bool solidRight, float force)
    {
        float spread = force * (1f - node.Viscosity);

        if (!solidLeft && !solidRight)
        {
            // Both sides open: preserve lateral momentum, add small random bias.
            float momentum = MathHelper.Clamp(node.Velocity.X, -spread * 0.5f, spread * 0.5f);
            node.Velocity.X += momentum + (Main.rand.NextFloat() - 0.5f) * spread * 0.3f;
        }
        else if (!solidLeft)
            node.Velocity.X -= spread * Main.rand.NextFloat(0.6f, 1f);
        else if (!solidRight)
            node.Velocity.X += spread * Main.rand.NextFloat(0.6f, 1f);
        else
        {
            node.Velocity.X += (Main.rand.NextFloat() - 0.5f) * 0.07f;
            node.Life -= 1f;
        }
    }

    private void CheckLiquidContactAt(ref GasNode node, int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
            return;

        Tile tile = Framing.GetTileSafely(tileX, tileY);
        if (tile.LiquidAmount > 0)
            OnLiquidContact(ref node, tileX, tileY, tile);
    }

    /// <summary>
    /// Whether this layer's nodes may act on <paramref name="npc"/> at all.
    /// </summary>
    /// <remarks>
    /// Checked in the sweep, before any hook runs, rather than at the top of
    /// <see cref="OnNPCContact"/>. That placement is the whole point: a guard inside an overridable
    /// hook only holds for as long as every override remembers that calling <c>base</c> is not the
    /// same as returning when <c>base</c> returns early. Four layers did exactly that -- they
    /// deferred to the chemical guard and then applied a debuff underneath it regardless -- and
    /// town NPCs caught fire, poison and frostburn from weapons aimed past them. Filtering here
    /// means no override can reach a target this says no to.
    /// <para>
    /// Matches the strictest check any layer was already making by hand: friendly covers town NPCs
    /// and critters, immortal covers target dummies, and dontTakeDamage covers everything else that
    /// is scenery wearing an NPC slot.
    /// </para>
    /// </remarks>
    protected virtual bool CanAffect(NPC npc)
        => !npc.friendly && !npc.dontTakeDamage && !npc.immortal;

    /// <inheritdoc cref="CanAffect(NPC)"/>
    protected virtual bool CanAffect(Player player) => true;

    private void CheckEntityContacts(ref GasNode node)
    {
        float radiusSq = node.Radius * node.Radius;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || !CanAffect(npc)) continue;
            if (Vector2.DistanceSquared(node.Position, npc.Center) < radiusSq)
                OnNPCContact(ref node, npc);
        }

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (!player.active || player.dead || !CanAffect(player)) continue;
            if (Vector2.DistanceSquared(node.Position, player.Center) < radiusSq)
                OnPlayerContact(ref node, player);
        }
    }

    /// <summary>
    /// Called for each entity a node overlaps. Targets have already passed
    /// <see cref="CanAffect(NPC)"/>, so an override does not need to re-check for friendlies.
    /// </summary>
    protected virtual void OnNPCContact(ref GasNode node, NPC npc) { }

    protected virtual void OnPlayerContact(ref GasNode node, Player player) { }

    protected virtual void OnTileContact(ref GasNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnLiquidContact(ref GasNode node, int tileX, int tileY, Tile tile) { }

    protected virtual void OnGasEnclosed(ref GasNode node) { }

    /// <summary>accumulates gas node blobs into the off-screen field target.</summary>
    public void AccumulateField()
    {
        if (gasNodes.Count == 0 || Main.gameMenu || Main.dedServ)
            return;

        // Queue up the request
        _gasTarget.Request();

        // Immediately process the request and draw to the render target
        _gasTarget.PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);
    }

    /// <summary>composites the field target to screen through the layer shader.</summary>
    public void CompositeToScreen()
    {
        if (!_gasTarget.IsReady || _gasTarget.GetTarget() == null) return;

        var sb = Main.spriteBatch;
        var effect = shader.Value;
        var device = Main.graphics.GraphicsDevice;

        // Set centrally, ahead of the layer's own block, because every layer needs it and none of
        // them should have to remember. The gas target bakes the zoom into its own transform, so a
        // shader reading world position back out of a screen uv has to undo it -- without this it
        // silently assumes 1 and its noise slides across the world as the camera pans.
        effect.Parameters["Zoom"]?.SetValue(Main.GameViewMatrix.Zoom);

        ApplyShaderParameters(effect);

        sb.Begin(SpriteSortMode.Immediate, CompositeBlendState, SamplerState.PointClamp,
                 DepthStencilState.None, RasterizerState.CullNone);

        BindAdditionalTextures(device);
        effect.CurrentTechnique.Passes[0].Apply();

        sb.Draw(
            _gasTarget.GetTarget(), Vector2.Zero, null, Color.White, 0f,
            Vector2.Zero, _gasTarget.PixelScale, SpriteEffects.None, 0f);

        sb.End();
    }
    protected virtual void BindAdditionalTextures(GraphicsDevice device) { }


    // TODO: take a list/params of effects for diabolical shaderslop activities
    protected virtual void ApplyShaderParameters(Effect effect) { }

    protected static bool IsSolidTile(int tileX, int tileY)
    {
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