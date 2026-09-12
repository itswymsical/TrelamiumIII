using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Content.Rarities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Misc;

/// <summary>
///     A cannon that eats masonry: each shot takes its numbers from the loaded round (see <see cref="StalagMagAmmo" />), so it
///     upgrades by mining new stone. The alternate trigger fires boulders on a cadence of its own.
/// </summary>
public class StalagMagMKI : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;

        // The Stone Block round, which is the floor of the table rather than a figure of its own.
        Item.damage = 10;

        Item.width = 92;
        Item.height = 34;

        Item.useTime = Item.useAnimation = GunShotProfile.Cannon.UseSpeed;
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

    /// <summary>Boulders are loaded on the right button and on nothing else.</summary>
    public override bool AltFunctionUse(Player player) => true;

    /// <summary>Accepts every table round; null rather than false otherwise, so the normal ammo rules still apply.</summary>
    public override bool? CanChooseAmmo(Item ammo, Player player)
        => StalagMagAmmo.TryGet(ammo.type, out _) ? true : null;

    /// <summary>One weapon out at a time, and only if the pulled trigger has something to fire.</summary>
    public override bool CanUseItem(Player player)
        => player.ownedProjectileCounts[Item.shoot] <= 0
           && StalagMagAmmo.Choose(player, player.altFunctionUse == 2) is not null;

    /// <summary>The held projectile spends the rounds, so the summoning use must not spend one too.</summary>
    public override bool CanConsumeAmmo(Item ammo, Player player) => false;

    /// <summary>
    ///     Puts the weapon in the player's hands. The trigger rides in <c>ai[2]</c> as spawn data, since remote clients can't read
    ///     anyone else's mouse buttons.
    /// </summary>
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(
            source,
            player.Center,
            Vector2.Zero,
            Item.shoot,
            damage,
            knockback,
            player.whoAmI,
            ai2: player.altFunctionUse == 2 ? 1f : 0f
        );

        return false;
    }

    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe();

        recipe.AddIngredient(ItemID.StoneBlock, 30);
        recipe.AddRecipeGroup(RecipeGroupID.Wood, 2);
        recipe.AddIngredient(ItemID.IllegalGunParts);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}

/// <summary>
///     The held StalagMag. Block fire is the stock cannon; boulder fire runs on a clock nearly twice as slow, with a heavy kick.
/// </summary>
public class StalagMagMKIProj : HeldGunProjectile
{
    /// <summary>Ticks from one boulder to the next. The reload, and the recoil, and the wait.</summary>
    private const int BOULDER_CYCLE = 75;

    /// <summary>Ticks into the reload at which the breech is heard closing.</summary>
    private const int BOULDER_RELOAD_TICK = 48;

    /// <summary>Pixels per tick the shot puts into the holder, backwards along the barrel.</summary>
    private const float BOULDER_SHOVE = 6.5f;

    /// <summary>Boulder fire. RecoilTicks is left unset, so the recoil lasts the whole cycle: the recoil is the wait.</summary>
    private static readonly GunShotProfile boulderProfile = GunShotProfile.Cannon with
    {
        UseSpeed = BOULDER_CYCLE,
        Kick = 26f,
        Rise = 10f,
        Climb = 0.26f,
        ArmKick = 0.75f,
    };

    public override string Texture => AssetReferences.Content.Misc.StalagMagMKI.KEY;

    /// <summary>Which trigger brought the weapon out. Fixed for the life of the projectile.</summary>
    private bool Boulder => Projectile.ai[2] != 0f;

    protected override GunShotProfile Profile => Boulder ? boulderProfile : GunShotProfile.Cannon;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = .85f;
    }

    protected override bool CanFire(Player owner) => StalagMagAmmo.Choose(owner, Boulder) is not null;

    /// <summary>
    ///     Keeps the weapon out while its button is held by forcing <c>Player.channel</c> (vanilla only maintains it for the primary
    ///     use) right before <c>base.PreAI</c> reads it; <see cref="HeldGunProjectile.WhileHeld" /> would be too late. Owner-side only.
    /// </summary>
    public override bool PreAI()
    {
        Player owner = Owner;

        // Only while still holding this weapon, or a hotbar swap with the button down would keep the cannon out.
        if (Main.myPlayer == Projectile.owner
            && owner.HeldItem.type == ModContent.ItemType<StalagMagMKI>()
            && (Boulder ? owner.controlUseTile : owner.controlUseItem))
        {
            owner.channel = true;
        }

        return base.PreAI();
    }

    /// <summary>The breech closing partway through the boulder reload. Every client runs the shot clock, so all of them hear it.</summary>
    protected override void WhileHeld(Player owner)
    {
        if (Boulder && TicksSinceShot == BOULDER_RELOAD_TICK)
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
    }

    protected override void Fire(Player owner, Vector2 direction, Vector2 muzzle)
    {
        Item? round = StalagMagAmmo.Choose(owner, Boulder);

        if (round is null || !StalagMagAmmo.TryGet(round.type, out StalagMagShot shot))
            return;

        Item held = owner.HeldItem;

        // Damage comes from the round, scaled by the holder's gear, so better stone stays better all run.
        int damage = (int)owner.GetTotalDamage(held.DamageType).ApplyTo(shot.Damage);
        float knockback = owner.GetWeaponKnockback(held, shot.Knockback);

        int index = Projectile.NewProjectile(
            Projectile.GetSource_FromThis(),
            muzzle,
            direction * shot.ShootSpeed,
            StalagMagAmmo.ResolveProjectile(shot),
            damage,
            knockback,
            owner.whoAmI,
            // Only the block round needs telling what it is: the other families have a projectile
            // per material and already know.
            ai0: shot.Family == StalagMagFamily.Block ? round.type : 0f
        );

        if (index >= 0 && index < Main.maxProjectiles)
            Main.projectile[index].CritChance = shot.Crit + (int)owner.GetTotalCritChance(held.DamageType);

        Consume(round);

        // Only boulders shove the holder, straight back along the barrel. Firing at your feet to travel is a kept side effect.
        if (Boulder)
            owner.velocity -= direction * BOULDER_SHOVE;

        SoundEngine.PlaySound(Assets.SFX.CannonBoom.Asset with
        {
            Volume = Boulder ? 1.1f : 1f,
            Pitch = Boulder ? -0.35f : 0f,
            PitchVariance = 0.2f,
            MaxInstances = 2,
        }, owner.position);
    }

    /// <summary>Spends the exact stack <see cref="StalagMagAmmo.Choose" /> picked; <see cref="Player.ConsumeItem" /> might take from another.</summary>
    private static void Consume(Item round)
    {
        if (--round.stack > 0)
            return;

        round.TurnToAir();
    }
}

/// <summary>
///     A block round in flight. <c>ai[0]</c> is the ammo item id, which resolves the sprite, debris and affliction on every client.
/// </summary>
public class StoneBlockProj : ModProjectile
{
    /// <summary>Registration texture only. What is drawn comes from the round. See PreDraw.</summary>
    public override string Texture => $"Terraria/Images/Item_{ItemID.StoneBlock}";

    private const float GRAVITY = 0.16f;
    private const float MAX_FALL = 16f;

    private const float SplashArc = MathHelper.Pi * 0.9f;

    private Vector2 _impactNormal;

    /// <summary>Item id of the block this was loaded from.</summary>
    private int AmmoType => (int)Projectile.ai[0];

    private StalagMagShot Shot => StalagMagAmmo.GetOrDefault(AmmoType);

    private int SpriteItem => AmmoType > 0 ? AmmoType : ItemID.StoneBlock;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.timeLeft = 240;
        Projectile.penetrate = 1;
        Projectile.aiStyle = -1;
    }

    /// <summary>A hand-rolled flat arc, since the arrow AI style uses the <c>ai[0]</c> slot this needs.</summary>
    public override void AI()
    {
        Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        Projectile.rotation = Projectile.velocity.ToRotation();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);

        StalagMagShot shot = Shot;

        if (shot.Buff > 0)
            target.AddBuff(shot.Buff, shot.BuffTime);
    }

    /// <summary>Records the surface normal so the burst in <see cref="OnKill" /> fans out of the wall.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        _impactNormal = new Vector2(
            Projectile.velocity.X != oldVelocity.X ? -MathF.Sign(oldVelocity.X) : 0f,
            Projectile.velocity.Y != oldVelocity.Y ? -MathF.Sign(oldVelocity.Y) : 0f
        ).SafeNormalize(-Vector2.UnitY);

        Projectile.Kill();

        return false;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadItem(SpriteItem);

        Texture2D texture = TextureAssets.Item[SpriteItem].Value;

        Vector2 drawOrigin = new(texture.Width * 0.5f, texture.Height * 0.5f);

        for (var k = 0; k < Projectile.oldPos.Length; k++)
        {
            var drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size * 0.5f + new Vector2(0f, Projectile.gfxOffY);
            var color = Projectile.GetAlpha(lightColor) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
            Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
        }

        Main.EntitySpriteDraw(
            texture,
            Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
            null,
            Projectile.GetAlpha(lightColor),
            Projectile.rotation,
            drawOrigin,
            Projectile.scale,
            SpriteEffects.None,
            0
        );

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

        StalagMagShot shot = Shot;
        int variant = StalagMagAmmo.BlockVariant(AmmoType);

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
                Projectile.damage / 3, 0f, Projectile.owner,
                ai0: shot.TileType,
                ai1: Main.rand.Next(EarthShardProj.CHUNK_SEEDS));

            ParticleSystem.Spawn<StoneBlockParticle>(origin, vel, 60, variant);
        }
    }
}

/// <summary>The alternate trigger's boulder, which bounces twice before stopping.</summary>
public class BoulderShotProj : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Boulder}";

    private const float GRAVITY = 0.22f;
    private const float MAX_FALL = 18f;

    /// <summary>Speed kept per bounce, and how many before it has spent itself.</summary>
    private const float BOUNCE = 0.55f;
    private const int MAX_BOUNCES = 2;

    /// <summary>Local because it is derived from physics every client already runs.</summary>
    private ref float Bounces => ref Projectile.localAI[0];

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 32;

        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 20;

        Projectile.penetrate = 4;
        Projectile.timeLeft = 300;
        Projectile.aiStyle = -1;
    }

    public override void AI()
    {
        Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        Projectile.velocity.X *= 0.995f;

        // Spin follows horizontal speed, so a boulder that loses momentum visibly stops rolling.
        Projectile.rotation += Projectile.velocity.X * 0.02f;

        if (Main.rand.NextBool(3))
        {
            Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                DustID.Stone,
                -Projectile.velocity * 0.08f,
                120,
                Color.White,
                1.1f
            );
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);

        target.AddBuff(BuffID.BrokenArmor, 300);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);

        if (++Bounces > MAX_BOUNCES)
            return true;

        if (Projectile.velocity.X != oldVelocity.X)
            Projectile.velocity.X = -oldVelocity.X * BOUNCE;

        if (Projectile.velocity.Y != oldVelocity.Y)
            Projectile.velocity.Y = -oldVelocity.Y * BOUNCE;

        Shatter(4);

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);

        Shatter(9);
    }

    /// <summary>Throws stone off the boulder, in the boulder's own rock.</summary>
    private void Shatter(int count)
    {
        if (Main.myPlayer != Projectile.owner)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 9f) - Vector2.UnitY * 2f;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                ModContent.ProjectileType<EarthShardProj>(),
                Projectile.damage / 6,
                0f,
                Projectile.owner,
                ai0: TileID.Stone,
                ai1: Main.rand.Next(EarthShardProj.CHUNK_SEEDS)
            );
        }
    }
}

/// <summary>Block round debris; the variant index is the round's position in <see cref="StalagMagAmmo" />'s table.</summary>
public class StoneBlockParticle : Particle
{
    public override string[] Textures => StalagMagAmmo.BlockTextures;

    public GoreDrift Drift { get; set; } = GoreDrift.Tumble;

    public override Vector2 Size => new(6f);
    public override float Gravity => Drift == GoreDrift.Tumble ? 0.2f : 0f;

    public override bool TileCollide => Drift == GoreDrift.Tumble;

    public override float Drag => Drift == GoreDrift.Tumble ? 0.995f : 0.98f;

    public override float Bounce => 0.34f;
    public override int FadeOutTicks => 30;
    public override bool UseLighting => true;
}
