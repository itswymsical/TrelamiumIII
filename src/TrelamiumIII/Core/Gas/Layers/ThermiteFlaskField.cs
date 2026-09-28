using ReLogic.Content;
using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Content.Items.Rusted;

namespace TrelamiumIII.Core.Gas.Layers;

public class ThermiteFlaskField : ChemicalGasField
{
    private const float CONVERSION_COVERAGE = 2.5f;

    /// <summary>Crawl speed per pixel of volume left, so a run decelerates into its tip.</summary>
    private const float CRAWL_EASE = 0.12f;

    private const float CRAWL_MIN_SPEED = 1.5f;

    private const float CRAWL_MAX_SPEED = 6f;

    /// <summary>Half a tile, so a crawl step never passes over a tile it should have tested.</summary>
    private const float CRAWL_MAX_STEP = 8f;

    /// <summary>Under a tile per tick, so the emitter's destination-point test cannot skip a one-tile floor.</summary>
    private const float FALL_MAX_SPEED = 12f;

    /// <summary>Tiles a floor run may drop before it counts as going over a ledge.</summary>
    private const int STEP_DOWN = 2;

    /// <summary>Pixels a crawler keeps between itself and the surface it follows.</summary>
    private const float SURFACE_GAP = 1f;

    /// <summary>Pixels a line node sits out from the crawler, away from the surface.</summary>
    private const float NODE_LIFT = 5f;

    /// <summary>Volume, in pixels, over which the end of a run thins and cools early.</summary>
    private const float TIP_TAPER = 40f;

    /// <summary>Horizontal speed at which a floor landing sends <see cref="MAX_LEAN" /> of its volume one way.</summary>
    private const float LEAN_REFERENCE = 2f;

    private const float MAX_LEAN = 0.3f;

    /// <summary>A run with less volume than this is not started.</summary>
    private const float MIN_VOLUME = 4f;

    /// <summary>Life of the short-lived nodes a falling pour sheds.</summary>
    private const float STREAM_LIFE = 10f;

    /// <summary>Alpha above which a node is fully hot. Below it the node cools, reaching cold at <see cref="COLD_ALPHA" />.</summary>
    private const float HOT_ALPHA = 0.7f;

    private const float COLD_ALPHA = 0.15f;

    /// <summary>Alpha above which a pinned node keeps its full weight, so a line holds its shape before it thins.</summary>
    private const float SET_ALPHA = 0.45f;

    private const float SPARK_CHANCE = 0.014f;

    private const float GLOW_SCALE = 5f;

    private const float GLOW_STRENGTH = 0.12f;

    private static readonly Vector3 GlowColor = new(1f, 0.55f, 0.16f);

    private enum PourMode : byte
    {
        Falling,
        Floor,
        Wall,
    }

    private static EmitterProfile PourProfile => EmitterProfile.Default with
    {
        Lifetime = 240,
        NodeRadius = 16f,
        NodeLife = 300f,
        Gravity = 0.3f,
        Drag = 0.99f,
        Bounces = 4,
        Restitution = 0.3f,
        TileCollide = true,
    };

    public override int LoadOrder => 40;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;
    protected override int GasType => GasTypeID.Liquid;
    protected override float DefaultDensity => 1.0f;
    protected override float DefaultViscosity => 0.2f;
    protected override float EnclosedLifePenalty => 1f;
    protected override float GravityScale => 0.33f;
    protected override float BuoyancyForce => 0.5f;
    protected override float Drag => 0.9956f;

    protected override float DefaultTemperature => 2400f;

    protected override Vector2 TemperatureRange => new(700f, 2400f);

    /// <summary>Falling nodes streak along their motion; pinned nodes have none and stay round.</summary>
    protected override float StampStretch => 0.6f;

    protected override float FlowReferenceSpeed => 8f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 1;

    private static readonly ClingBehaviour Drip = new PourCling(volume: 48f);

    public override ClingBehaviour? ClingBehaviour => Drip;

    private Asset<Texture2D>? noiseTex;

    private Asset<Texture2D>? glowTex;

    public override void Load()
    {
        base.Load();

        if (Main.dedServ)
            return;

        noiseTex = Assets.Textures.Noise.Melt_03.Asset;
        glowTex = Assets.Textures.VFX.RadialGradient.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTex = null;
        glowTex = null;
    }

    /// <summary>
    ///     Releases <paramref name="volume" /> pixels of line at <paramref name="origin" />. It falls to the nearest
    ///     surface, then runs across floors (both ways, leaning with <paramref name="momentum" />), down walls and over
    ///     ledges. The route depends only on terrain, not on random rolls.
    /// </summary>
    /// <param name="momentum">Velocity the pour starts falling with.</param>
    /// <param name="damage">Dealt once to each enemy the pour runs through, on the owner's client.</param>
    public void Pour(
        Vector2 origin,
        Vector2 momentum,
        float volume,
        int owner = FieldNode.NoOwner,
        int damage = 0,
        float knockback = 0f,
        ChemicalPayload? payload = null)
    {
        if (volume < MIN_VOLUME)
            return;

        SpawnEmitter(PourProfile with { Volume = volume }, origin, momentum, owner, damage, knockback, (byte)PourMode.Falling, payload);
    }

    private void SpawnRun(in GasEmitter from, Vector2 position, PourMode mode, Vector2 velocity, float volume)
    {
        if (volume < MIN_VOLUME)
            return;

        SpawnEmitter(PourProfile with { Volume = volume }, position, velocity, from.Owner, from.Damage, from.Knockback, (byte)mode, from.Payload);
    }

    private static PourMode ModeOf(in GasEmitter emitter) => (PourMode)emitter.Variant;

    protected override void MoveEmitter(ref GasEmitter emitter)
    {
        if (emitter.Volume <= 0f)
        {
            base.MoveEmitter(ref emitter);
            return;
        }

        if (ModeOf(in emitter) == PourMode.Falling)
        {
            if (emitter.Velocity.LengthSquared() > FALL_MAX_SPEED * FALL_MAX_SPEED)
                emitter.Velocity = Vector2.Normalize(emitter.Velocity) * FALL_MAX_SPEED;

            base.MoveEmitter(ref emitter);
            return;
        }

        float speed = MathHelper.Clamp(emitter.Volume * CRAWL_EASE, CRAWL_MIN_SPEED, CRAWL_MAX_SPEED)
                    * (emitter.Payload?.Stats.BurstSpeedMultiplier ?? 1f);

        speed = MathF.Min(speed, CRAWL_MAX_STEP);

        if (ModeOf(in emitter) == PourMode.Wall)
            CrawlWall(ref emitter, speed);
        else
            CrawlFloor(ref emitter, speed);

        if (ModeOf(in emitter) == PourMode.Falling)
            return;

        emitter.Volume -= speed;

        if (emitter.Volume <= 0f)
            emitter.Life = 0f;
    }

    /// <summary>
    ///     Advances a floor run along the open row above its floor. Climbs one-tile steps, drops up to
    ///     <see cref="STEP_DOWN" /> tiles, and turns down the face of anything deeper. A taller rise ends the run.
    /// </summary>
    private static void CrawlFloor(ref GasEmitter emitter, float speed)
    {
        float direction = MathF.Sign(emitter.Velocity.X);
        int row = (int)(emitter.Position.Y / 16f);
        float nextX = emitter.Position.X + direction * speed;
        int column = (int)(nextX / 16f);

        if (IsSolidTile(column, row))
        {
            if (IsSolidTile(column, row - 1) || IsSolidTile((int)(emitter.Position.X / 16f), row - 1))
            {
                emitter.Life = 0f;
                return;
            }

            row--;
        }
        else
        {
            int landing = row;

            while (!IsSolidTile(column, landing + 1))
            {
                if (++landing - row > STEP_DOWN)
                {
                    TurnOverLedge(ref emitter, direction, row);
                    return;
                }
            }

            row = landing;
        }

        emitter.Position = new Vector2(nextX, (row + 1) * 16f - SURFACE_GAP);
        emitter.Velocity = new Vector2(direction * speed, 0f);
    }

    /// <summary>Starts a wall run down the face below the lip of the floor tile the run is leaving.</summary>
    private static void TurnOverLedge(ref GasEmitter emitter, float direction, int row)
    {
        int column = (int)(emitter.Position.X / 16f);
        float lip = direction > 0f ? (column + 1) * 16f : column * 16f;

        emitter.Position = new Vector2(lip + direction * SURFACE_GAP, (row + 1) * 16f + 1f);
        emitter.Velocity = Vector2.UnitY;
        emitter.Variant = (byte)PourMode.Wall;
    }

    /// <summary>
    ///     Advances a wall run downward. On reaching a floor it turns away from the wall; where the wall ends it
    ///     falls, and lands as a new pour with what volume is left.
    /// </summary>
    private static void CrawlWall(ref GasEmitter emitter, float speed)
    {
        int column = (int)(emitter.Position.X / 16f);
        float nextY = emitter.Position.Y + speed;
        int row = (int)(nextY / 16f);

        if (IsSolidTile(column, row))
        {
            bool left = IsSolidTile(column - 1, row - 1);
            bool right = IsSolidTile(column + 1, row - 1);

            emitter.Position.Y = row * 16f - SURFACE_GAP;

            // Walled on both sides (a shaft) or neither: nowhere to run.
            if (left == right)
            {
                emitter.Life = 0f;
                return;
            }

            emitter.Velocity = new Vector2(left ? speed : -speed, 0f);
            emitter.Variant = (byte)PourMode.Floor;
            return;
        }

        emitter.Position.Y = nextY;
        emitter.Velocity = new Vector2(0f, speed);

        if (!IsSolidTile(column - 1, row) && !IsSolidTile(column + 1, row))
            emitter.Variant = (byte)PourMode.Falling;
    }

    /// <summary>A falling pour that reaches a floor or wall ends and starts runs along it; a ceiling deflects it.</summary>
    protected override bool OnEmitterBounce(ref GasEmitter emitter, Vector2 normal, int tileX, int tileY)
    {
        if (emitter.Volume <= 0f || normal.Y > 0.5f)
            return true;

        Land(in emitter, normal, tileX, tileY);

        return false;
    }

    private void Land(in GasEmitter drop, Vector2 normal, int tileX, int tileY)
    {
        if (normal.Y == 0f)
        {
            float face = normal.X > 0f ? (tileX + 1) * 16f : tileX * 16f;

            SpawnRun(in drop, new Vector2(face + normal.X * SURFACE_GAP, drop.Position.Y), PourMode.Wall, Vector2.UnitY, drop.Volume);
            return;
        }

        var start = new Vector2(drop.Position.X, tileY * 16f - SURFACE_GAP);
        int column = (int)(start.X / 16f);
        int row = tileY - 1;

        bool openLeft = CanStart(column, row, -1);
        bool openRight = CanStart(column, row, 1);

        // Into a corner, or with one side walled off: everything runs the open way.
        float lean = normal.X != 0f
            ? MathF.Sign(normal.X)
            : MathHelper.Clamp(drop.Velocity.X / LEAN_REFERENCE, -1f, 1f);

        if (!openLeft && openRight)
            lean = 1f;
        else if (!openRight && openLeft)
            lean = -1f;

        float direction = lean >= 0f ? 1f : -1f;
        float share = normal.X != 0f || openLeft != openRight ? 1f : 0.5f + MAX_LEAN * MathF.Abs(lean);

        SpawnRun(in drop, start, PourMode.Floor, new Vector2(direction, 0f), drop.Volume * share);
        SpawnRun(in drop, start, PourMode.Floor, new Vector2(-direction, 0f), drop.Volume * (1f - share));
    }

    /// <summary>Whether a floor run starting in this cell could take its first step in <paramref name="direction" />.</summary>
    private static bool CanStart(int column, int row, int direction)
        => !IsSolidTile(column + direction, row) || !IsSolidTile(column + direction, row - 1) && !IsSolidTile(column, row - 1);

    /// <summary>
    ///     A run lays a pinned node every tick, thinner and shorter-lived toward its tip, and burns the tile it runs on.
    ///     A falling pour sheds a short stream instead.
    /// </summary>
    protected override void Emit(ref GasEmitter emitter)
    {
        if (emitter.Volume <= 0f)
        {
            base.Emit(ref emitter);
            return;
        }

        if (ModeOf(in emitter) == PourMode.Falling)
        {
            SpawnNode(emitter.Position, emitter.Velocity * 0.5f, emitter.NodeRadius * 0.7f, STREAM_LIFE, owner: emitter.Owner, payload: emitter.Payload);
            return;
        }

        float taper = MathHelper.Clamp(emitter.Volume / TIP_TAPER, 0f, 1f);
        Vector2 outward = SurfaceNormal(in emitter);
        var along = new Vector2(-outward.Y, outward.X);

        SpawnNode(
            emitter.Position + outward * (NODE_LIFT + Main.rand.NextFloat(-1.5f, 2.5f)) + along * Main.rand.NextFloat(-1.5f, 1.5f),
            Vector2.Zero,
            emitter.NodeRadius * MathHelper.Lerp(0.55f, 1f, taper) * Main.rand.NextFloat(0.8f, 1.2f),
            emitter.NodeLife * MathHelper.Lerp(0.6f, 1f, taper),
            owner: emitter.Owner,
            payload: emitter.Payload,
            pinned: true);

        Burn(in emitter, outward, taper);
    }

    /// <summary>Away from the surface a run follows: up off a floor, out from a wall.</summary>
    private static Vector2 SurfaceNormal(in GasEmitter emitter)
    {
        if (ModeOf(in emitter) != PourMode.Wall)
            return -Vector2.UnitY;

        int column = (int)(emitter.Position.X / 16f);
        int row = (int)(emitter.Position.Y / 16f);

        return IsSolidTile(column - 1, row) ? Vector2.UnitX : -Vector2.UnitX;
    }

    /// <summary>Leaves the thermal residue on the tile a run is crossing, which also sets it alight.</summary>
    private void Burn(in GasEmitter emitter, Vector2 outward, float taper)
    {
        if (Residue is not { } residue || emitter.Owner == CosmeticOwnerID || emitter.Payload is { IsActive: false })
            return;

        Vector2 inside = emitter.Position - outward * 8f;

        residue.Linger((int)(inside.X / 16f), (int)(inside.Y / 16f), MathHelper.Lerp(1f, 1.8f, taper), emitter.Owner, emitter.Payload);
    }

    /// <summary>A pinned node whose supporting tile is gone falls.</summary>
    protected override void UpdateNode(ref FieldNode node, int index)
    {
        if (node.Pinned && !Supported(node.Position))
            node.Pinned = false;

        base.UpdateNode(ref node, index);

        node.Temperature = MathHelper.Lerp(TemperatureRange.X, TemperatureRange.Y, Heat(in node));
    }

    /// <summary>0 cold to 1 fully hot, from the node's remaining life.</summary>
    private static float Heat(in FieldNode node) => Utils.GetLerpValue(COLD_ALPHA, HOT_ALPHA, node.Alpha, clamped: true);

    protected override float NodeWeight(in FieldNode node)
        => FieldStamp.Weight(node.Pinned ? node.Alpha / SET_ALPHA : node.Alpha);

    private static bool Supported(Vector2 position)
    {
        int x = (int)(position.X / 16f);
        int y = (int)(position.Y / 16f);

        return IsSolidTile(x, y + 1) || IsSolidTile(x - 1, y) || IsSolidTile(x + 1, y);
    }

    protected override void ApplyLighting(ref FieldNode node)
    {
        if (Main.dedServ)
            return;

        Lighting.AddLight(node.Position, new Vector3(1f, 0.62f, 0.2f) * node.Alpha);

        if (!node.Pinned || Main.rand.NextFloat() > SPARK_CHANCE * Heat(in node))
            return;

        Dust spark = Dust.NewDustPerfect(
            node.Position + Main.rand.NextVector2Circular(node.Radius * 0.5f, 2f),
            DustID.FireworkFountain_Yellow,
            new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1.5f, 4f)),
            0,
            default,
            Main.rand.NextFloat(0.6f, 1.1f));

        spark.noGravity = true;
    }

    /// <summary>An additive glow under each line node, so the pour reads as a light source rather than paint.</summary>
    protected override void DrawOverlay(SpriteBatch spriteBatch)
    {
        if (glowTex is not { Value: { } texture })
            return;

        Vector2 origin = texture.Size() * 0.5f;
        float time = (float)Main.timeForVisualEffects * 0.08f;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix);

        foreach (FieldNode node in nodes)
        {
            if (!node.Pinned)
                continue;

            float flicker = 0.85f + 0.15f * MathF.Sin(time + node.Position.X * 0.13f);
            float strength = Heat(in node) * flicker * GLOW_STRENGTH;

            spriteBatch.Draw(
                texture,
                node.Position - Main.screenPosition,
                null,
                new Color(GlowColor * strength),
                0f,
                origin,
                node.Radius * GLOW_SCALE / texture.Width,
                SpriteEffects.None,
                0f);
        }

        spriteBatch.End();
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex?.Value);

        // Self-lit: the composite never darkens it with the room.
        effect.Parameters["WorldLight"]?.SetValue(Vector3.One);

        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.05f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.08f));
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.45f));

        effect.Parameters["FlowStrength"]?.SetValue(0.20f);

        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.97f);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.85f);
        effect.Parameters["OutlineAlpha"]?.SetValue(0.95f);
        effect.Parameters["HighlightAlpha"]?.SetValue(1f);

        // Hot: white core, amber body, orange rim.
        effect.Parameters["TemperatureBlend"]?.SetValue(1f);
        effect.Parameters["HotShadowColor"]?.SetValue(new Vector3(1f, 0.42f, 0.05f));
        effect.Parameters["HotBodyColor"]?.SetValue(new Vector3(1f, 0.72f, 0.16f));
        effect.Parameters["HotCoreColor"]?.SetValue(new Vector3(1f, 0.96f, 0.74f));

        // Cold: a dark red crust.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.42f, 0.06f, 0.03f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.78f, 0.17f, 0.04f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(1f, 0.4f, 0.07f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.3f, 0.04f, 0.03f));
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(1f, 0.5f, 0.15f));
    }

    /// <summary>Thermite left on a floor keeps burning as a thermal residue.</summary>
    protected override ChemicalReactionTileCondition? Residue => TileConditionSystem.Get<ThermalResidueCondition>();

    /// <summary>Just over one node's worth, so only thermite that has actually settled works on terrain.</summary>
    protected override float TileFieldThreshold => 1.15f;

    protected override int ContactBuff => BuffID.OnFire;

    protected override int ContactBuffTicks => 120;

    /// <summary>Thickens and slows in liquid. Conversions happen in <see cref="OnLiquidField" />.</summary>
    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        base.OnLiquidContact(ref node, tileX, tileY, tile);

        node.Life *= 0.99f;
    }

    /// <summary>Boils off a pool the thermite is sitting in, leaving slag. Drains faster the deeper the thermite.</summary>
    protected override void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (density < CONVERSION_COVERAGE)
            return;

        int drain = (int)MathHelper.Clamp(density * 5f, 3f, 20f);

        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.Obsidian, LiquidID.Water, drain, amountLeft: 50);
        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, (ushort)ModContent.TileType<RustedIronOre>(), LiquidID.Lava, drain, amountLeft: 50);
        LiquidUtils.BoilLiquidIntoTile(tileX, tileY, TileID.CrispyHoneyBlock, LiquidID.Honey, drain, amountLeft: 50);
    }

    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity *= 1.01f;
        node.Velocity *= 0.89f;
    }

    /// <summary>Pours a short line under the host as it attaches, then rides it like the default cling.</summary>
    private sealed class PourCling(float volume) : ClingBehaviour
    {
        public override void OnAttach(NPC host, ref ClingState cling)
        {
            if (FieldSystem.GetLayer<ThermiteFlaskField>() is not { } layer)
                return;

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, PitchRange = (0.1f, 0.4f) }, host.Center);

            // A one-off: it outlives the cling's scope, but never lands back on the host.
            layer.Pour(host.Center, host.velocity * 0.25f, volume, cling.Owner, payload: cling.Payload?.Derive(null, host.whoAmI));
        }
    }
}
