using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Rarities.Definitions;

namespace TrelamiumIII.Content.Misc;

public class StalagMagMKI : ModItem
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

        Item.rare = ModContent.RarityType<StalagMagMKRarity>();

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.knockBack = 15f;
        Item.shootSpeed = 25f;
        Item.scale = 0.8f;
        Item.shoot = ModContent.ProjectileType<StalagMagMKIProj>();
    }

    /// <summary>
    ///     Puts the weapon in the player's hands. It does the rest itself.
    /// </summary>
    /// <remarks>
    ///     No muzzle offset here any more: the held projectile pins itself to the holder's centre
    ///     regardless of where it was spawned, so offsetting the spawn only ever moved it for the
    ///     one frame before its first update. The barrel length that actually matters is
    ///     <see cref="GunShotProfile.MuzzleLength" />, which is where the shots come from.
    /// </remarks>
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

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

/// <summary>
///     The StalagMag in the player's hands: a heavy, slow-cycling cannon that shovels stone.
/// </summary>
/// <remarks>
///     All of the recoil, aiming and arm work this used to carry by hand now lives in
///     <see cref="HeldGunProjectile" />, which was lifted out of this class more or less verbatim.
///     What is left is what is actually particular to the StalagMag: the numbers in
///     <see cref="GunShotProfile.Cannon" />, the stone it eats, and the noise it makes.
/// </remarks>
public class StalagMagMKIProj : HeldGunProjectile
{
    public override string Texture => AssetReferences.Content.Misc.StalagMagMKI.KEY;

    protected override GunShotProfile Profile => GunShotProfile.Cannon;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = .85f;
    }

    protected override bool CanFire(Player owner) => owner.HasAmmo(owner.HeldItem);

    protected override void Fire(Player owner, Vector2 direction, Vector2 muzzle)
    {
        owner.ConsumeItem(ItemID.StoneBlock);

        Item held = owner.HeldItem;

        Projectile.NewProjectile(
            Projectile.GetSource_FromThis(),
            muzzle,
            direction * held.shootSpeed,
            ModContent.ProjectileType<StoneBlockProj>(),
            held.damage,
            held.knockBack,
            owner.whoAmI
        );

        SoundEngine.PlaySound(Assets.SFX.CannonBoom.Asset with
        {
            Volume = 1f,
            PitchVariance = 0.2f,
            MaxInstances = 2,
        }, owner.position);
    }
}

public class StoneBlockProj : ModProjectile
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