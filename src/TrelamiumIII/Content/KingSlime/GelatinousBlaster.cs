using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Content.Items.Weapons.Misc;

namespace TrelamiumIII.Content.KingSlime;

public class GelatinousBlaster : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.damage = 6;
        Item.knockBack = 1.5f;

        Item.width = 44;
        Item.height = 32;

        // Matches the burst period, so the item's own animation clock keeps step with the weapon's.
        Item.useTime = Item.useAnimation = GunShotProfile.BurstPistol.BurstPeriod;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useAmmo = ItemID.Gel;

        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 44);

        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shootSpeed = 11f;
        Item.scale = 0.9f;
        Item.shoot = ModContent.ProjectileType<GelatinousBlasterProj>();

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        // Solute: no element, so no matchup or reaction; the affliction and the coat are the whole effect.
        component.ChemicalType = ChemicalDamageType.Solute;
        component.ChemicalDamage = 2;
        component.debuffRate = 30f;
        component.debuffDuration = TimeConversion.SetTime(secs: 3);
        component.ReactionChance = 0;
        component.ReactionDamage = 0;
    }

    /// <inheritdoc cref="StalagMagMKI.Shoot" />
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

        return false;
    }

    /// <summary>One weapon out at a time; the held projectile is the weapon.</summary>
    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

    /// <summary>The held projectile consumes gel per shot, so the spawning use must not also take a round.</summary>
    public override bool CanConsumeAmmo(Item ammo, Player player) => false;

    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe();

        recipe.AddIngredient(ItemID.Gel, 60);
        recipe.AddRecipeGroup(RecipeGroupID.Wood, 5);
        recipe.AddIngredient(ItemID.IllegalGunParts);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}

/// <summary>The blaster in the player's hands: a three-round burst, a gel blob per round.</summary>
public class GelatinousBlasterProj : BurstGunProjectile
{
    public override string Texture => AssetReferences.Content.KingSlime.GelatinousBlaster.KEY;

    /// <summary>The stock <see cref="GunShotProfile.BurstPistol" />, with only the sprite-dependent numbers changed.</summary>
    protected override GunShotProfile Profile => GunShotProfile.BurstPistol with
    {
        MuzzleLength = 28f,
        HoldDistance = 6f,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.scale = 0.9f;
    }

    protected override bool CanFireBurst(Player owner) => owner.HasAmmo(owner.HeldItem);

    /// <summary>A heavier vent of gel on the last round, so the pause reads as finished rather than jammed.</summary>
    protected override void OnBurstComplete(Player owner)
    {
        SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = 0.6f, MaxInstances = 2 }, Owner.Center);

        MuzzleSpray(Muzzle, Aim, owner.whoAmI, count: 6, speed: 5.5f);
    }

    protected override void Fire(Player owner, Vector2 direction, Vector2 muzzle)
    {
        owner.ConsumeItem(ItemID.Gel, includeVoidBag: true);
        Item held = owner.HeldItem;

        // Spread is a fixed cone rather than a per-shot random walk, so the weapon is inaccurate in
        // a way the player can plan around: the blobs land in a patch, and the patch is the point.
        Vector2 velocity = direction.RotatedByRandom(0.07f) * held.shootSpeed * Main.rand.NextFloat(0.92f, 1.08f);

        Projectile.NewProjectile(
            Projectile.GetSource_FromThis(),
            muzzle,
            velocity,
            ModContent.ProjectileType<GelBallProj>(),
            held.damage,
            held.knockBack,
            owner.whoAmI
        );

        SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.35f, PitchVariance = 0.3f, MaxInstances = 3 }, muzzle);
        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, muzzle);

        MuzzleSpray(muzzle, direction, owner.whoAmI);
    }

    /// <summary>A short cone of gel wisps off the barrel, in the layer the shot itself uses.</summary>
    private static void MuzzleSpray(Vector2 muzzle, Vector2 direction, int owner, int count = 3, float speed = 4f)
    {
        if (FieldSystem.GetLayer<GelField>() is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 drift = direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(speed * 0.4f, speed);

            layer.SpawnNode(muzzle, drift, radius: Main.rand.NextFloat(5f, 8f), life: 18f, owner: owner);
        }
    }
}

/// <summary>
///     A blob of gel in flight. Mostly a hitbox: what the player sees is the <see cref="GelField" /> trail it lays every tick.
/// </summary>
public class GelBallProj : ModProjectile
{
    /// <summary>Pixels of terrain coated when the blob bursts.</summary>
    private const float SPLATTER_RADIUS = 28f;

    /// <summary>Nodes thrown out by the burst.</summary>
    private const int BURST_NODES = 9;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 5;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.aiStyle = -1;
        Projectile.scale = 1f;
    }

    public override void AI()
    {
        Projectile.velocity.Y += 0.16f;
        Projectile.velocity.X *= 0.997f;

        Projectile.rotation += Projectile.velocity.X * 0.04f;

        Trail();
    }

    /// <summary>Lays gel from a point behind the blob, thinning as it travels.</summary>
    private void Trail()
    {
        if (FieldSystem.GetLayer<GelField>() is not { } layer)
            return;

        Vector2 behind = Projectile.Center - Projectile.velocity * 0.4f;
        Vector2 drift = Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f);

        layer.SpawnNode(
            behind,
            drift,
            radius: Main.rand.NextFloat(6f, 9f),
            life: Main.rand.NextFloat(16f, 26f),
            owner: Projectile.owner
        );
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.AddBuff(BuffID.Slow, 120);

        Burst(Projectile.Center, -Projectile.velocity.SafeNormalize(-Vector2.UnitY), Projectile.owner, coatTerrain: false);
    }

    /// <summary>Bursts against terrain, splashing along the collision normal so floors spread and walls run.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        var normal = new Vector2(
            Projectile.velocity.X != oldVelocity.X ? -MathF.Sign(oldVelocity.X) : 0f,
            Projectile.velocity.Y != oldVelocity.Y ? -MathF.Sign(oldVelocity.Y) : 0f
        );

        Burst(Projectile.Center, normal.SafeNormalize(-Vector2.UnitY), Projectile.owner, coatTerrain: true);

        Projectile.Kill();

        return false;
    }

    public override void OnKill(int timeLeft)
        => SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);

    /// <summary>Throws gel outward and, on a terrain hit, coats what is under it.</summary>
    private static void Burst(Vector2 position, Vector2 outward, int owner, bool coatTerrain)
    {
        if (coatTerrain)
            SlimedCondition.Splatter(position, SPLATTER_RADIUS, intensity: 0.9f, owner: owner);

        if (FieldSystem.GetLayer<GelField>() is not { } layer)
            return;

        for (int i = 0; i < BURST_NODES; i++)
        {
            // Biased along the surface rather than straight out of it: a splash spreads across what
            // it landed on, and a cone centred on the normal would have it standing up off the wall.
            Vector2 direction = outward.RotatedByRandom(MathHelper.PiOver2 * 0.9f);

            layer.SpawnNode(
                position + direction * 4f,
                direction * Main.rand.NextFloat(1.5f, 4.5f),
                radius: Main.rand.NextFloat(7f, 12f),
                life: Main.rand.NextFloat(52f, 66f),
                owner: owner
            );
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadProjectile(Projectile.type);

        Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
        Vector2 origin = texture.Size() / 2f;

        for (int i = 0; i < Projectile.oldPos.Length; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;

            float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
            Vector2 position = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

            Main.EntitySpriteDraw(
                texture, position, null, Projectile.GetAlpha(lightColor) * fade * 0.6f,
                Projectile.oldRot[i], origin, Projectile.scale * fade, SpriteEffects.None, 0
            );
        }

        Main.EntitySpriteDraw(
            texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor),
            Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0
        );

        return false;
    }
}
