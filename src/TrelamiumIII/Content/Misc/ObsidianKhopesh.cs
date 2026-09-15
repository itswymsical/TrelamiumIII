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

public class ObsidianKhopesh : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.damage = 17;
        Item.knockBack = 1.5f;
        Item.width = Item.height = 34;

        Item.useTime = Item.useAnimation = ObsidianKhopeshProjectile.Swing.ComboPeriod;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.UseSound = null;

        Item.rare =  ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 12);
        Item.shoot = ModContent.ProjectileType<ObsidianKhopeshProjectile>();
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

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Obsidian, 10)
            .AddIngredient(ItemID.Silk, 2)
            .AddIngredient(ItemID.ShadowScale, 8)
            .Register();

        CreateRecipe()
        .AddIngredient(ItemID.Obsidian, 10)
        .AddIngredient(ItemID.Silk, 2)
        .AddIngredient(ItemID.TissueSample, 8)
        .Register();
    }
}

public class ObsidianKhopeshProjectile : HeldSwordProjectile
{
    /// <summary>Shared with the item so the two can't disagree about tempo.</summary>
    public static readonly SwordProfile Swing = SwordProfile.Shortsword with
    {
        BladeLength = 28f,
        ArcOffset = 0,
        RecoverTicks = 5,
        SwingSound = SoundID.Item1,
        Steps = [SwingStep.SlashDown, SwingStep.SlashUp, SwingStep.Thrust],

        TrailColor = Color.MediumPurple,
    };

    public override string Texture => AssetReferences.Content.Misc.ObsidianKhopesh.KEY;

    protected override SwordProfile Profile => Swing;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = 1.15f;       
    }
}