using System.Collections.Generic;
using Terraria.UI;

namespace TrelamiumIII.Core.UI.World;

/// <summary>
///     Ticks and draws every <see cref="IWorldUI" /> in one interface layer.
/// </summary>
/// <remarks>
///     Deliberately knows nothing about what it is holding: no reflection, no type switch, no
///     per-entity bookkeeping. Callers construct their own UI and hand it over with
///     <see cref="Register" />; it retires itself by reporting <see cref="IWorldUI.IsActive" />
///     false. A <see cref="ModSystem" /> is only involved at all because nothing else can hook
///     <see cref="ModifyInterfaceLayers" />.
///     <para />
///     Panels are held separately from the ambient list. Only one is ever focused at a time, and it
///     needs a <see cref="UserInterface" /> host to route mouse events into its element tree, which
///     the lightweight indicators do not.
/// </remarks>
public sealed class WorldUISystem : ModSystem
{
    private static readonly List<IWorldUI> Registered = [];

    private static UserInterface? _panelHost;
    private static WorldPanel? _openPanel;

    /// <summary>The focused panel, if any. Indicators check this to stay out of its way.</summary>
    public static WorldPanel? OpenPanel => _openPanel;

    public static bool HasOpenPanel => _openPanel != null;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        _panelHost = new UserInterface();
    }

    public override void Unload()
    {
        Registered.Clear();
        _openPanel = null;
        _panelHost = null;
    }

    public override void OnWorldUnload()
    {
        Registered.Clear();
        ForceClosePanel();
    }

    /// <summary>Adds a world UI to the draw and update set. Ignores duplicates.</summary>
    public static void Register(IWorldUI ui)
    {
        if (Main.dedServ || ui == null || Registered.Contains(ui))
            return;

        Registered.Add(ui);
    }

    public static void Unregister(IWorldUI ui) => Registered.Remove(ui);

    /// <summary>
    ///     Focuses a panel, closing whatever was open. The outgoing panel is dropped immediately
    ///     rather than allowed to fade, since the player asked for this one instead.
    /// </summary>
    public static void OpenPanelState(WorldPanel panel)
    {
        if (Main.dedServ || _panelHost == null || panel == null)
            return;

        ForceClosePanel();

        _openPanel = panel;
        _panelHost.SetState(panel); // Activates the state and builds its element tree.
        panel.OnOpened();
    }

    /// <summary>
    ///     Asks the open panel to close. It plays its close animation and drops out on its own once
    ///     <see cref="IWorldUI.IsActive" /> goes false, so callers do not have to wait on it.
    /// </summary>
    public static void RequestClosePanel() => _openPanel?.RequestClose();

    /// <summary>Drops the open panel this instant, skipping its close animation.</summary>
    public static void ForceClosePanel()
    {
        if (_openPanel == null)
            return;

        _openPanel.OnClosed();
        _openPanel = null;
        _panelHost?.SetState(null);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.dedServ)
            return;

        // Backwards so a UI that retires (or unregisters something) during its own tick cannot
        // shift the indices of the ones still to be updated.
        for (int i = Registered.Count - 1; i >= 0; i--)
        {
            IWorldUI ui = Registered[i];

            if (!ui.IsActive)
            {
                Registered.RemoveAt(i);
                continue;
            }

            ui.Update(gameTime);
        }

        UpdatePanel(gameTime);
    }

    private static void UpdatePanel(GameTime gameTime)
    {
        if (_openPanel == null)
            return;

        if (Main.gameMenu || Main.LocalPlayer.dead || !_openPanel.IsActive)
        {
            ForceClosePanel();
            return;
        }

        // The host updates the state, which is where vanilla dispatches MouseOver/MouseDown into
        // the element tree. Updating the panel directly as well would tick it twice.
        _panelHost?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        // Same anchor the job indicators always used: above the world and the house banners, below
        // the inventory and the cursor.
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Town NPC House Banners"));
        if (index == -1)
            return;

        layers.Insert(index + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: World UI",
            () =>
            {
                Draw(Main.spriteBatch);
                return true;
            },
            InterfaceScaleType.UI)
        );
    }

    private static void Draw(SpriteBatch spriteBatch)
    {
        // Indexed rather than foreach: drawing should not mutate the set, but a stray Register from
        // a draw callback would otherwise throw instead of merely being picked up next frame.
        for (int i = 0; i < Registered.Count; i++)
            Registered[i].Draw(spriteBatch);

        if (_openPanel != null)
            _panelHost?.Draw(spriteBatch, new GameTime());
    }
}
