using Terraria.DataStructures;
using TrelamiumIII.Core.Graphics.Fields.Layers;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Argie;

/// <summary>Argie's sidearm: sprays a short cone of glowing spore mist that hurts what it drifts through.</summary>
public class SporeSprayer : ModItem
{
    private const int Puffs = 3;

    /// <summary>Half-angle of the cone, in radians.</summary>
    private const float Spread = 0.22f;

    public override string Texture => $"Terraria/Images/Item_{ItemID.WaterGun}";

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 18;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useTime = 16;
        Item.useAnimation = 16;
        Item.damage = 5;
        Item.knockBack = 0.5f;
        Item.DamageType = DamageClass.Ranged;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<SporeMist>();
        Item.shootSpeed = 8f;
        Item.UseSound = SoundID.Item13;
        Item.rare = ItemRarityID.Blue;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Vector2 muzzle = position + velocity.SafeNormalize(Vector2.UnitX * player.direction) * 20f;

        if (!Collision.CanHit(position, 0, 0, muzzle, 0, 0))
            muzzle = position;

        // A world NPC's backing player has no slot; its shots are owned like vanilla town NPC shots.
        int owner = player.whoAmI < Main.maxPlayers ? player.whoAmI : Main.myPlayer;

        for (int i = 0; i < Puffs; i++)
        {
            Vector2 puff = velocity.RotatedByRandom(Spread) * Main.rand.NextFloat(0.8f, 1.15f);
            Projectile.NewProjectile(source, muzzle, puff, type, damage, knockback, owner);
        }

        return false;
    }
}

/// <summary>One puff of <see cref="SporeSprayer" /> mist: slows to a hang and fades, hitting each NPC once per 20 ticks.</summary>
public class SporeMist : ModProjectile
{
    private const int Lifetime = 36;

    private const float Drag = 0.94f;

    public override string Texture => Assets.Textures.VFX.RadialGradient.KEY;

    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 26;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 20;
        Projectile.timeLeft = Lifetime;
    }

    public override void AI()
    {
        Projectile.velocity *= Drag;

        if (Main.dedServ || Projectile.timeLeft % 2 != 0)
            return;

        float age = 1f - Projectile.timeLeft / (float)Lifetime;
        GlowSporeField.Puff(Projectile.Center, Projectile.velocity * 0.5f, MathHelper.Lerp(7f, 15f, age));
    }

    /// <summary>Hangs against the surface instead of dying on it.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity) => false;

    public override bool PreDraw(ref Color lightColor) => false;
}
