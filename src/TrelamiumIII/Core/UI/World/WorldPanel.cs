using Microsoft.Xna.Framework.Input;
using Terraria.UI;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.World;

/// <summary>
///     A panel pinned to a spot in the world: it tracks its anchor through camera zoom and pan, but
///     draws at a constant size so its text stays readable however far the camera is pulled back.
/// </summary>
/// <remarks>
///     A real <see cref="UIState" />, so hover states, click routing and cursor blocking all come
///     from vanilla rather than being re-derived from <see cref="Main.MouseScreen" />. Being an
///     <see cref="IWorldUI" /> on top of that is what lets <see cref="WorldUISystem" /> hold it.
/// </remarks>
public abstract class WorldPanel : UIState, IWorldUI
{
    /// <summary>Seconds the open and close fades take.</summary>
    private const float FADE_DURATION = 0.18f;

    /// <summary>Keeps the panel off the very edge of the viewport, in UI-space pixels.</summary>
    protected const float SCREEN_MARGIN = 12f;

    private PanelBody? _body;
    private bool _closing;
    private float _fade;

    /// <summary>The panel's own box. Subclasses append their widgets to this, not to the state.</summary>
    protected UIElement? Body => _body;

    /// <summary>Fixed size of the panel in UI-space pixels.</summary>
    protected abstract Vector2 PanelSize { get; }

    /// <summary>How far (in world pixels) the player may stray before the panel closes itself.</summary>
    protected virtual float MaxRange => 25 * 16f;

    /// <summary>Where the panel points. Usually re-read from a tracked entity every tick.</summary>
    public Vector2 WorldAnchor { get; protected set; }

    /// <summary>Runs 0 to 1 as the panel opens and back to 0 as it closes.</summary>
    public float Opacity => EaseFunction.EaseCubicOut.Ease(_fade);

    /// <summary>False once the close fade has finished; <see cref="WorldUISystem" /> drops it then.</summary>
    public bool IsActive { get; private set; } = true;

    public override void OnInitialize()
    {
        _body = new PanelBody(this)
        {
            Width = StyleDimension.FromPixels(PanelSize.X),
            Height = StyleDimension.FromPixels(PanelSize.Y)
        };

        BuildPanel(_body);
        Append(_body);
    }

    /// <summary>Builds the panel's contents. <paramref name="body" /> is the panel's own box.</summary>
    protected abstract void BuildPanel(UIElement body);

    /// <summary>Draws the panel's backing art, beneath its widgets.</summary>
    protected abstract void DrawChrome(SpriteBatch spriteBatch, Rectangle bounds, float opacity);

    /// <summary>Called once the panel has been focused.</summary>
    public virtual void OnOpened() { }

    /// <summary>Called when the panel is dropped, by its own request or otherwise.</summary>
    public virtual void OnClosed() { }

    /// <summary>Starts the close fade. The panel retires itself once it finishes.</summary>
    public void RequestClose()
    {
        if (_closing)
            return;

        _closing = true;
        OnCloseRequested();
    }

    protected virtual void OnCloseRequested() { }

    /// <summary>
    ///     Whether this frame's click landed outside the panel. Subclasses that put clickable
    ///     things outside <see cref="Body" /> can widen this.
    /// </summary>
    protected virtual bool ClickedAway()
        => _body != null && !_body.GetDimensions().ToRectangle().Contains(WorldUI.MouseUI.ToPoint());

    public override void Update(GameTime gameTime)
    {
        UpdateFade();

        if (!IsActive)
            return;

        UpdateAnchor();
        BlockCursor();

        if (!_closing && ShouldClose())
            RequestClose();

        base.Update(gameTime);
    }

    private void UpdateFade()
    {
        float step = (1f / 60f) / FADE_DURATION;

        if (_closing)
        {
            _fade -= step;

            if (_fade <= 0f)
            {
                _fade = 0f;
                IsActive = false;
            }

            return;
        }

        _fade = Math.Min(_fade + step, 1f);
    }

    /// <summary>Escape, a click outside, or walking off all dismiss the panel.</summary>
    private bool ShouldClose()
    {
        if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
            return true;

        if (Main.mouseLeft && Main.mouseLeftRelease && ClickedAway())
            return true;

        return Vector2.DistanceSquared(Main.LocalPlayer.Center, WorldAnchor) > MaxRange * MaxRange;
    }

    /// <summary>
    ///     Parks the panel over its anchor in UI coordinates and clamps it to the viewport, so a
    ///     panel anchored near an edge slides into view rather than hanging off it.
    /// </summary>
    private void UpdateAnchor()
    {
        if (_body == null)
            return;

        TrackAnchor();

        Vector2 anchor = WorldUI.ToUI(WorldAnchor);
        Vector2 screen = WorldUI.ScreenSizeUI;

        float width = _body.Width.Pixels;
        float height = _body.Height.Pixels;

        float maxX = Math.Max(SCREEN_MARGIN, screen.X - width - SCREEN_MARGIN);
        float maxY = Math.Max(SCREEN_MARGIN, screen.Y - height - SCREEN_MARGIN);

        // Centred horizontally on the anchor and sitting just above it, the way a speech bubble
        // hangs off the thing that is speaking.
        float left = MathHelper.Clamp(anchor.X - width * 0.5f, SCREEN_MARGIN, maxX);
        float top = MathHelper.Clamp(anchor.Y - height - 8f, SCREEN_MARGIN, maxY);

        _body.Left.Set(left, 0f);
        _body.Top.Set(top, 0f);
        _body.Recalculate();
    }

    /// <summary>Hook for re-reading <see cref="WorldAnchor" /> from a moving target each tick.</summary>
    protected virtual void TrackAnchor() { }

    private void BlockCursor()
    {
        if (_body != null && _body.GetDimensions().ToRectangle().Contains(WorldUI.MouseUI.ToPoint()))
            Main.LocalPlayer.mouseInterface = true;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!IsActive || Opacity <= 0f)
            return;

        base.Draw(spriteBatch);
    }

    /// <summary>
    ///     The panel's box. Exists so the chrome draws underneath the widgets and so hovering
    ///     anywhere on the panel blocks the cursor from reaching the world behind it.
    /// </summary>
    private sealed class PanelBody(WorldPanel owner) : UIElement
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            owner.DrawChrome(spriteBatch, GetDimensions().ToRectangle(), owner.Opacity);

            if (IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        }
    }
}
