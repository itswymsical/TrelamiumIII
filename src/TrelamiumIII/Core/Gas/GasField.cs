using System.Collections.Generic;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas;

/// <summary>
/// A field whose nodes act on the world: they sweep entities, carry ownership, throw ballistic
/// emitters and accumulate into a tile field that leaves residues behind.
/// </summary>
/// <remarks>
/// <para>
/// Everything about drifting, colliding with terrain and being drawn lives in
/// <see cref="FieldLayer" /> and is shared with the purely visual layers. This is the half that
/// makes a cloud something the world has to answer for.
/// </para>
/// <para>
/// Exposes override hooks for alchemy reactions; <see cref="ChemicalGasField" /> is the subclass
/// that knows the chemical system exists.
/// </para>
/// </remarks>
public abstract class GasField : FieldLayer
{
    internal class GasTypeID
    {
        public static int Liquid = 0;
        public static int Gas = 1;
        public static int Plasma = 2;
    }

    /// <summary>
    /// 0 = liquid, 1 = gas, 2 = plasma. Used for alchemy reaction checks.
    /// </summary>
    protected abstract int GasType { get; }

    /// <summary>
    /// Gas keeps simulating with no screen: the tile field is world state, not decoration.
    /// </summary>
    protected override bool SimulatesHeadless => true;

    /// <summary>A liquid that has reached the ground has arrived; a gas spreads along it.</summary>
    protected override bool PoolsOnFloor => GasType == GasTypeID.Liquid;

    /// <summary>
    /// Linear rather than the point sampling the visual layers use, which is what these layers have
    /// always been tuned against -- it softens each blob before the threshold ever sees it.
    /// </summary>
    protected internal override SamplerState StampSampler => SamplerState.LinearClamp;

    /// <summary>
    /// A safety ceiling rather than a budget. These lists used to be unbounded, so a stuck emitter
    /// grew one without limit and the graphics preset did nothing for gas at all; the number is set
    /// well above what any current burst produces so that inheriting one changes nothing on screen.
    /// </summary>
    protected override int BaseMaxNodes => 900;

    protected override void OnNodeSpawn(ref FieldNode node)
    {
        node.TypeId = GasType;
        node.ContactCooldown = Main.rand.Next(0, 20); // stagger initial contact sweeps
    }

    /// <summary>
    /// Which of the three families the node belongs to, spread across the variant channel.
    /// </summary>
    /// <remarks>
    /// Nothing in the shader reads this yet. It costs no draw and no channel that was going to be
    /// used otherwise, and it is the one piece of per-node identity the renderer cannot work out
    /// for itself, so a layer that later wants its liquid and its vapour shaded differently within
    /// one field will find it already there.
    /// </remarks>
    protected override float NodeVariant(in FieldNode node) => node.TypeId / 2f;

    /// <summary>
    /// The layer's ballistic sources: the arms of a burst, in flight, laying nodes behind them.
    /// </summary>
    /// <remarks>
    /// Kept apart from the node list rather than folded into it, for the reason set out on
    /// <see cref="GasEmitter" />: these are few and expensive per unit -- they collide, they sweep
    /// entities, they carry damage -- and the nodes are many and cheap. One list of each keeps both
    /// costs where they belong.
    /// </remarks>
    protected List<GasEmitter> emitters = new();

    public override void Unload()
    {
        base.Unload();
        emitters.Clear();
    }

    /// <summary>
    /// Advances the whole layer for one tick: its emitters, then its nodes, then the tile field.
    /// </summary>
    /// <remarks>
    /// Emitters first, and the order is not arbitrary. An emitter that emits this tick should have
    /// its node simulated this tick too, or every arm of a burst is drawn one frame behind where it
    /// actually is -- which at burst speeds is several pixels of lag between the trail and its head.
    /// </remarks>
    public override void Update()
    {
        UpdateEmitters();

        base.Update();

        UpdateTileField();
    }

    /// <summary>Base physics, then the entity sweep this layer adds on top of it.</summary>
    protected override void UpdateNode(ref FieldNode node, int index)
    {
        base.UpdateNode(ref node, index);

        if (node.ContactCooldown <= 0)
        {
            CheckEntityContacts(ref node);
            node.ContactCooldown = 15;
        }
        else
            node.ContactCooldown--;
    }

    #region Emitters

    /// <summary>Ticks an emitter waits between hits on the same pass. Stands in for local iframes.</summary>
    private const int EMITTER_HIT_COOLDOWN = 10;

    /// <summary>
    /// Ticks of vanilla immunity a landed emitter hit puts on its target, per owning player.
    /// </summary>
    /// <remarks>
    /// Matches the ten ticks a friendly projectile grants through <c>Projectile.Damage</c>, which is
    /// what the burst carriers used to get for free. <see cref="EMITTER_HIT_COOLDOWN" /> is the
    /// other half of the same guard and is not redundant: this one stops twenty arms of one burst
    /// from all landing at once, and that one stops a single arm from re-hitting as it crosses a
    /// large target once the shared window has lapsed.
    /// </remarks>
    private const int EMITTER_IMMUNITY = 10;

    /// <summary>Half-width of the square an emitter tests entity overlap with.</summary>
    /// <remarks>
    /// The old burst projectiles were 22x22, and the arms were tuned against that reach. A point
    /// test would be technically purer and would make every one of them miss.
    /// </remarks>
    private const int EMITTER_HIT_RADIUS = 11;

    /// <summary>The damage class an emitter's hits are credited to.</summary>
    /// <remarks>
    /// A virtual rather than a constant because <see cref="GasField" /> is deliberately ignorant of
    /// the chemical system -- <see cref="ChemicalGasField" /> is the layer that knows it exists, and
    /// it is the layer that answers this.
    /// </remarks>
    protected virtual DamageClass EmitterDamageClass => DamageClass.Generic;

    /// <summary>Emitters currently in flight.</summary>
    public int EmitterCount => emitters.Count;

    /// <summary>
    /// Throws a burst of emitters out of a point.
    /// </summary>
    /// <param name="direction">
    /// The axis the burst opens along: the surface normal for a terrain hit, or the payload's own
    /// heading for one in open air. Already biased and normalised by the caller.
    /// </param>
    /// <param name="grounded">
    /// Whether this burst hit something. Selects the profile's ground or air parameters, and for a
    /// <see cref="BurstShape.Fan" /> decides between an arc off the surface and a full circle.
    /// </param>
    /// <param name="damage">
    /// The parent's damage. Scaled by <see cref="BurstProfile.DamageShare" />, so a profile that
    /// shares nothing produces harmless arms no matter what is passed here.
    /// </param>
    /// <remarks>
    /// Called on every client, because a burst is something everyone has to see. Only the arms
    /// belonging to the local player will actually deal their damage -- see
    /// <see cref="GasEmitter.IsLocallyOwned" /> -- so running it everywhere costs a divergent
    /// scatter pattern and nothing else.
    /// </remarks>
    public void SpawnBurst(
        in BurstProfile profile,
        Vector2 origin,
        Vector2 direction,
        bool grounded,
        int owner = FieldNode.NoOwner,
        int damage = 0,
        float knockback = 0f,
        ChemicalPayload? payload = null)
    {
        Vector2 outward = direction.SafeNormalize(-Vector2.UnitY);

        if (grounded && profile.UpwardBias > 0f && Vector2.Dot(-Vector2.UnitY, outward) >= 0f)
            outward = Vector2.Lerp(outward, -Vector2.UnitY, profile.UpwardBias).SafeNormalize(outward);

        int count = (grounded ? profile.GroundCount : profile.AirCount).RollCount();

        if (count <= 0)
            return;

        Vector2 start = origin + (grounded ? outward * profile.OriginOffset : Vector2.Zero);
        int armDamage = (int)MathF.Round(damage * profile.DamageShare);

        for (int i = 0; i < count; i++)
        {
            (Vector2 velocity, float edge) = profile.Shape switch
            {
                BurstShape.Fan => FanArm(in profile, outward, grounded, i, count),
                _ => ScatterArm(in profile, outward, grounded),
            };

            SpawnEmitter(profile.Select(edge), start, velocity, owner, armDamage, knockback, (byte)i, payload);
        }
    }

    /// <summary>
    /// One arm of a fan: evenly spaced across the arc off a surface, or evenly spaced around the
    /// circle in open air.
    /// </summary>
    /// <remarks>
    /// The jitter added to each slot is what keeps an even distribution from looking mechanical. It
    /// is applied to the angle only and not to the slot index, so the arms stay in order and cannot
    /// bunch up into a gap.
    /// </remarks>
    private static (Vector2 Velocity, float Edge) FanArm(in BurstProfile profile, Vector2 outward, bool grounded, int index, int count)
    {
        if (!grounded)
        {
            float spin = MathHelper.TwoPi * (index / (float)count) + Main.rand.NextFloat(-0.2f, 0.2f);
            float edge = Main.rand.NextFloat();

            return (spin.ToRotationVector2() * profile.AirSpeed.Roll(), edge);
        }

        float half = profile.Arc * 0.5f;
        float slot = (index + Main.rand.NextFloat(0.15f, 0.85f)) / count;
        float angle = MathHelper.Lerp(-half, half, slot);
        float fromCentre = half <= 0f ? 0f : MathF.Abs(angle) / half;

        float speed = profile.GroundSpeed.At(fromCentre) * Main.rand.NextFloat(0.85f, 1.15f);

        return (outward.RotatedBy(angle) * speed, fromCentre);
    }

    /// <summary>One arm of a scatter: somewhere inside the cone, at some speed, unordered.</summary>
    private static (Vector2 Velocity, float Edge) ScatterArm(in BurstProfile profile, Vector2 outward, bool grounded)
    {
        float speed = (grounded ? profile.GroundSpeed : profile.AirSpeed).Roll();

        return (outward.RotatedByRandom(profile.Arc) * speed, Main.rand.NextFloat());
    }

    /// <summary>Puts a single emitter into the world from a profile.</summary>
    public void SpawnEmitter(
        in EmitterProfile profile,
        Vector2 position,
        Vector2 velocity,
        int owner = FieldNode.NoOwner,
        int damage = 0,
        float knockback = 0f,
        byte variant = 0,
        ChemicalPayload? payload = null)
    {
        var emitter = new GasEmitter
        {
            Position = position,
            Velocity = velocity,
            Life = profile.Lifetime,
            MaxLife = profile.Lifetime,
            Gravity = profile.Gravity,
            Drag = profile.Drag,
            NodeRadius = profile.NodeRadius,
            NodeLife = profile.NodeLife,
            NodeScatter = profile.NodeScatter,
            Inheritance = profile.Inheritance,
            Growth = profile.Growth,
            Interval = Math.Max(profile.Interval, (byte)1),
            BouncesLeft = profile.Bounces,
            Restitution = profile.Restitution,
            TileCollide = profile.TileCollide,
            Owner = owner,
            Payload = payload,
            Damage = damage,
            Knockback = knockback,
            Variant = variant,
        };

        OnEmitterSpawn(ref emitter);

        if (emitter.Life > 0f)
            emitters.Add(emitter);
    }

    private void UpdateEmitters()
    {
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            GasEmitter emitter = emitters[i];

            UpdateEmitter(ref emitter, i);

            if (emitter.Life <= 0f)
            {
                OnEmitterExpire(in emitter);
                emitters.RemoveAt(i);
                continue;
            }

            emitters[i] = emitter;
        }
    }

    /// <summary>
    /// One emitter's tick: move it, let it hit something, let it emit, age it.
    /// </summary>
    /// <remarks>
    /// The overridable half of the "projectile" this system replaces. A layer that wants an arm to
    /// home, split, or change what it emits partway through its life does it here and calls
    /// <c>base</c> for the rest, exactly as it would in a <see cref="ModProjectile" />'s AI.
    /// </remarks>
    protected virtual void UpdateEmitter(ref GasEmitter emitter, int index)
    {
        emitter.Velocity.Y += emitter.Gravity;
        emitter.Velocity *= emitter.Drag;

        if (emitter.TileCollide)
            MoveEmitterAgainstTiles(ref emitter);
        else
            emitter.Position += emitter.Velocity;

        if (emitter.Damage > 0)
            SweepEmitterContacts(ref emitter);

        if (emitter.Timer > 0)
            emitter.Timer--;
        else
        {
            Emit(ref emitter);
            emitter.Timer = (byte)(emitter.Interval - 1);
        }

        emitter.Life--;
    }

    /// <summary>
    /// Lays one node down from an emitter's current position.
    /// </summary>
    /// <remarks>
    /// The growth curve is square-rooted so an arm reaches most of its final size in the first
    /// third of its life. A linear ramp reads as the burst still inflating when it should already
    /// be dispersing, which is the difference between a splash and a slowly expanding balloon.
    /// </remarks>
    protected virtual void Emit(ref GasEmitter emitter)
    {
        float radius = emitter.NodeRadius;

        if (emitter.Growth > 0f)
            radius *= MathHelper.Lerp(1f - emitter.Growth, 1f, MathF.Sqrt(emitter.Progress));

        if (radius <= 0f || emitter.NodeLife <= 0f)
            return;

        Vector2 velocity = emitter.Velocity * emitter.Inheritance
                         + Main.rand.NextVector2Circular(emitter.NodeScatter.X, emitter.NodeScatter.Y);

        SpawnNode(emitter.Position, velocity, radius, emitter.NodeLife, owner: emitter.Owner, payload: emitter.Payload);
    }

    /// <summary>
    /// Moves an emitter, deflecting it off geometry until it runs out of bounces.
    /// </summary>
    /// <remarks>
    /// A point test against the destination tile rather than a swept box. Emitters are small, fast
    /// and short-lived, and the failure mode of a point test -- clipping a corner at high speed --
    /// costs a bounce that a player watching a spray of molten metal will never notice missing.
    /// </remarks>
    private void MoveEmitterAgainstTiles(ref GasEmitter emitter)
    {
        Vector2 destination = emitter.Position + emitter.Velocity;

        int nextX = (int)(destination.X / 16f);
        int nextY = (int)(destination.Y / 16f);

        if (!IsSolidTile(nextX, nextY))
        {
            emitter.Position = destination;
            return;
        }

        // Which axis actually carried it into the wall. Probing them separately is what tells a
        // floor from a wall, and so which component of the velocity to flip.
        int currentX = (int)(emitter.Position.X / 16f);
        int currentY = (int)(emitter.Position.Y / 16f);

        var normal = Vector2.Zero;

        if (IsSolidTile(nextX, currentY))
            normal.X = -MathF.Sign(emitter.Velocity.X);

        if (IsSolidTile(currentX, nextY))
            normal.Y = -MathF.Sign(emitter.Velocity.Y);

        // A diagonal entry: neither cardinal neighbour is solid, so the corner is what it clipped.
        if (normal == Vector2.Zero)
            normal = -emitter.Velocity.SafeNormalize(-Vector2.UnitY);

        if (emitter.BouncesLeft == 0 || !OnEmitterBounce(ref emitter, normal, nextX, nextY))
        {
            emitter.Life = 0f;
            return;
        }

        emitter.BouncesLeft--;

        if (normal.X != 0f)
            emitter.Velocity.X = -emitter.Velocity.X * emitter.Restitution;

        if (normal.Y != 0f)
            emitter.Velocity.Y = -emitter.Velocity.Y * emitter.Restitution;

        emitter.Position += emitter.Velocity;
    }

    /// <summary>
    /// Looks for something for a damaging emitter to hit, and hits the first thing it finds.
    /// </summary>
    /// <remarks>
    /// First rather than all, unlike the node sweep. A node is a volume of gas that everything
    /// standing in it is standing in; an emitter is a thrown object, and a thrown object that
    /// strikes six enemies in the same tick because they were stacked is not one the damage numbers
    /// were written for.
    /// </remarks>
    private void SweepEmitterContacts(ref GasEmitter emitter)
    {
        if (emitter.HitCooldown > 0)
        {
            emitter.HitCooldown--;
            return;
        }

        var box = new Rectangle(
            (int)emitter.Position.X - EMITTER_HIT_RADIUS,
            (int)emitter.Position.Y - EMITTER_HIT_RADIUS,
            EMITTER_HIT_RADIUS * 2,
            EMITTER_HIT_RADIUS * 2);

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || !CanAffect(npc) || !npc.Hitbox.Intersects(box))
                continue;

            OnEmitterHitNPC(ref emitter, npc);
            emitter.HitCooldown = EMITTER_HIT_COOLDOWN;

            return;
        }
    }

    /// <summary>Called on a fully built emitter before it enters the list, so it can be adjusted.</summary>
    /// <remarks>Setting <see cref="GasEmitter.Life" /> to zero here refuses the arm outright.</remarks>
    protected virtual void OnEmitterSpawn(ref GasEmitter emitter) { }

    /// <summary>Called once the emitter has already left the list.</summary>
    protected virtual void OnEmitterExpire(in GasEmitter emitter) { }

    /// <summary>
    /// An emitter has reached terrain and has a bounce left. Return false to end it instead.
    /// </summary>
    /// <remarks>
    /// Where a layer puts what its payload does to whatever it lands on -- scorching the tile,
    /// splashing, cracking. Runs before the deflection, so the velocity read here is still the one
    /// the arm arrived with.
    /// </remarks>
    protected virtual bool OnEmitterBounce(ref GasEmitter emitter, Vector2 normal, int tileX, int tileY) => true;

    /// <summary>
    /// A damaging emitter has passed through a target. Defaults to landing its hit and, when it lands,
    /// delivering the emitter's payload as a direct hit.
    /// </summary>
    /// <remarks>
    /// The place a layer adds what its old burst projectile did in <c>OnHitNPC</c> -- a debuff, a
    /// splash, a reaction roll. Call <c>base</c> to keep the hit itself.
    /// </remarks>
    protected virtual void OnEmitterHitNPC(ref GasEmitter emitter, NPC npc)
    {
        if (Strike(in emitter, npc))
            emitter.Payload?.Hit(npc);
    }

    /// <summary>
    /// Lands an emitter's damage on a target, from the one client entitled to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ownership is checked here rather than at the sweep so that
    /// <see cref="OnEmitterHitNPC" /> still runs everywhere. An override that only wants to throw
    /// dust or play a sound on contact should do that on every client; only the damage has to be
    /// single-sourced, and only the damage is.
    /// </para>
    /// <para>
    /// Credited to a player when one is on the hook for it, so the hit counts toward loot and aggro
    /// the way the burst projectile it replaces did. Ownerless gas -- a world hazard, an ambient
    /// vent -- strikes with no player interaction, exactly as <c>ChemicalReaction.Trigger</c> does.
    /// </para>
    /// </remarks>
    /// <returns>Whether the hit landed on this client.</returns>
    protected bool Strike(in GasEmitter emitter, NPC npc)
    {
        if (emitter.Damage <= 0 || !emitter.IsLocallyOwned)
            return false;

        bool owned = emitter.Owner >= 0 && emitter.Owner < npc.immune.Length;

        // The arms of one burst have to share immunity, and this is the array vanilla shares it
        // through. Left out, a burst is not a hit -- it is as many hits as it has arms, all on the
        // same frame, because SimpleStrikeNPC neither reads nor writes this the way
        // Projectile.Damage does. The carriers this replaced were plain friendly projectiles with
        // no local immunity of their own, so this array is exactly what used to gate them.
        if (owned && npc.immune[emitter.Owner] > 0)
            return false;

        if (owned)
            npc.immune[emitter.Owner] = EMITTER_IMMUNITY;

        int direction = MathF.Sign(emitter.Velocity.X);

        npc.SimpleStrikeNPC(
            emitter.Damage,
            direction == 0 ? 1 : direction,
            crit: false,
            emitter.Knockback,
            EmitterDamageClass,
            noPlayerInteraction: emitter.Owner < 0);

        return true;
    }

    #endregion

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

        /// <summary>The payload of the node that made <see cref="OwnerWeight" />'s contribution.</summary>
        public ChemicalPayload? Payload;

        /// <summary>The largest single contribution <see cref="Owner" /> has made, for comparison.</summary>
        public float OwnerWeight;

        /// <summary>Where the dominant contributor was, so the cell can be sight-tested from it.</summary>
        public Vector2 Source;
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
    /// The player who put most of this pool here, or <see cref="FieldNode.NoOwner" />. Pass it on to
    /// anything left behind, so the residue knows who is allowed to walk through it.
    /// </param>
    /// <param name="payload">The chemical release of that same largest contributor, or null.</param>
    protected virtual void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload) { }

    /// <inheritdoc cref="OnTileField"/>
    protected virtual void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload) { }

    private void UpdateTileField()
    {
        if (!UsesTileField || nodes.Count == 0)
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

            // Coverage is a radial falloff and knows nothing about walls, so a node pooled against
            // one face of a block was depositing into everything within its radius on the far side
            // too. Only tiles the gas could actually reach are allowed to be changed by it.
            if (!HasLineOfSight(cell.Source, pair.Key.X, pair.Key.Y))
                continue;

            Tile tile = Framing.GetTileSafely(pair.Key.X, pair.Key.Y);

            if (tile.LiquidAmount > 0)
                OnLiquidField(pair.Key.X, pair.Key.Y, tile, cell.Coverage, cell.Owner, cell.Payload);

            if (tile.HasTile && Main.tileSolid[tile.TileType])
                OnTileField(pair.Key.X, pair.Key.Y, tile, cell.Coverage, cell.Owner, cell.Payload);
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

        foreach (FieldNode node in nodes)
        {
            float radius = node.Radius;

            if (radius <= 0f || node.Life <= 0f)
                continue;

            // A node that has ended up inside geometry is on its way back out and is drawn by
            // nothing; letting it deposit would convert a sphere of tiles around a point the player
            // cannot see anything at.
            if (node.EnclosedTicks > 0)
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
                        cell.Payload = node.Payload;
                        cell.OwnerWeight = contribution;
                        cell.Source = node.Position;
                    }

                    tileField[key] = cell;
                }
            }
        }
    }

    /// <summary>
    /// Whether this layer's nodes may act on <paramref name="npc"/> at all.
    /// </summary>
    protected virtual bool CanAffect(NPC npc)
        => !npc.friendly && !npc.dontTakeDamage;

    /// <inheritdoc cref="CanAffect(NPC)"/>
    protected virtual bool CanAffect(Player player) => true;

    private void CheckEntityContacts(ref FieldNode node)
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
    protected virtual void OnNPCContact(ref FieldNode node, NPC npc) { }

    protected virtual void OnPlayerContact(ref FieldNode node, Player player) { }

}
