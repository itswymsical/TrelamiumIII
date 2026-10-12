using Terraria.Audio;
using Terraria.GameContent;

namespace TrelamiumIII.Content.Items.Ammo;

/// <summary>A metal-tipped wooden arrow. One instance per <see cref="Metals" /> row, each paired with a <see cref="TippedArrowProjectile" />.</summary>
public sealed class TippedArrow : ModItem, ICustomAutoload
{
    public readonly record struct Metal(string Name, int Bar, int Damage, float Knockback, float ShootSpeed, int ValueCopper);

    private static readonly Metal[] Metals =
    [
        new("Copper", ItemID.CopperBar, 4, 4.4f, 3.2f, 20),
        new("Tin", ItemID.TinBar, 4, 4.4f, 3.24f, 22),
        new("Iron", ItemID.IronBar, 5, 6f, 3.72f, 30),
        new("Lead", ItemID.LeadBar, 5, 7f, 3.74f, 36),
        new("Silver", ItemID.SilverBar, 6, 7f, 3.65f, 40),
        new("Tungsten", ItemID.TungstenBar, 7, 9f, 3.88f, 42),
        new("Gold", ItemID.GoldBar, 8, 8f, 4f, 60),
        new("Platinum", ItemID.PlatinumBar, 9, 8.9f, 5.74f, 68),
    ];

    private readonly Metal metal;

    [CloneByReference]
    private TippedArrowProjectile? projectile;

    public TippedArrow(Metal metal) => this.metal = metal;

    protected override bool CloneNewInstances => true;

    public override string Name => $"{metal.Name}TippedArrow";

    public static void Autoload(Mod mod)
    {
        foreach (Metal metal in Metals)
        {
            var arrow = new TippedArrow(metal);
            arrow.projectile = new TippedArrowProjectile(arrow);

            mod.AddContent(arrow);
            mod.AddContent(arrow.projectile);
        }
    }

    public override void SetDefaults()
    {
        Item.width = 14;
        Item.height = 36;
        Item.DamageType = DamageClass.Ranged;
        Item.maxStack = Item.CommonMaxStack;
        Item.ammo = AmmoID.Arrow;
        Item.consumable = true;

        Item.damage = metal.Damage;
        Item.knockBack = metal.Knockback;
        Item.shootSpeed = metal.ShootSpeed;
        Item.shoot = projectile!.Type;
        Item.value = Item.sellPrice(copper: metal.ValueCopper);
    }

    public override void AddRecipes()
    {
        CreateRecipe(25)
            .AddIngredient(ItemID.WoodenArrow, 25)
            .AddIngredient(metal.Bar, 2)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>A <see cref="TippedArrow" /> in flight, drawn with its item's sprite.</summary>
public sealed class TippedArrowProjectile : ModProjectile
{
    [CloneByReference]
    private readonly TippedArrow arrow;

    public TippedArrowProjectile(TippedArrow arrow) => this.arrow = arrow;

    protected override bool CloneNewInstances => true;

    public override string Name => arrow.Name + "Projectile";

    public override string Texture => arrow.Texture;

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
            Projectile.velocity.Y = 16f;
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
