using System.Runtime.InteropServices;
using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;
using static System.Net.Mime.MediaTypeNames;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class ThermiteFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 10;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Orange;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 27;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<ThermiteFlaskProjectile>();
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
        component.AfflictionRate = 10;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.007f;
        component.ReactionDamage = 5f;
        component.ReactionHandler = DoThermiteSpread;
    }

    private static void DoThermiteSpread(ChemicalReactionEvent e)
      {
        var layer = GasSystem.GetLayer<ThermiteLayer>();
        if (layer == null) return;

        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath);

        int count = Main.rand.Next(6, 10);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            layer.SpawnNode(e.Target.Center, vel, radius: 11f, life: 120);
        }
    }
}

public class ThermiteFlaskProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.ThermiteFlaskItem.KEY;
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
        var layer = GasSystem.GetLayer<ThermiteLayer>();
        if (layer == null) return;

        Vector2 impactDir = Projectile.oldVelocity.SafeNormalize(Vector2.UnitY);

        Vector2 scatter = impactDir * Main.rand.NextFloat(2f, 9f)
                        + Main.rand.NextVector2Circular(1.5f, 0.5f);

        layer.SpawnNode(Projectile.Center, scatter, radius: 4, life: 40);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.oldVelocity.Y = -Projectile.oldVelocity.Y;

        return true;
    }

    public override void OnKill(int timeLeft)
    {
        shattered = true;

        SoundEngine.PlaySound(Assets.SFX.Items.SmallFlask.Asset with { Volume = 0.7f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });

        SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.65f, PitchRange = (-0.15f, 0.15f) });

        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);

        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);

        Vector2 impactDir = Projectile.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f).SafeNormalize(-Vector2.UnitY);

        float spread = 2.5f;

        float centre = -MathHelper.PiOver2 + Projectile.direction;

        if (Projectile.owner == Main.myPlayer)
        {
            int count = Main.rand.Next(8, 11);

            for (int i = 0; i < count; i++)
            {
                float slot = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = centre + MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, slot) + Main.rand.NextFloat(-0.06f, 0.06f);
                float speed = 7f * Main.rand.NextFloat(0.75f, 1.15f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    angle.ToRotationVector2() * speed,
                    ModContent.ProjectileType<ThermiteBurstProjectile>(),
                    Projectile.damage, 0f, Projectile.owner
                );
            }
        }
    }
}

public class ThermiteBurstProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Assets.Textures.NullTex.KEY;

    public enum SplashType { Core, Body, Droplet }

    public SplashType splashType
    {
        get => (SplashType)(int)Projectile.ai[0];
        set => Projectile.ai[0] = (int)value;
    }
    private ref float Bounces => ref Projectile.localAI[0];

    private ref float Spin => ref Projectile.localAI[1];

    private const float BOUNCE = 0.42f;
    private const int MAX_BOUNCES = 2;
    private const float GRAVITY = 0.32f;
    private const float MAX_FALL = 14f;
    private const float AIR_DRAG = 0.992f;

    /// <summary>Pixels of terrain charred by a bounce. Roughly the 3x3 the old overlay pass marked.</summary>
    private const float SCORCH_RADIUS = 26f;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = -1;
        Projectile.timeLeft = 100;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = true;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (++Bounces > MAX_BOUNCES)
            return true;

        if (Projectile.velocity.X != oldVelocity.X)
            Projectile.velocity.X = -oldVelocity.X * BOUNCE;

        if (Projectile.velocity.Y != oldVelocity.Y)
            Projectile.velocity.Y = -oldVelocity.Y * BOUNCE;

        Spin *= BOUNCE;

        BurnedCondition.Scorch(Projectile.Center, SCORCH_RADIUS);

        return false;
    }

    public override void AI()
    {
        Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        Projectile.velocity.X *= AIR_DRAG;

        Projectile.rotation += Spin;

        var layer = GasSystem.GetLayer<ThermiteLayer>();
        if (layer == null) return;

        float progress = MathHelper.Lerp(1f, Projectile.timeLeft, 0);
        float radius = splashType switch
        {
            SplashType.Core => 6f * progress,
            SplashType.Body => 3f * progress,
            SplashType.Droplet => 1.5f * progress,
            _ => 0f
        };

        Vector2 nodeVelocity = Projectile.velocity * 0.2f
                             + Main.rand.NextVector2Circular(7.5f, 7.5f);

        layer.SpawnNode(Projectile.Center, nodeVelocity, radius: radius, life: 30);
    }
}