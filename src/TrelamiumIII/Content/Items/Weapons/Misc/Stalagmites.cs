using Terraria.Audio;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Items.Weapons.Misc;

/// <summary>
///     A knapped stone spike round: less shrapnel than the block, but it sticks and carries a long affliction.
///     One instance per stone, each paired with a <see cref="StalagmiteProj" />.
/// </summary>
public sealed class StalagmiteItem : ModItem, IStalagMagAmmo, ICustomAutoload
{
    private readonly string stone;
    private readonly int sourceBlock;
    private readonly StalagMagShot shot;

    private StalagmiteItem(string stone, int sourceBlock, StalagMagShot shot)
    {
        this.stone = stone;
        this.sourceBlock = sourceBlock;
        this.shot = shot;
    }

    protected override bool CloneNewInstances => true;

    public override string Name => $"{stone}Stalagmite";

    public override string Texture => AssetReferences.Assets.Textures.Placeholder.PlaceholderItem.KEY;

    public StalagMagShot Shot => shot;

    public static void Autoload(Mod mod)
    {
        Add(mod, "Stone", ItemID.StoneBlock, DustID.Stone, TileID.Stone, damage: 12, BuffID.BrokenArmor, buffTime: 300);
        Add(mod, "Ebonstone", ItemID.EbonstoneBlock, DustID.Corruption, TileID.Ebonstone, damage: 15, BuffID.Poisoned, buffTime: 600);
        Add(mod, "Crimstone", ItemID.CrimstoneBlock, DustID.Crimstone, TileID.Crimstone, damage: 15, BuffID.Bleeding, buffTime: 900);
    }

    private static void Add(Mod mod, string stone, int sourceBlock, int dust, ushort tile, int damage, int buff, int buffTime)
    {
        var shot = new StalagMagShot
        {
            Family = StalagMagFamily.Stalagmite,
            TileType = tile,
            Damage = damage,
            Knockback = 4f,
            ShootSpeed = 18f,
            Crit = 4,
            Buff = buff,
            BuffTime = buffTime,
        };

        var projectile = new StalagmiteProj(stone, dust, shot);
        mod.AddContent(projectile);

        mod.AddContent(new StalagmiteItem(stone, sourceBlock, shot with { ProjectileType = projectile.Type }));
    }

    public override void SetDefaults()
    {
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

    /// <summary>Ten of the stone block it is chipped out of.</summary>
    public override void AddRecipes()
    {
        CreateRecipe(10)
            .AddIngredient(sourceBlock, 10)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
///     A stalagmite in flight, then stuck in something (see <see cref="StickyProjectile" />). The sheet points down, so
///     <see cref="PostAI" /> pins <c>spriteDirection</c> and rotates off velocity instead of the base's horizontal flip.
/// </summary>
public sealed class StalagmiteProj : StickyProjectile
{
    private const float SPRITE_ROTATION = -MathHelper.PiOver2;

    private readonly string stone;
    private readonly int dust;
    private readonly StalagMagShot shot;

    public StalagmiteProj(string stone, int dust, StalagMagShot shot)
    {
        this.stone = stone;
        this.dust = dust;
        this.shot = shot;
    }

    protected override bool CloneNewInstances => true;

    public override string Name => $"{stone}StalagmiteProj";

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.StalactiteProj.KEY;

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

        if (shot.Buff > 0)
            target.AddBuff(shot.Buff, shot.BuffTime);
    }

    protected override void PostAI()
    {
        Projectile.spriteDirection = 1;

        if (stickingToNPC)
        {
            Projectile.rotation = oldRotation + SPRITE_ROTATION;

            return;
        }

        // Velocity is zero once lodged in terrain; keep the last heading.
        if (Projectile.velocity != Vector2.Zero)
            Projectile.rotation = Projectile.velocity.ToRotation() + SPRITE_ROTATION;
    }

    /// <summary>Pivot just behind the point.</summary>
    protected override Vector2 GetDrawOrigin(Texture2D texture)
        => new(texture.Width * 0.5f, texture.Height * 0.72f);

    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, PitchVariance = 0.4f, MaxInstances = 3 }, Projectile.Center);

        for (int i = 0; i < 4; i++)
            Dust.NewDustPerfect(Projectile.Center, dust, Main.rand.NextVector2Circular(2f, 2f), 80, Color.White, 0.95f);
    }
}
