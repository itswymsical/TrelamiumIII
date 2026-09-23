using Terraria.Audio;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Items.Weapons.Misc;

/// <summary>A knapped stone spike round: less shrapnel than the block, but it sticks and carries a long affliction.</summary>
public abstract class StalagmiteItem : ModItem, IStalagMagAmmo
{
    public override string Texture => AssetReferences.Assets.Textures.PlaceholderItem.KEY;

    public abstract StalagMagShot Shot { get; }

    /// <summary>The block this is chipped out of, and the whole of its recipe.</summary>
    protected abstract int SourceBlock { get; }

    public override void SetDefaults()
    {
        StalagMagShot shot = Shot;

        Item.DamageType = DamageClass.Ranged;
        Item.damage = shot.Damage;
        Item.knockBack = shot.Knockback;
        Item.crit = shot.Crit;
        Item.shootSpeed = shot.ShootSpeed;
        Item.shoot = shot.ProjectileType;

        Item.width = Item.height = 16;
        Item.maxStack = Item.CommonMaxStack;

        Item.consumable = true;
        Item.noMelee = true;
        Item.ammo = Item.type;

        Item.value = Item.sellPrice(copper: 10);
        Item.rare = ItemRarityID.White;
    }

    public override void AddRecipes()
    {
        CreateRecipe(10)
            .AddIngredient(SourceBlock, 10)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

public class StoneStalagmite : StalagmiteItem
{
    public override StalagMagShot Shot => new()
    {
        Family = StalagMagFamily.Stalagmite,
        TileType = TileID.Stone,
        ProjectileType = ModContent.ProjectileType<StoneStalagmiteProj>(),
        Damage = 12,
        Knockback = 4f,
        ShootSpeed = 18f,
        Crit = 4,
        Buff = BuffID.BrokenArmor,
        BuffTime = 300,
    };

    protected override int SourceBlock => ItemID.StoneBlock;
}

public class EbonstoneStalagmite : StalagmiteItem
{
    public override StalagMagShot Shot => new()
    {
        Family = StalagMagFamily.Stalagmite,
        TileType = TileID.Ebonstone,
        ProjectileType = ModContent.ProjectileType<EbonstoneStalagmiteProj>(),
        Damage = 15,
        Knockback = 4f,
        ShootSpeed = 18f,
        Crit = 4,
        Buff = BuffID.Poisoned,
        BuffTime = 600,
    };

    protected override int SourceBlock => ItemID.EbonstoneBlock;
}

public class CrimstoneStalagmite : StalagmiteItem
{
    public override StalagMagShot Shot => new()
    {
        Family = StalagMagFamily.Stalagmite,
        TileType = TileID.Crimstone,
        ProjectileType = ModContent.ProjectileType<CrimstoneStalagmiteProj>(),
        Damage = 15,
        Knockback = 4f,
        ShootSpeed = 18f,
        Crit = 4,
        Buff = BuffID.Bleeding,
        BuffTime = 900,
    };

    protected override int SourceBlock => ItemID.CrimstoneBlock;
}

/// <summary>
///     A stalagmite in flight, then stuck in something (see <see cref="StickyProjectile" />). The sheet points down, so
///     <see cref="PostAI" /> pins <c>spriteDirection</c> and rotates off velocity instead of the base's horizontal flip.
/// </summary>
public abstract class StalagmiteProj : StickyProjectile
{
    /// <summary>The sheet points down; velocity does not. This is the difference.</summary>
    private const float SPRITE_ROTATION = -MathHelper.PiOver2;

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.StalactiteProj.KEY;

    protected abstract ushort TileType { get; }

    protected abstract int DustType { get; }

    /// <summary>Affliction carried into whatever it lands in.</summary>
    protected abstract int Buff { get; }

    protected abstract int BuffTime { get; }

    protected override int MaxStickies => 4;

    protected override float Gravity => 0.14f;

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 12;

        Projectile.hide = true;
        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;

        Projectile.timeLeft = 480;
        Projectile.penetrate = Projectile.aiStyle = -1;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        base.OnHitNPC(target, hit, damageDone);

        if (Buff > 0)
            target.AddBuff(Buff, BuffTime);
    }

    protected override void PostAI()
    {
        // Rotation carries all orientation, so the base's horizontal flip must not apply.
        Projectile.spriteDirection = 1;

        if (stickingToNPC)
        {
            Projectile.rotation = oldRotation + SPRITE_ROTATION;

            return;
        }

        // Velocity is zeroed the moment it lands in terrain; there is no heading to read then, and
        // the last one is the one it should keep.
        if (Projectile.velocity != Vector2.Zero)
            Projectile.rotation = Projectile.velocity.ToRotation() + SPRITE_ROTATION;
    }

    /// <summary>Pivot just behind the point, so the shaft trails and the tip leads.</summary>
    protected override Vector2 GetDrawOrigin(Texture2D texture)
        => new(texture.Width * 0.5f, texture.Height * 0.72f);

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, PitchVariance = 0.4f, MaxInstances = 3 }, Projectile.Center);

        for (int i = 0; i < 4; i++)
            Dust.NewDustPerfect(Projectile.Center, DustType, Main.rand.NextVector2Circular(2f, 2f), 80, Color.White, 0.95f);
    }
}

public class StoneStalagmiteProj : StalagmiteProj
{
    protected override ushort TileType => TileID.Stone;

    protected override int DustType => DustID.Stone;

    protected override int Buff => BuffID.BrokenArmor;

    protected override int BuffTime => 300;
}

public class EbonstoneStalagmiteProj : StalagmiteProj
{
    protected override ushort TileType => TileID.Ebonstone;

    protected override int DustType => DustID.Corruption;

    protected override int Buff => BuffID.Poisoned;

    protected override int BuffTime => 600;
}

public class CrimstoneStalagmiteProj : StalagmiteProj
{
    protected override ushort TileType => TileID.Crimstone;

    protected override int DustType => DustID.Crimstone;

    protected override int Buff => BuffID.Bleeding;

    protected override int BuffTime => 900;
}
