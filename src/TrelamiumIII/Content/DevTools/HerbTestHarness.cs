using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Terraria.DataStructures;
using Terraria.IO;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.DevTools;

/// <summary> TEMPORARY: exercises CustomHerb in a real client and screenshots a test bed. </summary>
public sealed class HerbTestHarness : ModSystem
{
    private static string? output;
    private static readonly StringBuilder log = new();
    private static int step;
    private static int timer;
    private static int failures;
    private static string? capture;
    private static Point camera;
    private static Point powderHerb;
    private static int powderType;

    public override void PostSetupContent()
    {
        if (Main.dedServ || Environment.GetEnvironmentVariable("TRELAMIUM_HERB_TEST") is not { Length: > 0 } path)
            return;

        output = path;
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Main.PlayerPath);

        if (Directory.GetFiles(Main.PlayerPath, "*.plr").Length == 0)
            PlayerFileData.CreateAndSave(new Player { name = "Harness", difficulty = PlayerDifficultyID.SoftCore });

        Main.OnPostDraw += Capture;
    }

    public override void Unload() => Main.OnPostDraw -= Capture;

    public override void PostUpdateEverything()
    {
        if (output is null || Main.gameMenu)
            return;

        Player player = Main.LocalPlayer;
        player.immuneTime = 2;
        player.immune = true;
        player.statLife = player.statLifeMax2;
        Main.hideUI = true;
        Main.dayTime = true;
        Main.time = 27000;
        Main.raining = false;

        if (step > 0)
        {
            player.position = camera.ToWorldCoordinates() - new Vector2(player.width / 2f, player.height);
            player.velocity = Vector2.Zero;
            player.fallStart = (int)(player.position.Y / 16f);
        }

        timer++;

        try
        {
            switch (step)
            {
                case 0 when timer > 180:
                    camera = new Point(Main.spawnTileX, Math.Max(Main.spawnTileY - 60, 60));
                    step = 1;
                    timer = 0;
                    break;

                case 1 when timer > 60:
                    RunLogicTests();
                    step = 2;
                    timer = 0;
                    break;

                case 2 when timer > 30:
                    Expect("powder ripens a winterberry", ModContent.GetInstance<WinterberryBushTile>().IsGrown(powderHerb.X, powderHerb.Y));
                    Main.projectile[powderType].Kill();
                    ClearBox(camera.X - 30, camera.Y - 20, 60, 50);
                    BuildBed();
                    SetWind(0.05f);
                    step = 3;
                    timer = 0;
                    break;

                case 3 when timer > 90:
                    capture = "calm";
                    step = 4;
                    timer = 0;
                    break;

                case 4 when timer > 5:
                    SetWind(1.1f);
                    step = 5;
                    timer = 0;
                    break;

                case 5 when timer > 120:
                    SetWind(1.1f);
                    capture = "windy_a";
                    step = 6;
                    timer = 0;
                    break;

                case 6 when timer > 22:
                    SetWind(1.1f);
                    capture = "windy_b";
                    step = 7;
                    timer = 0;
                    break;

                case 7 when timer > 5:
                    Finish("done");
                    break;
            }

            if (step is 5 or 6)
                SetWind(1.1f);
        }
        catch (Exception exception)
        {
            Finish("exception: " + exception);
        }
    }

    private static void SetWind(float wind)
    {
        Main.windSpeedCurrent = wind;
        Main.windSpeedTarget = wind;
    }

    private static void Expect(string name, bool ok, string detail = "")
    {
        if (!ok)
            failures++;

        log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  [" + detail + "]" : "")}");
    }

    private static void ClearBox(int x, int y, int width, int height)
    {
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Tile tile = Main.tile[i, j];
                tile.ClearEverything();
            }
        }
    }

    /// <summary> Stone at <paramref name="y"/>+1 with <paramref name="ground"/> at <paramref name="y"/>, placed the way the game would. </summary>
    private static void Ground(int x, int y, int ground)
    {
        Tile stone = Main.tile[x, y + 1];
        stone.HasTile = true;
        stone.TileType = TileID.Stone;

        if (ground is TileID.PlanterBox or TileID.ClayPot || SeedBedItem.TileTypes.Contains(ground))
        {
            WorldGen.PlaceTile(x, y, ground, mute: true, forced: true);
        }
        else
        {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = (ushort)ground;
        }
    }

    private static int Dropped(int type, Point at)
    {
        int count = 0;

        foreach (Item item in Main.ActiveItems)
        {
            if (item.type == type && Vector2.Distance(item.Center, at.ToWorldCoordinates()) < 200f)
            {
                count += item.stack;
                item.active = false;
            }
        }

        return count;
    }

    private static void RunLogicTests()
    {
        var bushHerb = ModContent.GetInstance<WinterberryBushTile>();
        var magnoliaHerb = ModContent.GetInstance<MagnoliaTile>();
        int bush = bushHerb.Type;
        int magnolia = magnoliaHerb.Type;
        int berry = ModContent.ItemType<WinterberryItem>();
        int bed = SeedBedItem.TileTypes[0];
        int y = camera.Y;
        int x = camera.X - 25;

        ClearBox(camera.X - 30, y - 20, 60, 40);

        // Anchors
        (string, int, int, bool)[] anchors =
        [
            ("bush on highlands grass", bush, ModContent.TileType<HighlandsGrass>(), true),
            ("bush on snow grass", bush, ModContent.TileType<SnowHighlandsGrass>(), true),
            ("bush on podzol", bush, ModContent.TileType<PodzolGrass>(), true),
            ("bush on seed bed", bush, bed, true),
            ("bush on planter box", bush, TileID.PlanterBox, true),
            ("bush on clay pot", bush, TileID.ClayPot, true),
            ("bush refuses stone", bush, TileID.Stone, false),
            ("bush refuses vanilla grass", bush, TileID.Grass, false),
            ("magnolia on grass", magnolia, TileID.Grass, true),
            ("magnolia on seed bed", magnolia, bed, true),
            ("magnolia on planter box", magnolia, TileID.PlanterBox, true),
            ("magnolia on clay pot", magnolia, TileID.ClayPot, true),
            ("magnolia refuses stone", magnolia, TileID.Stone, false),
        ];

        foreach ((string name, int plant, int ground, bool allowed) in anchors)
        {
            Ground(x, y, ground);
            bool placed = WorldGen.PlaceTile(x, y - 1, plant, mute: true) && Main.tile[x, y - 1].HasTile && Main.tile[x, y - 1].TileType == plant;
            Expect(name, placed == allowed, $"ground on? {Main.tile[x, y].HasTile}/{Main.tile[x, y].TileType}");

            if (placed)
            {
                WorldGen.SquareTileFrame(x, y - 1);
                Expect(name + " survives framing", Main.tile[x, y - 1].HasTile && Main.tile[x, y - 1].TileType == plant);
            }

            x += 2;
        }

        ClearBox(camera.X - 30, y - 20, 60, 40);

        // Growth: magnolia on grass advances every random update and stops when grown.
        x = camera.X - 25;
        Ground(x, y, TileID.Grass);
        WorldGen.PlaceTile(x, y - 1, magnolia, mute: true);
        Expect("magnolia starts at stage 0", magnoliaHerb.GetStage(x, y - 1) == 0);
        TileLoader.RandomUpdate(x, y - 1, magnolia);
        Expect("magnolia grows on a random update", magnoliaHerb.GetStage(x, y - 1) == 1);
        Expect("magnolia frame matches old layout", Main.tile[x, y - 1].TileFrameX == 18);
        Expect("magnolia not spelunkable until grown", !magnoliaHerb.IsTileSpelunkable(x, y - 1));
        TileLoader.RandomUpdate(x, y - 1, magnolia);
        TileLoader.RandomUpdate(x, y - 1, magnolia);
        Expect("magnolia stops at grown", magnoliaHerb.GetStage(x, y - 1) == 2 && Main.tile[x, y - 1].TileFrameX == 36);
        Expect("magnolia spelunkable when grown", magnoliaHerb.IsTileSpelunkable(x, y - 1));

        // Winterberry rates: free ground vs seed bed.
        x += 3;
        Ground(x, y, ModContent.TileType<HighlandsGrass>());
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        double openRate = UpdatesPerStage(bushHerb, x, y - 1);
        x += 3;
        Ground(x, y, bed);
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        double bedRate = UpdatesPerStage(bushHerb, x, y - 1);
        Expect("bush on open ground ~8 updates a stage", openRate is > 6.5 and < 9.5, $"{openRate:F2}");
        Expect("bush on seed bed ~3.03 updates a stage (3 rolls at 1/8)", bedRate is > 2.8 and < 3.3, $"{bedRate:F2}");

        // Magnolia keeps its full rate on a seed bed rather than dropping to the bed's.
        x += 3;
        Ground(x, y, bed);
        WorldGen.PlaceTile(x, y - 1, magnolia, mute: true);
        TileLoader.RandomUpdate(x, y - 1, magnolia);
        Expect("magnolia no slower on a seed bed", magnoliaHerb.GetStage(x, y - 1) == 1);

        // TryGrowHerb, off and on a seed bed.
        Main.tile[x, y - 1].TileFrameX = 0;
        int tries = 0;
        while (!FarmingSystem.TryGrowHerb(x, y - 1) && tries < 1000)
            tries++;
        Expect("TryGrowHerb advances a custom herb on a bed", magnoliaHerb.GetStage(x, y - 1) == 1, $"{tries} failed rolls");

        x += 3;
        Ground(x, y, TileID.Grass);
        WorldGen.PlaceTile(x, y - 1, magnolia, mute: true);
        tries = 0;
        for (int k = 0; k < 500; k++)
            FarmingSystem.TryGrowHerb(x, y - 1);
        Expect("TryGrowHerb ignores herbs off a bed", magnoliaHerb.GetStage(x, y - 1) == 0);

        // Drops: real tile kills.
        Main.LocalPlayer.inventory[Main.LocalPlayer.selectedItem].SetDefaults(ItemID.None);
        x += 3;

        foreach ((int stage, int herbs, int seeds) in new[] { (0, 0, 0), (1, 1, 1), (2, 1, 2) })
        {
            Ground(x, y, TileID.Grass);
            WorldGen.PlaceTile(x, y - 1, magnolia, mute: true, style: stage);
            Point at = new(x, y - 1);
            Expect($"magnolia placed at stage {stage}", magnoliaHerb.GetStage(x, y - 1) == stage);
            WorldGen.KillTile(x, y - 1);
            Expect($"magnolia gone after kill at stage {stage}", !Main.tile[x, y - 1].HasTile);
            int gotHerbs = Dropped(ModContent.ItemType<MagnoliaItem>(), at);
            int gotSeeds = Dropped(ModContent.ItemType<MagnoliaSeedsItem>(), at);
            Expect($"magnolia stage {stage} drops", gotHerbs == herbs && gotSeeds == seeds, $"{gotHerbs} herbs, {gotSeeds} seeds");
        }

        foreach ((int stage, int min, int max) in new[] { (0, 0, 0), (1, 0, 0), (2, 2, 3) })
        {
            Ground(x, y, ModContent.TileType<HighlandsGrass>());
            WorldGen.PlaceTile(x, y - 1, bush, mute: true, style: stage);
            Point at = new(x, y - 1);
            WorldGen.KillTile(x, y - 1);
            int got = Dropped(berry, at);
            Expect($"bush stage {stage} drops", got >= min && got <= max, $"{got} berries");
        }

        Main.LocalPlayer.inventory[Main.LocalPlayer.selectedItem].SetDefaults(ItemID.StaffofRegrowth);
        WorldGen.PlaceTile(x, y - 1, bush, mute: true, style: 2);
        Point regrown = new(x, y - 1);
        WorldGen.KillTile(x, y - 1);
        int withStaff = Dropped(berry, regrown);
        Expect("bush with regrowth staff drops more", withStaff is >= 3 and <= 5, $"{withStaff} berries");
        WorldGen.PlaceTile(x, y - 1, magnolia, mute: true, style: 0);
        Main.tile[x, y].TileType = TileID.Grass;
        WorldGen.PlaceTile(x, y - 1, magnolia, mute: true, style: 2);
        Point regrownM = new(x, y - 1);
        WorldGen.KillTile(x, y - 1);
        int staffHerbs = Dropped(ModContent.ItemType<MagnoliaItem>(), regrownM);
        int staffSeeds = Dropped(ModContent.ItemType<MagnoliaSeedsItem>(), regrownM);
        Expect("magnolia with regrowth staff", staffHerbs is >= 1 and <= 2 && staffSeeds is >= 3 and <= 4, $"{staffHerbs} herbs, {staffSeeds} seeds");
        Main.LocalPlayer.inventory[Main.LocalPlayer.selectedItem].SetDefaults(ItemID.None);

        // Removing the seed bed takes the plant with it.
        x += 3;
        Ground(x, y, bed);
        WorldGen.PlaceTile(x, y - 1, bush, mute: true, style: 2);
        WorldGen.KillTile(x, y);
        Expect("plant breaks with its seed bed", !Main.tile[x, y - 1].HasTile, $"tile type now {Main.tile[x, y - 1].TileType}");
        Dropped(berry, new Point(x, y - 1));

        // Replanting rules.
        x += 3;
        Ground(x, y, ModContent.TileType<HighlandsGrass>());
        WorldGen.PlaceTile(x, y - 1, bush, mute: true, style: 1);
        Expect("can't replant over a growing bush", !bushHerb.CanPlace(x, y - 1));
        Expect("other herb can't take a growing bush's spot", !magnoliaHerb.CanPlace(x, y - 1));
        Expect("growing bush survives those checks", Main.tile[x, y - 1].HasTile);
        Main.tile[x, y - 1].TileFrameX = (short)(2 * 34);
        Expect("can replant over a grown bush", bushHerb.CanPlace(x, y - 1));
        Expect("other herb can take a grown bush's spot", magnoliaHerb.CanPlace(x, y - 1));
        Expect("which clears it", !Main.tile[x, y - 1].HasTile);
        Dropped(berry, new Point(x, y - 1));

        // Plain foliage makes way.
        x += 3;
        Ground(x, y, TileID.Grass);
        WorldGen.PlaceTile(x, y - 1, TileID.Plants, mute: true);
        Expect("grass plant placed for replace test", Main.tile[x, y - 1].HasTile);
        Expect("foliage is replaced", magnoliaHerb.CanPlace(x, y - 1) && !Main.tile[x, y - 1].HasTile);

        // Old saved frames from when the sheet's 12 frames were styles stay valid.
        x += 3;
        Ground(x, y, ModContent.TileType<HighlandsGrass>());
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        Main.tile[x, y - 1].TileFrameX = 11 * 34;
        Expect("old frame clamps to grown", bushHerb.GetStage(x, y - 1) == 2 && !bushHerb.TryAdvance(x, y - 1));
        WorldGen.SquareTileFrame(x, y - 1);
        Expect("old frame survives framing", Main.tile[x, y - 1].HasTile);

        // Job and act hookups.
        x += 3;
        Ground(x, y, bed);
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        GameEvent onBed = GameEvent.TilePlace(x, y - 1, bush, Main.LocalPlayer);
        Expect("job counts a custom herb on a bed", new PlantInSeedBedTask("t", 1).Matches(onBed));
        Expect("favoured-soil job doesn't", !new PlantInSeedBedTask("t", 1, favouredSoilOnly: true).Matches(onBed));
        x += 3;
        Ground(x, y, ModContent.TileType<HighlandsGrass>());
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        Expect("job ignores a herb off a bed", !new PlantInSeedBedTask("t", 1).Matches(GameEvent.TilePlace(x, y - 1, bush, Main.LocalPlayer)));
        Expect("job ignores other tiles", !new PlantInSeedBedTask("t", 1).Matches(GameEvent.TilePlace(x, y, TileID.Stone, Main.LocalPlayer)));
        Expect("planting act has both herbs", ActSets.HerbTiles.Contains(bush) && ActSets.HerbTiles.Contains(magnolia), string.Join(",", ActSets.HerbTiles));

        // Assets and registration.
        Expect("bush uses its real sprite", bushHerb.Texture.EndsWith("WinterberryBush"), bushHerb.Texture);
        Expect("berry item plants the bush", new Item(berry).createTile == bush);
        log.AppendLine("placeholder sprites: " + string.Join(", ", PlaceholderTextures.Missing.Where(e => e.Name.Contains("Winterberry") || e.Name.Contains("Magnolia")).Select(e => e.Name)));
        log.AppendLine($"map entry: {Lang._mapLegendCache?.ToString() ?? ""}");
        log.AppendLine($"DisplayName: {new Item(berry).Name}");

        ClearBox(camera.X - 30, y - 20, 60, 40);

        // Magnolia Powder, through its real projectile.
        x = camera.X;
        Ground(x, y, ModContent.TileType<HighlandsGrass>());
        WorldGen.PlaceTile(x, y - 1, bush, mute: true);
        powderHerb = new Point(x, y - 1);
        powderType = Projectile.NewProjectile(new EntitySource_DebugCommand("herb test"), powderHerb.ToWorldCoordinates(), Vector2.Zero, ModContent.ProjectileType<MagnoliaPowderProjectile>(), 0, 0f, Main.myPlayer);
    }

    private static double UpdatesPerStage(CustomHerb herb, int i, int j)
    {
        const int Trials = 3000;
        long updates = 0;

        for (int trial = 0; trial < Trials; trial++)
        {
            Main.tile[i, j].TileFrameX = 0;

            while (herb.GetStage(i, j) == 0)
            {
                TileLoader.RandomUpdate(i, j, herb.Type);
                updates++;
            }
        }

        Main.tile[i, j].TileFrameX = 0;
        return updates / (double)Trials;
    }

    /// <summary> Three rows of bushes by stage across every ground, and a row of magnolia by stage. </summary>
    private static void BuildBed()
    {
        int bush = ModContent.TileType<WinterberryBushTile>();
        int magnolia = ModContent.TileType<MagnoliaTile>();
        int[] grounds = [ModContent.TileType<HighlandsGrass>(), ModContent.TileType<SnowHighlandsGrass>(), ModContent.TileType<PodzolGrass>(), SeedBedItem.TileTypes[0], TileID.PlanterBox, TileID.ClayPot];

        for (int stage = 0; stage < 3; stage++)
        {
            int y = camera.Y - 12 + stage * 7;

            for (int g = 0; g < grounds.Length; g++)
            {
                for (int shape = 0; shape < 2; shape++)
                {
                    int x = camera.X - 24 + g * 8 + shape * 4 + shape * stage;
                    Ground(x, y, grounds[g]);
                    bool placed = WorldGen.PlaceTile(x, y - 1, bush, mute: true, style: stage);

                    if (!placed)
                        log.AppendLine($"bed: failed bush stage {stage} ground {g} at {x},{y - 1}");
                }
            }
        }

        int[] magnoliaGrounds = [TileID.Grass, SeedBedItem.TileTypes[0], TileID.PlanterBox, TileID.ClayPot];

        for (int stage = 0; stage < 3; stage++)
        {
            for (int g = 0; g < magnoliaGrounds.Length; g++)
            {
                int x = camera.X - 24 + (stage * 4 + g) * 3;
                int y = camera.Y + 9;
                Ground(x, y, magnoliaGrounds[g]);

                if (!WorldGen.PlaceTile(x, y - 1, magnolia, mute: true, style: stage))
                    log.AppendLine($"bed: failed magnolia stage {stage} ground {g}");
            }
        }

        // A row of seed beds fully planted, to see neighbours together.
        int rowY = camera.Y + 16;
        for (int k = 0; k < 18; k++)
        {
            int x = camera.X - 24 + k;
            Ground(x, rowY, SeedBedItem.TileTypes[0]);
            WorldGen.PlaceTile(x, rowY - 1, bush, mute: true, style: k % 3);
        }

        for (int x = camera.X - 24; x < camera.X - 6; x++)
            WorldGen.SquareTileFrame(x, rowY);

        camera = new Point(camera.X, camera.Y + 1);
        Lighting.Clear();
    }

    private static void Capture(GameTime gameTime)
    {
        if (capture is null || output is null)
            return;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        device.GetBackBufferData(pixels);
        PngWriter.Write(Path.Combine(output, $"herb_{capture}.png"), width, height, (x, y) => pixels[y * width + x]);
        log.AppendLine($"captured {capture}, wind {Main.WindForVisuals}, sway enabled {Main.SettingsEnabled_TilesSwayInWind}");
        capture = null;
    }

    private static void Finish(string reason)
    {
        log.AppendLine(reason);
        log.AppendLine($"failures: {failures}");
        File.WriteAllText(Path.Combine(output!, "herb_report.txt"), log.ToString());
        Process.GetCurrentProcess().Kill();
    }
}
