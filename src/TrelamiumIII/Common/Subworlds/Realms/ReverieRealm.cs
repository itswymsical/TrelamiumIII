using SubworldLibrary;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.IO;
using Terraria.WorldBuilding;
using TrelamiumIII.Core.Graphics.ScreenEffects;

namespace TrelamiumIII.Common.Subworlds.Realms;

public class TrelamiumIIIRealm : SubworldRealm
{
    public override int Width => 720;
    public override string Name => "TrelamiumIII";
    public override int Height => 256;

    public override List<GenPass> Tasks => [ new TrelamiumIIIDunesPass() ];
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
            TrelamiumIII.Instance?.Logger.Error($"Error in TrelamiumIIIRealm.SetStaticDefaults: {ex.Message}");
            OtherworldTitle = null;
        }
    }

    public override void Update()
    {
        ManageGameConditions();
        // RenderScreenEffects();
    }

    private void RenderScreenEffects()
    {
        ChromaticScreenEffect.Set(0.4f, 1.25f, 1);
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

public class TrelamiumIIIDunesPass : GenPass
{
    public TrelamiumIIIDunesPass() : base("[T3] Dunes", 247.43f) { }

    protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
    {
        progress.Message = "Generating Blank";

        int surfaceY = Main.maxTilesY / 3;

        for (int y = surfaceY; y < Main.maxTilesY; y++)
        {
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                Tile tile = Main.tile[x, y];

                tile.HasTile = true;
                tile.TileType = TileID.DiamondGemspark;
            }
        }
    }
}