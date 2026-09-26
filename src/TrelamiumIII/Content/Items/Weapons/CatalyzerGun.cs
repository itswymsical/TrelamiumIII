using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Items.Weapons;

public class CatalyzerGun : ModItem
{
    private const int SHOTS_PER_SOLUTE = 12;

    private int solventCharge;

    public override void SetStaticDefaults() => ItemID.Sets.gunProj[Type] = true;

    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.damage = 4;

        Item.width = 76;
        Item.height = 32;

        Item.useTime = Item.useAnimation = CatalyzerGunProj.ShotProfile.UseSpeed;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.useAmmo = Type;

        Item.rare = ItemRarityID.Green;
        Item.value = Item.sellPrice(silver: 60);

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shootSpeed = 9f;
        Item.shoot = ModContent.ProjectileType<CatalyzerGunProj>();

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        // Solute with no damage and no build-up: catalysis is the whole payload.
        component.ChemicalType = ChemicalDamageType.Solute;
        component.ChemicalDamage = 0;
        component.Concentration = 0;
        component.Catalysis = 0.2f;
        component.ReactionChance = 0;
        component.ReactionDamage = 0;
    }

    public static bool IsCatalyticSolute(Item item)
        => !item.IsAir
           && ChemicalRoles.Of(item) == ChemicalRole.Solute
           && ChemicalRoles.QualityOf(item).HasFlag(ChemicalQuality.Catalytic);

    public override bool? CanChooseAmmo(Item ammo, Player player) => IsCatalyticSolute(ammo);

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

        return false;
    }

    public bool HasSolvent(Player player) => solventCharge > 0 || player.ChooseAmmo(Item) is not null;

    public bool TryDrawSolvent(Player player)
    {
        if (solventCharge <= 0)
        {
            if (player.ChooseAmmo(Item) is not { } solute)
                return false;

            if (--solute.stack <= 0)
                solute.TurnToAir();

            solventCharge = SHOTS_PER_SOLUTE;
        }

        solventCharge--;

        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.CopperBar, 10)
            .AddIngredient(ItemID.TinBar, 6)
            .AddIngredient(ItemID.Glass, 8)
            .AddIngredient(ItemID.IllegalGunParts)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

public class CatalyzerGunProj : HeldGunProjectile
{
    public static readonly GunShotProfile ShotProfile = GunShotProfile.Pistol with
    {
        UseSpeed = 16,
        SnapTicks = 2,
        Kick = 5f,
        Rise = 2f,
        Climb = 0.1f,
        ArmKick = 0.2f,
        MuzzleLength = 58f,
        HoldDistance = 4f,
        Origin = new Vector2(0.24f, 0.56f),
    };

    private const int GLOBS_PER_SHOT = 3;

    private const float SPREAD = 0.16f;

    private const float BARREL_RISE = 10f;

    public override string Texture => AssetReferences.Content.Items.Weapons.CatalyzerGun.KEY;

    protected override GunShotProfile Profile => ShotProfile;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
    }

    protected override bool CanFire(Player owner) => owner.HeldItem.ModItem is CatalyzerGun gun && gun.HasSolvent(owner);

    protected override void Fire(Player owner, Vector2 direction, Vector2 muzzle)
    {
        Item held = owner.HeldItem;

        if (held.ModItem is not CatalyzerGun gun || !gun.TryDrawSolvent(owner))
            return;

        var source = new EntitySource_ItemUse(owner, held);
        int damage = owner.GetWeaponDamage(held);
        Vector2 barrel = muzzle + BarrelUp * BARREL_RISE;

        for (int i = 0; i < GLOBS_PER_SHOT; i++)
        {
            float angle = MathHelper.Lerp(-SPREAD, SPREAD, (i + Main.rand.NextFloat()) / GLOBS_PER_SHOT);
            Vector2 velocity = direction.RotatedBy(angle) * held.shootSpeed * Main.rand.NextFloat(0.85f, 1.1f);

            Projectile.NewProjectile(source, barrel, velocity, ModContent.ProjectileType<CatalyticSolventProj>(), damage, 0f, owner.whoAmI);
        }

        SoundEngine.PlaySound(Assets.SFX.Items.CatalyzerShot.Asset with { PitchVariance = 0.12f, MaxInstances = 3 }, muzzle);
    }
}

public class CatalyticSolventProj : ChemicalProjectile
{
    private const float GRAVITY = 0.18f;

    public override string Texture => Assets.Textures.NullTex.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<CatalystField>();

    protected override TrailProfile? Trail => TrailProfile.Shed with
    {
        NodeRadius = 5f,
        NodeLife = 26f,
        Speed = new FloatRange(0.2f, 0.7f),
    };

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(2f, 4f),
        GroundCount = new FloatRange(3f, 5f),
        AirSpeed = new FloatRange(1.5f, 3f),
        GroundSpeed = new FloatRange(3.5f, 2.5f),
        Core = EmitterProfile.Default with { Lifetime = 7, NodeRadius = 8f, NodeLife = 70f },
        Body = EmitterProfile.Default with { Lifetime = 6, NodeRadius = 7f, NodeLife = 60f },
        Droplet = EmitterProfile.Default with { Lifetime = 5, NodeRadius = 5f, NodeLife = 50f },
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.width = Projectile.height = 10;
        Projectile.timeLeft = 40;
        Projectile.aiStyle = -1;
    }

    public override void AI() => Projectile.velocity.Y += GRAVITY;

    protected override void PlayShatter()
        => SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.2f, Pitch = 0.5f, PitchVariance = 0.3f, MaxInstances = 3 }, Projectile.Center);

    protected override void Debris()
    {
        if (Main.dedServ)
            return;

        for (int i = 0; i < 3; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, 0.9f);
            dust.noGravity = true;
        }
    }
}