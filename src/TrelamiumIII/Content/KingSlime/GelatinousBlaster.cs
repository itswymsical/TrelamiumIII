using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
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

        MuzzleSpray(Muzzle, Aim, owner.whoAmI, ChemicalHandler.PayloadOf(Projectile), count: 6, speed: 5.5f);
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

        MuzzleSpray(muzzle, direction, owner.whoAmI, ChemicalHandler.PayloadOf(Projectile));
    }

    /// <summary>A short cone of gel wisps off the barrel, in the layer the shot itself uses.</summary>
    private static void MuzzleSpray(Vector2 muzzle, Vector2 direction, int owner, ChemicalPayload? payload, int count = 3, float speed = 4f)
    {
        if (FieldSystem.GetLayer<GelField>() is not { } layer)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 drift = direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(speed * 0.4f, speed);

            layer.SpawnNode(muzzle, drift, radius: Main.rand.NextFloat(5f, 8f), life: 18f, owner: owner, payload: payload);
        }
    }
}

/// <summary>The blaster's gel blob: a player shot that carries the weapon's chemistry and slows what it hits.</summary>
public class GelBallProj : GelShotProjectile
{
    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.AddBuff(BuffID.Slow, 120);

        BurstOnHit();
    }
}
