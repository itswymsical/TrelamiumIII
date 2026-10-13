using SubworldLibrary;
using System.Collections.Generic;
using Terraria.IO;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Generation.Reverie;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Seals;
using TrelamiumIII.Core.Tiles.Seals;
using TrelamiumIII.Content.Reverie;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.FbmScreen;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// Reverie, the Library of Knowledge, entered through the scar over the Altar of Ingress: one great hall
/// (<see cref="ReverieHall"/>) with Edith at the front desk. The hall sits well inside the world so the camera never
/// clamps to an edge, and the whole realm is protected from building and mining.
/// </summary>
public class ReverieRealm : SubworldRealm
{
    /// <summary> Solid stone either side of the hall, wider than half a screen plus the hidden world edge. </summary>
    private const int SideMargin = 132;

    /// <summary> Solid ground under the floor, for the same reason. </summary>
    private const int FloorDepth = 130;

    private const int WorldHeight = 440;

    private static readonly Vector3 AmbientLight = new(0.26f, 0.21f, 0.15f);
    private static readonly Vector3 WindowLight = new(0.55f, 0.6f, 0.75f);

    /// <summary> Fixed by the constants, so clients know it without generating. </summary>
    private static readonly ReverieHall Hall = ReverieHall.Create(SideMargin, WorldHeight - FloorDepth);

    /// <summary> Where the scar back to Ingress stands, in world pixels. </summary>
    public static Vector2 ScarCenter => Hall.ScarCenter;

    public override int Width => ReverieHall.Width + SideMargin * 2;
    public override string Name => "Reverie";
    public override int Height => WorldHeight;

    public override List<GenPass> Tasks => [new ReverieHallPass()];
    public override bool ShouldSave => false;
    public override bool NormalUpdates => true;
    public override bool NoPlayerSaving => false;

    public override Color LoadingAccent => new(255, 214, 140);

    internal override FbmScreenStyle? ScreenStyle => ReverieScreen.Style;

    internal override void PopulateScreen(FbmScreenRenderer screen) => ReverieScreen.Populate(screen);

    internal override bool StartEntryCutscene()
    {
        CutsceneSystem.PlayCutscene<ReverieEntryCutscene>();

        return true;
    }

    public override void OnEnter() => SubworldSystem.hideUnderworld = true;

    /// <summary> Seals the whole realm, over a generated or loaded world alike. </summary>
    public override void OnLoad() => SealSystem.Seal(new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY), ModContent.GetInstance<RealmSeal>());

    public override void Update()
    {
        if (Main.raining)
            Main.StopRain();

        Main.slimeRain = false;
        Main.townNPCCanSpawn[default] = false;
    }

    /// <summary> Open cells of the hall glow faintly, and window panes brighter, so the hall reads without torches. </summary>
    public override bool GetLight(Tile tile, int x, int y, ref FastRandom rand, ref Vector3 color)
    {
        if (tile.HasTile || tile.LiquidAmount > 0 || tile.WallType == WallID.None || !Hall.Bounds.Contains(x, y))
            return false;

        color = ReverieHall.IsPane(tile.WallType) ? WindowLight : AmbientLight;

        return true;
    }

    private class ReverieHallPass() : GenPass("[T3] Reverie Hall", 1f)
    {
        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Shelving the library";

            // Above the hall, so it counts as underground: no sky light through the windows' walls.
            Main.worldSurface = Hall.VaultY - 40;
            Main.rockLayer = Hall.VaultY - 20;

            Hall.Build(WorldGen.genRand);
            Hall.PlaceEdith();

            Main.spawnTileX = Hall.Spawn.X;
            Main.spawnTileY = Hall.Spawn.Y;
        }
    }
}
