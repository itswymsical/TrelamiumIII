using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Frostbark;

public class BorealHewerProj : ModProjectile, IDrawPrimitive
{
    // The axe in flight is the same sprite as the item, so it shares the asset rather than
    // duplicating it.
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

    TrailPositionCache? _trailCache;
    Vector2[]? _trailPositions;
    VertexTrail? _trail;

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

    public override void AI()
    {
        UpdateTrail();
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
            // Update the shake offset instead of the actual position
            shakeOffset = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
            shakeTimer--;
        }
        else
        {
            // Reset shake offset when shaking is done
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

            // Stop the projectile's movement when sticking to NPC
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
        StickToTile();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(SoundID.Item50, target.position);
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

        // Apply the shake offset to the drawing position, not the actual position
        for (var k = 0; k < Projectile.oldPos.Length; k++)
        {
            var drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);

            // Apply shake offset only to the current position (k==0), not to trail positions
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
        _trailCache ??= new TrailPositionCache(15);
        _trailPositions ??= new Vector2[15];

        _trail ??= new VertexTrail(15,
            f => f * 60f,
            f => new Color(120, 200, 255) * 0.6f * MathF.Pow(f, 2f));

        // Trail follows the point just behind the projectile, not center —
        // preserves the original ManageCaches behavior
        _trailCache.Update(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero));
        _trailCache.Fill(_trailPositions);
        _trail.Positions = _trailPositions;
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
