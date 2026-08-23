using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class CorruptedSporeFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 6;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 23;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<CorruptedSporeFlaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Necrotic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.007f;
        component.ReactionDamage = 9f;
        component.ReactionHandler = DoCorruptSporeSpread;
    }

    private static void DoCorruptSporeSpread(ChemicalReactionEvent e)
        => SporePlagueGlobalNPC.InfectWithBurst(e.Target);
}

public class CorruptedSporeFlaskProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.CorruptedSporeFlaskItem.KEY;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 600;
        Projectile.aiStyle = ProjAIStyleID.ThrownProjectile;
    }

    private Vector2 _impactNormal;

    private const float SplashArc = MathHelper.Pi * 0.9f;

    public override void PostAI()
    {
        base.PostAI();

        var layer = GasSystem.GetLayer<CorruptedSporeLayer>();
        if (layer == null) return;

        Vector2 drift = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.4f, 1.2f)
                      + Main.rand.NextVector2Circular(0.5f, 0.5f);

        layer.SpawnNode(Projectile.Center, drift, radius: 4f, life: 40f);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Vector2 normal = Vector2.Zero;
        if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f) normal.X = -Math.Sign(oldVelocity.X);
        if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f) normal.Y = -Math.Sign(oldVelocity.Y);

        _impactNormal = normal.SafeNormalize(Vector2.Zero);
        return true;
    }

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(Assets.SFX.Items.SmallFlask.Asset with { Volume = 0.7f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });

        SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.65f, PitchRange = (-0.15f, 0.15f) });

        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);

        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);

        if (Projectile.owner != Main.myPlayer) return;

        Vector2 outward = _impactNormal != Vector2.Zero
            ? _impactNormal
            : (-Projectile.oldVelocity).SafeNormalize(-Vector2.UnitY);

        Vector2 up = -Vector2.UnitY;
        if (Vector2.Dot(up, outward) >= 0f)
            outward = Vector2.Lerp(outward, up, 0.35f).SafeNormalize(outward);

        Vector2 origin = Projectile.Center + outward * 6f;

        int count = Main.rand.Next(11, 19);
        for (int i = 0; i < count; i++)
        {

            float t = (i + Main.rand.NextFloat(0.15f, 0.85f)) / count;
            float angle = MathHelper.Lerp(-SplashArc * 0.5f, SplashArc * 0.5f, t);
            float edge = Math.Abs(angle) / (SplashArc * 0.5f);

            float speed = MathHelper.Lerp(7.5f, 6f, edge) * Main.rand.NextFloat(0.85f, 1.15f);
            Vector2 vel = outward.RotatedBy(angle) * speed;

            var splash = edge switch
            {
                < 0.5f => CorruptedSporeBurstProjectile.SplashType.Core,
                < 0.7f => CorruptedSporeBurstProjectile.SplashType.Body,
                _ => CorruptedSporeBurstProjectile.SplashType.Droplet,
            };

            Projectile.NewProjectile(
                Projectile.GetProjectileSource_FromThis(),
                origin.X, origin.Y,
                vel.X, vel.Y,
                ModContent.ProjectileType<CorruptedSporeBurstProjectile>(),
                Projectile.damage, 0f, Projectile.owner,
                ai0: (float)(int)splash);
        }
    }
}

public class CorruptedSporeBurstProjectile : ModProjectile
{
    public override string Texture => Assets.Textures.NullTex.KEY;

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
        Projectile.timeLeft = 15;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
    }

    public override void AI()
    {
        var layer = GasSystem.GetLayer<CorruptedSporeLayer>();
        if (layer == null) return;

        float progress = MathHelper.Lerp(1f, Projectile.timeLeft, 0);
        float radius = splashType switch
        {
            SplashType.Core => 14f * progress,
            SplashType.Body => 12f * progress,
            SplashType.Droplet => 7f * progress,
            _ => 0f
        };

        Vector2 nodeVelocity = Projectile.velocity * 0.5f
                             + Main.rand.NextVector2Circular(1.5f, 1.5f);

        layer.SpawnNode(Projectile.Center, nodeVelocity, radius: radius, life: 80);
    }
}