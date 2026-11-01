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

        if (cover <= 0f || Hash(i, j, salt: 11) >= cover || !IsTopSurface(i, j) || !IsSkyExposed(i, j))
            return;

        Maintain<FrostCondition>(i, j, MathHelper.Lerp(0.5f, 1f, cover), LingerTicks);
    }
}

/// <summary>
/// Spawns the highlands' weather around the local player. Mist rises off exposed ground everywhere in the highlands, as
/// vanilla's graveyard fog does, thicker at dawn, dusk and night and in the rain, and now and then a string of it blows off
/// a ledge. Up on the peaks, sleet falls and freezes what it hits, and gusts carry ice crystals and freezing mist past.
/// </summary>
public sealed class HighlandsWeather : ModSystem
{
    // Vanilla's graveyard works the same area and gives each floor tile one chance in 120 a tick at full fog.
    private const int MistAreaWidth = 120;
    private const int MistAreaHeight = 40;
    private const float MistChancePerDensity = 1f / 240f;
    private const int MistBelowSurfaceRows = 5;
    private const float RainMistBoost = 1f;
    private const int MistLifeMin = 420;
    private const int MistLifeMax = 600;

    private const int DriftInterval = 30;
    private const float DriftStringsPerDensity = 4f;
    private const int DriftPuffsMin = 3;
    private const int DriftPuffsMax = 8;

    /// <summary> Pixels between puffs in a string, per unit of puff scale: vanilla's 1.4 speed times 6.5. </summary>
    private const float DriftPuffSpacing = 9f;

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
    /// The weather's own particles, ticked and drawn here: mist and sleet are numerous and constant, and in the shared list
    /// they would push out every other effect.
    /// </summary>
    private static readonly List<Particle> particles = [];

    private int ticks;

    /// <summary> This tick's mist colour, read by every mist particle so they don't each work out the palette. </summary>
    internal static Color MistTint { get; private set; } = Color.White;

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
                if (Main.rand.NextFloat() >= chance || !IsFloor(x, y))
                    continue;

                float rise = Main.rand.NextFloat(16f);
                Spawn<HighlandsMistParticle>(new Vector2(x * 16 + 8, y * 16 - 8 - rise), Vector2.Zero, Main.rand.Next(MistLifeMin, MistLifeMax + 1),
                    variant: rise < 4f ? 3 : Main.rand.Next(3), scale: Main.rand.NextFloat(0.8f, 1f));
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

    /// <summary> A string of faint cloud leaving a ledge along the wind, each puff a little further on, as vanilla's airborne clouds do. </summary>
    private static void BlowDrift(Vector2 position)
    {
        float wind = HighlandsDriftParticle.Wind;
        float heading = Main.rand.NextFloatDirection() * 0.15f;

        for (int i = Main.rand.Next(DriftPuffsMin, DriftPuffsMax + 1); i > 0; i--)
        {
            float scale = Main.rand.NextFloat(1.1f, 2.2f);
            Vector2 velocity = new Vector2(wind, 0f).RotatedBy(heading);

            Spawn<HighlandsDriftParticle>(position + Main.rand.NextVector2Circular(4f, 4f), velocity, Main.rand.Next(360, 540),
                variant: Main.rand.Next(2), scale: scale, rotation: velocity.ToRotation());

            position += velocity.SafeNormalize(Vector2.UnitX) * DriftPuffSpacing * scale;
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

    /// <summary> Flat solid ground with open air over it, as vanilla's graveyard asks of a tile before fog rises off it. </summary>
    private static bool IsFloor(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 10))
            return false;

        Tile tile = Main.tile[x, y];
        return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && tile.Slope == SlopeType.Solid && !tile.IsHalfBlock && !WorldGen.SolidTile(x, y - 1);
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

/// <summary>
/// Highland mist hugging the ground, drawn with vanilla's graveyard floor-cloud sprites through world light and tinted by
/// the palette. Like vanilla's, it rides the wind and fades in while there is ground under it, and fades away once it
/// drifts off a ledge or runs out of time.
/// </summary>
public sealed class HighlandsMistParticle : Particle
{
    // Vanilla's floor clouds peak at alpha 220 of 255, and fade a step of alpha each tick.
    private const float MaxOpacity = 35f / 255f;
    private const float FadeStep = 1f / 255f;
    private const float Drift = 0.4f;
    private const int FadeOutTime = 30;

    private float opacity;

    public override string[] Textures =>
        [$"Images/Gore_{GoreID.AmbientFloorCloud1}", $"Images/Gore_{GoreID.AmbientFloorCloud2}", $"Images/Gore_{GoreID.AmbientFloorCloud3}", $"Images/Gore_{GoreID.AmbientFloorCloud4}"];

    public override float Gravity => 0f;

    public override float Drag => 1f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override int FadeOutTicks => 0;

    public override void Update()
    {
        Velocity = new Vector2(Drift * Main.WindForVisuals, 0f);
        Color = HighlandsWeather.MistTint;

        Point tile = Position.ToTileCoordinates();
        bool grounded = WorldGen.InWorld(tile.X, tile.Y + 2, 10) && !WorldGen.SolidTile(tile.X, tile.Y)
            && (WorldGen.SolidTile(tile.X, tile.Y + 1) || WorldGen.SolidTile(tile.X, tile.Y + 2));

        if (grounded && TimeLeft > FadeOutTime)
            opacity = MathF.Min(opacity + FadeStep, MaxOpacity);
        else if ((opacity -= FadeStep) <= 0f)
            Kill();
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        spriteBatch.Draw(texture, drawPosition, null, lightColor * opacity, 0f, texture.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary> A faint puff of cloud blown along the wind, vanilla's airborne graveyard cloud tinted by the palette; it thins away against anything solid. </summary>
public sealed class HighlandsDriftParticle : Particle
{
    // Vanilla's airborne clouds peak at alpha 240 of 255.
    private const float MaxOpacity = 18f / 255f;
    private const float FadeStep = 0.2f / 255f;
    private const int FadeOutTime = 60;
    private const float Speed = 1.4f;

    /// <summary> Speed along the wind, with a floor so a still day still carries it off. </summary>
    internal static float Wind => Speed * (Main.WindForVisuals >= 0f ? 1f : -1f) * MathF.Max(MathF.Abs(Main.WindForVisuals), 0.25f);

    private float opacity;

    public override string[] Textures => [$"Images/Gore_{GoreID.AmbientAirborneCloud1}", $"Images/Gore_{GoreID.AmbientAirborneCloud3}"];

    public override float Gravity => 0f;

    public override float Drag => 1f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override int FadeOutTicks => 0;

    public override void Update()
    {
        Color = HighlandsWeather.MistTint;
        Rotation = Velocity.ToRotation();

        Point tile = Position.ToTileCoordinates();
        bool blocked = WorldGen.InWorld(tile.X, tile.Y, 10) && WorldGen.SolidTile(tile.X, tile.Y);

        if (!blocked && TimeLeft > FadeOutTime)
            opacity = MathF.Min(opacity + FadeStep * 5f, MaxOpacity);
        else if ((opacity -= FadeStep * 5f) <= 0f)
            Kill();
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        spriteBatch.Draw(texture, drawPosition, null, lightColor * opacity, Rotation, texture.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
        return false;
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

        if (WorldGen.InWorld(hit.X, hit.Y, 10) && WorldGen.SolidTile(hit.X, hit.Y))
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
