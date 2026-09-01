using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Misc;

public class StalactiteCannon : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;
        Item.damage = 14;

        Item.width = 92;
        Item.height = 34;

        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useAmmo = ItemID.StoneBlock;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.rare = ItemRarityID.Blue;

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.knockBack = 15f;
        Item.shootSpeed = 25f;
        Item.scale = 0.8f;
        Item.shoot = ModContent.ProjectileType<StalactiteCannonProj>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        var muzzleOffset = Vector2.Normalize(new Vector2(velocity.X, velocity.Y)) * 25f;
        if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0))
        {
            position += muzzleOffset;
        }
        Projectile.NewProjectile(source, position, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);
        return false;
    }

    public override bool CanUseItem(Player player)
    {
        return player.ownedProjectileCounts[Item.shoot] <= 0;
    }

    public override bool CanConsumeAmmo(Item ammo, Player player)
    {
        return false;
    }

    public override void AddRecipes()
    {
        var recipe = CreateRecipe();
        recipe.AddIngredient(ItemID.StoneBlock, 30);
        recipe.AddRecipeGroup(RecipeGroupID.Wood, 2);
        recipe.AddIngredient(ItemID.IllegalGunParts);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}

public class StalactiteCannonProj : ModProjectile
{
    public override string Texture => AssetReferences.Content.Misc.StalactiteCannon.KEY;
    private enum AIState
    {
        Firing,
        Recoil
    }

    AIState State
    {
        get => (AIState)Projectile.ai[0];
        set => Projectile.ai[0] = (int)value;
    }

    private Vector2 RecoilOffset
    {
        get
        {
            if (State != AIState.Recoil)
                return Vector2.Zero;

            float progress = MathHelper.Clamp(Projectile.ai[1] / RECOIL_DURATION, 0f, 1f);
            float eased = EaseFunction.EaseCubicOut.Ease(progress);

            float back = MathHelper.Lerp(MAX_RECOIL, 0f, eased);
            float up = MathHelper.Lerp(MAX_VER_KICK, 0f, eased);

            return -aimDirection * back + PerpendicularUp * up;
        }
    }

    private Vector2 PerpendicularUp
    {
        get
        {
            // Rotate aimDirection -90° (screen space: negative Y is "up")
            var perp = new Vector2(aimDirection.Y, -aimDirection.X);
            // Flip so it's always "up" relative to which way the gun is facing
            return Projectile.spriteDirection == 1 ? perp : -perp;
        }
    }

    private Vector2 aimDirection;
    private const float RECOIL_DURATION = 45f;
    private const float MAX_RECOIL = 14f;
    private const float MAX_VER_KICK = 6f;

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.scale = .85f;
    }
    public override bool? CanDamage() => false;

    public override bool PreAI()
    {
        var owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return false;
        }

        aimDirection = Vector2.Normalize(Main.MouseWorld - owner.Center);
        Projectile.Center = owner.Center;
        if (Main.myPlayer == Projectile.owner)
        {
            Projectile.direction = Main.MouseWorld.X > owner.Center.X ? 1 : -1;
            Projectile.netUpdate = true;
        }

        owner.ChangeDir(Projectile.direction);
        Projectile.spriteDirection = Projectile.direction;
        Projectile.rotation = aimDirection.ToRotation();

        if (Main.myPlayer == Projectile.owner)
        {
            if (State == AIState.Firing)
            {
                if (!owner.channel)
                {
                    Projectile.Kill();
                    return false;
                }

                if (owner.HasAmmo(owner.HeldItem))
                {
                    FireProjectile(owner, aimDirection);
                }
                State = AIState.Recoil;
                Projectile.ai[1] = 0;
            }
            else if (State == AIState.Recoil)
            {
                Projectile.ai[1]++;
                if (Projectile.ai[1] >= RECOIL_DURATION)
                {
                    State = AIState.Firing;
                }
            }
        }

        SetOwnerAnimation(owner);

        return false;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        var texture = ModContent.Request<Texture2D>(Texture).Value;
        var origin = new Vector2(texture.Width * 0.15f, texture.Height * 0.55f);
        var drawPos = Projectile.Center - Main.screenPosition + RecoilOffset;
        var spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

        Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

        return false;
    }

    private void FireProjectile(Player owner, Vector2 direction)
    {
        owner.ConsumeItem(ItemID.StoneBlock);
        var type = ModContent.ProjectileType<StalactiteProj>();
        var speed = owner.HeldItem.shootSpeed;
        var damage = owner.HeldItem.damage;
        var knockback = owner.HeldItem.knockBack;

        Projectile.NewProjectile(Projectile.GetSource_FromThis(), owner.Center, direction * speed, type, damage, knockback, owner.whoAmI);

        SoundEngine.PlaySound(Assets.SFX.CannonBoom.Asset with
        {
            Volume = 1f,
            PitchVariance = 0.2f,
            MaxInstances = 2,
        }, owner.position);
    }

    private void SetOwnerAnimation(Player owner)
    {
        owner.ChangeDir(Projectile.direction);
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = 2;
        owner.itemAnimation = 2;

        float kickAngle = 0f;
        if (State == AIState.Recoil)
        {
            float progress = MathHelper.Clamp(Projectile.ai[1] / RECOIL_DURATION, 0f, 1f);
            kickAngle = MathHelper.Lerp(-0.45f, 0f, EaseFunction.EaseCubicOut.Ease(progress));
        }

        var rot = (owner.Center - Main.MouseWorld).ToRotation() + MathHelper.PiOver2 + kickAngle * Projectile.direction;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot);
    }
}

public class StalactiteProj : ModProjectile
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.StoneBlock}";


    private Vector2 _impactNormal;

    private const float SplashArc = MathHelper.Pi * 0.9f;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 18;
        //Projectile.hide = true;
        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.timeLeft = 240;
        Projectile.penetrate = 1;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);
        target.AddBuff(BuffID.BrokenArmor, 60);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadProjectile(Projectile.type);
        var texture = TextureAssets.Projectile[Projectile.type].Value;

        Vector2 drawOrigin = new(texture.Width * 0.5f, Projectile.height * 0.5f);
        for (var k = 0; k < Projectile.oldPos.Length; k++)
        {
            var drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
            var color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
            Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
        }

        return true;
    }

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

        bool hitTile = _impactNormal != Vector2.Zero;

        int count = hitTile ? Main.rand.Next(3, 5) : Main.rand.Next(4, 7);

        Vector2 origin = Projectile.Center + (hitTile ? _impactNormal * 2f : Vector2.Zero);

        for (int i = 0; i < count; i++)
        {
            Vector2 vel;
            float edge;

            if (hitTile)
            {
                Vector2 outward = _impactNormal;
                Vector2 up = -Vector2.UnitY;

                if (Vector2.Dot(up, outward) >= 0f)
                    outward = Vector2.Lerp(outward, up, 0.35f).SafeNormalize(outward);

                float t = (i + Main.rand.NextFloat(0.15f, 0.85f)) / count;
                float angle = MathHelper.Lerp(-SplashArc * 0.5f, SplashArc * 0.5f, t);
                edge = Math.Abs(angle) / (SplashArc * 0.5f);

                float speed = MathHelper.Lerp(11.5f, 12f, edge) * Main.rand.NextFloat(0.85f, 1.15f);
                vel = outward.RotatedBy(angle) * speed;
            }
            else
            {
                float angle = MathHelper.TwoPi * ((float)i / count) + Main.rand.NextFloat(-0.2f, 0.2f);

                edge = Main.rand.NextFloat();

                float speed = Main.rand.NextFloat(3f, 8f);
                vel = angle.ToRotationVector2() * speed;
            }

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                origin.X, origin.Y,
                vel.X, vel.Y,
                ModContent.ProjectileType<EarthShardProj>(),
                Projectile.damage / 3, 0f, Projectile.owner, ai0: TileID.Stone);
            ParticleSystem.Spawn<StoneBlockParticle>(origin, vel, 60);
        }
    }
}

public sealed class StoneAmmoItem : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
    {
        return entity.type == ItemID.StoneBlock;
    }

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);

        entity.DamageType = DamageClass.Ranged;
        entity.damage = 3;
        entity.knockBack = 7f;
        entity.noMelee = true;
        entity.shootSpeed = 6f;
        entity.notAmmo = true;
        entity.ammo = entity.type;

    }
}

public class StoneBlockParticle : Particle
{
    public override string[] Textures => [$"Images/Item_{ItemID.StoneBlock}"];
    public GoreDrift Drift { get; set; } = GoreDrift.Tumble;

    public override Vector2 Size => new(6f);
    public override float Gravity => Drift == GoreDrift.Tumble ? 0.2f : 0f;

    public override bool TileCollide => Drift == GoreDrift.Tumble;

    public override float Drag => Drift == GoreDrift.Tumble ? 0.995f : 0.98f;

    public override float Bounce => 0.34f;
    public override int FadeOutTicks => 30;
    public override bool UseLighting => true;
}