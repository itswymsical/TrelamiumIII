using Terraria.Audio;
using Terraria.GameInput;
using TrelamiumIII.Utilities;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Core.UI.World;

/// <summary>How an indicator moves to draw the eye.</summary>
public enum AnimationType
{
    /// <summary>A steady idle bob. No burst on hover.</summary>
    Hover,

    /// <summary>Bobs, and rocks side to side once when first hovered.</summary>
    Wag,

    /// <summary>Bobs, and spins through a full turn once when first hovered.</summary>
    Swivel,

    /// <summary>Bobs, and jitters once when first hovered.</summary>
    Shake
}

/// <summary>
///     A small icon that floats at a world position and reacts to
///     the cursor. See <see cref="WorldPanel" />.
/// </summary>
/// <remarks>
///     Unlike the panel, the icon scales with the camera so it sits in the scene rather than on top
///     of it, which is how it read before this was a <see cref="IWorldUI" />.
/// </remarks>
public class WorldIndicator : IWorldUI
{
    #region Tuning
    private const float HOVER_FADE_SPEED = 0.12f;
    private const float HIDE_FADE_DURATION = 0.3f;

    private const float WAG_DURATION = 0.8f;
    private const float WAG_INTENSITY = 0.25f;
    private const float SWIVEL_DURATION = 1.2f;
    private const float SWIVEL_INTENSITY = 0.4f;
    private const float SHAKE_DURATION = 0.4f;
    private const float SHAKE_INTENSITY = 0.15f;
    #endregion

    #region State
    private readonly AnimationType? _instanceAnimation;

    private Entity? _tracked;
    private Vector2 _trackOffset;

    /// <summary>Continuous clock driving the idle bob. Never resets, so the bob never stutters.</summary>
    private float _bobTime;

    /// <summary>Seconds into the current one-shot burst. Only meaningful while <see cref="_bursting" />.</summary>
    private float _burstTime;
    private bool _bursting;

    private float _hoverFade;

    private bool _hidden;
    private bool _fading;
    private float _hideFade = 1f;
    private float _fadeTime;
    #endregion

    public Vector2 WorldAnchor { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Extra offset applied on top of <see cref="WorldAnchor" />, driven by the animation.</summary>
    public Vector2 Offset { get; private set; }

    public bool IsHovering { get; private set; }
    public bool WasHovering { get; private set; }
    public bool JustHovered => IsHovering && !WasHovering;
    public bool JustStoppedHovering => !IsHovering && WasHovering;

    public bool IsHidden => _hidden;

    /// <summary>0 when fully hidden, 1 when fully shown. Multiplied into everything drawn.</summary>
    public float Opacity => _hideFade;

    public virtual AnimationType AnimationStyle => _instanceAnimation ?? AnimationType.Hover;

    public event Action? OnClick;

    private bool IsTracking => _tracked?.IsActive() == true;

    public WorldIndicator(Vector2 worldAnchor, int width, int height, AnimationType? animationType = null)
    {
        WorldAnchor = worldAnchor;
        Width = width;
        Height = height;
        _instanceAnimation = animationType;
    }

    /// <summary>
    ///     Pins the indicator to an entity. NPCs anchor from their <see cref="Entity.Top" /> so the
    ///     icon sits over the head regardless of hitbox height; everything else uses its position.
    /// </summary>
    public void TrackEntity(Entity entity, Vector2 offset)
    {
        _tracked = entity;
        _trackOffset = offset;

        if (IsTracking)
            UpdateTrackedPosition();
    }

    private void UpdateTrackedPosition()
        => WorldAnchor = (_tracked is NPC npc ? npc.Top : _tracked!.position) + _trackOffset;

    #region Visibility
    /// <summary>Fades the indicator out. It stays registered but stops drawing and taking clicks.</summary>
    public void Hide()
    {
        if (_hidden)
            return;

        _hidden = true;
        _fading = true;
        _fadeTime = 0f;
    }

    /// <summary>Fades the indicator back in.</summary>
    public void Show()
    {
        if (!_hidden)
            return;

        _hidden = false;
        _fading = true;
        _fadeTime = 0f;
    }

    private void UpdateHideFade()
    {
        if (!_fading)
            return;

        _fadeTime += 1f / 60f;

        float progress = Math.Min(_fadeTime / HIDE_FADE_DURATION, 1f);
        float eased = EaseFunction.EaseCubicOut.Ease(progress);

        _hideFade = _hidden ? 1f - eased : eased;

        if (progress >= 1f)
        {
            _fading = false;
            _hideFade = _hidden ? 0f : 1f;
        }
    }
    #endregion

    public void Update(GameTime gameTime)
    {
        _bobTime += 0.1f;

        // A tracked entity that has gone means the indicator has nothing left to mark.
        if (_tracked != null && !_tracked.IsActive())
        {
            IsActive = false;
            return;
        }

        if (IsTracking)
            UpdateTrackedPosition();

        UpdateHideFade();

        WasHovering = IsHovering;
        IsHovering = !_hidden && _hideFade > 0f && CheckHovering();

        UpdateAnimation();
        PostUpdate();
    }

    /// <summary>Hook for subclasses to react to state changes each tick.</summary>
    protected virtual void PostUpdate() { }

    #region Animation
    private void UpdateAnimation()
    {
        if (IsHovering)
        {
            _hoverFade = Math.Min(_hoverFade + HOVER_FADE_SPEED, 1f);

            if (JustHovered)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                StartBurst();
            }
        }
        else
        {
            _hoverFade = Math.Max(_hoverFade - HOVER_FADE_SPEED, 0f);
        }

        if (_bursting)
        {
            _burstTime += 1f / 60f;

            if (_burstTime >= BurstDuration)
            {
                _bursting = false;
                _burstTime = 0f;
            }
        }

        Offset = ComputeOffset();
    }

    private float BurstDuration => AnimationStyle switch
    {
        AnimationType.Wag => WAG_DURATION,
        AnimationType.Swivel => SWIVEL_DURATION,
        AnimationType.Shake => SHAKE_DURATION,
        _ => 0f
    };

    protected virtual void StartBurst()
    {
        if (AnimationStyle == AnimationType.Hover)
            return;

        _bursting = true;
        _burstTime = 0f;
    }

    private Vector2 ComputeOffset()
    {
        float amplitude = AnimationStyle switch
        {
            AnimationType.Wag => 2.5f,
            AnimationType.Swivel => 3.5f,
            AnimationType.Shake => 2f,
            _ => 3f
        };

        var offset = new Vector2(0f, (float)Math.Sin(_bobTime * 0.8f) * amplitude);

        if (AnimationStyle == AnimationType.Shake && _bursting)
        {
            // Decays over the burst so the jolt settles rather than stopping dead.
            float intensity = SHAKE_INTENSITY * (1f - _burstTime / SHAKE_DURATION) * 15f;
            offset += new Vector2(
                (float)(Main.rand.NextDouble() - 0.5) * intensity,
                (float)(Main.rand.NextDouble() - 0.5) * intensity);
        }

        return offset;
    }

    public virtual float GetAnimationRotation()
    {
        if (!_bursting)
            return 0f;

        switch (AnimationStyle)
        {
            case AnimationType.Wag:
                float wag = _burstTime / WAG_DURATION;
                return (float)Math.Sin(wag * Math.PI * 5) * (WAG_INTENSITY * (1f - wag * wag));

            case AnimationType.Swivel:
                float swivel = _burstTime / SWIVEL_DURATION;
                float intensity = SWIVEL_INTENSITY * (float)Math.Sin(swivel * Math.PI);
                return swivel * (float)Math.PI * 2f * intensity;

            default:
                return 0f;
        }
    }

    /// <summary>
    ///     Idle-to-hover swell. Eased by the hover fade rather than snapped, so the icon grows into
    ///     the hover state instead of popping.
    /// </summary>
    public virtual float GetAnimationScale()
    {
        float hovered = AnimationStyle switch
        {
            AnimationType.Wag => 1.2f,
            AnimationType.Swivel => 1.1f,
            AnimationType.Shake => 1.05f,
            _ => 1.15f
        };

        return MathHelper.Lerp(1f, hovered, _hoverFade);
    }

    public virtual float GetHoverOpacity() => _hoverFade;
    #endregion

    #region Interaction
    /// <summary>
    ///     Draw scale in UI space. The icon should end up <see cref="WorldUI.ZoomScale" /> times its
    ///     texture size on screen so it sits in the scene, but the layer already multiplies by the
    ///     UI scale, so that has to be divided back out.
    /// </summary>
    protected float IconScale => GetAnimationScale() * WorldUI.ZoomScale / Main.UIScale;

    /// <summary>The icon's box in UI space.</summary>
    protected Rectangle Bounds()
    {
        Vector2 center = WorldUI.ToUI(WorldAnchor + Offset);

        float w = Width * IconScale;
        float h = Height * IconScale;

        return new Rectangle(
            (int)(center.X - w / 2f),
            (int)(center.Y - h / 2f),
            (int)w,
            (int)h);
    }

    public bool CheckHovering()
    {
        // A focused panel owns the cursor; indicators underneath it must not light up or steal the
        // click that is meant for one of its buttons.
        if (!IsActive || PlayerInput.IgnoreMouseInterface || WorldUISystem.HasOpenPanel)
            return false;

        return Bounds().Contains(WorldUI.MouseUI.ToPoint());
    }

    private void HandleClick()
    {
        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease)
            return;

        Main.mouseLeftRelease = false;
        OnClick?.Invoke();
    }
    #endregion

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        if (!IsActive || _hideFade <= 0f)
            return;

        if (IsHovering)
            HandleClick();

        DrawIcon(spriteBatch, WorldUI.ToUI(WorldAnchor + Offset), _hideFade);
    }

    /// <summary>
    ///     Draws the marker itself. <paramref name="uiPosition" /> is the anchor in UI space and is
    ///     where the icon should be centred; <paramref name="opacity" /> already carries the
    ///     hide/show fade.
    /// </summary>
    protected virtual void DrawIcon(SpriteBatch spriteBatch, Vector2 uiPosition, float opacity) { }
}
