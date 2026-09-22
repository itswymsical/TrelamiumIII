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

public class StalagMagMKII : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;

        Item.damage = 44;

        Item.width = 92;
        Item.height = 34;

        Item.useTime = Item.useAnimation = GunShotProfile.Cannon.UseSpeed;
        Item.useAmmo = ItemID.StoneBlock;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.rare = ModContent.RarityType<StalagMagMKIIRarity>();

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.knockBack = 15f;
        Item.shootSpeed = 25f;
        Item.scale = 0.8f;
        Item.shoot = ModContent.ProjectileType<StalagMagMKIIProj>();
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool? CanChooseAmmo(Item ammo, Player player)
        => StalagMagAmmo.TryGet(ammo.type, out _) ? true : null;

    public override bool CanUseItem(Player player)
        => player.ownedProjectileCounts[Item.shoot] <= 0
           && StalagMagAmmo.Choose(player, player.altFunctionUse == 2) is not null;

    public override bool CanConsumeAmmo(Item ammo, Player player) => false;

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

public class StalagMagMKIIProj : HeldGunProjectile
{
    private const int BOULDER_CYCLE = 75;

    private const int BOULDER_RELOAD_TICK = 48;

    private const float BOULDER_SHOVE = 6.5f;

    private static readonly GunShotProfile boulderProfile = GunShotProfile.Cannon with
    {
        UseSpeed = BOULDER_CYCLE,
        Kick = 26f,
        Rise = 10f,
        Climb = 0.26f,
        ArmKick = 0.75f,
        HoldDistance = -0.5f,
    };

    private static readonly GunShotProfile regular = GunShotProfile.Cannon with
    {
        UseSpeed = 30,
        Climb = 0.26f,
        ArmKick = 0.75f,
        HoldDistance = -0.5f,
    };

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.StalagMagMKII.KEY;

    private bool Boulder => Projectile.ai[2] != 0f;

    protected override GunShotProfile Profile => Boulder ? boulderProfile : regular;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = .85f;
    }

    protected override bool CanFire(Player owner) => StalagMagAmmo.Choose(owner, Boulder) is not null;

    public override bool PreAI()
    {
        Player owner = Owner;

        if (Main.myPlayer == Projectile.owner
            && owner.HeldItem.type == ModContent.ItemType<StalagMagMKI>()
            && (Boulder ? owner.controlUseTile : owner.controlUseItem))
        {
            owner.channel = true;
        }

        return base.PreAI();
    }

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

            ai0: shot.Family == StalagMagFamily.Block ? round.type : 0f
        );

        if (index >= 0 && index < Main.maxProjectiles)
            Main.projectile[index].CritChance = shot.Crit + (int)owner.GetTotalCritChance(held.DamageType);

        Consume(round);

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

    private static void Consume(Item round)
    {
        if (--round.stack > 0)
            return;

        round.TurnToAir();
    }
}