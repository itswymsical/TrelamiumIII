using ReLogic.Content;
using System.Collections.Generic;

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

    internal void DrawNodes(SpriteBatch sb)
    {
        foreach (var node in gasNodes)
        {
            Vector2 screenPos = node.Position - Main.screenPosition;
            float size = node.Radius * 2f;

            Vector3 light = Lighting.GetSubLight(node.Position);

            Color nodeColor = Color.White * node.Alpha;

            sb.Draw(
                radialTexture.Value,
                screenPos,
                null,
                nodeColor * 1.25f,
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
    public void SpawnNode(
        Vector2 worldPosition,
        Vector2 velocity,
        float radius,
        float life,
        float density = -1f,
        float viscosity = -1f,
        float temperature = -1f)
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

    private void CheckEntityContacts(ref GasNode node)
    {
        float radiusSq = node.Radius * node.Radius;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active) continue;
            if (Vector2.DistanceSquared(node.Position, npc.Center) < radiusSq)
                OnNPCContact(ref node, npc);
        }

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (!player.active || player.dead) continue;
            if (Vector2.DistanceSquared(node.Position, player.Center) < radiusSq)
                OnPlayerContact(ref node, player);
        }
    }

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