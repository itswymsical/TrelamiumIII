using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Content.DevTools.VfxLab.Room;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
///     A test facility for effects: a sealed room for each kind of thing the VFX Lab tunes, built to show it from every side. Nothing here
///     is saved, and vanilla world updates are off, so what's in a room stays as it was left and only what's being tested moves.
/// </summary>
public class VfxLabRealm : SubworldRealm
{
    public override int Width => LabRooms.WorldWidth;
    public override int Height => LabRooms.WorldHeight;
    public override string Name => "VfxLab";

    public override List<GenPass> Tasks => [new VfxLabPass()];

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

    public override bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color)
    {
        color = LabRooms.LightAt(x, y);
        return true;
    }

    private class VfxLabPass : GenPass
    {
        public VfxLabPass() : base("[T3] VFX Lab", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Sealing the test chambers";

            LabRooms.Build();
        }
    }
}
