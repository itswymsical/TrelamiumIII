using SubworldLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.GameContent;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Core.LocalizationReferences.Mods.TrelamiumIII.LoreGlossary;

namespace TrelamiumIII.Common.Subworlds.Realms;

public class ReverieRealm : SubworldRealm
{
    public override int Width => 720;

    public override int Height => 256;

    public override List<GenPass> Tasks => [ new ReverieDunesPass() ];
    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    public override void SetStaticDefaults()
    {
        try
        {
            base.SetStaticDefaults();
            OtherworldTitle = TextureAssets.Logo?.Value;
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance?.Logger.Error($"Error in ArchaeaSub.SetStaticDefaults: {ex.Message}");
            OtherworldTitle = null;
        }
    }

    public override void Update()
    {
        ManageGameConditions();
        RenderScreenEffects();
    }

    private void RenderScreenEffects()
    {
        ScreenWobbleSystem.Set(2f, 1, 1);
    }

    private void ManageGameConditions()
    {
        var player = Main.LocalPlayer;
        if (player.ZoneForest || player.ZoneSkyHeight || player.ZonePurity)
            player.ZoneDesert = true;


        if (player.ZoneRockLayerHeight)
            player.ZoneUndergroundDesert = true;

        if (Main.raining)
        {
            Main.StopRain();
            Main.raining = false;
            Main.slimeRain = false;
        }

        if (Main.slimeRain)
            Main.slimeRain = false;

        Main.townNPCCanSpawn[default] = false;
    }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;  
}
public class DuneConfiguration
{
    public float DuneFrequency { get; set; } = 0.05f;
    public int BaseHeightOffset { get; set; } = 10;
    public int DuneHeightVariation { get; set; } = 35;
    public int HardenedSandDepth { get; set; } = (int)(Main.maxTilesY / 1.6f);
}
public class ReverieDunesPass : GenPass
{
    #region Constants
    private const float SURFACE_LAYER_RATIO = 1f / 5f;
    #endregion

    #region Fields
    private readonly DesertConfiguration _config;
    private FastNoiseLite _duneNoise;
    private int _surfaceHeight;
    #endregion

    #region init
    public ReverieDunesPass() : base("[T3] Dunes", 247.43f)
    {
        _config = new DesertConfiguration();
    }
    public class DesertConfiguration
    {
        public float DuneFrequency { get; set; } = 0.05f;
        public int BaseHeightOffset { get; set; } = 10;
        public int DuneHeightVariation { get; set; } = 35;
        public int HardenedSandDepth { get; set; } = (int)(Main.maxTilesY / 1.6f);
    }
    private void Initialize()
    {
        InitializeNoise();
        CalculateWorldParameters();
    }

    private void InitializeNoise()
    {
        _duneNoise = new FastNoiseLite();
        _duneNoise.SetNoiseType(FastNoiseLite.NoiseType.ValueCubic);
        _duneNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        _duneNoise.SetFractalOctaves(3);
        _duneNoise.SetFrequency(_config.DuneFrequency);
    }

    private void CalculateWorldParameters()
    {
        _surfaceHeight = (int)(Main.maxTilesY * SURFACE_LAYER_RATIO);
        Main.worldSurface = _surfaceHeight;
        Main.spawnTileX = Main.maxTilesX / 2;
        Main.spawnTileY = _surfaceHeight;
    }
    #endregion

    #region pass
    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Generating Blank";
        Initialize();

        progress.Set(0.0f);
        GenerateTerrain();
        progress.Set(0.8f);

        progress.Message = "Adding Blank";
        GenerateSandPatches();
        progress.Set(1.0f);
    }

    private void GenerateTerrain()
    {
        for (var x = 0; x < Main.maxTilesX; x++)
        {
            GenerateColumn(x);
        }
    }

    private void GenerateColumn(int x)
    {
        if (!IsValidX(x)) return;

        int peakHeight = CalculatePeakHeight(x);
        FillColumn(x, peakHeight);
    }
    #endregion

    #region calculation
    private int CalculatePeakHeight(int x)
    {
        float noiseValue = _duneNoise.GetNoise(x / 2, Main.maxTilesY / 2);
        int height = (int)(_surfaceHeight - _config.BaseHeightOffset + noiseValue * _config.DuneHeightVariation);
        return Math.Clamp(height, 0, Main.maxTilesY - 1);
    }
    #endregion

    #region tile runners
    private void FillColumn(int x, int peakHeight)
    {
        for (var y = peakHeight; y < Main.UnderworldLayer; y++)
        {
            if (!IsValidY(y)) continue;
            PlaceSandTile(x, y);
        }
    }

    private void PlaceSandTile(int x, int y)
    {
        var tile = Main.tile[x, y];
        tile.HasTile = true;

        int transitionStart = Main.UnderworldLayer - _config.HardenedSandDepth;
        int transitionDepth = 40;

        if (y >= transitionStart + transitionDepth)
        {
            tile.TileType = TileID.HardenedSand;
        }
        else if (y < transitionStart)
        {
            tile.TileType = TileID.DiamondGemspark;
        }
        else
        {
            float depthRatio = (float)(y - transitionStart) / transitionDepth;
            float noise = _duneNoise.GetNoise(x * 0.8f, y * 0.8f) * 0.5f + 0.5f;

            if (noise < depthRatio)
            {
                tile.TileType = TileID.HardenedSand;
            }
            else
            {
                tile.TileType = TileID.DiamondGemspark;
            }
        }
    }
    private void GenerateSandPatches()
    {
        int transitionStart = Main.UnderworldLayer - _config.HardenedSandDepth;
        int transitionDepth = 40;

        int patchCount = (int)(Main.maxTilesX * 0.2f);

        for (int i = 0; i < patchCount; i++)
        {
            int x = GenBase._random.Next(10, Main.maxTilesX - 10);
            int y = GenBase._random.Next(
                transitionStart + transitionDepth / 2,
                transitionStart + transitionDepth
            );

            if (Main.tile[x, y].TileType == TileID.DiamondGemspark)
            {
                WorldGen.TileRunner(
                    x, y,
                    GenBase._random.Next(5, 15),
                    GenBase._random.Next(10, 30),
                    TileID.HardenedSand,
                    true
                );
            }

            y = GenBase._random.Next(
                transitionStart,
                transitionStart + transitionDepth / 2
            );

            if (Main.tile[x, y].TileType == TileID.HardenedSand)
            {
                WorldGen.TileRunner(
                    x, y,
                    GenBase._random.Next(5, 15),
                    GenBase._random.Next(10, 30),
                    TileID.DiamondGemsparkOff,
                    true
                );
            }
        }
    }
    #endregion

    #region validation
    private bool IsValidX(int x)
    {
        return x >= 0 && x < Main.maxTilesX;
    }

    private bool IsValidY(int y)
    {
        return y >= 0 && y < Main.maxTilesY;
    }
    #endregion
}