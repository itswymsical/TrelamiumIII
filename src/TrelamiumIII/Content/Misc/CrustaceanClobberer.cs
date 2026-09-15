using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Graphics.Metaballs;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Misc;

public class CrustaceanClobberer : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.damage = 45;
        Item.knockBack = 9.5f;
        Item.crit = 5;
        Item.width = Item.height = 34;

        Item.useTime = Item.useAnimation = CrustaceanClobbererProjectile.Swing.ComboPeriod;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.UseSound = null;

        Item.rare =  ItemRarityID.Orange;
        Item.value = Item.sellPrice(silver: 12);
        Item.shoot = ModContent.ProjectileType<CrustaceanClobbererProjectile>();
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

public class CrustaceanClobbererProjectile : HeldSwordProjectile
{
    /// <summary>Shared with the item so the two can't disagree about tempo.</summary>
    public static readonly SwordProfile Swing = SwordProfile.Broadsword with
    {
        BladeLength = 64f,
        ArcOffset = -0.5f,
        RecoverTicks = 22,
        SwingTicks = 18,
        WindupTicks = 8,

        SwingSound = SoundID.Item1,
        Steps = [SwingStep.SlashDown, SwingStep.SlashUp, SwingStep.Down, SwingStep.Up],

        TrailColor = Color.OrangeRed,
    };

    public override string Texture => AssetReferences.Content.Misc.CrustaceanClobberer.KEY;

    protected override SwordProfile Profile => Swing;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = 1f;       
    }
}