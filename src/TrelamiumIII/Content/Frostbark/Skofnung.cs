using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Frostbark;

public class Skofnung : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.damage = 12;
        Item.knockBack = 4.5f;
        Item.width = Item.height = 52;

        // Read off the projectile's profile rather than the preset it was built from, or the
        // item's clock silently drifts from the weapon's the moment the profile is tuned.
        Item.useTime = Item.useAnimation = SkofnungProjectile.Swing.ComboPeriod;
        Item.useStyle = ItemUseStyleID.Shoot;

        // The held projectile owns the swing sound, so the item must not also announce the use.
        Item.UseSound = null;

        Item.rare =  ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 22);
        Item.shoot = ModContent.ProjectileType<SkofnungProjectile>();
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
    }

    public override bool MeleePrefix() => true;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

        return false;
    }

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
}

public class SkofnungProjectile : HeldSwordProjectile
{
    /// <summary>Shared with the item so the two can't disagree about tempo.</summary>
    public static readonly SwordProfile Swing = SwordProfile.Dagger with
    {
        BladeLength = 60f,
        ArcOffset = 0,
        SwingTicks = 14,
        SwingSound = SoundID.Item1,
    };

    public override string Texture => AssetReferences.Content.Frostbark.Skofnung.KEY;

    protected override SwordProfile Profile => Swing;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = 1f;       
    }
}