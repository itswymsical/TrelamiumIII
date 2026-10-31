using Terraria.Graphics;

namespace TrelamiumIII.Core.Graphics.Cinematics.Camera;

public class ZoomHandler : ModSystem
{
    private static int zoomTimer;
    public static float zoomOverride = 1;
    private static float oldZoom = 1;
    private static int maxTimer = 0;

    private static int flatZoomTimer = 0;
    private static float oldFlatZoom = 0;
    private static float oldFlatZoomTarget = 0;
    private static float flatZoomTarget = 0;
    private static float flatZoom = 0;

    private static float extraZoomTarget = 1;

    public static float ExtraZoomTarget
    {
        get => extraZoomTarget;

        private set
        {
            oldZoom = zoomOverride;
            zoomTimer = 0;
            extraZoomTarget = value;
        }
    }

    public static float ClampedExtraZoomTarget => Math.Min(1, ExtraZoomTarget);

    public override void Load()
    {
        if (!Main.dedServ)
            On_Main.DoDraw_UpdateCameraPosition += PrimeFrameZoom;
    }

    public override void Unload()
    {
        if (!Main.dedServ)
            On_Main.DoDraw_UpdateCameraPosition -= PrimeFrameZoom;
    }

    /// <summary>
    ///     Settles this frame's zoom and puts it on <see cref="Main.GameViewMatrix" /> right after the
    ///     camera moves, ahead of <c>Main.CheckMonoliths</c>.
    /// </summary>
    /// <remarks>
    ///     Vanilla only writes the frame's zoom after <c>CheckMonoliths</c> has run, so every
    ///     render target built there -- fields, tile condition canvases, shockwaves -- was drawn with
    ///     last frame's zoom and then composited with this frame's. While the zoom eases the two
    ///     differ by several texels a frame, and the targets jitter.
    ///     <see cref="ModifyTransformMatrix" /> then writes the same value again, so nothing later in
    ///     the frame sees a difference.
    /// </remarks>
    private static void PrimeFrameZoom(On_Main.orig_DoDraw_UpdateCameraPosition orig)
    {
        orig();

        UpdateZoom();
        Main.GameViewMatrix.Zoom = ScaleVector;

        // A new zoom only marks the matrix dirty, and it rebuilds against whatever viewport is bound
        // when it is next read. Read now, while that is still the back buffer, rather than from inside
        // some CheckMonoliths pass with a half-size target bound.
        _ = Main.GameViewMatrix.TransformationMatrix;
    }

    public override void PostUpdateEverything()
    {
        TickZoom();
    }

    public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
    {
        Transform.Zoom = ScaleVector;
    }

    public static void AddFlatZoom(float value)
    {
        flatZoomTarget += value;
    }

    public static void SetZoomAnimation(float zoomValue, int maxTimer = 60)
    {
        ExtraZoomTarget = zoomValue;
        ZoomHandler.maxTimer = maxTimer;
    }

    public static Vector2 ScaleVector => new(zoomOverride + flatZoom, zoomOverride + flatZoom);

    public static void TickZoom()
    {
        if (zoomTimer < maxTimer)
            zoomTimer++;

        if (flatZoomTimer < 30)
            flatZoomTimer++;

        if (flatZoomTarget != oldFlatZoomTarget)
        {
            flatZoomTimer = 0;
            oldFlatZoom = flatZoom;
        }

        flatZoom = Vector2.SmoothStep(new Vector2(oldFlatZoom, 0), new Vector2(flatZoomTarget, 0), flatZoomTimer / 30f).X;

        oldFlatZoomTarget = flatZoomTarget;
        flatZoomTarget = 0;
    }

    public static void UpdateZoom()
    {
        zoomOverride = Vector2.SmoothStep(new Vector2(oldZoom, 0), new Vector2(extraZoomTarget, 0), zoomTimer / (float)maxTimer).X;

        if (zoomOverride == Main.GameZoomTarget)
            oldZoom = Main.GameZoomTarget;

        if (zoomTimer == maxTimer && extraZoomTarget == Main.GameZoomTarget)
            maxTimer = 0;

        if (maxTimer == 0)
            zoomOverride = Main.GameZoomTarget;
    }
}
