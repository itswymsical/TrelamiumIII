using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.UI;
using TrelamiumIII.Content.Items.Alchemy.AlchemyStations;

namespace TrelamiumIII.Core.UI.AlchemyStation;

public class AlchemyStationSystem : ModSystem
{
    private const int CLOSE_RANGE_TILES = 6;

    private static UserInterface _interface;

    // Two independent UIs: the basic early-game station and the advanced (progression) station. The bound
    // station type decides which one the shared UserInterface is showing.
    private static AlchemyStationUI _basicUi;
    private static AdvancedAlchemyStationUI _advancedUi;

    public static bool Visible { get; private set; }

    public static Point16 BoundStation { get; private set; }

    /// <summary>The advanced station entity the UI is currently editing, or null for the transient (simple) station.</summary>
    private static AdvancedAlchemyStationEntity _boundEntity;

    public override void Load()
    {
        if (Main.dedServ) return;

        _basicUi = new AlchemyStationUI();
        _basicUi.Activate();

        _advancedUi = new AdvancedAlchemyStationUI();
        _advancedUi.Activate();

        // No state until a station opens; OpenUI picks the right one.
        _interface = new UserInterface();
    }

    public override void Unload()
    {
        _interface = null;
        _basicUi = null;
        _advancedUi = null;
        PotencyFlash.Unload();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!Visible) return;

        if (Main.gameMenu || Main.LocalPlayer.dead || OutOfRange() || !Main.playerInventory)
        {
            CloseUI();
            return;
        }

        // Tell the active UI where its station is, before it updates, so it tracks the tile this frame: the
        // advanced overlay needs the tile origin to pin its slots; the basic panel anchors just above the tile.
        if (_boundEntity != null)
            _advancedUi.StationWorldOrigin = new Vector2(BoundStation.X * 16, BoundStation.Y * 16);
        else
            _basicUi.WorldAnchor = ComputeAnchorWorld();

        _interface?.Update(gameTime);
    }

    private static Vector2 ComputeAnchorWorld()
    {
        Vector2 origin = new(BoundStation.X * 16, BoundStation.Y * 16);
        return origin + new Vector2(8f, -8f);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int inventoryIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
        if (inventoryIndex == -1) return;

        layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Alchemy Station",
            () =>
            {
                if (Visible)
                    _interface?.Draw(Main.spriteBatch, new GameTime());
                return true;
            },
            InterfaceScaleType.UI
        ));
    }

    public static void ToggleUI(Point16 stationOrigin) => ToggleUI(stationOrigin, null);

    public static void ToggleUI(Point16 stationOrigin, AdvancedAlchemyStationEntity entity)
    {
        if (Visible && BoundStation == stationOrigin)
        {
            CloseUI();
            return;
        }

        OpenUI(stationOrigin, entity);
    }

    public static void OpenUI(Point16 stationOrigin) => OpenUI(stationOrigin, null);

    public static void OpenUI(Point16 stationOrigin, AdvancedAlchemyStationEntity entity)
    {
        if (_basicUi == null || _advancedUi == null) return;

        // Detach from any previously-bound station before binding the new one.
        if (Visible)
            Detach();

        _boundEntity = entity;
        BoundStation = stationOrigin;
        Visible = true;
        Main.playerInventory = true;

        // Advanced stations (those with an entity) use the world-overlay UI; everything else uses the basic UI.
        if (entity != null)
        {
            _interface.SetState(_advancedUi);
            _advancedUi.Bind(entity);
        }
        else
        {
            _interface.SetState(_basicUi);
        }

        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public static void CloseUI()
    {
        if (!Visible) return;

        Detach();
        Visible = false;
        _basicUi.WorldAnchor = null;
        _advancedUi.StationWorldOrigin = null;
        SoundEngine.PlaySound(SoundID.MenuClose);
    }

    /// <summary>
    /// Persists current contents back to the bound station entity (advanced), or returns them to the
    /// player (transient/simple). Resets the shared UI's transient state either way.
    /// </summary>
    private static void Detach()
    {
        if (_boundEntity != null)
        {
            _advancedUi.Release();
            _boundEntity.SyncToClients();
            _boundEntity = null;
        }
        else
        {
            _basicUi.ReturnAllToPlayer();
        }
    }

    private static bool OutOfRange()
    {
        var player = Main.LocalPlayer;
        var playerTile = player.Center.ToTileCoordinates16();

        // Measure from the nearest edge of the station's whole footprint, not the top-left origin
        // Advanced stations span the full multitile; the simple (transient) station is a single cell.
        int left = BoundStation.X;
        int top = BoundStation.Y;
        int right = left + (_boundEntity != null ? AdvancedAlchemyStationBase.Width - 1 : 0);
        int bottom = top + (_boundEntity != null ? AdvancedAlchemyStationBase.Height - 1 : 0);

        int dx = playerTile.X < left ? left - playerTile.X : (playerTile.X > right ? playerTile.X - right : 0);
        int dy = playerTile.Y < top ? top - playerTile.Y : (playerTile.Y > bottom ? playerTile.Y - bottom : 0);

        return dx * dx + dy * dy > CLOSE_RANGE_TILES * CLOSE_RANGE_TILES;
    }
}
