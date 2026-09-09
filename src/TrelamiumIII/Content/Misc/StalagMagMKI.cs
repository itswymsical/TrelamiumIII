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
///     A cannon that eats masonry.
/// </summary>
/// <remarks>
///     The weapon itself is deliberately unremarkable -- its damage figure is the weakest round it
///     accepts, and every shot takes its numbers from whatever was loaded instead. That is the whole
///     design: the player upgrades the StalagMag by walking somewhere new and mining, not by
///     crafting a better one. See <see cref="StalagMagAmmo" /> for the table that carries it.
///     <para>
///         The alternate trigger is a second weapon sharing a barrel. Boulders are not in the block
///         table, are never picked by the primary fire, and run a cadence of their own -- three
///         rounds almost on top of each other and then three seconds of nothing.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     Every round in the table is valid, whatever <see cref="Item.useAmmo" /> says.
    /// </summary>
    /// <remarks>
    ///     Null rather than false for everything else, so the ordinary <c>ammo</c>/<c>useAmmo</c>
    ///     rules still get their say -- this hook is here to widen the weapon's diet, not to
    ///     re-implement it. Which of the accepted rounds is actually loadable right now depends on
    ///     the trigger, and that is <see cref="CanUseItem" />'s question rather than this one's.
    /// </remarks>
    public override bool? CanChooseAmmo(Item ammo, Player player)
        => StalagMagAmmo.TryGet(ammo.type, out _) ? true : null;

    /// <summary>
    ///     One weapon out at a time, and only if the trigger being pulled has something to fire.
    /// </summary>
    public override bool CanUseItem(Player player)
        => player.ownedProjectileCounts[Item.shoot] <= 0
           && StalagMagAmmo.Choose(player, player.altFunctionUse == 2) is not null;

    /// <summary>
    ///     The held projectile spends the rounds, so the use that summons it must not spend one too.
    /// </summary>
    public override bool CanConsumeAmmo(Item ammo, Player player) => false;

    /// <summary>
    ///     Puts the weapon in the player's hands. It does the rest itself.
    /// </summary>
    /// <remarks>
    ///     No muzzle offset here any more: the held projectile pins itself to the holder's centre
    ///     regardless of where it was spawned, so offsetting the spawn only ever moved it for the
    ///     one frame before its first update. The barrel length that actually matters is
    ///     <see cref="GunShotProfile.MuzzleLength" />, which is where the shots come from.
    ///     <para>
    ///         Which trigger was pulled rides along in <c>ai[2]</c> and is fixed for the life of the
    ///         weapon. It has to be spawn data rather than something read per tick, because a remote
    ///         client has no view of anyone else's mouse buttons and would otherwise draw a cannon
    ///         cycling at the wrong rate.
    ///     </para>
    /// </remarks>
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
///     The StalagMag in the player's hands: a heavy, slow-cycling cannon that shovels stone.
/// </summary>
/// <remarks>
///     <para>
///         All of the recoil, aiming and arm work this used to carry by hand now lives in
///         <see cref="HeldGunProjectile" />, which was lifted out of this class more or less
///         verbatim. What is left is what is actually particular to the StalagMag: two cadences, the
///         stone it eats, and the noise it makes.
///     </para>
///     <para>
///         The two cadences are the whole of the difference between the triggers. Block fire is the
///         stock cannon, unmodified. Boulder fire is the same gun on a clock nearly twice as slow,
///         with a kick big enough to move the man holding it.
///     </para>
/// </remarks>
public class StalagMagMKIProj : HeldGunProjectile
{
    /// <summary>Ticks from one boulder to the next. The reload, and the recoil, and the wait.</summary>
    private const int BOULDER_CYCLE = 75;

    /// <summary>Ticks into the reload at which the breech is heard closing.</summary>
    private const int BOULDER_RELOAD_TICK = 48;

    /// <summary>Pixels per tick the shot puts into the holder, backwards along the barrel.</summary>
    private const float BOULDER_SHOVE = 6.5f;

    /// <summary>
    ///     Boulder fire: one round, and then a second and a quarter of getting over it.
    /// </summary>
    /// <remarks>
    ///     <see cref="GunShotProfile.RecoilTicks" /> is deliberately left alone, so the animation
    ///     takes the fire rate as its length. That is the point of the mode: the recoil is not a
    ///     flourish played over a wait, it *is* the wait, and the weapon is visibly still coming
    ///     back down for every frame the player cannot fire it.
    /// </remarks>
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
    ///     Keeps the weapon out for as long as the button that summoned it is down.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="HeldGunProjectile.HoldToKeep" /> reads <c>Player.channel</c>, which vanilla
    ///         maintains for the primary use and not for the alternate one. Left alone, the boulder
    ///         mode would lower the cannon after its first shot no matter how long the button was
    ///         held, and the reload the whole mode is built around would never be experienced.
    ///     </para>
    ///     <para>
    ///         It has to be here rather than in <see cref="HeldGunProjectile.WhileHeld" />, which
    ///         runs after the firing cycle has already read the flag -- and the player's own update
    ///         has been round again in between, with every opportunity to clear it. Set immediately
    ///         before <c>base.PreAI</c> it is still true when the cycle asks. Owner-side only:
    ///         nobody else's buttons are readable here, and nobody else needs to read them.
    ///     </para>
    /// </remarks>
    public override bool PreAI()
    {
        Player owner = Owner;

        // Still this weapon, specifically. Forcing the flag on regardless would keep the cannon out
        // -- and scaling its damage off whatever is now in the player's hand -- after a hotbar swap
        // made with the button still down.
        if (Main.myPlayer == Projectile.owner
            && owner.HeldItem.type == ModContent.ItemType<StalagMagMKI>()
            && (Boulder ? owner.controlUseTile : owner.controlUseItem))
        {
            owner.channel = true;
        }

        return base.PreAI();
    }

    /// <summary>
    ///     Something heavy being craned into the breech, partway through the reload.
    /// </summary>
    /// <remarks>
    ///     Not on a hook, because there is no hook for the middle of a recovery -- and it wants to
    ///     be in the middle rather than at either end, where it would be lost under the report or
    ///     arrive after the weapon is already able to fire again. Every client runs the shot clock,
    ///     so this lands once per shot for everyone watching rather than only for the holder.
    /// </remarks>
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

        // The round carries the numbers; the holder's gear scales them. Taking the damage off the
        // ammunition and then through the player is what keeps a Titanstone round meaningfully
        // better than a Stone one at every point in a run, rather than only at the start of it.
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

        // The boulder is the only round heavy enough to move the man holding it. Straight back along
        // the barrel, which makes firing at your own feet a way to travel -- that is a consequence
        // of the recoil being real rather than a separate feature, and it is worth keeping.
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

    /// <summary>
    ///     Spends the stack the round actually came out of.
    /// </summary>
    /// <remarks>
    ///     Rather than <see cref="Player.ConsumeItem" /> on the item type, which searches the
    ///     inventory again from the top and can take the round out of a different stack to the one
    ///     <see cref="StalagMagAmmo.Choose" /> picked -- visible to the player as the ammunition
    ///     slot they loaded staying full while their building supply drains.
    /// </remarks>
    private static void Consume(Item round)
    {
        if (--round.stack > 0)
            return;

        round.TurnToAir();
    }
}

/// <summary>
///     A block of stone in flight, drawn and broken in whatever it was cut from.
/// </summary>
/// <remarks>
///     One projectile for every block round rather than one per stone: the material lives in
///     <c>ai[0]</c> as the ammunition's item id, which is enough to recover the sprite, the debris
///     and the affliction from <see cref="StalagMagAmmo" />. It rides the standard spawn packet, so
///     every client cuts the same stone.
/// </remarks>
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

    /// <summary>
    ///     A flat arc, hand-rolled.
    /// </summary>
    /// <remarks>
    ///     Written out rather than left on <c>ProjAIStyleID.Arrow</c> because the arrow style spends
    ///     the low <c>ai</c> slots on its own bookkeeping, and this projectile needs <c>ai[0]</c> for
    ///     the stone it is made of.
    /// </remarks>
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

    /// <summary>
    ///     Records which way the surface faced before dying on it.
    /// </summary>
    /// <remarks>
    ///     The normal is what turns the burst in <see cref="OnKill" /> from a ring into a fan
    ///     thrown back out of the wall, which is the difference between debris that looks blown off
    ///     something and debris that looks like it appeared.
    /// </remarks>
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

/// <summary>
///     The alternate trigger's round: a boulder, thrown rather than dropped.
/// </summary>
/// <remarks>
///     It bounces twice before it gives up, which is most of what makes it feel like a boulder
///     rather than a very large stone block -- a shot that skips down a corridor is doing something
///     the block round cannot, and is the reason to carry the ammunition at all.
/// </remarks>
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

        // Rolling rather than tumbling: the spin comes off the horizontal speed, so a boulder that
        // has lost its momentum visibly stops turning.
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

/// <summary>
///     Chips off a block round, wearing the block's own sprite.
/// </summary>
/// <remarks>
///     One class with a variant per accepted stone rather than a class per stone, and the variant
///     index is the round's position in <see cref="StalagMagAmmo" />'s own table -- so a stone added
///     there arrives here already knowing what colour its debris is.
/// </remarks>
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
