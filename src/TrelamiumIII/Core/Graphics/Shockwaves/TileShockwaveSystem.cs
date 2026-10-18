using System.Collections.Generic;
using Terraria.Graphics.Shaders;

namespace TrelamiumIII.Core.Graphics.Shockwaves;

/// <summary>
///     Distorts the solid tile layer with <see cref="ITileWave" />s by swapping a distorted copy into
///     <c>Main.instance.tileTarget</c> for the draw call that blits it.
/// </summary>
internal sealed class TileShockwaveSystem : ModSystem
{
    private const int MaxConcurrentWaves = 4;

    private static readonly List<ITileWave> waves = [];

    private static RenderTarget2D? front;
    private static RenderTarget2D? back;

    /// <summary>This frame's distorted layer, or null to leave the scene alone.</summary>
    private static RenderTarget2D? distorted;

    /// <summary><see cref="Main.sceneTilePos" /> when <see cref="distorted" /> was built.</summary>
    private static Vector2 snapshotScenePos;

    private static bool loaded;

    /// <summary>Starts a wave; ignored on a server. Waves composite in spawn order.</summary>
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

        // Mods unload on a worker thread, and FNA3D only frees graphics resources on the main one.
        RenderTarget2D? oldFront = front;
        RenderTarget2D? oldBack = back;
        front = null;
        back = null;

        Main.QueueMainThreadAction(() =>
        {
            oldFront?.Dispose();
            oldBack?.Dispose();
        });
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

    /// <summary>Runs the displacement passes into the ping-pong targets before the scene draws.</summary>
    private static void PrepareDistortion(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        distorted = null;

        if (!loaded || waves.Count == 0)
            return;

        // No tile targets exist under drawToScreen (Retro/Trippy lighting without zoom).
        if (Main.drawToScreen)
            return;

        var source = Main.instance.tileTarget;

        if (source is null || source.IsDisposed)
            return;

        var device = Main.instance.GraphicsDevice;

        if (!EnsureTarget(ref front, device, source.Width, source.Height))
            return;

        // Captured once: the frame's tile re-render may move sceneTilePos after this hook.
        var sceneOrigin = Main.sceneTilePos;

        Texture2D read = source;
        int applied = 0;

        foreach (var wave in waves)
        {
            if (applied >= MaxConcurrentWaves)
                break;

            if (!wave.Intersects(source, sceneOrigin))
                continue;

            // Even passes write front, odd passes write back; back is allocated on the second visible wave.
            if (applied == 1 && !EnsureTarget(ref back, device, source.Width, source.Height))
                break;

            var write = applied % 2 == 0 ? front! : back!;

            ApplyPass(device, read, write, wave, sceneOrigin);

            read = write;
            applied++;
        }

        device.SetRenderTarget(null);

        if (applied == 0)
            return;

        distorted = (RenderTarget2D)read;
        snapshotScenePos = Main.sceneTilePos;
    }

    /// <summary>Substitutes the distorted surface for the duration of the vanilla tile blit.</summary>
    private static void SwapInDistortion(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        var replacement = distorted;
        var originalTarget = Main.instance.tileTarget;

        if (replacement is null || Main.drawToScreen || originalTarget is null)
        {
            orig(self);
            return;
        }

        // Skip the frame when the camera has moved past the snapshot's overscan.
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

        // Restored in finally: a target left in tileTarget would be disposed by ReleaseTargets.
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

        var shader = wave.PrepareShader(write, sceneOrigin);

        // CullNone: Main.Rasterizer culls this quad when gravity flips the view.
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

        // PreserveContents: written in CheckMonoliths, read later in the same frame.
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
