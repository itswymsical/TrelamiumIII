using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Frostbark;

public class BorealHewerProj : ModProjectile, IDrawPrimitive
{
    public override string Texture => AssetReferences.Content.Frostbark.BorealHewerItem.KEY;

    #region flight

    /// <summary>
    /// Per-tick speed kept on the way out. With the item's 10.5 throw this carries about 20 tiles over
    /// <see cref="OUT_TICKS"/>, ending slow enough that the turn reads as the throw running out of arm.
    /// </summary>
    private const float OUT_DRAG = 0.985f;

    /// <summary>Ticks of outward travel before the axe turns around on its own.</summary>
    private const int OUT_TICKS = 40;

    /// <summary>
    /// Pixels the axe may get from its owner before it turns around early. A backstop for an owner who
    /// leaves under their own power, since the drag alone never carries the axe this far.
    /// </summary>
    private const float MAX_RANGE = 620f;

    /// <summary>Pixels per tick squared the return adds toward the owner.</summary>
    private const float RETURN_ACCELERATION = 0.55f;

    /// <summary>Cap on the return speed, in pixels per tick.</summary>
    private const float RETURN_SPEED = 16f;

    /// <summary>Distance from the owner at which the axe is caught, in pixels.</summary>
    private const float CATCH_DISTANCE = 30f;

    /// <summary>Enemies one throw cuts through before it heads home.</summary>
    private const int MAX_PIERCE = 4;

    /// <summary>Ticks an axe buried in terrain waits before it recalls itself unasked.</summary>
    private const int STUCK_PATIENCE = 240;

    /// <summary>Fraction of the axe's damage each arm of an ice burst carries.</summary>
    private const float BURST_DAMAGE_SHARE = 0.3f;

    #endregion

    private bool isStuck = false;
    private bool isReturning = false;

    private int shakeTimer = 0;
    private const int SHAKE_DURATION = 30;
    private int stickTimer = 0;
    private int soundTimer = 0;

    /// <summary>Ticks spent travelling out. Past <see cref="OUT_TICKS"/> the axe returns.</summary>
    private int outTicks = 0;

    /// <summary>Enemies cut on the way out. See <see cref="MAX_PIERCE"/>.</summary>
    private int pierced = 0;

    protected bool stickToTile;

    protected bool stickingToTile;

    private float oldRotation;

    private const int TRAIL_POINTS = 15;

    TrailPositionCache? _trailCache;
    Vector2[]? _trailPositions;
    VertexTrail? _trail;

    private bool InFlight => !stickingToTile && Projectile.velocity != Vector2.Zero;

    /// <summary>Sends every axe this player has out back to them. The item's right click calls this.</summary>
    public static void RecallFor(Player player)
    {
        int type = ModContent.ProjectileType<BorealHewerProj>();

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile.active && projectile.type == type && projectile.owner == player.whoAmI
                && projectile.ModProjectile is BorealHewerProj hewer && !hewer.isReturning)
            {
                hewer.InitiateReturn();
            }
        }
    }

    /// <summary>Whether this player has an axe out that the right click could call back.</summary>
    public static bool AnyOut(Player player)
        => player.ownedProjectileCounts[ModContent.ProjectileType<BorealHewerProj>()] > 0;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.damage = 10;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.width = 42;
        Projectile.height = 32;

        stickToTile =
            Projectile.friendly =
            Projectile.tileCollide =
            Projectile.usesLocalNPCImmunity = true;

        Projectile.localNPCHitCooldown = 20;
        Projectile.timeLeft = 660;
        Projectile.penetrate = Projectile.aiStyle = -1;
        Projectile.light = 0.2f;
        base.SetDefaults();
    }

    /// <summary>Seeds the whole trail history so the ribbon doesn't spend fifteen ticks growing out of the spawn point.</summary>
    public override void OnSpawn(IEntitySource source)
    {
        _trailCache ??= new TrailPositionCache(TRAIL_POINTS);
        _trailCache.Prime(Projectile.Center);
    }

    public override void AI()
    {
        UpdateTrail();
        TrailMist();

        if (stickingToTile)
            Projectile.rotation = oldRotation;

        var player = Main.player[Projectile.owner];
        var distanceToPlayer = Vector2.Distance(Projectile.Center, player.Center);
        soundTimer++;

        Projectile.spriteDirection = Projectile.direction;
        if (!isStuck && !isReturning)
        {
            if (soundTimer >= 14)
            {
                soundTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing, Projectile.position);
            }

            Projectile.rotation += 0.4f * Projectile.direction;

            // Bleeds off speed on the way out, so the turn reads as the throw running out of arm
            // rather than as a reversal.
            Projectile.velocity *= OUT_DRAG;

            if (++outTicks >= OUT_TICKS || distanceToPlayer > MAX_RANGE)
                InitiateReturn();
        }

        else if (isStuck && !isReturning)
        {
            stickTimer++;

            // Buried axes wait to be called back; the patience is only so one cannot be lost.
            if (distanceToPlayer < 70f || stickTimer >= STUCK_PATIENCE)
                InitiateReturn();
        }

        else if (isReturning) HandleReturn(player);
    }

    private void StickToTile()
    {
        isStuck = true;
        Projectile.velocity = Vector2.Zero;
        stickTimer = 0;
    }

    private void InitiateReturn()
    {
        if (isReturning)
            return;

        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with
        {
            MaxInstances = 1,
            Volume = 0.4f,
            Pitch = -0.9f,
        }, Projectile.position);

        Projectile.tileCollide = false;
        stickToTile = false;
        isReturning = true;

        // Only an axe being pulled out of something shudders first; one that simply ran out of throw
        // turns immediately.
        shakeTimer = isStuck ? SHAKE_DURATION : 0;

        Projectile.netUpdate = true;
    }

    private Vector2 shakeOffset = Vector2.Zero;

    /// <summary>
    /// Steers the axe home under acceleration rather than a fixed speed, so it hauls itself back and
    /// arrives fast from wherever the throw ended.
    /// </summary>
    private void HandleReturn(Player player)
    {
        Projectile.tileCollide = false;
        stickToTile = false;
        stickingToTile = false;
        isStuck = false;

        if (shakeTimer > 0)
        {
            shakeOffset = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
            shakeTimer--;

            return;
        }

        shakeOffset = Vector2.Zero;

        Vector2 toPlayer = player.MountedCenter - Projectile.Center;

        if (toPlayer.Length() < CATCH_DISTANCE)
        {
            Projectile.Kill();

            return;
        }

        Projectile.velocity += toPlayer.SafeNormalize(Vector2.Zero) * RETURN_ACCELERATION;

        if (Projectile.velocity.Length() > RETURN_SPEED)
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * RETURN_SPEED;

        Projectile.rotation += 0.4f;

        if (soundTimer >= 14)
        {
            soundTimer = 0;
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing, Projectile.position);
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (!stickingToTile && stickToTile)
        {
            oldRotation = Projectile.rotation;

            Projectile.velocity = Vector2.Zero;

            stickingToTile = true;
        }

        SoundEngine.PlaySound(SoundID.Item50, Projectile.position);

        IceBurst(Projectile.Center, -oldVelocity.SafeNormalize(-Vector2.UnitY), grounded: true, 0.8f);
        BurstMist(Projectile.Center, 0.8f);

        StickToTile();
        return false;
    }

    /// <summary>The axe cuts through what it hits, and turns for home once it has cut enough.</summary>
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(SoundID.Item50, target.position);

        // A crit is the hit that frostburns, so it gets the heavier burst to match.
        float weight = hit.Crit ? 1.4f : 1f;

        IceBurst(Projectile.Center, Projectile.velocity.SafeNormalize(-Vector2.UnitY), grounded: false, weight);
        BurstMist(Projectile.Center, weight);

        if (hit.Crit)
            target.AddBuff(BuffID.Frostburn, 40 + damageDone / 10);

        if (!isReturning && ++pierced >= MAX_PIERCE)
            InitiateReturn();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadProjectile(Projectile.type);
        var texture = TextureAssets.Projectile[Projectile.type].Value;

        var drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);

        for (var k = 0; k < Projectile.oldPos.Length; k++)
        {
            var drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);

            if (k == 0 && shakeTimer > 0)
                drawPos += shakeOffset;

            var color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
            Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
        }

        return false;
    }

    private void UpdateTrail()
    {
        _trailCache ??= new TrailPositionCache(TRAIL_POINTS);
        _trailPositions ??= new Vector2[TRAIL_POINTS];

        _trail ??= new VertexTrail(TRAIL_POINTS,
            f => f * 60f,
            f => new Color(120, 200, 255) * 0.6f * MathF.Pow(f, 2f));

        _trailCache.Update(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero));
        _trailCache.Fill(_trailPositions);
        _trail.Positions = _trailPositions;
    }

    /// <summary>Thin frost shed in flight, so it doesn't compete with the ribbon.</summary>
    private void TrailMist()
    {
        if (!InFlight || !Main.rand.NextBool(2))
            return;

        Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);

        FieldSystem.GetLayer<ArcticField>()?.SpawnNode(
            Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
            back * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.3f, 0.3f),
            radius: Main.rand.NextFloat(9f, 16f),
            life: Main.rand.NextFloat(20f, 34f)
        );
    }

    /// <summary>
    /// Flash-frozen mist thrown off where the axe bites, on the same layer the Hyperborean flask uses,
    /// so the arms carry its frostburn and its rime rather than being decoration.
    /// </summary>
    private void IceBurst(Vector2 position, Vector2 direction, bool grounded, float scale)
    {
        if (FieldSystem.GetLayer<IceMistField>() is not { } mist)
            return;

        mist.SpawnBurst(
            BurstProfile.Vent with
            {
                AirCount = new FloatRange(7f * scale, 12f * scale),
                GroundCount = new FloatRange(7f * scale, 12f * scale),
                AirSpeed = new FloatRange(1.6f, 5.2f),
                GroundSpeed = new FloatRange(1.6f, 5.2f),

                // A bite in open air opens all the way round; one into terrain fans off the surface.
                Arc = grounded ? MathHelper.Pi * 0.8f : MathHelper.TwoPi,
                UpwardBias = 0.25f,
                DamageShare = BURST_DAMAGE_SHARE,
                Core = EmitterProfile.Default with { Lifetime = 8, NodeRadius = 15f * scale },
            },
            position,
            direction,
            grounded,
            Projectile.owner,
            Projectile.damage,
            Projectile.knockBack * 0.25f);
    }

    /// <summary>The puff of frost thrown out where the axe bites in.</summary>
    private static void BurstMist(Vector2 position, float scale = 1f)
    {
        if (FieldSystem.GetLayer<ArcticField>() is not { } mist)
            return;

        int count = (int)(7 * scale);

        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.TwoPi / count * i;
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f) * scale
                               + Main.rand.NextVector2Circular(0.4f, 0.4f);

            mist.SpawnNode(
                position + velocity * 3f,
                velocity,
                radius: Main.rand.NextFloat(13f, 24f) * scale,
                life: Main.rand.NextFloat(28f, 48f)
            );
        }
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        var world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
        var view = Main.GameViewMatrix.TransformationMatrix;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["time"]?.SetValue(Main.GameUpdateCount * 0.07f);
        effect.Parameters["repeats"]?.SetValue(8f);
        effect.Parameters["pixelation"]?.SetValue(4f);
        effect.Parameters["resolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
        effect.Parameters["sampleTexture"]?.SetValue(
            ModContent.Request<Texture2D>(Assets.Textures.VFX.StormTrail.KEY).Value);

        _trail?.Render(effect);
    }
}
