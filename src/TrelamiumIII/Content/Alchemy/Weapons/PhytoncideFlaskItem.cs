using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;
using Terraria.Audio;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class PhytoncideFlaskItem : ModItem
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
        Item.shoot = ModContent.ProjectileType<PhytoncideFlaskProjectile>();
        Item.shootSpeed = 8f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Caustic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 2f;
        component.ReactionHandler = DoPhytoncideBloom;
    }

    /// <summary>Fraction of the reaction's landed damage returned to the thrower as life.</summary>
    private const float LifestealFraction = 0.4f;

    private static void DoPhytoncideBloom(ChemicalReactionEvent e)
    {
        PhytoncideBloomGlobalNPC.BloomWithBurst(e.Target);

        // Lifesteal needs somebody to credit, and only projectile-delivered reactions carry an
        // owner; one rolled by a drifting cloud has none.
        if (e.DeliveryProjectile is not { } proj) return;
        if (proj.owner < 0 || proj.owner >= Main.maxPlayers) return;

        Player owner = Main.player[proj.owner];
        if (!owner.active) return;

        owner.GetModPlayer<PhytoncidePlayer>()
             .HealSelf((int)MathF.Round(e.DamageDealt * LifestealFraction));
    }
}

public class PhytoncideFlaskProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.PhytoncideFlaskItem.KEY;

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

        var layer = GasSystem.GetLayer<PhytoncideSporeLayer>();
        if (layer == null) return;

        Vector2 drift = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.4f, 1.2f)
                      + Main.rand.NextVector2Circular(0.5f, 0.5f);

        layer.SpawnNode(Projectile.Center, drift, radius: 5f, life: 8f);
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

        Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);

        if (Projectile.owner != Main.myPlayer) return;

        bool hitTile = _impactNormal != Vector2.Zero;

        int count = hitTile ? Main.rand.Next(7, 11) : Main.rand.Next(12, 16);

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

                float speed = MathHelper.Lerp(11.5f, 12f, edge) * Main.rand.NextFloat(0.85f, 1.15f);
                vel = outward.RotatedBy(angle) * speed;
            }
            else
            {
                float angle = MathHelper.TwoPi * ((float)i / count) + Main.rand.NextFloat(-0.2f, 0.2f);

                edge = Main.rand.NextFloat();

                float speed = Main.rand.NextFloat(3f, 8f);
                vel = angle.ToRotationVector2() * speed;
            }

            var splash = edge switch
            {
                < 0.3f => PhytoncideSporeBurstProjectile.SplashType.Core,
                < 0.7f => PhytoncideSporeBurstProjectile.SplashType.Body,
                _ => PhytoncideSporeBurstProjectile.SplashType.Droplet,
            };

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                origin.X, origin.Y,
                vel.X, vel.Y,
                ModContent.ProjectileType<PhytoncideSporeBurstProjectile>(),
                Projectile.damage, 0f, Projectile.owner,
                ai0: (float)(int)splash);
        }
    }
}

public class PhytoncideSporeBurstProjectile : ModProjectile
{
    public override string Texture => AssetReferences.Assets.Textures.NullTex.KEY;

    public enum SplashType { Core, Body, Droplet }

    public SplashType splashType
    {
        get => (SplashType)(int)Projectile.ai[0];
        set => Projectile.ai[0] = (int)value;
    }

    /// <summary>Ticks each splash arm seeds nodes for. Drives the expansion curve in <see cref="AI"/>.</summary>
    private const int Lifetime = 15;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
    }

    public override void AI()
    {
        Projectile.velocity *= 0.88f;

        var layer = GasSystem.GetLayer<PhytoncideSporeLayer>();
        if (layer == null) return;

        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float grow = MathHelper.Lerp(0.35f, 1f, MathF.Sqrt(progress));

        float radius = splashType switch
        {
            SplashType.Core => 28f * grow,
            SplashType.Body => 12f * grow,
            SplashType.Droplet => 7f * grow,
            _ => 0f
        };

        Vector2 nodeVelocity = Projectile.velocity * 0.5f
                             + Main.rand.NextVector2Circular(1.5f, 5f);

        layer.SpawnNode(Projectile.Center, nodeVelocity, radius: radius, life: Lifetime * 1.65f);
    }
}