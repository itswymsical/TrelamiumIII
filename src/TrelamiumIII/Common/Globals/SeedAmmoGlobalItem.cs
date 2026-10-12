using System.Collections.Generic;
using TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;

namespace TrelamiumIII.Common.Globals.Items;

/// <summary>Makes vanilla herb seeds dart-class ammunition, with stats and projectile from <see cref="SeedProjectile" />'s table.</summary>
public sealed class SeedAmmoGlobalItem : GlobalItem
{
    public const string TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Seed Ammo Tooltips";

    public override bool AppliesToEntity(Item item, bool lateInstantiation) => SeedProjectile.ForSeed(item.type) is not null;

    public override void SetDefaults(Item item)
    {
        if (SeedProjectile.ForSeed(item.type) is not { } projectile)
            return;

        item.DamageType = DamageClass.Ranged;
        item.ammo = AmmoID.Dart;
        item.noMelee = true;
        item.shootSpeed = 0.5f;
        item.notAmmo = true;

        item.damage = projectile.Info.Damage;
        item.crit = projectile.Info.Crit;
        item.shoot = projectile.Type;
    }

    public override bool CanShoot(Item item, Player player) => false;

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (SeedProjectile.ForSeed(item.type) is not { } projectile)
            return;

        int soil = projectile.Info.FavouriteSoil();

        string text = Mod.GetLocalizationValue($"SeedAmmo.{projectile.Info.Herb}")
                      + '\n'
                      + Mod.GetLocalizationValue("SeedAmmo.FavouriteSoil", soil, Lang.GetItemNameValue(soil));

        tooltips.Add(new TooltipLine(Mod, TOOLTIP_KEY, text));
    }
}
