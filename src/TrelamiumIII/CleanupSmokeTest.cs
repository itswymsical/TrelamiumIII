// TEMPORARY load test for the class-consolidation pass. Env-gated; delete after use.
using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.Items.Ammo;
using TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII;

internal sealed class CleanupSmokeTest : ModSystem
{
    public override bool IsLoadingEnabled(Mod mod) => Environment.GetEnvironmentVariable("T3_CLEANUP_TEST") == "1";

    public override void PostAddRecipes()
    {
        var log = Mod.Logger;
        int failures = 0;

        void Fail(string message)
        {
            failures++;
            log.Error("[CleanupTest] FAIL " + message);
        }

        string[] metals = ["Copper", "Tin", "Iron", "Lead", "Silver", "Tungsten", "Gold", "Platinum"];
        string[] herbs = ["Daybloom", "Blinkroot", "Fireblossom", "Shiverthorn", "Moonglow", "Deathweed", "Waterleaf"];
        string[] soils = ["Dirt", "Mud", "Sand", "Ash"];
        string[] stones = ["Stone", "Ebonstone", "Crimstone"];
        string[] gases = ["Poison", "Hyperborean", "Vilespore"];
        string[] pieces = ["Chair", "Table", "WorkBench", "Dresser", "Bookcase", "Sofa", "Bed", "Sink", "Bathtub", "Lamp", "Chandelier", "Candelabra", "Lantern", "Piano", "Chest", "Platform"];

        var items = new List<string>();
        var projectiles = new List<string>();
        var tiles = new List<string>();

        items.AddRange(metals.Select(m => $"{m}TippedArrow"));
        projectiles.AddRange(metals.Select(m => $"{m}TippedArrowProjectile"));
        projectiles.AddRange(herbs.Select(h => $"{h}SeedProjectile"));
        items.AddRange(soils.Select(s => $"SeedBedItem_{s}"));
        tiles.AddRange(soils.Select(s => $"SeedBedTile_{s}"));
        items.AddRange(stones.Select(s => $"{s}Stalagmite"));
        projectiles.AddRange(stones.Select(s => $"{s}StalagmiteProj"));
        items.AddRange(gases.SelectMany(g => new[] { $"Timed{g}Canister", $"Contact{g}Canister" }));
        items.AddRange(pieces.Select(p => $"Spruce{p}Item"));
        tiles.AddRange(pieces.Select(p => $"Spruce{p}Tile"));
        items.Add("SpruceDoorItem");
        tiles.Add("SpruceDoorClosedTile");
        tiles.Add("SpruceDoorOpenTile");

        foreach (string name in items)
        {
            if (!Mod.TryFind(name, out ModItem item))
            {
                Fail($"item {name} missing");
                continue;
            }

            if (!ModContent.HasAsset(item.Texture))
                Fail($"item {name} texture {item.Texture} missing");

            var instance = new Item(item.Type);

            if (instance.shoot == 0 && instance.createTile < 0)
                Fail($"item {name} neither shoots nor places");

            if (!Main.recipe.Any(r => r.createItem.type == item.Type))
                Fail($"item {name} has no recipe");
        }

        foreach (string name in projectiles)
        {
            if (!Mod.TryFind(name, out ModProjectile projectile))
            {
                Fail($"projectile {name} missing");
                continue;
            }

            if (!ModContent.HasAsset(projectile.Texture))
                Fail($"projectile {name} texture {projectile.Texture} missing");

            var instance = new Projectile();
            instance.SetDefaults(projectile.Type);

            if (instance.ModProjectile?.Name != name)
                Fail($"projectile {name} instance resolves to {instance.ModProjectile?.Name}");
        }

        foreach (string name in tiles)
        {
            if (!Mod.TryFind(name, out ModTile tile))
            {
                Fail($"tile {name} missing");
                continue;
            }

            if (!ModContent.HasAsset(tile.Texture))
                Fail($"tile {name} texture {tile.Texture} missing");
        }

        if (Mod.TryFind("SpruceDoorClosedTile", out ModTile closed) && Mod.TryFind("SpruceDoorOpenTile", out ModTile open))
        {
            if (TileID.Sets.OpenDoorID[closed.Type] != open.Type || TileID.Sets.CloseDoorID[open.Type] != closed.Type)
                Fail($"door pair not linked: open {TileID.Sets.OpenDoorID[closed.Type]} vs {open.Type}, closed {TileID.Sets.CloseDoorID[open.Type]} vs {closed.Type}");
        }

        foreach (int seed in new[] { ItemID.DaybloomSeeds, ItemID.BlinkrootSeeds, ItemID.FireblossomSeeds, ItemID.ShiverthornSeeds, ItemID.MoonglowSeeds, ItemID.DeathweedSeeds, ItemID.WaterleafSeeds })
        {
            var ammo = new Item(seed);
            if (ammo.ammo != AmmoID.Dart || ammo.shoot <= ProjectileID.Count)
                Fail($"seed {seed} ammo {ammo.ammo} shoot {ammo.shoot}");
        }

        if (SeedBedItem.TileTypes.Length != 4 || SeedBedItem.Dirt == 0)
            Fail("seed bed lookups unset");

        if (GasCanisterAmmo.TimedVilespore == 0 || GasCanisterAmmo.ContactHyperborean == 0)
            Fail("canister lookups unset");

        var ids = ActRegistry.Acts.Select(a => a.ID).OrderBy(i => i, StringComparer.Ordinal).ToList();
        log.Info($"[CleanupTest] acts={ids.Count} ids={string.Join(",", ids)}");

        if (ids.Count != 46)
            Fail($"expected 46 acts, got {ids.Count}");

        log.Info($"[CleanupTest] DONE failures={failures} items={items.Count} projectiles={projectiles.Count} tiles={tiles.Count}");
    }
}
