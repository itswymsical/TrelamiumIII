using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class VilesporeFlaskItem : ModItem
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
        Item.shoot = ModContent.ProjectileType<VilesporeFlaskProjectile>();
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

public class VilesporeFlaskProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.VilesporeFlaskItem.KEY;

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

        layer.SpawnNode(Projectile.Center, drift, radius: 4f, life: 40f, owner: Projectile.owner);
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

        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);

        Gore.NewGore(Projectile.GetProjectileSource_FromThis(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.GetProjectileSource_FromThis(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);

        if (Projectile.owner != Main.myPlayer) return;

        bool hitTile = _impactNormal != Vector2.Zero;

        int count = hitTile ? Main.rand.Next(11, 19) : Main.rand.Next(16, 24);

        Vector2 origin = Projectile.Center + (hitTile ? _impactNormal * 2f : Vector2.Zero);

        for (int i = 0; i < count; i++)
        {
            Vector2 vel;
            float edge;

            if (hitTile)
            {
                Vector2 outward = _impactNormal;
                Vector2 up = -Vector2.UnitY;
                if (Vector2.Dot(up, outward) >= 0f)
                    outward = Vector2.Lerp(outward, up, 0.35f).SafeNormalize(outward);

                float t = (i + Main.rand.NextFloat(0.15f, 0.85f)) / count;
                float angle = MathHelper.Lerp(-SplashArc * 0.5f, SplashArc * 0.5f, t);
                edge = Math.Abs(angle) / (SplashArc * 0.5f);

                float speed = MathHelper.Lerp(7.5f, 6f, edge) * Main.rand.NextFloat(0.85f, 1.15f);
                vel = outward.RotatedBy(angle) * speed;
            }
            else
            {
                float angle = MathHelper.TwoPi * ((float)i / count) + Main.rand.NextFloat(-0.2f, 0.2f);
                edge = Main.rand.NextFloat();

                float speed = Main.rand.NextFloat(3.5f, 5.5f);
                vel = angle.ToRotationVector2() * speed;
            }

            var splash = edge switch
            {
                < 0.5f => VilesporeFlaskBurstProjectile.SplashType.Core,
                < 0.7f => VilesporeFlaskBurstProjectile.SplashType.Body,
                _ => VilesporeFlaskBurstProjectile.SplashType.Droplet,
            };

            Projectile.NewProjectile(
                Projectile.GetProjectileSource_FromThis(),
                origin.X, origin.Y,
                vel.X, vel.Y,
                ModContent.ProjectileType<VilesporeFlaskBurstProjectile>(),
                Projectile.damage, 0f, Projectile.owner,
                ai0: (float)(int)splash);
        }
    }
}

public class VilesporeFlaskBurstProjectile : ModProjectile
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

        layer.SpawnNode(Projectile.Center, nodeVelocity, radius: radius, life: 80, owner: Projectile.owner);
    }
}