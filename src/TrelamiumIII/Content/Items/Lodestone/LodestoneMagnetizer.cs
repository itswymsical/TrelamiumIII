using ReLogic.Utilities;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Lodestone;

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
    public override string Texture => AssetReferences.Content.Items.Lodestone.LodestoneMagnetizer.KEY;

    #region tuning

    /// <summary>How far the gun floats from the player, along the aim vector.</summary>
    private const float MUZZLE_OFFSET = 32f;

    /// <summary>Full angle of the mining cone. Public because the drawn cone is sized from it.</summary>
    public const float ARC_ANGLE = MathHelper.Pi / 6;

    private const int MINING_SPEED = 5;

    private const int CONE_RAYS = 9;

    /// <summary>Intake radius and speed at <see cref="LodestoneMagnetizerItem.PICK_POWER" />; both scale with the held pick.</summary>
    private const float VACUUM_RANGE = 300f;
    private const float VACUUM_SPEED = 8f;

    /// <summary>Pick power the intake constants are quoted at.</summary>
    private const float VACUUM_REFERENCE_PICK = 25f;

    /// <summary>Aim movement, in pixels, before the owner sends a packet; <see cref="_smoothAim" /> hides smaller gaps.</summary>
    private const float SYNC_THRESHOLD = 6f;

    /// <summary>Ticks between forced resyncs, so a slow drift cannot accumulate unbounded.</summary>
    private const int SYNC_INTERVAL = 12;

    /// <summary>Ticks the field takes to reach full strength. Short: this is a tool, not a charge-up.</summary>
    private const int SPOOL_TICKS = 14;

    /// <summary>Ore blobs added to the stream per tick while the beam is biting.</summary>
    private const int ORE_PER_TICK = 2;

    /// <summary>Ticks between chips struck off the rock; slower than the ore stream so chips stay distinct.</summary>
    private const int CHIP_INTERVAL = 3;

    /// <summary>Light the beam throws, at full strength, as a fraction of the field colour.</summary>
    private const float LIGHT_STRENGTH = 0.35f;

    /// <summary>Points along the beam that emit light. Enough to light a corridor, few enough to be free.</summary>
    private const int LIGHT_SAMPLES = 5;

    // ---- trail -------------------------------------------------------------------------------

    /// <summary>
    ///     Points in the ribbon's spine. Generated between muzzle and aim point every frame, so it only has to resolve the sine.
    /// </summary>
    private const int TRAIL_POINTS = 40;

    /// <summary>Waves along the beam. Low: two big lazy coils read as a heavy pull, six read as a wire.</summary>
    private const float TRAIL_WAVES = 1.15f;

    /// <summary>Radians per tick the sine travels.</summary>
    private const float TRAIL_SINE_SPEED = 0.3f;

    /// <summary>Peak sideways sine throw in world pixels, capped at a fraction of beam length for point-blank shots.</summary>
    private const float TRAIL_AMPLITUDE = 30f;
    private const float TRAIL_AMPLITUDE_CAP = 0.2f;

    /// <summary>Ribbon half-width at the muzzle and at the aim point. Opens outward: this is an intake.</summary>
    private const float TRAIL_WIDTH_MUZZLE = 13f;
    private const float TRAIL_WIDTH_FAR = 48f;

    /// <summary>
    ///     Chunky pixel size in world pixels. <c>pixelTrail.fx</c> quantises by <c>resolution / pixelation</c> in trail UV, so
    ///     the ribbon's own dimensions must be passed as resolution; screen size gives no visible quantisation.
    /// </summary>
    private const float TRAIL_PIXEL = 12f;

    /// <summary>
    ///     Bands across the ribbon's width, decoupled from <see cref="TRAIL_PIXEL" /> by passing <c>TRAIL_BANDS * TRAIL_PIXEL</c>
    ///     as the vertical resolution, since the shader has one pixelation value for both axes.
    /// </summary>
    private const float TRAIL_BANDS = 11f;

    private const float TRAIL_REPEATS = 3f;

    /// <summary>Beam-lengths per second the sine's features travel (<c>SPEED / (WAVES * TAU)</c> per tick). Derived, not tuned.</summary>
    private const float TRAIL_SINE_TRAVEL = TRAIL_SINE_SPEED * 60f / (TRAIL_WAVES * MathHelper.TwoPi);

    /// <summary>
    ///     Grain scroll speed as a multiple of <see cref="TRAIL_SINE_TRAVEL" />; above 1 the material outruns the coil, reading as
    ///     suction. Applied negatively at the call site, since a positive scroll slides grain outward and reads as firing.
    /// </summary>
    private const float TRAIL_SCROLL_RATIO = 1.4f;

    /// <summary>What the ribbon brightens to as it enters the gun. Cool and near-white; the ore colour supplies the other end.</summary>
    private static readonly Color TrailCoreColor = new(214, 240, 255);

    #endregion

    /// <summary>
    ///     Aim point as an offset from the owner, clamped to reach. Stored in <see cref="Projectile.ai" /> so it syncs for free.
    /// </summary>
    public Vector2 AimOffset
    {
        get => new(Projectile.ai[0], Projectile.ai[1]);
        set { Projectile.ai[0] = value.X; Projectile.ai[1] = value.Y; }
    }

    public Vector2 Muzzle => Projectile.Center;

    /// <summary>Where it ends, in world space. Cached each tick so the draw side never recomputes it.</summary>
    public Vector2 Focus { get; private set; }

    /// <summary>The colour the field and the ore stream are drawn in, eased toward whatever is targeted.</summary>
    public Color FieldColor { get; private set; } = new(150, 150, 150);

    /// <summary>0..1 field strength: the spool-up envelope, dipping when the beam isn't biting into anything.</summary>
    public float Intensity
    {
        get
        {
            float spool = MathHelper.Clamp(_age / (float)SPOOL_TICKS, 0f, 1f);

            return spool * MathHelper.Lerp(0.72f, 1f, _bite);
        }
    }

    /// <summary>Eased toward the synced aim, so remote clients don't see the gun step between packets.</summary>
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
    ///     Ribbon spine: [0] is the aim point and [^1] the muzzle (the trail's head), so trail factor runs 0 far to 1 at the gun.
    /// </summary>
    private readonly Vector2[] _spine = new Vector2[TRAIL_POINTS];

    /// <summary>Built once; its width and colour functions read this projectile's current state, so there's no per-frame rebuild.</summary>
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

    /// <summary>Collapses the beam onto the muzzle so <see cref="Focus" /> is valid before the first <see cref="AI" />.</summary>
    public override void OnSpawn(IEntitySource source) => Focus = Projectile.Center;

    /// <summary>Total reach in pixels, summed like vanilla's <c>Player.PlaceThing</c>, so it matches hand mining.</summary>
    private static float MaxReach(Player owner)
    {
        // tileRangeX is a static base reach; the per-player parts are the item's tileBoost and blockRange.
        int tiles = Player.tileRangeX + owner.HeldItem.tileBoost + owner.blockRange;

        return tiles * 16f;
    }

    /// <summary>From the constant, since <c>HeldItem.pick</c> is zero by design (see <see cref="LodestoneMagnetizerItem.PICK_POWER" />).</summary>
    private static int PickPower() => LodestoneMagnetizer.PICK_POWER;

    public override void AI()
    {
        var owner = Main.player[Projectile.owner];

        if (!owner.active || owner.dead || !owner.channel || owner.noItems || owner.CCed)
        {
            Projectile.Kill();
            return;
        }

        // Refreshed every tick; the 3600 default would expire the beam after a minute of channelling.
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

    /// <summary>The owner publishes a clamped cursor aim; every client eases toward it, so the gun moves identically everywhere.</summary>
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

            // Rate-limited: syncing every tick would send a packet per tick per magnetizer for a slowly moving value.
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
    ///     One cone walk that records the nearest breakable tile for visuals and, on the owner's mining tick, breaks tiles.
    /// </summary>
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

        bool miningTick = mine && _miningTimer % MINING_SPEED == 0;

        // Tracked by distance: rays are walked one at a time, so the first hit is on the cone's edge, not nearest the aim.
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
    ///     Anything flagged in <see cref="Main.tileSpelunker" />, plus Hellstone. Reads the flag rather than
    ///     <c>Main.IsTileSpelunkable</c>, which a brewed Spelunker's tier gate filters.
    /// </summary>
    private static bool IsMagnetizable(Tile tile)
    {
        if (tile.TileType == TileID.Hellstone)
            return true;

        return Main.tileSpelunker[tile.TileType]
               && tile.TileType is not (TileID.Plants or TileID.MatureHerbs or TileID.BloomingHerbs);
    }

    /// <summary>Uses real pick power so <see cref="Player.PickTile" />'s tile requirements apply (e.g. meteorite).</summary>
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
    ///     Lays the spine along a travelling sine from aim point to muzzle, widest at the rock so it reads as a funnel.
    /// </summary>
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

    /// <summary>The rock's colour at the far end, brightening to a pale core at the gun; both end vertices are transparent.</summary>
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
    ///     Draws the ribbon between sprite batches, setting the blend state explicitly since the device keeps whatever was last used.
    /// </summary>
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

        // Stepped a whole cell at a time: the shader quantises before adding the offset, so a continuous value slides colour
        // through a static grid (a mosaic filter look). Snapping makes the pixels themselves move.
        float scroll = -Main.GlobalTimeWrappedHourly * TRAIL_SINE_TRAVEL * TRAIL_SCROLL_RATIO;

        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
        effect.Parameters["sampleTexture"]?.SetValue(grain);
        effect.Parameters["time"]?.SetValue(MathF.Round(scroll * cells) / cells);
        effect.Parameters["repeats"]?.SetValue(TRAIL_REPEATS);

        // The ribbon's own dimensions, not the screen's (see TRAIL_PIXEL); height is pre-multiplied so TRAIL_BANDS survive.
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
    ///     Eases toward the targeted tile's colour, or the tile under the aim point; uses synced state so every client agrees.
    /// </summary>
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

        MagnetizerField.SetOre(FieldColor);
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

            MagnetizerField.Stream(
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

    /// <summary>Lights along the beam, since the field filter can only brighten pixels that are already lit.</summary>
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
            _startupSoundId = SoundEngine.PlaySound(Assets.SFX.Items.OreVacuum_Start.Asset with
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
            _loopSoundId = SoundEngine.PlaySound(Assets.SFX.Items.OreVacuum_Loop.Asset with
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

            SoundEngine.PlaySound(Assets.SFX.Items.OreVacuum_End.Asset with
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
    ///     Draws the gun; the ribbon goes through <see cref="DrawPrimitives" /> and the shimmer through <see cref="MagnetizerFieldRenderer" />.
    /// </summary>
    public override bool PreDraw(ref Color lightColor)
    {
        var texture = ModContent.Request<Texture2D>(Texture).Value;

        // Mirror from the aim, not Projectile.direction: a held projectile has zero velocity, so vanilla's direction is always +1.
        var spriteEffects = _smoothAim.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;

        var origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f) + new Vector2(16, 0);
        var drawPos = Projectile.Center - Main.screenPosition;

        Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

        return false;
    }
}
