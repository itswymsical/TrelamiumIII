using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;
using Terraria.Audio;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class PrismaticStarflaskItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 7;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 27;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<PrismaticStarflaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Thermal;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.015f;
        component.ReactionDamage = 6f;
        component.ReactionHandler = OnPrismaticReaction;
    }

    private static void OnPrismaticReaction(ChemicalReactionEvent e)
    {
        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset);
        e.Target.AddBuff(BuffID.RainbowWhipNPCDebuff, 60);

        int count = Main.rand.Next(8, 16);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 2f);

            Dust.NewDustDirect(vel, e.Target.width, e.Target.height, DustID.Frost, vel.X, vel.Y);
        }
    }
}

public class PrismaticStarflaskProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.PrismaticStarflaskItem.KEY;
    public static bool shattered;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 600;
        Projectile.aiStyle = ProjAIStyleID.ThrownProjectile;
    }

    public override void PostAI()
    {
        base.PostAI();
        shattered = false;

        var layer = GasSystem.GetLayer<PrismaticStarflaskLayer>();

        if (layer == null) return;

        Vector2 impactDir = Projectile.oldVelocity.SafeNormalize(Vector2.UnitY);

        Vector2 scatter = impactDir * Main.rand.NextFloat(1f, 2f)
                        + Main.rand.NextVector2Circular(2.5f, 2.5f);

        layer.SpawnNode(Projectile.Center, scatter, radius: 9, life: 40);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.oldVelocity.Y = -Projectile.oldVelocity.Y;
        return true;
    }

    public override void OnKill(int timeLeft)
    {
        shattered = true;

        SoundEngine.PlaySound(Assets.SFX.Items.BigFlask.Asset with { Volume = 0.7f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });
        SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.65f });
        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with { Volume = 0.2f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });

        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);

        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);

        Vector2 impactDir = Projectile.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f).SafeNormalize(-Vector2.UnitY);

        if (Projectile.owner == Main.myPlayer)
        {
            int count = Main.rand.Next(7, 8);
            for (int i = 0; i < count; i++)
            {
                Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver2)
                            * Main.rand.NextFloat(1f, 7f);

                Projectile.NewProjectile(
                    Projectile.GetProjectileSource_FromThis(),
                    Projectile.Center.X, Projectile.Center.Y,
                    vel.X, vel.Y,
                    ModContent.ProjectileType<PrismaticStarflaskBurstProjectile>(),
                    Projectile.damage, 0f, Projectile.owner);
            }
        }
    }
}

public class PrismaticStarflaskBurstProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Assets.Textures.NullTex.KEY;

    public enum SplashType { Core, Body, Droplet }

    public SplashType splashType
    {
        get => (SplashType)(int)Projectile.ai[0];
        set => Projectile.ai[0] = (int)value;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90 / 4;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
    }

    public override void AI()
    {
        var layer = GasSystem.GetLayer<PrismaticStarflaskLayer>();
        if (layer == null) return;

        float progress = 1f - Projectile.timeLeft / 40f;
        float radius = splashType switch
        {
            SplashType.Core => 19f * progress,
            SplashType.Body => 11 * progress,
            SplashType.Droplet => 12f * progress,
            _ => 0f
        };

        Vector2 nodeVelocity = Projectile.velocity * 0.5f
                             + Main.rand.NextVector2Circular(1.5f, 1.5f);
        var dust = Main.dust[Dust.NewDust(Projectile.position, 8, 8, DustID.PortalBoltTrail, nodeVelocity.X, nodeVelocity.Y)];

        layer.SpawnNode(Projectile.Center, nodeVelocity, radius: radius, life: 80);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {

    }
}