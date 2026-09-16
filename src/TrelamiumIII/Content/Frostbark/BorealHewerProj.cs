using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Frostbark;

public class BorealHewerProj : ModProjectile, IDrawPrimitive
{
    public override string Texture => AssetReferences.Content.Frostbark.BorealHewerItem.KEY;

    public int MaxStickies => 1;
    private bool isStuck = false;
    private bool isReturning = false;

    private int shakeTimer = 0;
    private const int SHAKE_DURATION = 30;
    private int stickTimer = 0;
    private int soundTimer = 0;
    protected NPC Target => Main.npc[(int)Projectile.ai[1]];

    protected bool stickToNPC;
    protected bool stickToTile;

    protected bool stickingToNPC;
    protected bool stickingToTile;

    private Vector2 offset;

    private float oldRotation;

    private const int TRAIL_POINTS = 15;

    TrailPositionCache? _trailCache;
    Vector2[]? _trailPositions;
    VertexTrail? _trail;

    private bool InFlight => !stickingToNPC && !stickingToTile && Projectile.velocity != Vector2.Zero;

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
            stickToNPC =
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

        if (stickingToNPC)
        {
            if (Target.active && !Target.dontTakeDamage)
            {
                Projectile.tileCollide = false;

                Projectile.Center = Target.Center - offset;

                Projectile.gfxOffY = Target.gfxOffY;
            }
            else
            {
                Projectile.Kill();
            }
        }

        if (stickingToTile || stickingToNPC)
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
            if (distanceToPlayer > 450f)
            {
                isReturning = true;
                HandleReturn(player);
            }
        }

        else if (isStuck && !isReturning)
        {
            stickTimer++;
            if (distanceToPlayer < 70f || stickTimer >= 20)
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
        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with
        {
            MaxInstances = 1,
            Volume = 0.4f,
            Pitch = -0.9f,
        }, Projectile.position);

        Projectile.tileCollide = false;
        stickToNPC = false;
        stickToTile = false;
        stickingToNPC = false;
        isReturning = true;
        shakeTimer = SHAKE_DURATION;
    }

    private Vector2 shakeOffset = Vector2.Zero;

    private void HandleReturn(Player player)
    {
        Projectile.tileCollide = false;
        stickToNPC = false;
        stickToTile = false;
        stickingToNPC = false;

        if (shakeTimer > 0)
        {
            shakeOffset = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
            shakeTimer--;
        }
        else
        {
            shakeOffset = Vector2.Zero;

            var directionToPlayer = player.Center - Projectile.Center;
            directionToPlayer.Normalize();
            var speed = 15f;
            Projectile.velocity = directionToPlayer * speed;

            Projectile.rotation += 0.4f;

            if (Projectile.Hitbox.Intersects(player.Hitbox))
            {
                Projectile.Kill();
            }
            if (soundTimer >= 14)
            {
                soundTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing, Projectile.position);
            }
        }
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!stickingToNPC && !stickingToTile && stickToNPC)
        {
            Projectile.ai[1] = target.whoAmI;

            oldRotation = Projectile.rotation;

            offset = target.Center - Projectile.Center + Projectile.velocity * 0.75f;

            Projectile.velocity = Vector2.Zero;

            stickingToNPC = true;

            Projectile.netUpdate = true;
        }
        else
        {
            RemoveStackProjectiles();
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (!stickingToTile && !stickingToNPC && stickToTile)
        {
            oldRotation = Projectile.rotation;

            Projectile.velocity = Vector2.Zero;

            stickingToTile = true;
        }

        SoundEngine.PlaySound(SoundID.Item50, Projectile.position);
        BurstMist(Projectile.Center, 0.8f);
        StickToTile();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(SoundID.Item50, target.position);

        // A crit is the hit that frostburns, so it gets the heavier cloud to match.
        BurstMist(Projectile.Center, hit.Crit ? 1.4f : 1f);

        if (hit.Crit)
        {
            target.AddBuff(BuffID.Frostburn, 40 + damageDone / 10);
        }
        isStuck = true;
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

    protected void RemoveStackProjectiles()
    {
        var sticking = new Point[MaxStickies];
        var index = 0;

        for (var i = 0; i < Main.maxProjectiles; i++)
        {
            var currentProjectile = Main.projectile[i];

            if (i != Projectile.whoAmI
                && currentProjectile.active
                && currentProjectile.owner == Main.myPlayer
                && currentProjectile.type == Projectile.type
                && currentProjectile.ai[0] == 1f
                && currentProjectile.ai[1] == Target.whoAmI
            )
            {
                sticking[index++] = new Point(i, currentProjectile.timeLeft);

                if (index >= sticking.Length)
                    break;
            }
        }

        if (index >= sticking.Length)
        {
            var oldIndex = 0;

            for (var i = 1; i < sticking.Length; i++)
                if (sticking[i].Y < sticking[oldIndex].Y)
                    oldIndex = i;

            Main.projectile[sticking[oldIndex].X].Kill();
        }
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
