using System.Collections.Generic;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
///     Bends the solid tile layer around expanding rings, by handing
///     <c>Main.instance.tileTarget</c> a distorted stand-in for the duration of the one draw call
///     that puts it on the screen.
/// </summary>
/// <remarks>
///     <para>
///         vanilla renders solid tiles once every few frames into <c>Main.instance.tileTarget</c>
///         -- a target the size of the screen plus <see cref="Main.offScreenRange" /> on every
///         side -- and then blits it each frame in <c>DoDraw_Tiles_Solid</c> at
///         <c>sceneTilePos - screenPosition</c>. Substituting the texture for that blit means the
///         distortion touches nothing else: players, NPCs, projectiles and dust are all drawn later
///         in world space and stay perfectly still, which is what makes it read as the ground moving
///         rather than the camera shaking.
///     </para>
///     <para>
///         <b>Why this is split across two hooks.</b> The distortion passes bind render targets, and
///         binding one during the scene is destructive: <c>Main.screenTarget</c> is allocated with
///         the default <see cref="RenderTargetUsage.DiscardContents" /> (unlike <c>mapTarget</c>,
///         which vanilla explicitly allocates as <c>PreserveContents</c>), so restoring it with
///         <c>SetRenderTargets</c> mid-frame discards everything drawn up to that point -- sky,
///         backgrounds, walls, non-solid tiles -- and they come back black. vanilla never hits this
///         because it never re-binds the scene surface once the frame is underway.
///     </para>
///     <para>
///         So the passes run in <c>CheckMonoliths</c>, before anything has claimed the backbuffer,
///         and only the cheap texture swap happens during the scene.
///     </para>
///     <para>
///         <b>What that costs.</b> <c>CheckMonoliths</c> runs before the block that may re-render
///         <c>tileTarget</c> this frame, so on a refresh frame the snapshot is one lighting cycle
///         behind. <see cref="snapshotScenePos" /> is carried alongside it and swapped in too, so
///         the stand-in is drawn at the position its source was actually valid for -- the image is
///         momentarily old, never misaligned. Three frames in four the two agree exactly and the
///         override is a no-op.
///     </para>
///     <para>
///         Tile entities and the block-damage animations that <c>DoDraw_Tiles_Solid</c> draws after
///         the blit are not displaced -- they go straight to the screen in world space. In practice
///         that reads fine, but a chest sitting perfectly still inside a rippling wall is the one
///         artefact to expect.
///     </para>
/// </remarks>
internal sealed class TileShockwaveSystem : ModSystem
{
    /// <summary>
    ///     How many waves may distort the same frame.
    /// </summary>
    /// <remarks>
    ///     Each one is a separate full-target pass -- at 1080p that is a 2304x1464 blit apiece --
    ///     so this is a fill-rate ceiling, not a gameplay limit. Waves past it are still updated
    ///     and still expire on schedule; they just do not draw.
    /// </remarks>
    private const int MaxConcurrentWaves = 4;

    private static readonly List<ITileWave> waves = [];

    private static RenderTarget2D? front;
    private static RenderTarget2D? back;

    /// <summary>
    ///     The surface holding this frame's distorted layer, or null if the scene should be left
    ///     alone. Produced in <see cref="PrepareDistortion" />, consumed in
    ///     <see cref="SwapInDistortion" />.
    /// </summary>
    private static RenderTarget2D? distorted;

    /// <summary>
    ///     The value <see cref="Main.sceneTilePos" /> held when <see cref="distorted" /> was built.
    /// </summary>
    private static Vector2 snapshotScenePos;

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

        On_Main.CheckMonoliths += PrepareDistortion;
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
    private static void PrepareDistortion(On_Main.orig_CheckMonoliths orig)
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
        // who is zoomed in DOES get populated targets and DOES see this effect. A mod that needs
        // them unconditionally can clear Main.LightingEveryFrame, at the cost of retro lighting
        // no longer relighting every frame -- which is the entire point of those modes.
        if (Main.drawToScreen)
            return;

        var source = Main.instance.tileTarget;

        if (source is null || source.IsDisposed)
            return;

        var device = Main.instance.GraphicsDevice;

        if (!EnsureTarget(ref front, device, source.Width, source.Height))
            return;

        // Captured once. Every wave in this pass must be positioned against the same origin, and
        // the frame's tile re-render may move Main.sceneTilePos after this hook returns.
        var sceneOrigin = Main.sceneTilePos;

        Texture2D read = source;
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

            var write = applied % 2 == 0 ? front! : back!;

            ApplyPass(device, read, write, wave, sceneOrigin);

            read = write;
            applied++;
        }

        // Unbind to null rather than restoring previous bindings -- exactly what RenderTiles and
        // friends do. Nothing is bound this early in the frame, so there is nothing to put back.
        device.SetRenderTarget(null);

        if (applied == 0)
            return;

        distorted = (RenderTarget2D)read;
        snapshotScenePos = Main.sceneTilePos;
    }

    /// <summary>
    ///     Substitutes the distorted surface for the duration of the vanilla tile blit.
    /// </summary>
    private static void SwapInDistortion(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        var replacement = distorted;
        var originalTarget = Main.instance.tileTarget;

        if (replacement is null || Main.drawToScreen || originalTarget is null)
        {
            orig(self);
            return;
        }

        // If tiles were re-rendered after the snapshot and the camera has since travelled further
        // than the overscan, the snapshot no longer covers the screen and would show unpainted
        // edges. One frame without the effect beats one frame with holes in the terrain. This is
        // the same test vanilla uses to force an out-of-schedule rebuild.
        var wanted = Main.screenPosition - new Vector2(Main.offScreenRange);

        if (Math.Abs(snapshotScenePos.X - wanted.X) > Main.offScreenRange ||
            Math.Abs(snapshotScenePos.Y - wanted.Y) > Main.offScreenRange)
        {
            orig(self);
            return;
        }

        var originalScenePos = Main.sceneTilePos;

        Main.instance.tileTarget = replacement;
        Main.sceneTilePos = snapshotScenePos;

        // finally, not just trailing assignments: orig swallows its own draw exceptions but
        // DrawTileEntities inside it does not, and leaving one of our targets in that field would
        // have Terraria dispose it in ReleaseTargets on the next resolution change.
        try
        {
            orig(self);
        }
        finally
        {
            Main.instance.tileTarget = originalTarget;
            Main.sceneTilePos = originalScenePos;
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

    private static bool EnsureTarget(ref RenderTarget2D? target, GraphicsDevice device, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return false;

        if (target is { IsDisposed: false } && target.Width == width && target.Height == height)
            return true;

        target?.Dispose();

        // Matched to how vanilla allocates its own tile targets in InitTargets, except for the
        // usage: PreserveContents, because these are read on a later hook in the same frame than
        // the one that writes them.
        target = new RenderTarget2D(
            device,
            width,
            height,
            false,
            device.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        return true;
    }
}
