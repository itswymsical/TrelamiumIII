namespace TrelamiumIII.Content.FarmingOverhaul;

/// <summary>
/// The satchel's contents, as data. Every flower and bouquet in the mod is declared here; the values
/// are per unit held (each slot's stack counts up to
/// <see cref="Content.Accessories.Miscellaneous.SatchelPlayer.MAX_STACK_PER_SLOT"/>).
/// </summary>
public class FlowerCatalogSystem : ModSystem
{
    public override void PostSetupContent()
    {
        FlowerRegistry.Clear();

        RegisterVanillaFlowers();
        RegisterCrossModFlowers();
        RegisterBouquets();

        FlowerRegistry.PruneUnformable();
    }

    public override void Unload() => FlowerRegistry.Clear();

    private static void RegisterVanillaFlowers()
    {
        FlowerRegistry.Register(ItemID.Blinkroot)
            .PerUnit(FlowerStat.PickSpeed, 0.003f)
            .PerUnit(FlowerStat.Damage, 0.0005f);

        FlowerRegistry.Register(ItemID.Daybloom)
            .PerUnit(FlowerStat.Life, 0.5f)
            .PerUnit(FlowerStat.Defense, 0.1f);

        FlowerRegistry.Register(ItemID.Fireblossom)
            .PerUnit(FlowerStat.MeleeDamage, 0.002f)
            .PerUnit(FlowerStat.Crit, 0.1f);

        FlowerRegistry.Register(ItemID.Sunflower)
            .PerUnit(FlowerStat.MoveSpeed, 0.003f)
            .PerUnit(FlowerStat.BuildSpeed, 0.001f);

        FlowerRegistry.Register(ItemID.Shiverthorn)
            .PerUnit(FlowerStat.Crit, 0.1f)
            .PerUnit(FlowerStat.BuildSpeed, 0.001f);

        FlowerRegistry.Register(ItemID.Moonglow)
            .PerUnit(FlowerStat.Mana, 0.5f)
            .PerUnit(FlowerStat.ManaRegen, 0.25f);

        FlowerRegistry.Register(ItemID.Waterleaf)
            .PerUnit(FlowerStat.Knockback, 0.001f)
            .PerUnit(FlowerStat.MagicDamage, 0.001f);

        FlowerRegistry.Register(ItemID.Deathweed)
            .PerUnit(FlowerStat.Thorns, 0.001f)
            .PerUnit(FlowerStat.Crit, 0.001f);
    }

    /// <summary>
    /// Flowers from other mods. Each call resolves by name and quietly does nothing when that mod
    /// isn't installed.
    /// <para>A bouquet may name a cross-mod flower too: <see cref="FlowerRegistry.PruneUnformable"/>
    /// drops the whole bouquet when a member can't resolve, so it never half-forms.</para>
    /// </summary>
    private static void RegisterCrossModFlowers()
    {
        FlowerRegistry.Register("SpiritReforged", "Cloudstalk")
            .PerUnit(FlowerStat.MoveSpeed, 0.002f)
            .PerUnit(FlowerStat.JumpSpeed, 0.015f);
    }

    private static void RegisterBouquets()
    {
        FlowerRegistry.RegisterBouquet("Dawn's Grace Bouquet")
            .From(ItemID.Shiverthorn, FlowerStat.Endurance, 0.002f)
            .From(ItemID.Daybloom, FlowerStat.Life, 2f)
            .From(ItemID.Blinkroot, FlowerStat.PickSpeed, 0.0075f)
            .Perk(30, FlowerPerk.HealthySight);

        FlowerRegistry.RegisterBouquet("Duskwilt Bouquet")
            .From(ItemID.Deathweed, FlowerStat.Thorns, 0.003f)
            .From(ItemID.Moonglow, FlowerStat.MeleeDamage, 0.0031515f)
            .From(ItemID.Daybloom, FlowerStat.Crit, 0.005f)
            .Perk(30, FlowerPerk.SaplingOnKill);

        FlowerRegistry.RegisterBouquet("Emberfrost Arrangement")
            .From(ItemID.Shiverthorn, FlowerStat.Crit, 0.35f)
            .From(ItemID.Fireblossom, FlowerStat.MeleeDamage, 0.004f)
            .From(ItemID.Fireblossom, FlowerStat.Knockback, 0.003f);

        FlowerRegistry.RegisterBouquet("Sunrise Spray")
            .From(ItemID.Fireblossom, FlowerStat.LifeRegen, 1f / 3f)
            .From(ItemID.Daybloom, FlowerStat.Defense, 0.25f)
            .From(ItemID.Sunflower, FlowerStat.MoveSpeed, 0.003f)
            .Perk(30, FlowerPerk.DaytimeSpeed);

        FlowerRegistry.RegisterBouquet("Explorer's Corsage")
            .From(ItemID.Moonglow, FlowerStat.BuildSpeed, 0.005f)
            .From(ItemID.Shiverthorn, FlowerStat.Crit, 0.19f)
            .From(ItemID.Blinkroot, FlowerStat.PickSpeed, 0.005f)
            .Perk(15, FlowerPerk.NightVision)
            .Perk(30, FlowerPerk.DangersenseOnHit);
    }
}
