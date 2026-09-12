using System.Collections.Generic;
using Terraria.UI;
using TrelamiumIII.Core.Dialogue.Projected;

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
///     Rebuilds the faces of every registered <see cref="IProjectedPanel" /> once per frame, and
///     wraps the quad pass every one of them draws with.
/// </summary>
/// <remarks>
///     <para>
///         Alongside <see cref="ProjectedDialogueSystem" /> rather than inside it. That system is
///         shaped around the dialogue box and its response atlas; this one holds anything else that
///         wants to be a plane in the scene -- the job board, its hover card, the item tooltip -- and
///         only shares the quad mesh with it.
///     </para>
///     <para>
///         Faces are built from <c>CheckMonoliths</c> for the reason given on the dialogue system: it
///         is the point in the frame where vanilla does its own off-screen work, and the last one
///         where a render target can still be set without discarding the frame so far.
///     </para>
/// </remarks>
public sealed class ProjectedPanelSystem : ModSystem
{
    private static readonly List<IProjectedPanel> live = [];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        On_Main.CheckMonoliths += BuildFrame;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        foreach (IProjectedPanel panel in live)
            panel.Dispose();

        live.Clear();
    }

    /// <summary>
    ///     Brackets the mouse-text layer, so a claimed item tooltip is in vanilla's hands for
    ///     exactly as long as it takes to draw and no longer.
    /// </summary>
    /// <remarks>
    ///     Two layers rather than one because the second has to run even when no tooltip was drawn:
    ///     it puts back the player's opaque-box setting, which the first switches off. See
    ///     <see cref="ProjectedTooltip.Assert" /> for why either is needed.
    /// </remarks>
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
    internal static bool CanDraw => ProjectedDialogueSystem.Quad is { Ready: true };

    /// <summary>
    ///     Runs <paramref name="draw" /> as a quad pass in the middle of an interface layer.
    /// </summary>
    /// <remarks>
    ///     The layer opened a sprite batch and primitives cannot be issued inside one, so it is closed
    ///     and reopened around the quads exactly as the UI layers leave it. Every panel that draws
    ///     from a UI-scaled layer goes through here so that reopening is written once.
    /// </remarks>
    internal static void DrawQuads(SpriteBatch spriteBatch, Action<ProjectedQuad, GraphicsDevice> draw)
    {
        ProjectedQuad? quad = ProjectedDialogueSystem.Quad;

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
