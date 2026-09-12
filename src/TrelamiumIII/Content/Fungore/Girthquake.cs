using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using TrelamiumIII.Content.Rarities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Shockwaves;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.Fungore;

public class Girthquake : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 56;
        Item.damage = 27;
        Item.crit = 21;
        Item.knockBack = 16f;
        Item.DamageType = DamageClass.Melee;
        Item.rare = ModContent.RarityType<GirthquakeRarity>();
        Item.value = Item.sellPrice(silver: 5);

        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useTime = Item.useAnimation = 32;
        Item.channel = true;
        Item.autoReuse = false;
        Item.useTurn = false;

        Item.noMelee = Item.noUseGraphic = true;

        Item.UseSound = null;

        Item.shoot = ModContent.ProjectileType<GirthquakeProj>();
        Item.shootSpeed = 1f;
    }

    public override bool MeleePrefix() => true;

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] < 1;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.MountedCenter, new Vector2(velocity.X, 0f), type, damage, knockback, player.whoAmI);

        return false;
    }
}

public class GirthquakeProj : ModProjectile
{
    public override string Texture => AssetReferences.Content.Fungore.Girthquake.KEY;

    public enum SwingState
    {
        Planted,
        Raise,
        Charge,
        Slam,
        Impact,
        Recover,
    }

    #region tuning

    private const float ANGLE_PLANTED = 1.88f;

    private const float ANGLE_CHARGED = -0.35f;

    private const float ANGLE_BURIED = 2.15f;

    private const int PLANT_TICKS = 12;

    private const int RAISE_TICKS = 30;

    private const int CHARGE_TICKS = 40;

    private const int SLAM_TICKS = 10;

    private const int IMPACT_TICKS = 8;
    private const int RECOVER_TICKS = 18;

    private const float CHARGE_DRIFT = 0.02f;

    private const int BODY_FRAME_UP = 2;
    private const int BODY_FRAME_DOWN = 4;

    private const int IMPACT_DAMAGE_TICKS = 4;

    private const float HAMMER_LENGTH = 62f;

    private const float HEAD_REACH = 24f;

    private const float HILT_FRACTION = 0.5f;

    private const float HEAD_THICKNESS = 26f;

    private const int MIN_SHARDS = 5;
    private const int MAX_SHARDS = 12;

    private const float SHARD_DAMAGE_FRACTION = 0.15f;

    private const float SHARD_SPREAD_MIN = 0.96f;
    private const float SHARD_SPREAD_MAX = 2.36f;

    private const float SHARD_FORWARD_LEAN = 0.49f;

    private const float FACING_DEADZONE = 14f;

    private const float SLOW_CHARGE = 0.90f;
    private const float SLOW_SWING = 0.86f;
    private const float SLOW_IDLE = 0.93f;


    private const float TWIRL_TIP = 0.73f;

    private const float TWIRL_RADIUS = 216f;
    private const float TWIRL_CORE_RADIUS = 196f;

    private const float TWIRL_CORE_INSET = 0.82f;

    private const float TWIRL_REVEAL = 0.4f;

    private static readonly Color TwirlColor = new(255, 214, 158);
    private static readonly Color TwirlCoreColor = new(255, 248, 232);

    private static readonly bool DEBUG_HITBOX = false;

    #endregion

    #region audio

    private static readonly SoundStyle PlantSound = SoundID.Dig with { Volume = 0.7f, Pitch = -0.6f };
    private static readonly SoundStyle RaiseSound = SoundID.Item1 with { Volume = 0.5f, Pitch = -0.85f };
    private static readonly SoundStyle SlamSound = SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = -0.5f };
    private static readonly SoundStyle GroundSound = Assets.SFX.Girthquake_Impact.Asset with { Volume = 1f, Pitch = -0.35f };
    private static readonly SoundStyle AirSound = SoundID.Item1 with { Volume = 0.6f, Pitch = -0.7f };

    #endregion

    private static readonly EaseFunction SlamEase = BuildSlamEase();

    /// <summary>Overshoots past the charged pose and settles back, so the lift ends on a bounce.</summary>
    private static readonly EaseFunction RaiseEase = BuildRaiseEase();

    public Player Owner => Main.player[Projectile.owner];

    public SwingState State
    {
        get => (SwingState)(int)Projectile.ai[0];
        private set => Projectile.ai[0] = (int)value;
    }

    public ref float StateTimer => ref Projectile.ai[1];


    public ref float ReleasedCharge => ref Projectile.ai[2];

    public float Charge => State == SwingState.Charge
        ? MathHelper.Clamp(StateTimer / ScaledTicks(CHARGE_TICKS), 0f, 1f)
        : ReleasedCharge;

    public bool FullyCharged => Charge >= 1f;

    /// <summary>Melee size modifiers apply to reach and to the sprite alike.</summary>
    public float TotalScale => Projectile.scale * meleeScale;

    /// <summary>Only the descent and the first few impact frames connect.</summary>
    public bool IsDamaging => State == SwingState.Slam
        || (State == SwingState.Impact && StateTimer < IMPACT_DAMAGE_TICKS);

    private int facing = 1;

    private float CurrentAngle;

    private float previousAngle;

    /// <summary>Tile the head was planted in, and the material every shard is cut from.</summary>
    private ushort materialTile = TileID.Stone;

    private bool hitGround;

    private Vector2 impactPoint;

    private bool burstFired;
    private float meleeScale = 1f;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 74;

        Projectile.aiStyle = -1;
        Projectile.DamageType = DamageClass.Melee;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;

        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;

        Projectile.timeLeft = 600;
    }

    public override void OnSpawn(IEntitySource source)
    {
        facing = Math.Abs(Projectile.velocity.X) < 0.05f
            ? Owner.direction
            : Math.Sign(Projectile.velocity.X);

        Projectile.direction = Projectile.spriteDirection = facing;
        Projectile.velocity = Vector2.Zero;

        CurrentAngle = previousAngle = ANGLE_PLANTED;

        SamplePlantedMaterial();

        SoundEngine.PlaySound(PlantSound, Owner.Center);
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Owner;

        if (!owner.active || owner.dead || owner.noItems || owner.CCed)
        {
            Projectile.Kill();
            return;
        }

        meleeScale = owner.GetAdjustedItemScale(owner.HeldItem);

        UpdateFacing(owner);

        previousAngle = CurrentAngle;
        CurrentAngle = AngleForState();

        Projectile.Center = HandFor(CurrentAngle);
        Projectile.rotation = DirectionFor(CurrentAngle).ToRotation();

        PoseOwner(owner);
        DampOwner(owner);

        RunState();

        StateTimer++;
    }

    #region state machine

    private void RunState()
    {
        switch (State)
        {
            case SwingState.Planted:
                if (StateTimer >= PLANT_TICKS)
                {
                    SetState(SwingState.Raise);
                    SoundEngine.PlaySound(RaiseSound, Owner.Center);
                }

                break;

            case SwingState.Raise:
                // Non-cancellable, and reaching the shoulder is what fires it.
                if (StateTimer >= ScaledTicks(RAISE_TICKS))
                {
                    if (Owner.channel)
                        SetState(SwingState.Charge);
                    else
                        Release(0f);
                }

                break;

            case SwingState.Charge:
                // Fires itself the moment the hold stops paying, so there is never a stretch of
                // holding for nothing and never a loaded hammer carried around the world.
                if (!Owner.channel || FullyCharged)
                    Release(Charge);

                break;

            case SwingState.Slam:
                // Scanned before the timer check so a slam that lands early stops on the tile
                // rather than swinging through it to its terminal angle.
                if (ScanForGround())
                    break;

                if (StateTimer >= SlamTicks)
                    Land(hit: false, HeadFor(CurrentAngle));

                break;

            case SwingState.Impact:
                if (StateTimer >= IMPACT_TICKS)
                    SetState(SwingState.Recover);

                break;

            case SwingState.Recover:
                if (StateTimer >= ScaledTicks(RECOVER_TICKS))
                    Projectile.Kill();

                break;
        }
    }

    private void SetState(SwingState state)
    {
        State = state;
        StateTimer = 0f;
        Projectile.netUpdate = true;
    }

    private void Release(float charge)
    {
        ReleasedCharge = MathHelper.Clamp(charge, 0f, 1f);
        SetState(SwingState.Slam);

        SoundEngine.PlaySound(SlamSound, Owner.Center);
    }

    private void Land(bool hit, Vector2 point)
    {
        hitGround = hit;
        impactPoint = point;

        SetState(SwingState.Impact);

        if (hit)
            GroundBurst();
        else
            SoundEngine.PlaySound(AirSound, Projectile.Center);
    }

    private int ScaledTicks(int ticks)
    {
        float speed = MathHelper.Clamp(Owner.GetTotalAttackSpeed(DamageClass.Melee), 0.5f, 2f);
        float damped = MathHelper.Lerp(1f, 1f / speed, 0.6f);

        return Math.Max(1, (int)(ticks * damped));
    }

    private int SlamTicks => ScaledTicks(SLAM_TICKS);

    #endregion

    #region pose

    /// <summary>
    ///     Hammer direction before the gravity mirror; arms use this because vanilla applies <see cref="Player.gravDir" /> itself.
    /// </summary>
    private Vector2 RawDirectionFor(float angle) => (-Vector2.UnitY).RotatedBy(angle * facing);

    private Vector2 DirectionFor(float angle)
    {
        Vector2 direction = RawDirectionFor(angle);

        if (Owner.gravDir < 0f)
            direction.Y = -direction.Y;

        return direction;
    }

    /// <summary>Composite arm rotation is measured from straight down, hence the quarter-turn offset.</summary>
    private float ArmRotationFor(float angle) => RawDirectionFor(angle).ToRotation() - MathHelper.PiOver2;

    /// <summary>Where the grip sits. Follows the hand, so the hammer never detaches from the pose.</summary>
    private Vector2 HandFor(float angle)
        => Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, ArmRotationFor(angle))
           + new Vector2(0f, Owner.gfxOffY);

    /// <summary>Centre of the head, which is the part that hits things.</summary>
    private Vector2 HeadFor(float angle)
        => HandFor(angle) + DirectionFor(angle) * (HAMMER_LENGTH + HEAD_REACH * 0.5f) * TotalScale;

    private float AngleForState()
    {
        float progress;

        switch (State)
        {
            case SwingState.Planted:
                return ANGLE_PLANTED;

            case SwingState.Raise:
                progress = MathHelper.Clamp(StateTimer / ScaledTicks(RAISE_TICKS), 0f, 1f);
                return MathHelper.Lerp(ANGLE_PLANTED, ANGLE_CHARGED, RaiseEase.Ease(progress));

            case SwingState.Charge:
                return ANGLE_CHARGED + (float)Math.Sin(StateTimer * 0.11f) * CHARGE_DRIFT;

            case SwingState.Slam:
                progress = MathHelper.Clamp(StateTimer / SlamTicks, 0f, 1f);
                return MathHelper.Lerp(ANGLE_CHARGED, ANGLE_BURIED, SlamEase.Ease(progress));

            case SwingState.Impact:
                return CurrentAngle;

            case SwingState.Recover:
                progress = MathHelper.Clamp(StateTimer / ScaledTicks(RECOVER_TICKS), 0f, 1f);
                return MathHelper.Lerp(ANGLE_BURIED, ANGLE_PLANTED, EaseQuadOut.Ease(progress));

            default:
                return ANGLE_PLANTED;
        }
    }

    private void UpdateFacing(Player owner)
    {
        bool committed = State is SwingState.Slam or SwingState.Impact or SwingState.Recover;

        if (!committed && Main.myPlayer == Projectile.owner)
        {
            float offset = Main.MouseWorld.X - owner.Center.X;

            // A cursor parked on top of the player is not asking for anything, and without a band to
            // cross it would strobe the facing on sub-pixel mouse movement for the whole wind-up.
            if (Math.Abs(offset) > FACING_DEADZONE && Math.Sign(offset) != facing)
            {
                facing = Math.Sign(offset);
                Projectile.netUpdate = true;
            }
        }

        // Pushed out every tick rather than only on a change, because vanilla has already
        // overwritten Projectile.direction by the time this runs. Nothing here reads it back.
        Projectile.direction = Projectile.spriteDirection = facing;
        owner.ChangeDir(facing);
    }

    private void PoseOwner(Player owner)
    {
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = owner.itemAnimation = 2;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotationFor(CurrentAngle));
        owner.SetCompositeArmBack(false, Player.CompositeArmStretchAmount.Full, 0f);

        owner.bodyFrame.Y = owner.bodyFrame.Height * BodyFrameForState();
    }

    private int BodyFrameForState() => State switch
    {
        // Down, out, up: the back arm heaving the weight over the shoulder.
        SwingState.Raise => BODY_FRAME_DOWN - ThirdOf(StateTimer / ScaledTicks(RAISE_TICKS)),

        SwingState.Charge => BODY_FRAME_UP,

        // And the same three frames the other way as it comes down.
        SwingState.Slam => BODY_FRAME_UP + ThirdOf(StateTimer / SlamTicks),

        _ => BODY_FRAME_DOWN,
    };

    /// <summary>Which third of an animation <paramref name="progress" /> falls in: 0, 1 or 2.</summary>
    private static int ThirdOf(float progress) => Math.Clamp((int)(progress * 3f), 0, 2);

    /// <summary>
    ///     Damps horizontal movement while the weight is up; applied to velocity because projectiles update after players.
    /// </summary>
    private void DampOwner(Player owner)
    {
        owner.velocity.X *= State switch
        {
            SwingState.Charge => SLOW_CHARGE,
            SwingState.Slam or SwingState.Impact => SLOW_SWING,
            _ => SLOW_IDLE,
        };
    }

    #endregion

    #region ground

    private void SamplePlantedMaterial()
    {
        Point coords = HeadFor(ANGLE_PLANTED).ToTileCoordinates();

        if (IsSlammable(coords))
            materialTile = Framing.GetTileSafely(coords).TileType;
    }

    private bool ScanForGround()
    {
        Vector2 from = HeadFor(previousAngle);
        Vector2 to = HeadFor(CurrentAngle);

        Vector2 delta = to - from;
        int steps = Math.Max(1, (int)(delta.Length() / 4f));

        for (int i = 1; i <= steps; i++)
        {
            float slice = i / (float)steps;
            Vector2 point = from + delta * slice;
            Point coords = point.ToTileCoordinates();

            if (!IsSlammable(coords))
                continue;

            materialTile = Framing.GetTileSafely(coords).TileType;

            // Snap back to the contact point within the tick, so the hitstop pose rests on the surface.
            CurrentAngle = MathHelper.Lerp(previousAngle, CurrentAngle, slice);

            Land(hit: true, point);

            return true;
        }

        return false;
    }

    private static bool IsSlammable(Point coords)
    {
        Tile tile = Framing.GetTileSafely(coords);

        if (!tile.HasUnactuatedTile)
            return false;

        return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
    }

    private void GroundBurst()
    {
        if (burstFired)
            return;

        burstFired = true;

        float charge = ReleasedCharge;

        SoundEngine.PlaySound(GroundSound with { Pitch = GroundSound.Pitch - charge * 0.3f }, impactPoint);

        TileShockwaveSystem.Spawn(new BlockWave(impactPoint, 90f * Charge, lifetime: 30)
        {
            Amplitude = 16f * Charge,
            Thickness = (16f * 4.5f) * Charge,
            Reach = (16f * 3) * Charge,
            Blend = 0.64f,
            Snap = 0.75f,
        });

        Shake(charge);
        Rubble(charge);

        if (Main.myPlayer != Projectile.owner)
            return;

        int count = (int)MathHelper.Lerp(MIN_SHARDS, MAX_SHARDS, charge);
        float spread = MathHelper.Lerp(SHARD_SPREAD_MIN, SHARD_SPREAD_MAX, charge);

        float centre = -MathHelper.PiOver2 + SHARD_FORWARD_LEAN * facing;

        int damage = Math.Max(1, (int)(Projectile.damage * SHARD_DAMAGE_FRACTION));
        int type = ModContent.ProjectileType<EarthShardProj>();

        for (int i = 0; i < count; i++)
        {
            float slot = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = centre + MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, slot) + Main.rand.NextFloat(-0.06f, 0.06f);
            float speed = MathHelper.Lerp(5.5f, 10.5f, charge) * Main.rand.NextFloat(0.75f, 1.15f);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                impactPoint,
                angle.ToRotationVector2() * speed,
                type,
                damage,
                Projectile.knockBack * 0.4f,
                Projectile.owner,
                materialTile,
                Main.rand.Next(EarthShardProj.CHUNK_SEEDS)
            );
        }
    }

    private void Shake(float charge)
    {
        if (Main.dedServ)
            return;

        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
            impactPoint,
            Vector2.UnitY,
            MathHelper.Lerp(2.5f, 9f, charge),
            6f,
            (int)MathHelper.Lerp(8f, 18f, charge),
            1200f,
            nameof(GirthquakeProj)
        ));
    }

    /// <summary>Material-correct debris via <see cref="WorldGen.KillTile" /> with <c>fail: true</c>; the world is left intact.</summary>
    private void Rubble(float charge)
    {
        if (Main.dedServ)
            return;

        Point centre = impactPoint.ToTileCoordinates();
        int reach = (int)MathHelper.Lerp(1f, 3f, charge);

        for (int x = -reach; x <= reach; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Point coords = new(centre.X + x, centre.Y + y);

                if (WorldGen.InWorld(coords.X, coords.Y, 2) && IsSlammable(coords))
                    WorldGen.KillTile(coords.X, coords.Y, fail: true);
            }
        }

        // TODO: swap for a tile-textured Particle once the debris art exists. Stand-in dust so the
        // impact reads while the state machine is being tuned.
        int puffs = (int)MathHelper.Lerp(8f, 26f, charge);

        for (int i = 0; i < puffs; i++)
        {
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-5f, -1f)) * (0.6f + charge);
            Dust.NewDustPerfect(impactPoint + Main.rand.NextVector2Circular(20f, 6f), DustID.Smoke, velocity, 120, Color.White, 1.2f + charge * 0.6f);
        }
    }

    #endregion

    #region damage

    public override bool? CanDamage() => IsDamaging ? null : false;

    /// <summary>
    ///     A thick segment along the head end of the shaft, swept from last tick's angle so a fast slam can't skip an enemy.
    ///     Starts at <see cref="HILT_FRACTION" />; the grip isn't a weapon.
    /// </summary>
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!IsDamaging)
            return false;

        const int SAMPLES = 4;

        for (int i = 0; i < SAMPLES; i++)
        {
            float angle = MathHelper.Lerp(previousAngle, CurrentAngle, i / (float)(SAMPLES - 1));

            Vector2 hand = HandFor(angle);
            Vector2 direction = DirectionFor(angle);

            Vector2 start = hand + direction * HAMMER_LENGTH * HILT_FRACTION * TotalScale;
            Vector2 end = hand + direction * (HAMMER_LENGTH + HEAD_REACH) * TotalScale;

            float _ = 0f;

            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, HEAD_THICKNESS * TotalScale, ref _))
                return true;
        }

        return false;
    }

    /// <summary>Charge doesn't scale damage or knockback; holding only buys more shrapnel.</summary>
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        // Away from the player rather than along the sweep -- the hammer is coming down, so a
        // velocity-derived hit direction would fling things at the floor.
        modifiers.HitDirectionOverride = facing;
    }

    #endregion

    #region net

    /// <summary>Sends facing, which isn't in the vanilla projectile packet and comes from movement input.</summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((sbyte)facing);
        writer.Write(materialTile);
        writer.Write(hitGround);
        writer.WriteVector2(impactPoint);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        facing = reader.ReadSByte() < 0 ? -1 : 1;
        materialTile = reader.ReadUInt16();
        hitGround = reader.ReadBoolean();
        impactPoint = reader.ReadVector2();
    }

    #endregion

    #region draw

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;

        bool flipX = facing < 0;
        bool flipY = Owner.gravDir < 0f;

        SpriteEffects effects = Mirroring(flipX, flipY);

        // Behind the hammer, so the head reads as leading the streak rather than sitting in it.
        float twirl = TwirlOpacity;

        if (twirl > 0f)
            DrawTwirl(twirl, effects, flipX, flipY);

        // Pivot on the grip end, inset slightly so it sits in the hand, then mirrored into the grip's corner.
        const float INSET = 5f;
        Vector2 origin = new(INSET, texture.Height - INSET);

        if (flipX)
            origin.X = texture.Width - origin.X;

        if (flipY)
            origin.Y = texture.Height - origin.Y;

        float correction = MathHelper.PiOver4 * (flipX ? 3f : 1f) * (flipY ? -1f : 1f);

        Main.EntitySpriteDraw(
            texture,
            HandFor(CurrentAngle) - Main.screenPosition,
            null,
            Projectile.GetAlpha(lightColor),
            Projectile.rotation + correction,
            origin,
            TotalScale,
            effects,
            0
        );

        if (DEBUG_HITBOX)
            DrawDebugSegment();

        return false;
    }

    private static SpriteEffects Mirroring(bool flipX, bool flipY)
    {
        SpriteEffects effects = SpriteEffects.None;

        if (flipX)
            effects |= SpriteEffects.FlipHorizontally;

        if (flipY)
            effects |= SpriteEffects.FlipVertically;

        return effects;
    }

    /// <summary>Swing streak strength, 0 to 1, driven by how far the head has swept rather than by time.</summary>
    private float TwirlOpacity
    {
        get
        {
            if (State == SwingState.Slam)
            {
                float swept = (CurrentAngle - ANGLE_CHARGED) / (ANGLE_BURIED - ANGLE_CHARGED);
                return EaseQuadOut.Ease(MathHelper.Clamp(swept / TWIRL_REVEAL, 0f, 1f));
            }

            // Carried through the hitstop so the streak burns out behind the impact instead of
            // blinking off on the frame the head stops.
            if (State == SwingState.Impact)
                return 1f - MathHelper.Clamp(StateTimer / IMPACT_TICKS, 0f, 1f);

            return 0f;
        }
    }

    /// <summary>
    ///     The swing streak: a wide warm arc plus a tighter bright edge. Anchored by its leading tip so it trails the head;
    ///     mirroring maps a texture angle to <c>Pi - angle</c> horizontally and negates it vertically.
    /// </summary>
    private void DrawTwirl(float opacity, SpriteEffects effects, bool flipX, bool flipY)
    {
        float tip = TWIRL_TIP;

        if (flipX)
            tip = MathHelper.Pi - tip;

        if (flipY)
            tip = -tip;

        float rotation = Projectile.rotation - tip;
        Vector2 centre = HandFor(CurrentAngle) - Main.screenPosition;
        float reach = (HAMMER_LENGTH + HEAD_REACH) * TotalScale;

        Texture2D wide = Assets.Textures.VFX.SwordSwings.Twirl_01.Asset.Value;
        Texture2D core = Assets.Textures.VFX.SwordSwings.twirl_03.Asset.Value;

        // Additive, and linear rather than point sampled: these are soft 512px gradients, and
        // PointClamp would band them into rings.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        Main.spriteBatch.Draw(wide, centre, null, TwirlColor * (opacity * 0.75f), rotation, wide.Size() * 0.5f, reach / TWIRL_RADIUS, effects, 0f);
        Main.spriteBatch.Draw(core, centre, null, TwirlCoreColor * opacity, rotation, core.Size() * 0.5f, reach * TWIRL_CORE_INSET / TWIRL_CORE_RADIUS, effects, 0f);

        Main.spriteBatch.RestartToDefault();
    }

    private void DrawDebugSegment()
    {
        Vector2 hand = HandFor(CurrentAngle);
        Vector2 direction = DirectionFor(CurrentAngle);

        Main.spriteBatch.DrawLine(
            hand + direction * HAMMER_LENGTH * HILT_FRACTION * TotalScale - Main.screenPosition,
            hand + direction * (HAMMER_LENGTH + HEAD_REACH) * TotalScale - Main.screenPosition,
            IsDamaging ? Color.Red : Color.DarkSlateGray,
            (int)(HEAD_THICKNESS * TotalScale)
        );
    }

    #endregion

    private static EaseFunction BuildSlamEase()
    {
        var ease = new EaseBuilder();

        // Negative: further back past the charged pose before anything comes down.
        ease.AddPoint(0.18f, -0.10f, EaseQuadOut);
        ease.AddPoint(1f, 1f, EaseCubicIn);

        return ease;
    }

    private static EaseFunction BuildRaiseEase()
    {
        var ease = new EaseBuilder();

        ease.AddPoint(0.65f, 1.06f, EaseCircularOut);
        ease.AddPoint(1f, 1f, EaseQuadInOut);

        return ease;
    }
}