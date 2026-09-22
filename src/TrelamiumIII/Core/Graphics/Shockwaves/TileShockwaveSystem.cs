using System.Collections.Generic;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
///     Bends the solid tile layer around expanding rings, by handing
///     <c>Main.tileTarget</c> a distorted stand-in for the duration of the one draw call
///     that puts it on the screen.
/// </summary>
internal sealed class TileShockwaveSystem : ModSystem
{
    /// <summary>
    ///     How many waves may distort the same frame.
    /// </summary>
    private const int MaxConcurrentWaves = 4;

    private static readonly List<ITileWave> waves = [];

    private static WorldSceneLayerTarget? front;
    private static WorldSceneLayerTarget? back;

    /// <summary>
    ///     The surface holding this frame's distorted layer, positioned where the tile target was
    ///     when it was built, or null if the scene should be left alone. Produced in
    ///     <see cref="PrepareDistortion" />, consumed in <see cref="SwapInDistortion" />.
    /// </summary>
    private static WorldSceneLayerTarget? distorted;

    private static bool loaded;

    /// <summary>
    ///     Starts a wave. Safe to call from anywhere; ignored on a server.
    /// </summary>
    /// <remarks>
    ///     Takes any <see cref="ITileWave" />, so a smooth <see cref="Shockwave" /> and a
    ///     tile-quantised <see cref="BlockWave" /> can be in flight together and will be composited
    ///     in the order they were spawned.
    /// </remarks>
    public static void Spawn(ITileWave wave)
    {
        if (Main.dedServ || !loaded)
            return;

        waves.Add(wave);
    }

    public override void OnModLoad()
    {
        if (Main.dedServ)
            return;

        loaded = true;

        On_Main.DoDraw_UpdateCameraPosition += PrepareDistortion;
        On_Main.DoDraw_Tiles_Solid += SwapInDistortion;
    }

    public override void OnModUnload()
    {
        loaded = false;
        waves.Clear();
        distorted = null;

        front?.Dispose();
        back?.Dispose();
        front = null;
        back = null;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        for (int i = waves.Count - 1; i >= 0; i--)
        {
            waves[i].Update();

            if (waves[i].Finished)
                waves.RemoveAt(i);
        }
    }

    /// <summary>
    ///     Runs the displacement passes into our own targets, before the scene starts.
    /// </summary>
    private static void PrepareDistortion(On_Main.orig_DoDraw_UpdateCameraPosition orig)
    {
        orig();

        // Cleared unconditionally so a frame that bails for any reason below cannot leave last
        // frame's surface behind for the swap hook to pick up.
        distorted = null;

        if (!loaded || waves.Count == 0)
            return;

        // drawToScreen means the tile targets were never populated: vanilla drew tiles straight
        // to the backbuffer instead, and ReleaseTargets disposed the lot on the way in.
        //
        // It is set from Lighting.UpdateEveryFrame, which is
        //     Main.LightingEveryFrame && !Main.RenderTargetsRequired && !Lighting.NotRetro
        // so the usual "no targets under Retro lighting" warning is only two thirds true:
        // RenderTargetsRequired is set by zoom (GameZoomTarget > 1), so a Retro-lighting player
        // who is zoomed in does actually get populated targets and DOES see this effect. A mod that needs
        // them unconditionally can clear Main.LightingEveryFrame, at the cost of retro lighting
        // no longer relighting every frame, which is the entire point of those modes.
        if (Main.drawToScreen)
            return;

        var sourceLayer = Main.tileTarget;

        if (sourceLayer is null || sourceLayer.Texture.IsDisposed)
            return;

        var source = (RenderTarget2D)sourceLayer.Texture;
        var device = Main.instance.GraphicsDevice;

        if (!EnsureTarget(ref front, device, source.Width, source.Height))
            return;

        // Captured once. Every wave in this pass must be positioned against the same origin, and
        // the frame's tile re-render may move the tile target after this hook returns.
        var sceneOrigin = sourceLayer.Position;

        Texture2D read = source;
        WorldSceneLayerTarget? last = null;
        int applied = 0;

        foreach (var wave in waves)
        {
            if (applied >= MaxConcurrentWaves)
                break;

            if (!wave.Intersects(source, sceneOrigin))
                continue;

            // A pass cannot read and write the same surface. front takes every even pass, back
            // every odd one; back is allocated only once a second visible wave actually turns up,
            // so the common single-wave case costs one target instead of two.
            if (applied == 1 && !EnsureTarget(ref back, device, source.Width, source.Height))
                break;

            last = applied % 2 == 0 ? front! : back!;
            var write = (RenderTarget2D)last.Texture;

            ApplyPass(device, read, write, wave, sceneOrigin);

            read = write;
            applied++;
        }

        // Unbind to null rather than restoring previous bindings, exactly what RenderTiles and
        // friends do. Nothing is bound this early in the frame, so there is nothing to put back.
        device.SetRenderTarget(null);

        if (last is null)
            return;

        last._position = sceneOrigin;
        distorted = last;
    }

    /// <summary>
    ///     Substitutes the distorted surface for the duration of the vanilla tile blit.
    /// </summary>
    private static void SwapInDistortion(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        var replacement = distorted;
        var originalTarget = Main.tileTarget;

        if (replacement is null || Main.drawToScreen || originalTarget is null)
        {
            orig(self);
            return;
        }

        // If the camera has since travelled further than the overscan, the snapshot no longer
        // covers the screen and would show unpainted edges. One frame without the effect beats one
        // frame with holes in the terrain. This is the test vanilla uses to force a rebuild.
        if (replacement.IsPartiallyOffscreen)
        {
            orig(self);
            return;
        }

        Main.tileTarget = replacement;

        // finally, not just trailing assignments: orig swallows its own draw exceptions but
        // DrawTileEntities inside it does not, and leaving one of our targets in that field would
        // have Terraria dispose it in ReleaseTargets on the next resolution change.
        try
        {
            orig(self);
        }
        finally
        {
            Main.tileTarget = originalTarget;
        }
    }

    private static void ApplyPass(GraphicsDevice device, Texture2D read, RenderTarget2D write, ITileWave wave, Vector2 sceneOrigin)
    {
        device.SetRenderTarget(write);
        device.Clear(Color.Transparent);

        // The wave configures its own shader. The system deliberately knows nothing about which
        // uniforms exist -- that is the whole point of the interface, and it is what lets a smooth
        // ring and a block heave share this loop.
        var shader = wave.PrepareShader(write, sceneOrigin);

        // PointClamp because that is exactly what Main.DefaultSamplerState resolves to whenever
        // the targets are in use. Linear filtering would soften every tile edge across the whole
        // band, including where the displacement is sub-texel and nothing should have changed.
        //
        // CullNone rather than Main.Rasterizer: that flips to CullClockwise whenever the view
        // matrix is mirrored, which would cull a plain full-target quad on gravity-flipped frames.
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        shader.Apply();

        Main.spriteBatch.Draw(read, new Rectangle(0, 0, write.Width, write.Height), Color.White);

        Main.spriteBatch.End();
    }

    private static bool EnsureTarget(ref WorldSceneLayerTarget? target, GraphicsDevice device, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return false;

        if (target is { Texture: { IsDisposed: false } texture } && texture.Width == width && texture.Height == height)
            return true;

        target?.Dispose();

        // Allocated through vanilla's own wrapper so it can stand in for Main.tileTarget, which is
        // written and read on separate hooks of a frame the same way.
        target = new WorldSceneLayerTarget(device, width, height);

        return true;
    }
}
