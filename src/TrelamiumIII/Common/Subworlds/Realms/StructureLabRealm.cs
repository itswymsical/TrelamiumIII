using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Content.DevTools.StructureLab;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
///     Empty, evenly lit world holding the <see cref="StructureEditor" /> canvas at its centre. Vanilla world updates are
///     off, so time, liquid flow and tile growth stand still; players float.
/// </summary>
public class StructureLabRealm : SubworldRealm
{
    public const int WorldWidth = 1400;
    public const int WorldHeight = 900;

    public override int Width => WorldWidth;
    public override int Height => WorldHeight;
    public override string Name => "StructureLab";

    public override List<GenPass> Tasks => [new StructureLabPass()];

    public override bool ShouldSave => false;

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    public override void OnLoad()
    {
        Main.dayTime = true;
        Main.time = Main.dayLength / 2;
        Main.raining = false;
        Main.maxRaining = 0f;
        Main.cloudAlpha = 0f;
    }

    public override void OnUnload() => StructureEditor.OnLeaveLab();

    public override bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color)
    {
        color = Vector3.One;
        return true;
    }

    public override float GetGravity(Entity entity) => entity is Player ? 0f : 1f;

    private class StructureLabPass : GenPass
    {
        public StructureLabPass() : base("[T3] Structure Lab", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Stretching the canvas";

            Main.worldSurface = Main.maxTilesY - 100;
            Main.rockLayer = Main.maxTilesY - 60;
            Main.spawnTileX = Main.maxTilesX / 2;
            Main.spawnTileY = Main.maxTilesY / 2;

            StructureEditor.BuildPendingCanvas();
        }
    }
}
