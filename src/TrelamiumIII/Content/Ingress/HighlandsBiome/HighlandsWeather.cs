using System.Collections.Generic;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Rules;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// Gusts over the peaks, shared by the frost they blow onto the tiles, the sleet and the blowing ice. A function of world
/// time rather than of any random roll, so every machine agrees on when the frost comes in.
/// </summary>
internal static class AlpineWeather
{
    private const double GustFrequency = 0.0021;
    private const double GustBeat = 0.00077;
    private const float GustLow = 0.45f;
    private const float GustHigh = 0.85f;
    private const float RainGust = 0.35f;

    /// <summary> Rows above the treeline at which the alpine weather is at its strongest. </summary>
    private const float FullAltitudeRows = 70f;

    /// <summary> 0 to 1: how hard it is gusting, in bursts a minute or two apart. Rain makes it blow harder and more often. </summary>
    public static float Gust
    {
        get
        {
            double ticks = Main.dayTime ? Main.time : Main.time + Main.dayLength;
            float wave = 0.5f + 0.5f * MathF.Sin((float)(ticks * GustFrequency)) * MathF.Cos((float)(ticks * GustBeat) + 1.3f);
            float rain = Main.raining ? Main.maxRaining : 0f;
            float t = Math.Clamp((wave + rain * RainGust - GustLow) / (GustHigh - GustLow), 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }

    /// <summary> 0 at the treeline and below, rising to 1 <see cref="FullAltitudeRows"/> above it. </summary>
    public static float Altitude(float tileY) => Math.Clamp((IngressRealm.AlpineRow - tileY) / FullAltitudeRows, 0f, 1f);
}

/// <summary> Gusts blow ice onto the sky-facing tops of the peaks, from the most exposed tiles outward, and it melts off between them. </summary>
public sealed class AlpineFrostRule : TileConditionRule
{
    /// <summary> How long a frosted tile keeps its ice once the gust that laid it has passed. </summary>
    private const float LingerTicks = 10f * 60f;

    public override int SweepTicks => 25;

    public override bool IsActive(Player player) => player.InModBiome<HighlandBiome>() && AlpineWeather.Gust > 0f;

    public override void Visit(int i, int j, Tile tile)
    {
        float cover = AlpineWeather.Gust * AlpineWeather.Altitude(j);

        if (cover <= 0f || Hash(i, j, salt: 11) >= cover || HighlandLeafTile.IsLeaf(tile) || !IsTopSurface(i, j) || !IsSkyExposed(i, j))
            return;

        Maintain<FrostCondition>(i, j, MathHelper.Lerp(0.5f, 1f, cover), LingerTicks);
    }
}

/// <summary>
/// Spawns the highlands' weather around the local player. Mist rises off exposed ground everywhere in the highlands, as
/// vanilla's graveyard fog does, into <see cref="HighlandsMistField"/>: thicker at dawn, dusk and night and in the rain, and
/// now and then a string of it blows off a ledge. Up on the peaks, sleet falls and freezes what it hits, and gusts carry ice
/// crystals and freezing mist past.
/// </summary>
public sealed class HighlandsWeather : ModSystem
{
    // Vanilla's graveyard works the same area and gives each floor tile one chance in 120 a tick at full fog.
    private const int MistAreaWidth = 120;
    private const int MistAreaHeight = 40;
    private const float MistChancePerDensity = 1f / 1200f;

    /// <summary> Nodes in a bank of mist, and the columns between them: close enough that they merge into one body along the ground. </summary>
    private const int BankNodesMin = 4;

    private const int BankNodesMax = 7;
    private const int BankSpacingMin = 2;
    private const int BankSpacingMax = 3;

    /// <summary> Rows a bank will follow the ground up or down between one node and the next; past that it stops at the drop. </summary>
    private const int BankFollowRows = 3;

    private const float MistRadiusMin = 30f;
    private const float MistRadiusMax = 56f;
    private const int MistBelowSurfaceRows = 5;

    /// <summary> Rows over a floor that have to be clear for mist to rise off it, which leaves out caves and hollow trunks. </summary>
    private const int MistOpenRows = 12;

    private const float RainMistBoost = 1f;
    private const int MistLifeMin = 420;
    private const int MistLifeMax = 600;

    private const int DriftInterval = 30;
    private const float DriftStringsPerDensity = 4f;
    private const int DriftPuffsMin = 3;
    private const int DriftPuffsMax = 8;

    /// <summary> Gap between puffs in a string, as a fraction of a puff's radius, so they merge into one streak. </summary>
    private const float DriftPuffSpacing = 0.8f;

    private const float DriftSpeed = 1.4f;
    private const float DriftCalmWind = 0.25f;
    private const float DriftRadiusMin = 20f;
    private const float DriftRadiusMax = 34f;
    private const int DriftLifeMin = 160;
    private const int DriftLifeMax = 260;

    private const float SleetBase = 0.3f;
    private const float SleetGust = 2.5f;
    private const float SleetRain = 2f;
    private const float SleetWind = 4f;
    private const float SleetFallMin = 7f;
    private const float SleetFallMax = 10f;
    private const int SleetLife = 150;
    private const float SleetSpawnMargin = 200f;

    /// <summary> Tiles above a sleet spawn that have to be open, so none falls inside a mountain. </summary>
    private const int SleetSkyRows = 25;

    private const float MoteGust = 0.3f;
    private const float MoteRate = 1.2f;
    private const float MoteWind = 7f;

    private const float IceMistGust = 0.5f;
    private const int IceMistInterval = 12;
    private const int IceMistTries = 6;

    /// <summary> Weather particles allowed per particle of the shared budget the graphics preset sets. </summary>
    private const int BudgetShare = 2;

    /// <summary>
    /// The weather's own particles, ticked and drawn here: sleet is numerous and constant, and in the shared list it would
    /// push out every other effect.
    /// </summary>
    private static readonly List<Particle> particles = [];

    private int ticks;

    /// <summary> This tick's mist colour, so <see cref="HighlandsMistField"/> doesn't work out the palette each time it draws. </summary>
    internal static Color MistTint { get; private set; } = Color.White;

    /// <summary> Speed the ground mist drifts at, in pixels a tick: vanilla's floor-cloud drift. </summary>
    internal static float MistWind => 0.4f * Main.WindForVisuals;

    private static int Capacity => ModContent.GetInstance<VFXConfig>().ParticleCount * BudgetShare;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);
            DrawParticles();
        };
    }

    public override void Unload() => particles.Clear();

    public override void OnWorldUnload() => particles.Clear();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gamePaused)
            return;

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            particles[i].Tick();

            if (!particles[i].Active)
                particles.RemoveAt(i);
        }

        float presence = HighlandsAtmosphere.Presence;

        if (presence <= 0f)
            return;

        ticks++;

        Player player = Main.LocalPlayer;
        HighlandsPalette palette = HighlandsPalette.Now;
        float rain = Main.raining ? Main.maxRaining : 0f;

        MistTint = palette.Mist;
        SpawnMist(player, palette.MistDensity * (1f + rain * RainMistBoost) * presence);

        float altitude = AlpineWeather.Altitude(player.Center.Y / 16f);

        if (altitude > 0f)
            SpawnAlpine(altitude * presence, AlpineWeather.Gust, rain);
    }

    private void SpawnMist(Player player, float density)
    {
        Point center = player.Center.ToTileCoordinates();
        int left = Math.Max(center.X - MistAreaWidth / 2, 10);
        int right = Math.Min(center.X + MistAreaWidth / 2, Main.maxTilesX - 10);
        int top = Math.Max(center.Y - MistAreaHeight / 2, 10);
        int bottom = Math.Min(center.Y + MistAreaHeight / 2, (int)Main.worldSurface + MistBelowSurfaceRows);
        float chance = density * MistChancePerDensity;

        for (int x = left; x < right; x++)
        {
            for (int y = top; y < bottom; y++)
            {
                // The roll comes first: it rejects nearly every tile before any tile is read.
                if (Main.rand.NextFloat() >= chance || !IsMistFloor(x, y))
                    continue;

                RaiseBank(x, y);
            }
        }

        if (ticks % DriftInterval != 0)
            return;

        for (int strings = Rate(density * DriftStringsPerDensity); strings > 0; strings--)
        {
            int x = Main.rand.Next(left, right);
            int y = Main.rand.Next(top, bottom);

            if (IsFloor(x, y) && IsLedge(x, y))
                BlowDrift(new Vector2(x * 16 + 8, y * 16 - 8));
        }
    }

    /// <summary> A low bank of mist centred on a floor tile, its nodes laid along the ground so they fuse into one body rather than standing as separate puffs. </summary>
    private static void RaiseBank(int x, int y)
    {
        if (FieldSystem.GetLayer<HighlandsMistField>() is not { } mist)
            return;

        int nodes = Main.rand.Next(BankNodesMin, BankNodesMax + 1);
        int column = x - nodes * (BankSpacingMin + BankSpacingMax) / 4;
        int row = y;

        for (; nodes > 0; nodes--, column += Main.rand.Next(BankSpacingMin, BankSpacingMax + 1))
        {
            if (FollowGround(column, ref row))
            {
                mist.SpawnNode(new Vector2(column * 16 + 8, row * 16 - 8 - Main.rand.NextFloat(16f)), new Vector2(MistWind, 0f),
                    radius: Main.rand.NextFloat(MistRadiusMin, MistRadiusMax), life: Main.rand.Next(MistLifeMin, MistLifeMax + 1));
            }
        }
    }

    /// <summary> Moves <paramref name="row"/> to the floor of <paramref name="column"/> nearest to it, if there is one within <see cref="BankFollowRows"/>. </summary>
    private static bool FollowGround(int column, ref int row)
    {
        for (int offset = 0; offset <= BankFollowRows; offset++)
        {
            if (IsMistFloor(column, row + offset))
            {
                row += offset;
                return true;
            }

            if (IsMistFloor(column, row - offset))
            {
                row -= offset;
                return true;
            }
        }

        return false;
    }

    /// <summary> A string of thin mist leaving a ledge along the wind, each puff a little further on, as vanilla's airborne clouds do. </summary>
    private static void BlowDrift(Vector2 position)
    {
        if (FieldSystem.GetLayer<HighlandsMistField>() is not { } mist)
            return;

        // With a floor on its speed, so a still day still carries it off.
        float wind = DriftSpeed * (Main.WindForVisuals >= 0f ? 1f : -1f) * MathF.Max(MathF.Abs(Main.WindForVisuals), DriftCalmWind);
        float heading = Main.rand.NextFloatDirection() * 0.15f;

        for (int i = Main.rand.Next(DriftPuffsMin, DriftPuffsMax + 1); i > 0; i--)
        {
            float radius = Main.rand.NextFloat(DriftRadiusMin, DriftRadiusMax);
            Vector2 velocity = new Vector2(wind, 0f).RotatedBy(heading);

            mist.SpawnNode(position + Main.rand.NextVector2Circular(4f, 4f), velocity, radius, Main.rand.Next(DriftLifeMin, DriftLifeMax + 1));

            position += velocity.SafeNormalize(Vector2.UnitX) * radius * DriftPuffSpacing;
            heading += Main.rand.NextFloatDirection() * 0.05f;
        }
    }

    private void SpawnAlpine(float altitude, float gust, float rain)
    {
        for (int n = Rate(altitude * (SleetBase + gust * SleetGust + rain * SleetRain)); n > 0; n--)
        {
            var position = new Vector2(Main.screenPosition.X + Main.rand.NextFloat(-SleetSpawnMargin, Main.screenWidth + SleetSpawnMargin), Main.screenPosition.Y - 16f);

            if (!OpenSky(position.ToTileCoordinates()))
                continue;

            var velocity = new Vector2(Main.WindForVisuals * SleetWind + Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(SleetFallMin, SleetFallMax));
            Spawn<SleetParticle>(position, velocity, SleetLife);
        }

        if (gust < MoteGust)
            return;

        float wind = Main.WindForVisuals >= 0f ? 1f : -1f;

        for (int n = Rate(altitude * gust * MoteRate); n > 0; n--)
        {
            // From the upwind edge of the screen, so the crystals cross it on the gust.
            float x = wind > 0f ? Main.screenPosition.X - 16f : Main.screenPosition.X + Main.screenWidth + 16f;
            var position = new Vector2(x, Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
            var velocity = new Vector2(wind * MoteWind * Main.rand.NextFloat(0.7f, 1.3f) * (0.5f + gust), Main.rand.NextFloat(-0.5f, 0.8f));

            Spawn<IceMoteParticle>(position, velocity, Main.rand.Next(150, 240), scale: Main.rand.NextFloat(0.5f, 1f));
        }

        if (gust < IceMistGust || ticks % IceMistInterval != 0 || FieldSystem.GetLayer<ArcticField>() is not { } mist)
            return;

        for (int tries = 0; tries < IceMistTries; tries++)
        {
            var tile = new Vector2(Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth), Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight)).ToTileCoordinates();

            if (AlpineWeather.Altitude(tile.Y) <= 0f || !IsFloor(tile.X, tile.Y))
                continue;

            mist.SpawnNode(new Vector2(tile.X * 16 + 8, tile.Y * 16 - 6), new Vector2(wind * Main.rand.NextFloat(1.5f, 3f) * gust, -Main.rand.NextFloat(0.2f, 0.6f)),
                radius: Main.rand.NextFloat(14f, 22f), life: Main.rand.NextFloat(40f, 70f));
            break;
        }
    }

    private static void Spawn<T>(Vector2 position, Vector2 velocity, int timeLeft, int variant = ParticleSystem.RANDOM_VARIANT, float scale = 1f, float rotation = 0f)
        where T : Particle, new()
    {
        if (particles.Count < Capacity && ParticleSystem.Create<T>(position, velocity, timeLeft, variant, scale, rotation) is { } particle)
            particles.Add(particle);
    }

    /// <summary> Drawn as the shared particles are: after dust, in the game's view, pixel art kept crisp. </summary>
    private static void DrawParticles()
    {
        if (particles.Count == 0 || Main.gameMenu)
            return;

        SpriteBatch spriteBatch = Main.spriteBatch;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null,
            Main.GameViewMatrix.TransformationMatrix);

        foreach (Particle particle in particles)
            particle.Draw(spriteBatch);

        spriteBatch.End();
    }

    /// <summary> Whole spawns this tick for a rate of <paramref name="rate"/> a tick, the fraction rolled. </summary>
    private static int Rate(float rate) => (int)rate + (Main.rand.NextFloat() < rate - (int)rate ? 1 : 0);

    /// <summary> Flat solid ground with dry, open air over it, as vanilla's graveyard asks of a tile before fog rises off it. </summary>
    private static bool IsFloor(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 10))
            return false;

        Tile tile = Main.tile[x, y];
        return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && tile.Slope == SlopeType.Solid && !tile.IsHalfBlock
            && !WorldGen.SolidTile(x, y - 1) && Main.tile[x, y - 1].LiquidAmount == 0;
    }

    /// <summary> A floor out in the open that isn't a bough: it gathers on the ground under the trees, not in caves or up in the crowns. </summary>
    private static bool IsMistFloor(int x, int y)
    {
        if (!IsFloor(x, y) || HighlandLeafTile.IsLeaf(Main.tile[x, y]))
            return false;

        for (int row = y - 1; row >= Math.Max(y - MistOpenRows, 10); row--)
        {
            Tile tile = Main.tile[x, row];

            if (tile.WallType != WallID.None || (WorldGen.SolidTile(x, row) && !HighlandLeafTile.IsLeaf(tile)))
                return false;
        }

        return true;
    }

    /// <summary> Ground whose downwind neighbour drops away, so mist leaving it blows out over the drop. </summary>
    private static bool IsLedge(int x, int y)
    {
        int downwind = x + (Main.WindForVisuals >= 0f ? 1 : -1);
        return WorldGen.InWorld(downwind, y + 2, 10) && !WorldGen.SolidTile(downwind, y) && !WorldGen.SolidTile(downwind, y + 1) && !WorldGen.SolidTile(downwind, y + 2);
    }

    private static bool OpenSky(Point tile)
    {
        for (int y = tile.Y; y > tile.Y - SleetSkyRows; y--)
        {
            if (WorldGen.InWorld(tile.X, y, 10) && WorldGen.SolidTile(tile.X, y))
                return false;
        }

        return true;
    }
}

/// <summary> Sleet on the peaks: a streak of ice falling with the wind that freezes the tile it hits for a while, and melts below the treeline. </summary>
public sealed class SleetParticle : Particle
{
    private const float StreakLength = 10f;
    private const float StreakWidth = 1.5f;
    private const float FreezeMin = 0.45f;
    private const float FreezeMax = 0.8f;
    private const float FreezeTicks = 12f * 60f;

    /// <summary> Rows under the treeline a falling streak survives before it melts into nothing. </summary>
    private const int MeltRows = 10;

    private static readonly Color Ice = new(206, 228, 255);

    public override string[] Textures => ["Images/MagicPixel"];

    public override Vector2 Size => new(2f);

    public override float Gravity => 0.12f;

    public override float Bounce => 0f;

    public override int FadeOutTicks => 4;

    public override void OnSpawn() => Color = Ice;

    public override void Update()
    {
        if (Position.Y / 16f > IngressRealm.AlpineRow + MeltRows)
            Kill();
    }

    public override void OnTileCollide(Vector2 oldVelocity)
    {
        Point hit = (Position + oldVelocity.SafeNormalize(Vector2.UnitY) * 8f).ToTileCoordinates();

        // The film reads as a pale block over the needles, so a living tree's crown stays clear of it.
        if (WorldGen.InWorld(hit.X, hit.Y, 10) && WorldGen.SolidTile(hit.X, hit.Y) && !HighlandLeafTile.IsLeaf(Main.tile[hit.X, hit.Y]))
            TileConditionSystem.Apply<FrostCondition>(hit.X, hit.Y, Main.rand.NextFloat(FreezeMin, FreezeMax), FreezeTicks, sync: false);

        Kill();
    }

    public override void OnEnterLiquid(int liquid) => Kill();

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        spriteBatch.Draw(texture, drawPosition, new Rectangle(0, 0, 1, 1), lightColor, Velocity.ToRotation(), new Vector2(1f, 0.5f),
            new Vector2(StreakLength, StreakWidth), SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary> A glint of ice crystal carried across the screen on a gust, fluttering as it goes. </summary>
public sealed class IceMoteParticle : Particle
{
    private static readonly Color Ice = new(220, 240, 255);

    public override string[] Textures => [Assets.Textures.VFX.PixelStar.KEY];

    public override float Gravity => 0.01f;

    public override float Drag => 0.995f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override int FadeOutTicks => 30;

    public override void OnSpawn() => Color = Ice;

    public override void Update()
    {
        Velocity.Y += MathF.Sin(Position.X * 0.03f + Progress * 12f) * 0.03f;
        Rotation += Velocity.X * 0.02f;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        float twinkle = 0.6f + 0.4f * MathF.Sin(Position.X * 0.11f + Progress * 30f);

        spriteBatch.Draw(texture, drawPosition, null, lightColor * twinkle, Rotation, texture.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0f);
        return false;
    }
}
