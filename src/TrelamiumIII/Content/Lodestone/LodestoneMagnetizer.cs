using ReLogic.Utilities;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Lodestone;

public class LodestoneMagnetizer : ModItem
{
    public const int PICK_POWER = 25;

    public const int TILE_BOOST = 15;

    public override void SetDefaults()
    {
        Item.useTime = Item.useAnimation = 7;
        Item.width = Item.height = 32;

        Item.useTurn = true;

        Item.value = Item.sellPrice(gold: 4);

        Item.useStyle = ItemUseStyleID.Shoot;
        Item.rare = ItemRarityID.Green;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shootSpeed = 7f;
        Item.shoot = ModContent.ProjectileType<LodestoneMagnetizerProjectile>();

        Item.tileBoost = TILE_BOOST;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var line = new TooltipLine(Mod, "PickPower", $"{PICK_POWER}{Lang.tip[26].Value}");
        var line2 = new TooltipLine(Mod, "UseSpeed", $"Egregiously {Lang.tip[8].Value}");

        int index = tooltips.FindIndex(l => l.Mod == "Terraria" && l.Name.StartsWith("Tooltip"));

        if (index < 0)
        {
            tooltips.Add(line2);

            tooltips.Add(line);
        }
        else
        {
            tooltips.Insert(index, line2);

            tooltips.Insert(index, line);
        }
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero,
            ModContent.ProjectileType<LodestoneMagnetizerProjectile>(), damage, knockback, player.whoAmI);

        return false;
    }

    public override bool CanUseItem(Player player)
        => player.ownedProjectileCounts[ModContent.ProjectileType<LodestoneMagnetizerProjectile>()] <= 0;

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient<LodestoneItem>(25)
        .AddIngredient<MagnetizedCoil>(8)
        .AddRecipeGroup(nameof(ItemID.CopperBar), 15)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class LodestoneMagnetizerProjectile : ModProjectile, IDrawPrimitive
{
    public override string Texture => AssetReferences.Content.Lodestone.LodestoneMagnetizer.KEY;

    #region tuning

    /// <summary>How far the gun floats from the player, along the aim vector.</summary>
    private const float MUZZLE_OFFSET = 32f;

    /// <summary>
    ///     Full angle of the mining cone. Public because the drawn cone is sized from it: the field is
    ///     the readout for what the beam reaches, so drawing it at any other angle would make it lie.
    /// </summary>
    public const float ARC_ANGLE = MathHelper.Pi / 6;

    private const int MINING_SPEED = 5;

    private const int CONE_RAYS = 9;

    /// <summary>
    ///     Intake radius around the gun at <see cref="LodestoneMagnetizerItem.PICK_POWER" />, and how
    ///     fast items come in. Both scale with the held pick so a future upgrade tier only has to
    ///     change one number on the item.
    /// </summary>
    private const float VACUUM_RANGE = 300f;
    private const float VACUUM_SPEED = 8f;

    /// <summary>Pick power the intake constants are quoted at.</summary>
    private const float VACUUM_REFERENCE_PICK = 25f;

    /// <summary>
    ///     How far the aim has to move before the owner spends a packet on it. Below this the remote
    ///     copy is close enough that <see cref="_smoothAim" /> hides the difference.
    /// </summary>
    private const float SYNC_THRESHOLD = 6f;

    /// <summary>Ticks between forced resyncs, so a slow drift cannot accumulate unbounded.</summary>
    private const int SYNC_INTERVAL = 12;

    /// <summary>Ticks the field takes to reach full strength. Short: this is a tool, not a charge-up.</summary>
    private const int SPOOL_TICKS = 14;

    /// <summary>Ore blobs added to the stream per tick while the beam is biting.</summary>
    private const int ORE_PER_TICK = 2;

    /// <summary>Ticks between chips struck off the rock. Slower than the ore stream, because a chip is a distinct object and a torrent of them reads as noise.</summary>
    private const int CHIP_INTERVAL = 3;

    /// <summary>Light the beam throws, at full strength, as a fraction of the field colour.</summary>
    private const float LIGHT_STRENGTH = 0.35f;

    /// <summary>Points along the beam that emit light. Enough to light a corridor, few enough to be free.</summary>
    private const int LIGHT_SAMPLES = 5;

    // ---- trail -------------------------------------------------------------------------------

    /// <summary>
    ///     Points in the ribbon's spine.
    /// </summary>
    /// <remarks>
    ///     A vertex trail's resolution is a point budget, which is a real problem for a trail built
    ///     from POSITION HISTORY. Too few points to describe a curve and it kinks, too few to cover
    ///     the distance and it stops short. Neither applies here, because this spine is generated
    ///     procedurally between the muzzle and the aim point every frame, so it always spans exactly
    ///     the beam however long that is. All the count has to do is resolve the sine, and forty points
    ///     across two-odd waves is more than enough.
    /// </remarks>
    private const int TRAIL_POINTS = 40;

    /// <summary>Waves along the beam. Low: two big lazy coils read as a heavy pull, six read as a wire.</summary>
    private const float TRAIL_WAVES = 1.15f;

    /// <summary>Radians per tick the sine travels.</summary>
    private const float TRAIL_SINE_SPEED = 0.3f;

    /// <summary>
    ///     Peak sideways throw of the sine, in pixels, and the cap on it as a fraction of beam length.
    /// </summary>
    /// <remarks>
    ///     Held in world pixels rather than as a fraction, so the coil is the same physical size at
    ///     every range and a long beam does not become a giant lazy S. The fraction is only a floor
    ///     guard: at point-blank the beam is shorter than the throw, and a ribbon thrown further
    ///     sideways than it travels forward stops reading as a beam at all.
    /// </remarks>
    private const float TRAIL_AMPLITUDE = 30f;
    private const float TRAIL_AMPLITUDE_CAP = 0.2f;

    /// <summary>Ribbon half-width at the muzzle and at the aim point. Opens outward: this is an intake.</summary>
    private const float TRAIL_WIDTH_MUZZLE = 13f;
    private const float TRAIL_WIDTH_FAR = 48f;

    /// <summary>
    ///     Side of the chunky pixel the ribbon is quantised into, in world pixels.
    /// </summary>
    /// <remarks>
    ///     <c>pixelTrail.fx</c> quantises by <c>resolution / pixelation</c>, and every other caller
    ///     passes the screen size as the resolution -- which for a trail is wrong, and wrong in a way
    ///     that looks like the parameter doing nothing. The shader's UV is trail-relative, not
    ///     screen-relative, so screen resolution over a pixelation of 2 gives 960 cells across the
    ///     ribbon's length: sub-pixel, i.e. no visible quantisation whatever the value.
    ///     <para>
    ///         Passing the ribbon's OWN pixel dimensions as the resolution instead makes the division
    ///         mean what it reads like -- cells this many world pixels on a side, square on both axes.
    ///     </para>
    /// </remarks>
    private const float TRAIL_PIXEL = 12f;

    /// <summary>
    ///     Chunky bands across the ribbon's width, independent of <see cref="TRAIL_PIXEL" />.
    /// </summary>
    /// <remarks>
    ///     The shader has one pixelation scalar for both axes, and the two axes do not want the same
    ///     thing. Along the beam the cell size IS the effect -- that is what pixelates the rings. Across
    ///     the width it only has to be fine enough that the ribbon still reads as a ribbon, and tying
    ///     it to the same figure means coarsening the rings silently flattens the ribbon into three or
    ///     four flat stripes. Feeding <c>TRAIL_BANDS * TRAIL_PIXEL</c> as the vertical resolution makes
    ///     the shader's own division cancel the coupling out, so this many bands survive whatever the
    ///     cell size becomes.
    /// </remarks>
    private const float TRAIL_BANDS = 11f;

    private const float TRAIL_REPEATS = 3f;

    /// <summary>
    ///     Beam-lengths per second the sine's own features travel. Derived, not tuned.
    /// </summary>
    /// <remarks>
    ///     A feature of the sine sits at a constant phase, so <c>factor * WAVES * TAU + t * SPEED</c> is
    ///     fixed for it and it therefore slides along the beam at <c>SPEED / (WAVES * TAU)</c> per tick.
    ///     This exists so the grain below can be quoted against it instead of guessed at.
    /// </remarks>
    private const float TRAIL_SINE_TRAVEL = TRAIL_SINE_SPEED * 60f / (TRAIL_WAVES * MathHelper.TwoPi);

    /// <summary>
    ///     How fast the grain scrolls, as a multiple of <see cref="TRAIL_SINE_TRAVEL" />.
    /// </summary>
    /// <remarks>
    ///     A ratio rather than a rate of its own, because as two independent numbers they disagreed and
    ///     there was nothing in the code to say they should not: the coil crossed the beam about two and
    ///     a half times a second while the grain managed 1.6, and material moving slower than the shape
    ///     carrying it reads as a coil sliding over something standing still. Above 1 the material
    ///     outruns the coil, which is what being sucked down a pipe looks like.
    ///     <para>
    ///         Applied negatively at the call site: the spine runs from the aim point to the muzzle and
    ///         the shader ADDS this to its sample coordinate, so a positive value slides the grain
    ///         outward -- which reads as firing. This is a vacuum.
    ///     </para>
    /// </remarks>
    private const float TRAIL_SCROLL_RATIO = 1.4f;

    /// <summary>What the ribbon brightens to as it enters the gun. Cool and near-white; the ore colour supplies the other end.</summary>
    private static readonly Color TrailCoreColor = new(214, 240, 255);

    #endregion

    /// <summary>
    ///     Aim point, as an offset from the owner's centre, already clamped to reach. Lives in
    ///     <see cref="Projectile.ai" /> because those two floats are part of the standard projectile
    ///     sync packet -- no custom net code needed, and it survives a late joiner syncing the world.
    /// </summary>
    public Vector2 AimOffset
    {
        get => new(Projectile.ai[0], Projectile.ai[1]);
        set { Projectile.ai[0] = value.X; Projectile.ai[1] = value.Y; }
    }

    /// <summary>Where the intake starts. The gun's own position.</summary>
    public Vector2 Muzzle => Projectile.Center;

    /// <summary>Where it ends, in world space. Cached each tick so the draw side never recomputes it.</summary>
    public Vector2 Focus { get; private set; }

    /// <summary>The colour the field and the ore stream are drawn in, eased toward whatever is targeted.</summary>
    public Color FieldColor { get; private set; } = new(150, 150, 150);

    /// <summary>
    ///     0..1 overall field strength: the spool-up envelope, modulated by whether the beam is
    ///     actually biting into something. The dip when it is pointed at nothing is what makes
    ///     connecting with a vein feel like connecting with it.
    /// </summary>
    public float Intensity
    {
        get
        {
            float spool = MathHelper.Clamp(_age / (float)SPOOL_TICKS, 0f, 1f);

            return spool * MathHelper.Lerp(0.72f, 1f, _bite);
        }
    }

    /// <summary>
    ///     What everything actually reads. The synced value only lands every few ticks, so following it
    ///     raw would make the gun visibly step on remote clients; this chases it instead.
    /// </summary>
    private Vector2 _smoothAim;
    private bool _aimPrimed;

    private int _syncTimer;
    private int _miningTimer;
    private int _age;

    /// <summary>0..1 smoothed "is the cone on something breakable".</summary>
    private float _bite;

    /// <summary>The nearest breakable tile in the cone this tick, or null if there was none.</summary>
    private (ushort Type, Vector2 Position)? _contact;

    private readonly HashSet<int> _magnetizedItems = [];

    /// <summary>
    ///     The ribbon's spine. <c>[0]</c> is the aim point and <c>[^1]</c> the muzzle, matching
    ///     <see cref="VertexTrail" />'s convention that the last position is the head -- so the trail's
    ///     own factor runs 0 at the far end to 1 at the gun, and every width and colour function below
    ///     reads in those terms.
    /// </summary>
    private readonly Vector2[] _spine = new Vector2[TRAIL_POINTS];

    /// <summary>
    ///     Built once and kept. Its width and colour functions close over this projectile, so they see
    ///     the current <see cref="FieldColor" /> and <see cref="Intensity" /> every frame without
    ///     needing to be rebuilt -- and rebuilding would mean reallocating the staging arrays and
    ///     re-deriving the index winding on every frame of every magnetizer.
    /// </summary>
    private VertexTrail? _trail;

    private SlotId _startupSoundId = SlotId.Invalid;
    private SlotId _loopSoundId = SlotId.Invalid;
    private bool _hasPlayedStartup;
    private bool _hasStartedLoop;
    private int _soundTimer;

    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 26;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.ownerHitCheck = true;
        Projectile.netImportant = true;
    }

    /// <summary>
    ///     Collapses the beam onto the muzzle before the first tick.
    /// </summary>
    /// <remarks>
    ///     <see cref="Intensity" /> is already zero on the frame a magnetizer spawns, so the renderer
    ///     would skip it either way -- but leaving <see cref="Focus" /> at the world origin means the
    ///     one value the draw side reads is nonsense until <see cref="AI" /> has run once, and a value
    ///     that is only valid most of the time is the kind that eventually gets read at the wrong
    ///     moment.
    /// </remarks>
    public override void OnSpawn(IEntitySource source) => Focus = Projectile.Center;

    /// <summary>
    ///     Total reach in pixels: the player's own tile range, the held item's boost, and any
    ///     block-range accessory. Same three terms vanilla adds up in <c>Player.PlaceThing</c>, so the
    ///     beam stops exactly where hand mining would.
    /// </summary>
    private static float MaxReach(Player owner)
    {
        // tileRangeX is static on Player -- it is the global base reach, not a per-player stat. The
        // per-player parts are the held item's boost and blockRange from accessories.
        int tiles = Player.tileRangeX + owner.HeldItem.tileBoost + owner.blockRange;

        return tiles * 16f;
    }

    /// <summary>
    ///     Read off the constant rather than <c>HeldItem.pick</c>, which is zero by design -- see
    ///     <see cref="LodestoneMagnetizerItem.PICK_POWER" />.
    /// </summary>
    private static int PickPower() => LodestoneMagnetizer.PICK_POWER;

    public override void AI()
    {
        var owner = Main.player[Projectile.owner];

        if (!owner.active || owner.dead || !owner.channel || owner.noItems || owner.CCed)
        {
            Projectile.Kill();
            return;
        }

        // Refreshed every tick, the way a held projectile has to be. Left at the 3600 default it
        // would quietly expire after a minute of continuous channelling, taking the beam with it
        // while the player is still holding the button.
        Projectile.timeLeft = 2;

        UpdateAim(owner);

        Projectile.Center = owner.Center + _smoothAim.SafeNormalize(Vector2.UnitX) * MUZZLE_OFFSET;
        Projectile.rotation = _smoothAim.ToRotation();
        Projectile.direction = Projectile.spriteDirection = _smoothAim.X >= 0f ? 1 : -1;

        Focus = owner.Center + _smoothAim;

        owner.ChangeDir(Projectile.direction);
        SetOwnerAnimation(owner);

        HandleSounds();

        // One pure read of the cone, on every client. The owner is the only one allowed to act on it.
        ScanCone(owner, mine: Projectile.owner == Main.myPlayer);

        UpdateFieldColor();

        if (!Main.dedServ)
        {
            BuildSpine();
            EmitOre();
            EmitLight();
        }

        // Item intake mutates world state, so only the owner runs it; everyone else just draws.
        if (Projectile.owner == Main.myPlayer)
            VacuumItems();

        _miningTimer++;
        _age++;
    }

    /// <summary>
    ///     Owner reads the cursor, clamps it to reach and publishes it. Everyone -- owner included --
    ///     then eases toward whatever is published, so the gun moves identically on every client.
    /// </summary>
    private void UpdateAim(Player owner)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            Vector2 desired = Main.MouseWorld - owner.Center;

            float reach = MaxReach(owner);

            if (desired.LengthSquared() > reach * reach)
                desired = desired.SafeNormalize(Vector2.UnitX) * reach;
            else if (desired.LengthSquared() < 1f)
                desired = Vector2.UnitX * owner.direction;

            _syncTimer++;

            // Rate-limited rather than every frame. The old code set netUpdate unconditionally, which
            // is a projectile packet per tick per magnetizer for a value that barely moves.
            if (Vector2.DistanceSquared(desired, AimOffset) > SYNC_THRESHOLD * SYNC_THRESHOLD
                || _syncTimer >= SYNC_INTERVAL)
            {
                AimOffset = desired;
                Projectile.netUpdate = true;
                _syncTimer = 0;
            }
        }

        Vector2 target = AimOffset;

        if (target.LengthSquared() < 1f)
            target = Vector2.UnitX * owner.direction;

        // Snapped on the first tick so the beam does not sweep in from wherever ai[] started.
        if (!_aimPrimed)
        {
            _smoothAim = target;
            _aimPrimed = true;
        }
        else
        {
            _smoothAim = Vector2.Lerp(_smoothAim, target, 0.35f);
        }
    }

    /// <summary>
    ///     Walks the cone, records the nearest breakable tile for the visuals, and -- on the owner, on
    ///     a mining tick -- breaks everything it passes.
    /// </summary>
    /// <remarks>
    ///     One pass for both jobs. Splitting it gave two copies of the arc walk that could disagree
    ///     about what the beam was pointed at, which is how a beam ends up glowing the colour of one
    ///     tile while chewing through another.
    /// </remarks>
    private void ScanCone(Player owner, bool mine)
    {
        _contact = null;

        Vector2 start = Muzzle;
        Vector2 delta = Focus - start;
        float distance = delta.Length();

        if (distance < 1f)
        {
            _bite = MathHelper.Lerp(_bite, 0f, 0.12f);
            return;
        }

        float baseAngle = delta.ToRotation();
        int pick = PickPower();

        // Hoisted out of the loops: constant for the whole tick, and testing it per tile was doing
        // the same arithmetic a hundred-odd times to get the same answer.
        bool miningTick = mine && _miningTimer % MINING_SPEED == 0;

        // Tracked by distance rather than by taking the first hit found, because the rays are walked
        // one whole ray at a time -- so "first" is the nearest tile on the cone's leftmost edge, not
        // the nearest tile in the cone. The effects want the tile the player is aiming AT.
        float nearest = float.MaxValue;

        for (int r = 0; r < CONE_RAYS; r++)
        {
            float offset = MathHelper.Lerp(-ARC_ANGLE / 2f, ARC_ANGLE / 2f, r / (float)(CONE_RAYS - 1));

            Vector2 ray = (baseAngle + offset).ToRotationVector2();

            for (float travelled = 0f; travelled <= distance; travelled += 16f)
            {
                Vector2 point = start + ray * travelled;

                int tileX = (int)(point.X / 16f);
                int tileY = (int)(point.Y / 16f);

                if (!WorldGen.InWorld(tileX, tileY, 1))
                    continue;

                Tile tile = Main.tile[tileX, tileY];

                if (!tile.HasTile || !IsMagnetizable(tile))
                    continue;

                if (travelled < nearest)
                {
                    nearest = travelled;
                    _contact = (tile.TileType, point);
                }

                if (miningTick)
                    BreakTile(tileX, tileY, owner, pick);
            }
        }

        _bite = MathHelper.Lerp(_bite, _contact.HasValue ? 1f : 0f, 0.12f);
    }

    /// <summary>
    ///     What the beam is willing to pull apart: anything the spelunker potion would highlight, plus
    ///     Hellstone.
    /// </summary>
    /// <remarks>
    ///     The herb exclusion was originally written as <c>is not 82 or 83 or 84</c>, which parses as
    ///     <c>(is not 82) or 83 or 84</c> and so only ever excluded 82. It is regrouped here, and the
    ///     Hellstone branch sits inside the tile check rather than beside it -- as a top-level
    ///     or-clause it was reading TileType out of empty tiles.
    /// </remarks>
    private static bool IsMagnetizable(Tile tile)
    {
        if (tile.TileType == TileID.Hellstone)
            return true;

        return Main.tileSpelunker[tile.TileType]
               && tile.TileType is not (TileID.Plants or TileID.MatureHerbs or TileID.BloomingHerbs);
    }

    /// <summary>
    ///     <paramref name="pick" /> is the held item's real pickaxe power, so the tile requirement
    ///     checks inside <see cref="Player.PickTile" /> apply exactly as they would to a swing. That is
    ///     what stops the beam quietly chewing through meteorite.
    /// </summary>
    private static void BreakTile(int x, int y, Player player, int pick)
    {
        player.PickTile(x, y, pick);

        if (Main.netMode is NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
    }

    private void VacuumItems()
    {
        _magnetizedItems.Clear();

        // Intake scales off the pick, so the gun's stated power is also what you feel.
        float power = PickPower() / VACUUM_REFERENCE_PICK;
        float range = VACUUM_RANGE * MathHelper.Clamp(power, 0.5f, 2f);
        float speed = VACUUM_SPEED * MathHelper.Clamp(power, 0.5f, 2f);

        for (int i = 0; i < Main.maxItems; i++)
        {
            Item item = Main.item[i];

            if (!item.active || item.noGrabDelay != 0 || item.type == ItemID.None)
                continue;

            if (!ItemUtils.IsAMetalItem(item) && !item.IsGem())
                continue;

            float distance = Vector2.Distance(item.Center, Projectile.Center);

            if (distance > range)
                continue;

            item.beingGrabbed = true;

            Vector2 toMagnet = (Projectile.Center - item.Center).SafeNormalize(Vector2.Zero);

            item.velocity = toMagnet * MathHelper.Lerp(speed, 1f, distance / range);
            item.velocity.Y -= 0.2f; // counteract gravity

            _magnetizedItems.Add(i);
        }
    }

    #region Effects

    /// <summary>
    ///     Lays out the ribbon's spine down a travelling sine, from the aim point back to the muzzle.
    /// </summary>
    /// <remarks>
    ///     The throw opens toward the aim point rather than being even along the beam, which is the
    ///     whole reason this reads as suction. A ribbon of constant amplitude is a wire; one that is
    ///     wide where it meets the rock and narrows into the gun is a funnel, and a funnel has a
    ///     direction even when it is standing still. The slow pulse on top of it is the machine
    ///     breathing -- deliberately on a different period to the sine, so the two never settle into an
    ///     obvious loop.
    /// </remarks>
    private void BuildSpine()
    {
        // Lazily, and here rather than in SetDefaults or OnSpawn: this is the one place guaranteed to
        // run every tick on every client that can actually see the beam, and never on a server.
        _trail ??= new VertexTrail(TRAIL_POINTS, TrailWidth, TrailColor);

        Vector2 delta = Focus - Muzzle;
        float length = delta.Length();

        if (length < 1f)
        {
            Array.Fill(_spine, Muzzle);
            return;
        }

        Vector2 direction = delta / length;
        Vector2 perpendicular = new(-direction.Y, direction.X);

        float phase = Main.GameUpdateCount * TRAIL_SINE_SPEED;
        float pulse = 1f + 0.25f * MathF.Sin(phase * 0.7f);

        // Capped against the beam's own length: a ribbon thrown further sideways than it travels
        // forward stops reading as a beam, which is what a point-blank shot would otherwise do.
        float swing = MathF.Min(TRAIL_AMPLITUDE, length * TRAIL_AMPLITUDE_CAP) * pulse * Intensity;

        for (int i = 0; i < TRAIL_POINTS; i++)
        {
            // 0 at the aim point, 1 at the muzzle. See _spine.
            float factor = i / (float)(TRAIL_POINTS - 1);

            Vector2 spine = Focus - direction * (length * factor);

            float wave = MathF.Sin(factor * TRAIL_WAVES * MathHelper.TwoPi + phase);

            // Widest at the far end, pinched to almost nothing at the muzzle.
            float amplitude = swing * MathHelper.Lerp(1f, 0.12f, factor);

            _spine[i] = spine + perpendicular * wave * amplitude;
        }
    }

    /// <summary>Ribbon half-width. Factor 0 is the aim point, 1 the muzzle -- see <see cref="_spine" />.</summary>
    private static float TrailWidth(float factor) => MathHelper.Lerp(TRAIL_WIDTH_FAR, TRAIL_WIDTH_MUZZLE, factor);

    /// <summary>
    ///     Ribbon colour: the rock's own colour where it meets the rock, brightening to a pale core as
    ///     it is drawn into the gun.
    /// </summary>
    /// <remarks>
    ///     Both ends resolve to transparent, which with forty points means exactly the one end vertex
    ///     each: enough to taper the ribbon instead of cutting it off square, and not enough to eat
    ///     into the beam.
    /// </remarks>
    private Color TrailColor(float factor)
    {
        if (factor <= 0.03f || factor >= 0.985f)
            return Color.Transparent;

        Color colour = Color.Lerp(FieldColor, TrailCoreColor, factor);

        // Deliberately not the sine's period, so the brightness and the coil drift in and out of step
        // instead of pulsing together and giving the whole thing an obvious loop.
        float shimmer = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.35f + factor * 4f);

        return colour * (shimmer * Intensity);
    }

    /// <summary>
    ///     Draws the ribbon.
    /// </summary>
    /// <remarks>
    ///     Called by <c>PrimitiveRenderer</c> after dust and outside any sprite batch, which is what
    ///     makes it safe to set device state here at all.
    ///     <para>
    ///         The blend state is set explicitly rather than inherited. Primitive draws run between
    ///         batches, so whatever was left on the device is whatever the last thing to touch it
    ///         wanted -- and a trail that renders additively on one frame and alpha-blended on the next
    ///         depending on what else happened to be on screen is a bug that only shows up in a
    ///         crowded fight.
    ///     </para>
    /// </remarks>
    public void DrawPrimitives()
    {
        if (Main.dedServ || _trail is null || Intensity <= 0f)
            return;

        if (Vector2.DistanceSquared(Muzzle, Focus) < 4f)
            return;

        if (Assets.Shaders.pixelTrail.Asset.Value is not { } effect)
            return;

        Texture2D grain = Assets.Textures.VFX.RibbonTrail.Asset.Value;

        Matrix world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
        Matrix view = Main.GameViewMatrix.TransformationMatrix;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -1f, 1f);

        float length = Vector2.Distance(Muzzle, Focus);

        // Cells along the beam. This is the grid the shader quantises into, and the unit the scroll
        // below is stepped in.
        float cells = MathF.Max(length / TRAIL_PIXEL, 1f);

        // Stepped a whole cell at a time, rather than passed as a continuous offset.
        //
        // This is the difference between pixel art and a mosaic filter, and it is worth being precise
        // about why. The shader quantises the coordinate and THEN adds this, so a smoothly advancing
        // value leaves the cell grid standing still while the colour inside each cell slides -- the
        // rings glide through a static lattice, which is exactly what a mosaic post-process looks like
        // and nothing like a hand-animated sprite. Snapping the offset to the same grid makes the rings
        // jump cell to cell instead, so the pixels are what moves.
        float scroll = -Main.GlobalTimeWrappedHourly * TRAIL_SINE_TRAVEL * TRAIL_SCROLL_RATIO;

        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
        effect.Parameters["sampleTexture"]?.SetValue(grain);
        effect.Parameters["time"]?.SetValue(MathF.Round(scroll * cells) / cells);
        effect.Parameters["repeats"]?.SetValue(TRAIL_REPEATS);

        // The ribbon's own dimensions, NOT the screen's -- see TRAIL_PIXEL for why that distinction is
        // the difference between chunky pixels and no visible quantisation at all. The vertical figure
        // is pre-multiplied by the cell size so the shader's own division cancels it back out to
        // TRAIL_BANDS bands, whatever the cell size is.
        effect.Parameters["resolution"]?.SetValue(new Vector2(length, TRAIL_BANDS * TRAIL_PIXEL));
        effect.Parameters["pixelation"]?.SetValue(TRAIL_PIXEL);

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        BlendState restore = device.BlendState;

        device.BlendState = BlendState.Additive;

        _trail.Positions = _spine;
        _trail.Render(effect);

        device.BlendState = restore;
    }

    /// <summary>
    ///     Eases the field colour toward whatever the cone is biting into, falling back to the tile
    ///     under the aim point when it is biting into nothing.
    /// </summary>
    /// <remarks>
    ///     Both paths read synced state rather than a cursor, so the colour is correct on every client
    ///     rather than only the owner's.
    /// </remarks>
    private void UpdateFieldColor()
    {
        Color target;

        if (_contact is { } contact)
        {
            target = TileColorUtils.Enhance(TileColorUtils.TileColor(
                contact.Type,
                (int)(contact.Position.X / 16f),
                (int)(contact.Position.Y / 16f)));
        }
        else
        {
            // Pale sky blue: the colour of the field with nothing in it.
            target = TileColorUtils.Sample((int)(Focus.X / 16f), (int)(Focus.Y / 16f), new Color(135, 206, 235));
        }

        FieldColor = Color.Lerp(FieldColor, target, 0.1f);

        MagnetizerLayer.SetOre(FieldColor);
    }

    /// <summary>Feeds the ore stream and strikes chips off whatever the beam has hold of.</summary>
    private void EmitOre()
    {
        if (_contact is not { } contact)
            return;

        Vector2 away = (contact.Position - Muzzle).SafeNormalize(Vector2.UnitY);

        for (int i = 0; i < ORE_PER_TICK; i++)
        {
            // Thrown outward first: material that snaps onto the beam where it appears reads as
            // having been spawned there rather than torn out of anything.
            Vector2 velocity = away.RotatedByRandom(0.8f) * Main.rand.NextFloat(0.5f, 2.5f);

            MagnetizerLayer.Stream(
                Projectile,
                contact.Position + Main.rand.NextVector2Circular(8f, 8f),
                velocity,
                radius: Main.rand.NextFloat(6f, 12f),
                life: Main.rand.NextFloat(40f, 65f),
                gripTicks: 60
            );
        }

        if (_miningTimer % CHIP_INTERVAL == 0)
            MagnetizedChip.Strike(Projectile, contact.Position, away, contact.Type);
    }

    /// <summary>
    ///     Lights the beam's own length.
    /// </summary>
    /// <remarks>
    ///     The field filter can only ever brighten pixels that were already drawn, so without this the
    ///     beam would light nothing: pointing it down an unlit shaft would glow against blackness and
    ///     show no more of the shaft than before. A tool for working in the dark has to carry a lamp.
    /// </remarks>
    private void EmitLight()
    {
        Vector3 colour = FieldColor.ToVector3() * LIGHT_STRENGTH * Intensity;

        for (int i = 0; i < LIGHT_SAMPLES; i++)
        {
            Vector2 point = Vector2.Lerp(Muzzle, Focus, i / (float)(LIGHT_SAMPLES - 1));

            // Brightest at the muzzle and tapering out, matching where the field's own energy is.
            float falloff = MathHelper.Lerp(1f, 0.45f, i / (float)(LIGHT_SAMPLES - 1));

            Lighting.AddLight(point, colour * falloff);
        }
    }

    #endregion

    private void HandleSounds()
    {
        if (Projectile.owner != Main.myPlayer) return;

        _soundTimer++;

        if (!_hasPlayedStartup)
        {
            _startupSoundId = SoundEngine.PlaySound(Assets.SFX.OreVacuum_Start.Asset with
            {
                Volume = 0.4f,
                MaxInstances = 1,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, Projectile.Center);

            _hasPlayedStartup = true;
        }

        const int startupDurationTicks = 77;

        if (_hasPlayedStartup && !_hasStartedLoop && _soundTimer >= startupDurationTicks)
        {
            _loopSoundId = SoundEngine.PlaySound(Assets.SFX.OreVacuum_Loop.Asset with
            {
                Volume = 0.4f,
                IsLooped = true,
                MaxInstances = 1,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, Projectile.Center);

            _hasStartedLoop = true;
        }

        if (_hasStartedLoop && SoundEngine.TryGetActiveSound(_loopSoundId, out var activeSound) && activeSound != null)
            activeSound.Position = Projectile.Center;
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            if (SoundEngine.TryGetActiveSound(_loopSoundId, out var loopSound))
                loopSound?.Stop();

            // The spin-up is 77 ticks long, so releasing the button early leaves it still playing --
            // and a magnetizer that has already stopped should not still be winding up.
            if (SoundEngine.TryGetActiveSound(_startupSoundId, out var startupSound))
                startupSound?.Stop();

            SoundEngine.PlaySound(Assets.SFX.OreVacuum_End.Asset with
            {
                Volume = 0.4f,
                MaxInstances = 1
            }, Projectile.Center);
        }

        foreach (int itemIndex in _magnetizedItems)
        {
            if (itemIndex >= Main.item.Length || !Main.item[itemIndex].active)
                continue;

            Item item = Main.item[itemIndex];

            item.beingGrabbed = false;
            item.velocity *= 0.5f;
            item.velocity.Y -= 1f;
            item.noGrabDelay = 0;
        }

        _magnetizedItems.Clear();
    }

    private void SetOwnerAnimation(Player owner)
    {
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = 2;
        owner.itemAnimation = 2;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            (-_smoothAim).ToRotation() + MathHelper.PiOver2);
    }

    /// <summary>
    ///     Draws the gun
    /// </summary>
    /// <remarks>
    ///     The ribbon goes out through <see cref="DrawPrimitives" /> and the shimmer through
    ///     <see cref="MagnetizerFieldRenderer" />.
    /// </remarks>
    public override bool PreDraw(ref Color lightColor)
    {
        var texture = ModContent.Request<Texture2D>(Texture).Value;

        // Taken off the aim rather than Projectile.direction. A held projectile never moves, so its
        // velocity stays at zero, and vanilla resolves direction from the sign of velocity.X --
        // which settles on +1 every tick no matter where the gun is pointed. The mirror therefore
        // never applied, leaving the sprite to swing through a full 360 and hang upside down on the
        // left half. Reading the aim directly cannot be undone by anything upstream.
        var spriteEffects = _smoothAim.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;

        var origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f) + new Vector2(16, 0);
        var drawPos = Projectile.Center - Main.screenPosition;

        Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

        return false;
    }
}
