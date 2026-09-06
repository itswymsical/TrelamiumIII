using System.Collections.Generic;
using TrelamiumIII.Content.FarmingOverhaul;

namespace TrelamiumIII.Common.Globals.Items;

/// <summary>
///     Makes vanilla herb seeds dart-class ammunition, each firing its own projectile and carrying
///     its own on-crit effect.
/// </summary>
public sealed class SeedAmmoGlobalItem : GlobalItem
{
    public const string TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Seed Ammo Tooltips";

    /// <summary>
    ///     Everything that varies from one herb seed to the next.
    /// </summary>
    /// <remarks>
    ///     The two content lookups are deferred behind delegates because the table is built at
    ///     static init, which runs before this mod's own types have ids to hand out.
    /// </remarks>
    private readonly record struct SeedAmmo(
        int Damage,
        int Crit,
        Func<int> Projectile,
        Func<int> FavouriteSoil,
        string EffectKey);

    /// <summary>
    ///     The single list of what counts as seed ammunition. Everything else here reads off it, so
    ///     a seed cannot end up with stats but no tooltip, or a tooltip but no projectile.
    /// </summary>
    private static readonly Dictionary<int, SeedAmmo> Seeds = new()
    {
        [ItemID.DaybloomSeeds] = new(4, 10,
            () => ModContent.ProjectileType<DaybloomSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Dirt>(),
            "Daybloom"),

        [ItemID.BlinkrootSeeds] = new(4, 13,
            () => ModContent.ProjectileType<BlinkrootSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Dirt>(),
            "Blinkroot"),

        [ItemID.FireblossomSeeds] = new(5, 9,
            () => ModContent.ProjectileType<FireblossomSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Ash>(),
            "Fireblossom"),

        // TODO: peat or snow, once there is a bed for either.
        [ItemID.ShiverthornSeeds] = new(4, 10,
            () => ModContent.ProjectileType<ShiverthornSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Mud>(),
            "Shiverthorn"),

        [ItemID.MoonglowSeeds] = new(4, 11,
            () => ModContent.ProjectileType<MoonglowSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Mud>(),
            "Moonglow"),

        [ItemID.DeathweedSeeds] = new(3, 11,
            () => ModContent.ProjectileType<DeathweedSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Ash>(),
            "Deathweed"),

        [ItemID.WaterleafSeeds] = new(4, 11,
            () => ModContent.ProjectileType<WaterleafSeedProjectile>(),
            () => ModContent.ItemType<SeedBedItem_Sand>(),
            "Waterleaf"),
    };

    public override bool AppliesToEntity(Item item, bool lateInstantiation) => Seeds.ContainsKey(item.type);

    public override void SetDefaults(Item item)
    {
        if (!Seeds.TryGetValue(item.type, out SeedAmmo seed))
            return;

        item.DamageType = DamageClass.Ranged;
        item.ammo = AmmoID.Dart;
        item.noMelee = true;
        item.shootSpeed = 0.5f;
        item.notAmmo = true;
 
        item.damage = seed.Damage;
        item.crit = seed.Crit;
        item.shoot = seed.Projectile();
    }

    public override bool CanShoot(Item item, Player player) => false;

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!Seeds.TryGetValue(item.type, out SeedAmmo seed))
            return;

        int soil = seed.FavouriteSoil();

        // Soil name pulled off the item rather than written into the line, so renaming a bed cannot
        // leave seven seed tooltips pointing at a name that no longer exists.
        string text = Mod.GetLocalizationValue($"SeedAmmo.{seed.EffectKey}")
                      + '\n'
                      + Mod.GetLocalizationValue("SeedAmmo.FavouriteSoil", soil, Lang.GetItemNameValue(soil));

        tooltips.Add(new TooltipLine(Mod, TOOLTIP_KEY, text));
    }
}
