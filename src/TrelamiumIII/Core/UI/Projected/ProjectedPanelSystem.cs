using System.Collections.Generic;
using Terraria.UI;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>A panel with an off-screen face that has to be rebuilt once a frame.</summary>
internal interface IProjectedPanel : IDisposable
{
    /// <summary>True once the panel has finished closing and can be dropped and disposed.</summary>
    bool ShouldBeRemoved { get; }

    /// <summary>Redraws the face. Called with no sprite batch open and render targets free.</summary>
    void BuildSurfaces();
}

/// <summary>
///     Owns the quad mesh every projected panel shares, rebuilds the faces of every registered
///     <see cref="IProjectedPanel" /> at <c>CheckMonoliths</c> (where render targets may be swapped), and wraps the quad pass.
/// </summary>
public sealed class ProjectedPanelSystem : ModSystem
{
    private static readonly List<IProjectedPanel> live = [];

    internal static ProjectedQuad? Quad { get; private set; }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Quad = new ProjectedQuad();
        Quad.Load();

        On_Main.CheckMonoliths += BuildFrame;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        foreach (IProjectedPanel panel in live)
            panel.Dispose();

        live.Clear();

        // Unload may run off the main thread; the quad's effect is a graphics resource.
        ProjectedQuad? pending = Quad;
        Quad = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    /// <summary>Whether any registered panel is a <typeparamref name="T" />.</summary>
    internal static bool AnyLive<T>() where T : IProjectedPanel
    {
        foreach (IProjectedPanel panel in live)
        {
            if (panel is T)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Brackets the mouse-text layer, so a claimed item tooltip is in vanilla's hands for
    ///     exactly as long as it takes to draw and no longer.
    /// </summary>
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index == -1)
            return;

        // The tail first: inserting it after the head would shift the index the head was found at.
        layers.Insert(index + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Projected Tooltip Release",
            () =>
            {
                ProjectedTooltip.Release();
                return true;
            },
            InterfaceScaleType.UI));

        layers.Insert(index, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Projected Tooltip Claim",
            () =>
            {
                ProjectedTooltip.Assert();
                return true;
            },
            InterfaceScaleType.UI));
    }

    internal static void Register(IProjectedPanel panel)
    {
        if (!live.Contains(panel))
            live.Add(panel);
    }

    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        if (Main.dedServ)
            return;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            IProjectedPanel panel = live[i];

            if (panel.ShouldBeRemoved)
            {
                live.RemoveAt(i);
                panel.Dispose();
                continue;
            }

            if (!Main.gameMenu)
                panel.BuildSurfaces();
        }
    }

    /// <summary>True when the shared quad exists and can draw.</summary>
    internal static bool CanDraw => Quad is { Ready: true };

    /// <summary>
    ///     Runs <paramref name="draw" /> as a quad pass in the middle of an interface layer.
    /// </summary>
    internal static void DrawQuads(SpriteBatch spriteBatch, Action<ProjectedQuad, GraphicsDevice> draw)
    {
        ProjectedQuad? quad = Quad;

        if (quad is null || !quad.Ready)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        spriteBatch.End();

        quad.Begin(device);
        draw(quad, device);
        quad.End(device);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            Main.SamplerStateForCursor,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.UIScaleMatrix);
    }
}
