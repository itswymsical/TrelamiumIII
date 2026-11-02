using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SubworldLibrary;
using Terraria.IO;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Core.Graphics;
using static TrelamiumIII.Content.DevTools.IngressScan;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Dev tool. Drives a real game client through the Ingress realm and saves screenshots of what it draws: the sky, haze,
///     mist, weather and tiles as a player sees them. Runs only when <see cref="OutputVariable" /> names an output folder;
///     tools/IngressHarness/Run-ClientHarness.ps1 launches the client with <c>-skipselect</c> into a throwaway world, this
///     enters Ingress, visits its landmarks at several times of day and in the rain, and exits the game without saving.
/// </summary>
public sealed class IngressClientHarness : ModSystem
{
    private const string OutputVariable = "TRELAMIUM_CLIENT_HARNESS";
    private const string PlayerName = "Harness";

    private const int EnterDelay = 90;
    private const int SettleTicks = 240;
    private const int HoldTicks = 300;
    private const int GiveUpTicks = 60 * 60 * 4;

    private const double Noon = 27000;
    private const double Dawn = 2400;
    private const double Dusk = 50000;
    private const double Midnight = 16200;

    private const float LightRain = 0.4f;
    private const float HeavyRain = 0.9f;

    /// <summary> Rows from the ground to centre a living tree's three shots on: its crown, its trunk and its foot, overlapping. </summary>
    private const int TreeCrownRows = -75;

    private const int TreeTrunkRows = -30;
    private const int TreeFootRows = 18;

    /// <summary> Narrower runs of leaves than this are a stray bough, not a whole tree. </summary>
    private const int TreeMinWidth = 20;

    private const int FloodlightSpacing = 8;

    private static string? output;
    private static readonly Queue<Shot> shots = new();
    private static readonly StringBuilder log = new();
    private static Stage stage;
    private static Shot? current;
    private static int timer;
    private static int totalTicks;
    private static bool captureRequested;

    private enum Stage
    {
        MainWorld,
        Settling,
        Shooting,
        Done
    }

    /// <param name="Tile"> Where the camera is held: the player stands with its feet on this tile's top. </param>
    /// <param name="Floodlit"> Lights the area around the camera, to show the form of what's underground. </param>
    private sealed record Shot(string Name, Point Tile, bool Day, double Time, float Rain, bool Floodlit = false);

    public override void PostSetupContent()
    {
        if (Main.dedServ || Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } path)
            return;

        output = path;
        Directory.CreateDirectory(path);

        // A failure here would otherwise stop at tModLoader's error screen, waiting for someone to click through it.
        try
        {
            EnsurePlayer();
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(path, "client_error.txt"), exception.ToString());
            Environment.Exit(1);
        }

        Main.OnPostDraw += Capture;
    }

    public override void Unload() => Main.OnPostDraw -= Capture;

    /// <summary> <c>-skipselect</c> picks a player once mods have loaded, so one is made here if the throwaway save folder has none. </summary>
    private static void EnsurePlayer()
    {
        Directory.CreateDirectory(Main.PlayerPath);

        if (Directory.GetFiles(Main.PlayerPath, "*.plr").Length > 0)
            return;

        PlayerFileData.CreateAndSave(new Player { name = PlayerName, difficulty = PlayerDifficultyID.SoftCore });
    }

    public override void PostUpdateEverything()
    {
        if (output is null || Main.gameMenu || stage == Stage.Done)
            return;

        if (++totalTicks > GiveUpTicks)
            Finish("gave up: ran out of time");

        Player player = Main.LocalPlayer;
        player.immuneTime = Math.Max(player.immuneTime, 2);
        player.immune = true;
        player.statLife = player.statLifeMax2;
        player.breath = player.breathMax;
        Main.hideUI = true;

        switch (stage)
        {
            case Stage.MainWorld:
                if (!SubworldSystem.AnyActive() && ++timer > EnterDelay)
                {
                    timer = 0;
                    log.AppendLine("entering Ingress");
                    SubworldSystem.Enter<IngressRealm>();
                }
                else if (SubworldSystem.IsActive<IngressRealm>())
                {
                    stage = Stage.Settling;
                    timer = 0;
                }

                break;

            case Stage.Settling:
                if (++timer < SettleTicks)
                    break;

                PlanShots();
                stage = Stage.Shooting;
                timer = 0;
                break;

            case Stage.Shooting:
                if (current is null)
                {
                    if (!shots.TryDequeue(out current))
                    {
                        Finish("done");
                        return;
                    }

                    timer = 0;
                    Main.dayTime = current.Day;
                    Main.time = current.Time;
                    SetRain(current.Rain);
                }

                Hold(player, current.Tile);

                if (current.Floodlit)
                    Floodlight(current.Tile);

                if (++timer == HoldTicks)
                    captureRequested = true;

                break;
        }
    }

    /// <summary> Spawn at four times of day and in a morning rain, then each landmark the scan finds, at noon. </summary>
    private static void PlanShots()
    {
        int[] ground = Ground();
        var spawn = new Point(Main.spawnTileX, Main.spawnTileY);

        shots.Enqueue(new Shot("spawn_noon", spawn, true, Noon, 0f));
        shots.Enqueue(new Shot("spawn_dawn", spawn, true, Dawn, 0f));
        shots.Enqueue(new Shot("spawn_dusk", spawn, true, Dusk, 0f));
        shots.Enqueue(new Shot("spawn_night", spawn, false, Midnight, 0f));
        shots.Enqueue(new Shot("spawn_rain_morning", spawn, true, Dawn + 1800, HeavyRain));
        shots.Enqueue(new Shot("spawn_rain_noon", spawn, true, Noon, LightRain));

        int peak = Peak(ground);
        shots.Enqueue(new Shot("peak_noon", new Point(peak, ground[peak]), true, Noon, 0f));
        shots.Enqueue(new Shot("peak_rain", new Point(peak, ground[peak]), true, Noon, HeavyRain));

        if (CanyonSpans() is [var canyon, ..])
        {
            int x = (canyon.Left + canyon.Right) / 2;
            shots.Enqueue(new Shot("canyon", new Point(x, Min(ground, canyon.Left - 30, canyon.Right + 30) + 20), true, Noon, 0f));
        }

        if (BlightSpans(ground, 40) is [var blight, ..])
        {
            int x = (blight.Left + blight.Right) / 2;
            shots.Enqueue(new Shot("blight", new Point(x, ground[x]), true, Noon, 0f));
        }

        PlanTreeShots<SpruceLeafTile>("spruce", ground);
        PlanTreeShots<BirchLeafTile>("birch", ground);

        if (TaigaSpans(ground, 100) is [var taiga, ..])
        {
            int x = (taiga.Left + taiga.Right) / 2;
            shots.Enqueue(new Shot("taiga_noon", new Point(x, ground[x]), true, Noon, 0f));
            shots.Enqueue(new Shot("taiga_dawn", new Point(x, ground[x]), true, Dawn, 0f));
        }

        // Standing in the middle of its footprint, to show it drawn over the player.
        if (FirstForegroundBush() is { } bush)
            shots.Enqueue(new Shot("foreground_bush", new Point(bush.X + ForegroundBushTile.Width / 2, bush.Y + ForegroundBushTile.Height), true, Noon, 0f));

        if (WaterSpans(ground, 120) is [var river, ..])
            shots.Enqueue(new Shot("river", new Point(river.Left + 30, ground[river.Left - 4]), true, Noon, 0f));

        foreach (Shot shot in shots)
            log.AppendLine($"planned {shot.Name} at ({shot.Tile.X}, {shot.Tile.Y})");

        LogSurfaceTiles(ground);
    }

    /// <summary> The first whole living tree of a kind, framed from the middle of its canopy: crown, trunk, then its foot and root room lit up. </summary>
    private static void PlanTreeShots<TLeaf>(string kind, int[] ground) where TLeaf : ModTile
    {
        foreach ((int left, int right) in LeafSpans())
        {
            int x = (left + right) / 2;

            if (right - left < TreeMinWidth || !HasLeaf(x, ModContent.TileType<TLeaf>()))
                continue;

            // Leaves read as ground, so its own column can't say where the ground is; the columns just past the canopy can.
            int floor = (ground[left - 3] + ground[right + 3]) / 2;

            shots.Enqueue(new Shot($"tree_{kind}_crown", new Point(x, floor + TreeCrownRows), true, Noon, 0f));
            shots.Enqueue(new Shot($"tree_{kind}_trunk", new Point(x, floor + TreeTrunkRows), true, Noon, 0f));
            shots.Enqueue(new Shot($"tree_{kind}_foot", new Point(x, floor + TreeFootRows), true, Noon, 0f, Floodlit: true));
            return;
        }
    }

    private static bool HasLeaf(int x, int type)
    {
        for (int y = 10; y < Main.worldSurface; y++)
        {
            if (Main.tile[x, y] is { HasTile: true } tile && tile.TileType == type)
                return true;
        }

        return false;
    }

    /// <summary> What stands on the surface and what's still drawn with a placeholder sprite, to put names to anything odd in the shots. </summary>
    private static void LogSurfaceTiles(int[] ground)
    {
        var counts = new SortedDictionary<string, int>();

        for (int x = 10; x < Main.maxTilesX - 10; x++)
        {
            Tile above = Main.tile[x, ground[x] - 1];

            if (!above.HasTile)
                continue;

            string name = TileLoader.GetTile(above.TileType)?.Name ?? $"vanilla {above.TileType}";
            counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        foreach ((string name, int count) in counts)
            log.AppendLine($"surface {name}: {count}");

        foreach (PlaceholderTextures.Entry entry in PlaceholderTextures.Missing)
        {
            if (entry.Kind == "Tile")
                log.AppendLine($"placeholder tile {entry.Name}");
        }
    }

    /// <summary> Pins the player standing on the tile and keeps the camera on it. </summary>
    private static void Hold(Player player, Point tile)
    {
        player.position = new Vector2(tile.X * 16 + 8 - player.width / 2f, tile.Y * 16 - player.height);
        player.velocity = Vector2.Zero;
        player.fallStart = (int)(player.position.Y / 16f);
    }

    private static void Floodlight(Point center)
    {
        for (int x = center.X - Main.screenWidth / 32; x <= center.X + Main.screenWidth / 32; x += FloodlightSpacing)
        {
            for (int y = center.Y - Main.screenHeight / 32; y <= center.Y + Main.screenHeight / 32; y += FloodlightSpacing)
                Lighting.AddLight(x, y, 1f, 1f, 1f);
        }
    }

    private static void SetRain(float strength)
    {
        Main.raining = strength > 0f;
        Main.maxRaining = strength;
        Main.cloudAlpha = strength;
        Main.rainTime = strength > 0f ? 18000 : 0;
    }

    /// <summary> After the frame is drawn and before it is presented: the backbuffer holds exactly what the player sees. </summary>
    private static void Capture(GameTime gameTime)
    {
        if (!captureRequested || output is null || current is null)
            return;

        captureRequested = false;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        device.GetBackBufferData(pixels);

        string path = Path.Combine(output, $"client_{current.Name}.png");
        PngWriter.Write(path, width, height, (x, y) => pixels[y * width + x]);
        log.AppendLine($"captured {current.Name} ({width}x{height}) at ({current.Tile.X}, {current.Tile.Y}), time {current.Time}, rain {current.Rain}");

        current = null;
    }

    private static void Finish(string reason)
    {
        stage = Stage.Done;
        log.AppendLine(reason);

        if (output is not null)
            File.WriteAllText(Path.Combine(output, "client_report.txt"), log.ToString());

        // Straight out: nothing in this throwaway world is worth saving, and Environment.Exit hangs from inside the game loop.
        Process.GetCurrentProcess().Kill();
    }
}
