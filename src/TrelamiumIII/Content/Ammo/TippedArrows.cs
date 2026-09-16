using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Ammo;

#region Abstract
public abstract class TippedArrow : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 14;
        Item.height = 36;
        Item.DamageType = DamageClass.Ranged;
        Item.maxStack = Item.CommonMaxStack;
        Item.ammo = AmmoID.Arrow;
        Item.consumable = true;
    }

}
public abstract class TippedArrowProjectile : ModProjectile
{
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 10;

        Projectile.arrow = true;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.timeLeft = 1200;
    }

    public override void AI()
    {
        Projectile.ai[0] += 1f;
        if (Projectile.ai[0] >= 15f)
        {
            Projectile.ai[0] = 15f;
            Projectile.velocity.Y += 0.1f;
        }

        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

        if (Projectile.velocity.Y > 16f)
        {
            Projectile.velocity.Y = 16f;
        }
    }

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
        for (int i = 0; i < 5; i++)
        {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.t_LivingWood);
            dust.noGravity = true;
            dust.velocity *= 1.5f;
            dust.scale *= 0.9f;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;

        Main.EntitySpriteDraw(
            texture,
            Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
            null,
            Projectile.GetAlpha(lightColor),
            Projectile.rotation,
            texture.Size() * 0.5f,
            Projectile.scale,
            SpriteEffects.FlipVertically
        );

        return false;
    }
}
#endregion

public class CopperTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 4;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 4.4f;
        Item.shootSpeed = 3.2f;

        Item.shoot = ModContent.ProjectileType<CopperTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 20);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.CopperBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class CopperTippedArrowProjectile : TippedArrowProjectile 
{
    public override string Texture => AssetReferences.Content.Ammo.CopperTippedArrow.KEY;
}

public class TinTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 4;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 4.4f;
        Item.shootSpeed = 3.24f;

        Item.shoot = ModContent.ProjectileType<TinTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 22);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.TinBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class TinTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.TinTippedArrow.KEY;
}

public class IronTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 5;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 6f;
        Item.shootSpeed = 3.72f;

        Item.shoot = ModContent.ProjectileType<IronTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 30);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.IronBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class IronTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.IronTippedArrow.KEY;
}

public class LeadTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 5;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 7f;
        Item.shootSpeed = 3.74f;

        Item.shoot = ModContent.ProjectileType<LeadTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 36);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.LeadBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class LeadTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.LeadTippedArrow.KEY;
}

public class SilverTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 6;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 7f;
        Item.shootSpeed = 3.65f;

        Item.shoot = ModContent.ProjectileType<SilverTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 40);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.SilverBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class SilverTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.SilverTippedArrow.KEY;
}

public class TungstenTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 7;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 9f;
        Item.shootSpeed = 3.88f;

        Item.shoot = ModContent.ProjectileType<TungstenTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 42);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.TungstenBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class TungstenTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.TungstenTippedArrow.KEY;
}

public class GoldTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 8;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 8f;
        Item.shootSpeed = 4f;

        Item.shoot = ModContent.ProjectileType<GoldTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 60);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.GoldBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class GoldTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.GoldTippedArrow.KEY;
}

public class PlatinumTippedArrow : TippedArrow
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 9;
        Item.maxStack = Item.CommonMaxStack;

        Item.knockBack = 8.9f;
        Item.shootSpeed = 5.74f;

        Item.shoot = ModContent.ProjectileType<PlatinumTippedArrowProjectile>();
        Item.value = Item.sellPrice(copper: 68);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
        .AddIngredient(ItemID.WoodenArrow, 25)
        .AddIngredient(ItemID.PlatinumBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class PlatinumTippedArrowProjectile : TippedArrowProjectile
{
    public override string Texture => AssetReferences.Content.Ammo.PlatinumTippedArrow.KEY;
}