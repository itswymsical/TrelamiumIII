using System.Collections.Generic;
using TrelamiumIII.Content.FarmingOverhaul;

namespace TrelamiumIII.Common.Globals.Items;

public sealed class SeedAmmoGlobalItem : GlobalItem
{
    public const string TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Seed Ammo Tooltips";

    public override bool AppliesToEntity(Item item, bool lateInstantiation)
    {
        return item.type == ItemID.DaybloomSeeds || item.type == ItemID.WaterleafSeeds 
            || item.type == ItemID.MoonglowSeeds  || item.type == ItemID.ShiverthornSeeds 
            || item.type == ItemID.FireblossomSeeds || item.type == ItemID.DeathweedSeeds 
            || item.type == ItemID.BlinkrootSeeds;
    }

    public override void SetDefaults(Item item)
    {
        base.SetDefaults(item);
        
        item.DamageType = DamageClass.Ranged;
        item.ammo = AmmoID.Dart;
        item.noMelee = true;
        item.shootSpeed = 0.5f;
        if (item.type == ItemID.DaybloomSeeds)
        {
            item.damage = 4;
            item.crit = 10;
            item.shoot = ModContent.ProjectileType<DaybloomSeedProjectile>();
        }
        else if (item.type == ItemID.BlinkrootSeeds)
        {
            item.damage = 4;
            item.crit = 13;
            item.shoot = ModContent.ProjectileType<BlinkrootSeedProjectile>();
        }
        else if (item.type == ItemID.FireblossomSeeds)
        {
            item.damage = 5;
            item.crit = 9;
            item.shoot = ModContent.ProjectileType<FireblossomSeedProjectile>();
        }
        else if (item.type == ItemID.ShiverthornSeeds)
        {
            item.damage = 4;
            item.crit = 10;
            item.shoot = ModContent.ProjectileType<ShiverthornSeedProjectile>();
        }
        else if (item.type == ItemID.MoonglowSeeds)
        {
            item.damage = 4;
            item.crit = 11;
            item.shoot = ModContent.ProjectileType<MoonglowSeedProjectile>();
        }
        else if (item.type == ItemID.DeathweedSeeds)
        {
            item.damage = 3;
            item.crit = 11;
            item.shoot = ModContent.ProjectileType<DeathweedSeedProjectile>();
        }
        else if (item.type == ItemID.WaterleafSeeds)
        {
            item.damage = 4;
            item.crit = 11;
            item.shoot = ModContent.ProjectileType<WaterleafSeedProjectile>();
        }
    }

    public override bool CanShoot(Item item, Player player)
    {
        return false;
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        base.ModifyTooltips(item, tooltips);

        var line = new TooltipLine(Mod, TOOLTIP_KEY, $"");
        if (item.type == ItemID.DaybloomSeeds)
        {
            line.Text = $"Critical strikes grant temporary[i:{ItemID.IronskinPotion}]affects" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Dirt>()}]";
        }

        if (item.type == ItemID.BlinkrootSeeds)
        {
            line.Text = $"Critical strikes grant temporary[i:{ItemID.HunterPotion}]&[i:{ItemID.SwiftnessPotion}]affects" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Dirt>()}]";
        }

        if (item.type == ItemID.FireblossomSeeds)
        {
            line.Text = $"Critical strikes inflict '[c/f28755:On Fire!]' for a short duration" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Ash>()}]";
        }

        if (item.type == ItemID.ShiverthornSeeds)
        {
            line.Text = $"Critical strikes inflict '[c/6fc5f6:Frostburn]' for a short duration" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Mud>()}]"; // change to peat or snow
        }

        if (item.type == ItemID.MoonglowSeeds)
        {
            line.Text = $"Critical strikes grant temporary[i:{ItemID.RegenerationPotion}]affects" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Mud>()}]";
        }

        if (item.type == ItemID.DeathweedSeeds)
        {
            line.Text = $"Critical strikes inflict '[c/87b041:Poisioned]' for a short duration" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Ash>()}]";
        }

        if (item.type == ItemID.WaterleafSeeds)
        {
            line.Text = $"Critical strikes inflict '[c/3d9eff:Drenched]' for a short duration" +
                $"\nFavorite soil: [i:{ModContent.ItemType<SeedBedItem_Sand>()}]";
        }
        tooltips.Add(line);
    }
}
