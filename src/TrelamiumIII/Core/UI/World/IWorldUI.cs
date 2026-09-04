namespace TrelamiumIII.Core.UI.World;

/// <summary>
///     UI that lives at a point in the world. Used for Job Indicators.
/// </summary>
public interface IWorldUI
{
    /// <summary>Where in the world (pixels) this UI hangs from.</summary>
    Vector2 WorldAnchor { get; }

    /// <summary>
    ///     False once the UI has outlived its purpose (Provider NPC despawned, job taken, etc.).
    /// </summary>
    bool IsActive { get; }

    void Update(GameTime gameTime);

    void Draw(SpriteBatch spriteBatch);
}

/// <summary>
///     Projection helpers shared by everything that draws at a world position. These are statics
///     rather than members of a base class so that a <see cref="Terraria.UI.UIState" /> can be a
///     world UI without inheriting from anything of ours.
/// </summary>
public static class WorldUI
{
    /// <summary>
    ///     World pixel to raw screen pixel, through the same matrix the world itself is drawn with,
    ///     so a projected point stays glued to the thing it marks as the camera zooms and pans.
    /// </summary>
    public static Vector2 ToScreen(Vector2 world)
        => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

    /// <summary>
    ///     World pixel to UI-space coordinate. <see cref="WorldUISystem" /> registers its layer as
    ///     <see cref="Terraria.UI.InterfaceScaleType.UI" />, so the UI scale matrix is already
    ///     applied at draw time and positions must be handed over divided by it — otherwise
    ///     everything drifts off its anchor at any UI scale but 1.0.
    /// </summary>
    public static Vector2 ToUI(Vector2 world) => ToScreen(world) / Main.UIScale;

    /// <summary>The cursor in the same UI space as <see cref="ToUI" />, for hit-testing against it.</summary>
    public static Vector2 MouseUI => Main.MouseScreen / Main.UIScale;

    /// <summary>Screen size in UI space, for clamping a panel to the viewport.</summary>
    public static Vector2 ScreenSizeUI => new(Main.screenWidth / Main.UIScale, Main.screenHeight / Main.UIScale);

    /// <summary>
    ///     Current camera zoom. World UI that should grow with the scene multiplies its scale by
    ///     this; UI that should stay readable (panels, text) ignores it.
    /// </summary>
    public static float ZoomScale => Main.GameViewMatrix.Zoom.X;
}
