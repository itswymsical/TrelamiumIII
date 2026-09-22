using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Misc;

/// <summary>Launcher for <see cref="GasCanisterAmmo" />. The held projectile picks and consumes the ammo per shot.</summary>
public class CanisterCannon : ModItem
{
    /// <summary><c>gunProj</c> stops the use that spawns the held projectile from consuming ammo.</summary>
    public override void SetStaticDefaults() => ItemID.Sets.gunProj[Type] = true;

    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();

        Item.damage = 10;

        Item.width = 70;
        Item.height = 38;

        Item.useTime = Item.useAnimation = CanisterCannonProj.ShotProfile.UseSpeed;
        Item.useAmmo = Type;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.rare = ItemRarityID.Blue;

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.knockBack = 7f;
        Item.shootSpeed = 15f;
        Item.scale = 0.97f;
        Item.shoot = ModContent.ProjectileType<CanisterCannonProj>();
    }

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

        return false;
    }
}

public class CanisterCannonProj : HeldGunProjectile
{
    public static readonly GunShotProfile ShotProfile = GunShotProfile.Cannon with
    {
        UseSpeed = 50,
        Kick = 12f,
        Rise = 8f,
        Climb = 0.3f,
        MuzzleLength = 44f,
        Origin = new Vector2(0.3f, 0.5f),
    };

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.CanisterCannon.KEY;

    protected override GunShotProfile Profile => ShotProfile;

    protected override bool CanFire(Player owner) => owner.HasAmmo(owner.HeldItem);

    /// <summary><see cref="Player.PickAmmo" /> applies ammo damage, speed and conservation; <c>ai[0]</c> carries the ammo id.</summary>
    protected override void Fire(Player owner, Vector2 direction, Vector2 muzzle)
    {
        if (!owner.PickAmmo(owner.HeldItem, out int type, out float speed, out int damage, out float knockback, out int ammoType))
            return;

        Projectile.NewProjectile(
            Projectile.GetSource_FromThis(),
            muzzle,
            direction * speed,
            type,
            damage,
            knockback,
            owner.whoAmI,
            ai0: ammoType
        );

        SoundEngine.PlaySound(SoundID.Item61 with { PitchVariance = 0.15f, MaxInstances = 2 }, muzzle);
    }
}

public enum CanisterFuse : byte
{
    /// <summary>Bounces off tiles, passes through NPCs, and bursts after <see cref="GasCanisterAmmo.FuseTicks" />.</summary>
    Timed,

    /// <summary>Bursts on the first NPC or tile it hits.</summary>
    Contact,
}

/// <summary>Base for Canister Cannon ammo. Subclasses supply the gas layer, fuse and burst read by <see cref="GasCanisterProj" />.</summary>
public abstract class GasCanisterAmmo : ModItem
{
    public abstract GasField? Field { get; }

    public abstract CanisterFuse Fuse { get; }

    /// <summary>Ticks from launch to burst. Read only for <see cref="CanisterFuse.Timed" />.</summary>
    public virtual int FuseTicks => 90;

    public virtual BurstProfile Burst => BurstProfile.Vent with { DamageShare = 1f };

    /// <summary>Nodes shed in flight, or null for none.</summary>
    public virtual TrailProfile? Trail => null;

    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.damage = 6;
        Item.width = 24;
        Item.height = 16;
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.ammo = ModContent.ItemType<CanisterCannon>();
        Item.shoot = ModContent.ProjectileType<GasCanisterProj>();
    }
}

/// <summary>A canister in flight. <c>ai[0]</c> is the <see cref="GasCanisterAmmo" /> item id, resolved on every client.</summary>
public class GasCanisterProj : ChemicalProjectile
{
    private const float GRAVITY = 0.25f;
    private const float MAX_FALL = 16f;

    private const float BOUNCE = 0.45f;
    private const float GROUND_FRICTION = 0.85f;

    /// <summary>Floor hits slower than this stop vertical motion instead of bouncing.</summary>
    private const float SETTLE_SPEED = 2f;

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.GasCanister.KEY;

    private GasCanisterAmmo? Ammo => ItemLoader.GetItem((int)Projectile.ai[0]) as GasCanisterAmmo;

    private bool Timed => Ammo?.Fuse == CanisterFuse.Timed;

    protected override GasField? Field => Ammo?.Field;

    protected override TrailProfile? Trail => Ammo?.Trail;

    protected override BurstProfile Burst => Ammo?.Burst ?? base.Burst;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.width = Projectile.height = 14;
        Projectile.aiStyle = -1;
    }

    public override bool? CanDamage() => Timed ? false : null;

    public override void AI()
    {
        // ai[0] is unset during SetDefaults, so the fuse is applied on the first tick (all clients).
        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;

            if (Ammo is { Fuse: CanisterFuse.Timed } ammo)
                Projectile.timeLeft = ammo.FuseTicks;
        }

        Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        Projectile.rotation += Projectile.velocity.X * 0.05f;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        base.OnTileCollide(oldVelocity);

        if (!Timed)
            return true;

        if (Projectile.velocity.X != oldVelocity.X)
            Projectile.velocity.X = -oldVelocity.X * BOUNCE;

        if (Projectile.velocity.Y != oldVelocity.Y)
        {
            Projectile.velocity.Y = MathF.Abs(oldVelocity.Y) < SETTLE_SPEED ? 0f : -oldVelocity.Y * BOUNCE;
            Projectile.velocity.X *= GROUND_FRICTION;
        }

        if (oldVelocity.LengthSquared() > 9f)
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, PitchVariance = 0.2f, MaxInstances = 3 }, Projectile.Center);

        return false;
    }

    /// <summary>A timed canister's last recorded normal can be from an earlier bounce, so grounded is re-tested against the floor.</summary>
    public override void OnKill(int timeLeft)
    {
        if (Timed)
            ImpactNormal = Collision.SolidCollision(Projectile.BottomLeft, Projectile.width, 4, true) ? -Vector2.UnitY : Vector2.Zero;

        base.OnKill(timeLeft);
    }

    protected override void PlayShatter()
        => SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);

    protected override void Debris()
    {
        if (Main.dedServ)
            return;

        for (int i = 0; i < 6; i++)
            Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
    }
}
