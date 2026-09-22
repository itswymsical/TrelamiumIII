using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Rusted;

public class TetanusShuriken : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Ranged;
        Item.damage = 5;
        Item.width = Item.height = 22;
        Item.useTime = Item.useAnimation = 20;

        Item.knockBack = 0.2f;
        Item.shoot = ModContent.ProjectileType<TetanusShurikenProj>();
        Item.shootSpeed = 10f;
        Item.value = Item.sellPrice(copper: 2);

        Item.UseSound = SoundID.Item1;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.rare = ItemRarityID.Gray;

        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;

        Item.useTurn = 
            Item.noUseGraphic = 
            Item.noMelee = true;
    }
    public override void AddRecipes()
    {
        base.AddRecipes();
        CreateRecipe(15)
            .AddIngredient<RustedIronBar>()
            .AddTile(TileID.Anvils)
            .Register();
    }
}

public class TetanusShurikenProj : StickyProjectile
{
    public override string Texture => AssetReferences.Content.Rusted.TetanusShuriken.KEY;

    private bool thisDaggerChecked = false;

    protected override int MaxStickies => 3;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 12;
        Projectile.drawLayer = ProjectileDrawLayerID.BehindNPCs;
        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.timeLeft = 240;
        Projectile.penetrate = Projectile.aiStyle = -1;
        Projectile.light = 0.2f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);
        target.AddBuff(BuffID.BloodButcherer, 60);
    }

    protected override void OnStuck(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!thisDaggerChecked)
        {
            modifiers.FinalDamage *= CalculateDamageMultiplier(target);
            thisDaggerChecked = true;
        }
    }

    private float CalculateDamageMultiplier(NPC target)
    {
        int stuckDaggers = 0;
        const float dmgBoost = 0.1f;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            var currentProjectile = Main.projectile[i];

            if (i != Projectile.whoAmI
                && currentProjectile.active
                && currentProjectile.type == Projectile.type
                && currentProjectile.ModProjectile is TetanusShurikenProj otherDagger
                && otherDagger.stickingToNPC
                && otherDagger.Target.whoAmI == target.whoAmI)
            {
                stuckDaggers++;
            }
        }

        return Math.Min(1f + (stuckDaggers * dmgBoost), 2.5f);
    }
}