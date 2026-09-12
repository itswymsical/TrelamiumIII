using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     Where a bubble-shaped panel stands on screen: sized from its content, hung off a target
///     point by its tail, mirrored to face the viewer, and eased in from behind.
/// </summary>
/// <remarks>
///     <para>
///         The same rules the projected dialogue box works out for itself, lifted so a board and a
///         card can stand the way a bubble does without re-deriving them. The dialogue box keeps its
///         own copy: it also drives a response stack off the same numbers, and folding that in here
///         would make this a dialogue class with a different name.
///     </para>
///     <para>
///         Facing is decided in screen space, from where the target sits relative to the player,
///         with a deadband and an eased blend so a panel over someone standing on top of the player
///         does not flip back and forth with every idle step. See the dialogue box for the reasoning
///         in full.
///     </para>
/// </remarks>
internal sealed class BubblePlacer
{
    private static readonly float RestYaw = MathHelper.ToRadians(15f);
    private static readonly float RestPitch = MathHelper.ToRadians(-7f);
    private static readonly float RestRoll = MathHelper.ToRadians(-1.6f);
    private static readonly float EntryYaw = MathHelper.ToRadians(-38f);

    private const float ENTRY_DEPTH = 0.46f;
    private const float FACING_DEADBAND = 28f;

    private readonly float pixel;
    private readonly QuadWarp warp;

    private float facing = 1f;
    private float facingBlend = 1f;

    /// <param name="pixel">Screen pixels per texel, before UI scale.</param>
    /// <param name="seed">Picks the hand-made warp, so two panels never share the same crookedness.</param>
    public BubblePlacer(float pixel, int seed, float warpAmount = 5f)
    {
        this.pixel = pixel;
        warp = QuadWarp.Handmade(seed, warpAmount);
    }

    public float Scale => pixel * Main.UIScale;

    public BubbleGeometry Geometry { get; private set; }

    /// <summary>Centre of the sampled region on screen, in raw pixels.</summary>
    public Vector2 Centre { get; private set; }

    /// <summary>The sampled region's size on screen, in raw pixels.</summary>
    public Vector2 Size { get; private set; }

    /// <summary>Which way round the perspective stands this frame, eased: +1 default, -1 mirrored.</summary>
    public float FacingBlend => facingBlend;

    /// <summary>
    ///     Sizes the bubble and hangs it off <paramref name="target" /> (raw screen pixels), or
    ///     centres it on screen when there is no target.
    /// </summary>
    /// <param name="anchorGap">Screen pixels between the tail's point and the target.</param>
    /// <param name="snapFacing">Take the facing at once rather than easing into it -- for the first frame.</param>
    public void Place(Point content, int nameWidth, int nameHeight, Vector2? target, float anchorGap, bool snapFacing = false)
    {
        UpdateFacing(target, snapFacing);

        // Two passes: the first sizes the bubble so the target can be compared against its centre,
        // the second rebuilds it with the tail actually leaning at the target.
        Geometry = PanelStyle.Measure(content, nameWidth, nameHeight, 0f);
        PlacePanel(target, anchorGap);
        Geometry = PanelStyle.Measure(content, nameWidth, nameHeight, TailAim(target));
        PlacePanel(target, anchorGap);
    }

    private void UpdateFacing(Vector2? target, bool snap)
    {
        if (target.HasValue)
        {
            Vector2 player = Vector2.Transform(
                Main.LocalPlayer.Center - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

            float offset = target.Value.X - player.X;

            if (MathF.Abs(offset) > FACING_DEADBAND)
                facing = offset < 0f ? -1f : 1f;
        }

        facingBlend = snap ? facing : MathHelper.Lerp(facingBlend, facing, 0.12f);
    }

    private void PlacePanel(Vector2? target, float anchorGap)
    {
        BubbleGeometry geometry = Geometry;
        float scale = Scale;

        Size = new Vector2(geometry.Used.X, geometry.Used.Y) * scale;

        if (!target.HasValue)
        {
            Centre = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            return;
        }

        // The tail's point is what has to meet the target, and it is not the bottom of the region:
        // the border and the ghost hang past it.
        float apexFromBottom = (geometry.Used.Y - geometry.TailApex.Y) * scale;
        float bottom = target.Value.Y - anchorGap + apexFromBottom;

        // Centred on the body rather than the region, which is wider on the left by the ghost margin.
        float bodyOffset = (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * scale;

        Centre = new Vector2(
            OnScreen(target.Value.X - bodyOffset, Size.X * 0.5f, Main.screenWidth),
            OnScreen(bottom - Size.Y * 0.5f, Size.Y * 0.5f, Main.screenHeight));
    }

    /// <summary>
    ///     Keeps one axis of the panel inside the window, centring instead of clamping when the panel
    ///     is wider than the space -- a plain clamp would be handed a minimum above its maximum.
    /// </summary>
    public static float OnScreen(float centre, float half, float extent)
    {
        const float EDGE = 8f;

        float low = half + EDGE;
        float high = extent - half - EDGE;

        return high < low ? extent * 0.5f : Math.Clamp(centre, low, high);
    }

    private float TailAim(Vector2? target)
    {
        if (!target.HasValue)
            return 0f;

        BubbleGeometry geometry = Geometry;
        float scale = Scale;

        float bodyCentre = Centre.X + (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * scale;
        float reach = MathF.Max(geometry.Body.Width * 0.5f * scale, 1f);

        return Math.Clamp((target.Value.X - bodyCentre) / reach, -1f, 1f);
    }

    /// <summary>
    ///     Eased 0 to 1 with an overshoot on the way in, so the panel arrives slightly past its rest
    ///     angle and settles back. Closing runs the curve without the overshoot.
    /// </summary>
    public static float Entrance(float presence, bool closing)
    {
        float t = Math.Clamp(presence, 0f, 1f);

        if (closing)
            return t * t;

        float back = 1f - t;
        return 1f + (2.2f * back * back * back - 3.2f * back * back) * back;
    }

    /// <summary>The quad for this frame: at rest with a slow drift, or part-way through arriving.</summary>
    public QuadPlacement Placement(float presence, bool closing, float farShade = 0.22f)
    {
        float entrance = Entrance(presence, closing);
        float settle = 1f - entrance;
        float time = Main.GlobalTimeWrappedHourly;

        return new QuadPlacement
        {
            Center = Centre,
            Size = Size * MathHelper.Lerp(0.74f, 1f, entrance),
            Yaw = (RestYaw + settle * EntryYaw) * facingBlend + MathF.Sin(time * 0.7f) * 0.022f,
            Pitch = RestPitch + MathF.Sin(time * 0.53f) * 0.016f,
            Roll = (RestRoll + settle * 0.12f) * facingBlend,
            Depth = settle * ENTRY_DEPTH * PanelCamera.Distance,
            Warp = warp,
            FarShade = farShade,
        };
    }
}
