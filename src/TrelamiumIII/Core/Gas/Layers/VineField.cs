using System.Collections.Generic;
using System.Linq;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

/// <summary>A kind of vine: the seed that grows it, the element it afflicts with, and its blossom and dust.</summary>
/// <param name="Key">Name prefix for its items; empty for the default strain.</param>
/// <param name="Herb">The herb whose name the strain takes, or 0 for the default strain.</param>
public sealed record VineStrain(string Key, int Seed, int Herb, ChemicalDamageType Element, Color Blossom, int Dust)
{
    /// <summary>Index into <see cref="All" />. Stored in <see cref="FieldNode.Variant" /> and projectile <c>ai[0]</c>.</summary>
    public byte Id { get; private init; }

    public bool IsDefault => Id == 0;

    /// <summary>Every strain; the first is the default. Order is the id, so appending is safe and reordering is not.</summary>
    public static readonly VineStrain[] All = Number(
        new("", ItemID.GrassSeeds, 0, ChemicalDamageType.Solute, new Color(240, 240, 205), DustID.GrassBlades),
        new("Daybloom", ItemID.DaybloomSeeds, ItemID.Daybloom, ChemicalDamageType.Kinetic, new Color(255, 214, 70), DustID.YellowTorch),
        new("Blinkroot", ItemID.BlinkrootSeeds, ItemID.Blinkroot, ChemicalDamageType.Kinetic, new Color(255, 165, 70), DustID.OrangeTorch),
        new("Moonglow", ItemID.MoonglowSeeds, ItemID.Moonglow, ChemicalDamageType.Caustic, new Color(110, 205, 255), DustID.BlueTorch),
        new("Waterleaf", ItemID.WaterleafSeeds, ItemID.Waterleaf, ChemicalDamageType.Cryonic, new Color(120, 165, 255), DustID.Water),
        new("Fireblossom", ItemID.FireblossomSeeds, ItemID.Fireblossom, ChemicalDamageType.Thermal, new Color(255, 105, 40), DustID.Torch),
        new("Shiverthorn", ItemID.ShiverthornSeeds, ItemID.Shiverthorn, ChemicalDamageType.Cryonic, new Color(205, 235, 255), DustID.Frost),
        new("Deathweed", ItemID.DeathweedSeeds, ItemID.Deathweed, ChemicalDamageType.Necrotic, new Color(170, 80, 205), DustID.Shadowflame));

    public static VineStrain Get(byte id) => id < All.Length ? All[id] : All[0];

    private static VineStrain[] Number(params VineStrain[] strains)
        => strains.Select(static (strain, index) => strain with { Id = (byte)index }).ToArray();
}

/// <summary>
///     Vines. <see cref="Sprout" /> drops a seed pod that lands and grows runs along the terrain, climbing walls, and sends a
///     lash that chains between nearby enemies and entangles them. Runs and lashes carry their <see cref="VineStrain" />.
/// </summary>
public sealed class VineField : ChemicalGasField
{
    /// <summary>Pixels a run grows per tick.</summary>
    private const float RUN_SPEED = 2.4f;

    /// <summary>Pixels a run node sits out from its crawler.</summary>
    private const float NODE_LIFT = 3f;

    private const float LEAF_CHANCE = 0.3f;

    /// <summary>Alpha above which a pinned node keeps its full weight, so a run holds its shape before it thins.</summary>
    private const float SET_ALPHA = 0.35f;

    /// <summary>Alpha above which a node is fully green. Below it the node withers, reaching dry at <see cref="DRY_ALPHA" />.</summary>
    private const float FRESH_ALPHA = 0.55f;

    private const float DRY_ALPHA = 0.1f;

    private const float LASH_SPEED = 9f;

    private const float LASH_RANGE = 16f * 14f;

    /// <summary>Further enemies a lash chains to after its first.</summary>
    private const int LASH_HOPS = 3;

    /// <summary>Damage and exposure kept per hop.</summary>
    private const float LASH_FALLOFF = 0.75f;

    /// <summary>Ticks a lash may chase one target before it gives up.</summary>
    private const int LASH_PATIENCE = 60;

    private const int ENTANGLE_TICKS = 100;

    private const int BLOSSOM_SPACING = 11;

    private const int DUST_CHANCE = 400;

    private static EmitterProfile RunProfile => EmitterProfile.Default with
    {
        Lifetime = 360,
        NodeRadius = 9f,
        NodeLife = 420f,
        Interval = 2,
        Gravity = 0.3f,
        Drag = 0.99f,
        Bounces = 4,
        Restitution = 0.3f,
        TileCollide = true,
    };

    /// <summary>A lash in flight: it chases <see cref="Target" />, latches on, then leaps to the nearest enemy it hasn't hit.</summary>
    private sealed class Lash
    {
        public Vector2 Position;

        public int Target;

        public int HopsLeft;

        public float Scale = 1f;

        public int Age;

        public readonly List<int> Visited = [];

        public byte Strain;

        public int Owner;

        public int Damage;

        public float Knockback;

        public ChemicalPayload? Payload;
    }

    private readonly List<Lash> lashes = [];

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Solute;

    public override int ChemicalDamage => 1;

    public override int AfflictionDuration => 90;

    public override float AfflictionRate => 30f;

    public override int ReactionChance => 0;

    public override int ReactionDamage => 0;

    /// <summary>Over mud and gel, under the bright liquids.</summary>
    public override int LoadOrder => 25;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 1f;

    protected override float GravityScale => 0.2f;

    /// <summary>Equal to <see cref="GravityScale" />, so a node at density 0.5 hangs where it was left.</summary>
    protected override float BuoyancyForce => 0.2f;

    protected override float Drag => 0.92f;

    protected override float EnclosedLifePenalty => 1f;

    /// <summary>Run nodes carry their direction as velocity, so they stretch along the vine.</summary>
    protected override float StampStretch => 0.9f;

    protected override float FlowReferenceSpeed => 4f;

    protected override CrawlRules CrawlRules => CrawlRules.Climbing;

    protected override float CrawlSpeed(in GasEmitter emitter) => RUN_SPEED * (emitter.Payload?.Stats.BurstSpeedMultiplier ?? 1f);

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.Turbulence_05.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTexture = null;
        lashes.Clear();
    }

    /// <summary>
    ///     Drops a pod at <paramref name="origin" /> that grows <paramref name="volume" /> pixels of vine where it lands and,
    ///     when <paramref name="lash" /> is set, lashes the nearest visible enemy for <paramref name="damage" />.
    /// </summary>
    public void Sprout(
        Vector2 origin,
        Vector2 momentum,
        float volume,
        VineStrain strain,
        int owner = FieldNode.NoOwner,
        int damage = 0,
        float knockback = 0f,
        ChemicalPayload? payload = null,
        bool lash = true)
    {
        if (volume > 0f)
            SpawnEmitter(RunProfile with { Volume = volume }, origin, momentum, owner, variant: strain.Id, payload: payload);

        if (!lash)
            return;

        List<NPC> nearest = ChemicalTargets.Nearest(origin, LASH_RANGE, 1, npc => !Collision.CanHitLine(origin, 1, 1, npc.Center, 1, 1));

        if (nearest.Count == 0)
            return;

        lashes.Add(new Lash
        {
            Position = origin,
            Target = nearest[0].whoAmI,
            HopsLeft = LASH_HOPS,
            Strain = strain.Id,
            Owner = owner,
            Damage = damage,
            Knockback = knockback,
            Payload = payload,
        });
    }

    public override void Update()
    {
        for (int i = lashes.Count - 1; i >= 0; i--)
        {
            if (!Advance(lashes[i]))
                lashes.RemoveAt(i);
        }

        base.Update();
    }

    #region Runs

    /// <summary>A falling pod that reaches a floor grows both ways along it; against a wall, up and down it. A ceiling deflects it.</summary>
    protected override bool OnEmitterBounce(ref GasEmitter emitter, Vector2 normal, int tileX, int tileY)
    {
        if (emitter.Volume <= 0f || normal.Y > 0.5f)
            return true;

        float half = emitter.Volume * 0.5f;

        if (normal.Y == 0f)
        {
            Vector2 start = SurfaceCrawl.WallStart(emitter.Position, normal, tileX);

            SpawnRun(in emitter, start, CrawlMode.Wall, -Vector2.UnitY, half);
            SpawnRun(in emitter, start, CrawlMode.Wall, Vector2.UnitY, half);
        }
        else
        {
            Vector2 start = SurfaceCrawl.FloorStart(emitter.Position, tileY);

            SpawnRun(in emitter, start, CrawlMode.Floor, -Vector2.UnitX, half);
            SpawnRun(in emitter, start, CrawlMode.Floor, Vector2.UnitX, half);
        }

        if (!Main.dedServ)
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, PitchVariance = 0.3f, MaxInstances = 4 }, emitter.Position);

        return false;
    }

    private void SpawnRun(in GasEmitter from, Vector2 position, CrawlMode mode, Vector2 direction, float volume)
        => SpawnEmitter(RunProfile with { Volume = volume, Crawl = mode }, position, direction, from.Owner, variant: from.Variant, payload: from.Payload);

    /// <summary>A run lays a pinned strand node, sometimes with a leaf beside it. A falling pod sheds a short-lived tuft.</summary>
    protected override void Emit(ref GasEmitter emitter)
    {
        if (emitter.Volume <= 0f)
        {
            base.Emit(ref emitter);
            return;
        }

        if (emitter.Crawl == CrawlMode.None)
        {
            SpawnNode(emitter.Position, Vector2.Zero, emitter.NodeRadius * 0.8f, 8f, owner: CosmeticOwnerID, variant: emitter.Variant);
            return;
        }

        Vector2 outward = SurfaceCrawl.Outward(in emitter);
        Vector2 along = emitter.Velocity.SafeNormalize(new Vector2(-outward.Y, outward.X));

        SpawnNode(
            emitter.Position + outward * (NODE_LIFT + Main.rand.NextFloat(-1f, 1.5f)),
            along * FlowReferenceSpeed,
            emitter.NodeRadius * Main.rand.NextFloat(0.85f, 1.1f),
            emitter.NodeLife * Main.rand.NextFloat(0.9f, 1f),
            owner: emitter.Owner,
            payload: emitter.Payload,
            pinned: true,
            variant: emitter.Variant);

        if (!Main.rand.NextBool((int)(1f / LEAF_CHANCE)))
            return;

        float tilt = Main.rand.NextFloat(0.5f, 1f) * (Main.rand.NextBool() ? 1f : -1f);

        SpawnNode(
            emitter.Position + outward * Main.rand.NextFloat(6f, 9f),
            (along * MathF.Sign(tilt) + outward).SafeNormalize(outward).RotatedBy(tilt * 0.3f) * FlowReferenceSpeed * 0.6f,
            emitter.NodeRadius * Main.rand.NextFloat(0.8f, 1f),
            emitter.NodeLife * Main.rand.NextFloat(0.8f, 0.95f),
            owner: emitter.Owner,
            payload: emitter.Payload,
            pinned: true,
            variant: emitter.Variant);
    }

    #endregion

    #region Lashes

    /// <summary>Moves a lash one tick toward its target, laying its trail. False once it is spent.</summary>
    private bool Advance(Lash lash)
    {
        NPC target = Main.npc[lash.Target];

        if (!target.active || target.life <= 0 || ++lash.Age > LASH_PATIENCE)
            return false;

        Vector2 toTarget = target.Center - lash.Position;
        float distance = toTarget.Length();
        Vector2 step = distance <= LASH_SPEED ? toTarget : toTarget / distance * LASH_SPEED;

        Trail(lash, lash.Position, lash.Position + step);
        lash.Position += step;

        if (distance > LASH_SPEED)
            return true;

        Latch(lash, target, MathF.Sign(toTarget.X));

        return Leap(lash, target);
    }

    private void Trail(Lash lash, Vector2 from, Vector2 to)
    {
        if (Main.dedServ)
            return;

        float length = Vector2.Distance(from, to);
        Vector2 along = (to - from).SafeNormalize(Vector2.UnitX);

        for (float travelled = 0f; travelled < length; travelled += 6f)
        {
            SpawnNode(
                from + along * travelled + Main.rand.NextVector2Circular(1.5f, 1.5f),
                along * 0.3f,
                Main.rand.NextFloat(6f, 7.5f),
                Main.rand.NextFloat(22f, 30f),
                density: 0.5f,
                owner: CosmeticOwnerID,
                variant: lash.Strain);
        }
    }

    /// <summary>
    ///     Strikes and entangles <paramref name="target" /> and wraps it in vine. The first latch is a full hit that can
    ///     react; later ones expose at the lash's falling <see cref="Lash.Scale" />.
    /// </summary>
    private void Latch(Lash lash, NPC target, int direction)
    {
        Strike(target, (int)MathF.Round(lash.Damage * lash.Scale), lash.Knockback * lash.Scale, direction, lash.Owner);

        if (lash.Payload is { } payload)
        {
            if (lash.Visited.Count == 0)
                payload.Hit(target, this);
            else
                payload.Derived.Apply(target, ExposureKind.Hit, lash.Scale, this);
        }

        if (ChemicalPayload.IsAuthority(lash.Owner))
            target.AddBuff(ModContent.BuffType<Entangled>(), ENTANGLE_TICKS);

        Wrap(lash, target);

        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.2f, PitchVariance = 0.25f, MaxInstances = 4 }, target.Center);

        for (int i = 0; i < 8; i++)
            Dust.NewDust(target.position, target.width, target.height, VineStrain.Get(lash.Strain).Dust, 0f, -1f);
    }

    /// <summary>Vine nodes riding <paramref name="target" /> for as long as it is entangled. Their payload never touches the host.</summary>
    private void Wrap(Lash lash, NPC target)
    {
        ChemicalPayload? wrap = lash.Payload?.Derive(null, target.whoAmI);
        int count = Math.Clamp((target.width + target.height) / 12, 3, 8);

        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.3f, 0.3f);
            Vector2 offset = angle.ToRotationVector2() * new Vector2(target.width * 0.45f, target.height * 0.45f);

            SpawnAnchoredNode(
                target,
                target.Center + offset,
                offset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitX) * 0.01f,
                Main.rand.NextFloat(5.5f, 7f),
                ENTANGLE_TICKS + 20f,
                ENTANGLE_TICKS,
                owner: lash.Owner,
                payload: wrap,
                variant: lash.Strain);
        }
    }

    /// <summary>Retargets the lash on the nearest visible enemy it hasn't hit. False when none is in range or it has no hops left.</summary>
    private static bool Leap(Lash lash, NPC from)
    {
        lash.Visited.Add(from.whoAmI);

        if (lash.HopsLeft <= 0)
            return false;

        List<NPC> next = ChemicalTargets.Nearest(
            from.Center,
            LASH_RANGE,
            1,
            npc => lash.Visited.Contains(npc.whoAmI) || !Collision.CanHitLine(from.Center, 1, 1, npc.Center, 1, 1));

        if (next.Count == 0)
            return false;

        lash.Target = next[0].whoAmI;
        lash.HopsLeft--;
        lash.Scale *= LASH_FALLOFF;
        lash.Age = 0;

        return true;
    }

    #endregion

    #region Nodes

    /// <summary>Exposes to the vine's chemistry, entangles, and adds the strain element's contact debuff.</summary>
    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        if (!ChemicalPayload.Contact(npc, this, node.Payload, node.Owner, this))
            return;

        ChemicalControlGlobalNPC.Refresh<Entangled>(npc, 45);

        ElementProfile element = ElementProfile.For(VineStrain.Get(node.Variant).Element);

        if (element.ContactBuff >= 0)
            npc.AddBuff(element.ContactBuff, element.ContactBuffTicks);
    }

    /// <summary>A pinned node whose supporting tile is gone drops. Freshness rides the temperature channel, which the palette withers along.</summary>
    protected override void UpdateNode(ref FieldNode node, int index)
    {
        if (node.Pinned && !node.IsAnchored && !SurfaceCrawl.IsSupported(node.Position))
        {
            node.Pinned = false;
            node.Velocity = Vector2.Zero;
        }

        base.UpdateNode(ref node, index);

        node.Temperature = MathHelper.Lerp(TemperatureRange.X, TemperatureRange.Y, Freshness(in node));
    }

    private static float Freshness(in FieldNode node) => Utils.GetLerpValue(DRY_ALPHA, FRESH_ALPHA, node.Alpha, clamped: true);

    protected override float NodeWeight(in FieldNode node)
        => FieldStamp.Weight(node.Pinned || node.IsAnchored ? node.Alpha / SET_ALPHA : node.Alpha);

    protected override void ApplyLighting(ref FieldNode node)
    {
        if (Main.dedServ || !node.Pinned || !Main.rand.NextBool(DUST_CHANCE))
            return;

        Dust dust = Dust.NewDustPerfect(
            node.Position + Main.rand.NextVector2Circular(node.Radius * 0.6f, node.Radius * 0.6f),
            VineStrain.Get(node.Variant).Dust,
            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.2f)),
            0,
            default,
            Main.rand.NextFloat(0.6f, 0.9f));

        dust.noGravity = true;
    }

    #endregion

    #region Rendering

    /// <summary>Whether a strand node carries a blossom: every few nodes, by position, so the pattern holds from frame to frame.</summary>
    private static bool HasBlossom(in FieldNode node)
        => node.Pinned && ((int)node.Position.X * 7 + (int)node.Position.Y * 13 & 0x7FFFFFFF) % BLOSSOM_SPACING == 0;

    /// <summary>Four-petal pixel blossoms in the strain's colour, closing as the vine dries.</summary>
    protected override void DrawOverlay(SpriteBatch spriteBatch)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        var source = new Rectangle(0, 0, 1, 1);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix);

        foreach (FieldNode node in nodes)
        {
            if (!HasBlossom(in node))
                continue;

            float open = Utils.GetLerpValue(DRY_ALPHA, FRESH_ALPHA, node.Alpha, clamped: true);

            if (open <= 0f)
                continue;

            Color petal = VineStrain.Get(node.Variant).Blossom * open;
            var centre = (node.Position - Main.screenPosition).ToPoint();

            spriteBatch.Draw(pixel, new Rectangle(centre.X - 1, centre.Y - 3, 2, 2), source, petal);
            spriteBatch.Draw(pixel, new Rectangle(centre.X - 1, centre.Y + 1, 2, 2), source, petal);
            spriteBatch.Draw(pixel, new Rectangle(centre.X - 3, centre.Y - 1, 2, 2), source, petal);
            spriteBatch.Draw(pixel, new Rectangle(centre.X + 1, centre.Y - 1, 2, 2), source, petal);
            spriteBatch.Draw(pixel, new Rectangle(centre.X - 1, centre.Y - 1, 2, 2), source, new Color(255, 236, 150) * open);
        }

        spriteBatch.End();
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.35f));

        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.4f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.18f);
        effect.Parameters["NoiseDrift"]?.SetValue(Vector2.Zero);
        effect.Parameters["FlowStrength"]?.SetValue(0.25f);

        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(1f);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.9f);
        effect.Parameters["OutlineAlpha"]?.SetValue(1f);
        effect.Parameters["HighlightAlpha"]?.SetValue(0.6f);

        // Fresh: green, darkest at the rim.
        effect.Parameters["TemperatureBlend"]?.SetValue(1f);
        effect.Parameters["HotShadowColor"]?.SetValue(new Vector3(0.07f, 0.22f, 0.05f));
        effect.Parameters["HotBodyColor"]?.SetValue(new Vector3(0.2f, 0.5f, 0.11f));
        effect.Parameters["HotCoreColor"]?.SetValue(new Vector3(0.36f, 0.68f, 0.17f));

        // Dry: brown and straw.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.2f, 0.16f, 0.07f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.33f, 0.28f, 0.12f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.45f, 0.4f, 0.18f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.1f, 0.08f, 0.04f));
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(0.5f, 0.45f, 0.25f));
    }

    #endregion
}
