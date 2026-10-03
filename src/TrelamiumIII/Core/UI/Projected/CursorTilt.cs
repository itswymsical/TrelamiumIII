using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     Turns a projected quad after the cursor while it is over the face: the side under the cursor recedes and the
///     quad lifts toward the camera, easing back to rest when the cursor leaves.
/// </summary>
internal sealed class CursorTilt
{
    private static readonly float MaxYaw = MathHelper.ToRadians(7f);
    private static readonly float MaxPitch = MathHelper.ToRadians(6f);

    /// <summary>Pull toward the camera at full hover, as a fraction of <see cref="PanelCamera.Distance" />.</summary>
    private const float LIFT = 0.025f;

    /// <summary>Per-tick lerp toward the sampled target.</summary>
    private const float EASE = 0.14f;

    /// <summary>Face fraction past each edge that still counts as over it, so the receding edge cannot drop the hover.</summary>
    private const float MARGIN = 0.08f;

    private Vector2 targetLean;
    private float targetHover;

    private Vector2 lean;
    private float hover;

    /// <summary>
    ///     Reads <paramref name="screen" /> against <paramref name="rest" />, the placement before <see cref="Apply" />.
    ///     Sampling the untilted quad keeps the target independent of the tilt it produces.
    /// </summary>
    public void Sample(in QuadPlacement rest, Vector2 screen, bool enabled)
    {
        targetLean = Vector2.Zero;
        targetHover = 0f;

        if (!enabled || rest.Size.X <= 0f || rest.Size.Y <= 0f || !ProjectedHitTest.TryProject(rest, screen, out Vector2 uv))
            return;

        if (uv.X < -MARGIN || uv.X > 1f + MARGIN || uv.Y < -MARGIN || uv.Y > 1f + MARGIN)
            return;

        targetLean = Vector2.Clamp(uv * 2f - Vector2.One, -Vector2.One, Vector2.One);
        targetHover = 1f;
    }

    public void Update()
    {
        lean = Vector2.Lerp(lean, targetLean, EASE);
        hover = MathHelper.Lerp(hover, targetHover, EASE);
    }

    public QuadPlacement Apply(QuadPlacement placement)
    {
        placement.Yaw -= lean.X * MaxYaw;
        placement.Pitch += lean.Y * MaxPitch;
        placement.Depth -= hover * LIFT * PanelCamera.Distance;

        return placement;
    }
}
