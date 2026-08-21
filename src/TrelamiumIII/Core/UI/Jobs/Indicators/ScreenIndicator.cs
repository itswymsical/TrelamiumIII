using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.Jobs.Indicators;

public enum AnimationType
{
    Hover,
    Wag,
    Swivel,
    Shake
}

public class ScreenIndicator : IInGameNotification
{
    #region Properties
    public Vector2 WorldPosition { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Entity trackingEntity;
    public Vector2 trackingOffset;
    private bool isTracking => trackingEntity != null && trackingEntity.active;

    public bool IsHovering { get; private set; }
    public bool WasHovering { get; private set; }
    public bool JustHovered => IsHovering && !WasHovering;
    public bool JustStoppedHovering => !IsHovering && WasHovering;
    public bool IsActive { get; set; } = true;

    public float AnimationTimer { get; private set; } = 0f;
    public Vector2 Offset { get; set; } = Vector2.Zero;

    public event Action OnClick;

    // Animation system
    public virtual AnimationType AnimationStyle => instanceAnimationType ?? AnimationType.Hover;
    private AnimationType? instanceAnimationType = null;
    protected float hoverFadeIn = 0f;
    protected float animationTimer = 0f;
    protected bool animationActive = false;

    // Hide/unhide system
    private bool isHidden = false;
    private bool isTransitioning = false;
    private float hideFade = 1f; // 0 = fully hidden, 1 = fully visible
    private float fadeTimer = 0f;

    public bool IsHidden => isHidden;
    public bool IsTransitioning => isTransitioning;
    public float HideFadeOpacity => hideFade;

    public bool ShouldBeRemoved => throw new NotImplementedException();

    private const float HOVER_FADE_SPEED = 0.12f;
    private const float WAG_DURATION = 0.8f;
    private const float WAG_INTENSITY = 0.25f;
    private const float SWIVEL_DURATION = 1.2f;
    private const float SWIVEL_INTENSITY = 0.4f;
    private const float SHAKE_DURATION = 0.4f;
    private const float SHAKE_INTENSITY = 0.15f;
    private const float HIDE_FADE_DURATION = 0.3f; // Duration in seconds for hide/unhide fade
    #endregion

    public delegate void DrawDelegate(SpriteBatch spriteBatch, Vector2 screenPos, float opacity);
    public DrawDelegate OnDrawWorld;

    public ScreenIndicator(Vector2 worldPosition, int width, int height, AnimationType? animationType = null)
    {
        WorldPosition = worldPosition;
        Width = width;
        Height = height;
        instanceAnimationType = animationType;
    }

    public void SetAnimationType(AnimationType animationType)
    {
        instanceAnimationType = animationType;
    }

    public void TrackEntity(Entity entity, Vector2 offset)
    {
        trackingEntity = entity;
        trackingOffset = offset;

        if (entity != null && entity.active)
        {
            UpdateTrackingPosition();
        }
    }

    protected virtual Rectangle GetInteractionBounds()
    {
        var screenPos = GetScreenPosition();

        return new Rectangle(
            (int)screenPos.X - Width / 2,
            (int)screenPos.Y - Height / 2,
            Width,
            Height
        );
    }

    /// <summary>
    /// Hides the indicator with a smooth fade-out effect.
    /// While hidden, the indicator won't render or respond to clicks, but remains in the world.
    /// </summary>
    public void Hide()
    {
        if (isHidden)
            return;

        isHidden = true;
        isTransitioning = true;
        fadeTimer = 0f;
    }

    /// <summary>
    /// Unhides the indicator with a smooth fade-in effect.
    /// </summary>
    public void Unhide()
    {
        if (!isHidden)
            return;

        isHidden = false;
        isTransitioning = true;
        fadeTimer = 0f;
    }

    /// <summary>
    /// Alias for Unhide(). Shows the indicator with a smooth fade-in effect.
    /// </summary>
    public void Show() => Unhide();

    private void UpdateTrackingPosition()
    {
        if (trackingEntity is NPC npc)
        {
            WorldPosition = npc.Top + trackingOffset;
        }
        else
        {
            WorldPosition = trackingEntity.position + trackingOffset;
        }
    }

    public virtual void Update()
    {
        AnimationTimer += 0.1f;

        if (trackingEntity != null && !trackingEntity.active)
        {
            IsActive = false;
            return;
        }

        if (isTracking)
            UpdateTrackingPosition();

        UpdateHideFade();

        if (!isHidden || hideFade > 0f)
        {
            WasHovering = IsHovering;
            IsHovering = !isHidden && CheckHovering();

            UpdateAnimations();

        }
        else
        {
            WasHovering = false;
            IsHovering = false;
        }

        PostUpdate();
    }

    private void UpdateHideFade()
    {
        if (!isTransitioning)
            return;

        fadeTimer += 1f / 60f;

        var progress = Math.Min(fadeTimer / HIDE_FADE_DURATION, 1f);

        var easedProgress = EaseFunction.EaseCubicOut.Ease(progress);

        if (isHidden)
        {
            hideFade = 1f - easedProgress;
        }
        else
        {
            hideFade = easedProgress;
        }

        if (progress >= 1f)
        {
            isTransitioning = false;
            hideFade = isHidden ? 0f : 1f;
        }
    }

    protected virtual void UpdateAnimations()
    {
        if (IsHovering && hoverFadeIn < 1f)
        {
            hoverFadeIn += HOVER_FADE_SPEED;
            if (hoverFadeIn > 1f) hoverFadeIn = 1f;

            if (JustHovered)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                StartAnimation();
            }
        }
        else if (!IsHovering && hoverFadeIn > 0f)
        {
            hoverFadeIn -= HOVER_FADE_SPEED;
            if (hoverFadeIn < 0f) hoverFadeIn = 0f;
        }

        if (animationActive)
        {
            animationTimer += 1f / 60f;

            switch (AnimationStyle)
            {
                case AnimationType.Wag:
                    if (animationTimer >= WAG_DURATION)
                    {
                        animationActive = false;
                        animationTimer = 0f;
                    }
                    break;

                case AnimationType.Swivel:
                    if (animationTimer >= SWIVEL_DURATION)
                    {
                        animationActive = false;
                        animationTimer = 0f;
                    }
                    break;

                case AnimationType.Shake:
                    if (animationTimer >= SHAKE_DURATION)
                    {
                        animationActive = false;
                        animationTimer = 0f;
                    }
                    break;
            }
        }

        ApplyAnimationOffset();
    }

    protected virtual void StartAnimation()
    {
        if (AnimationStyle != AnimationType.Hover)
        {
            animationActive = true;
            animationTimer = 0f;
        }
    }

    protected virtual void ApplyAnimationOffset()
    {
        switch (AnimationStyle)
        {
            case AnimationType.Hover:
                Offset = new Vector2(0, (float)Math.Sin(AnimationTimer * 0.8f) * 3f);
                break;

            case AnimationType.Wag:
                Offset = new Vector2(0, (float)Math.Sin(AnimationTimer * 0.8f) * 2.5f);
                break;

            case AnimationType.Swivel:
                Offset = new Vector2(0, (float)Math.Sin(AnimationTimer * 0.8f) * 3.5f);
                break;

            case AnimationType.Shake:
                var baseOffset = new Vector2(0, (float)Math.Sin(AnimationTimer * 0.8f) * 2f);
                if (animationActive)
                {
                    var progress = animationTimer / SHAKE_DURATION;
                    var intensity = SHAKE_INTENSITY * (1f - progress);
                    var shakeX = (float)(Main.rand.NextDouble() - 0.5) * intensity * 15f;
                    var shakeY = (float)(Main.rand.NextDouble() - 0.5) * intensity * 15f;
                    baseOffset += new Vector2(shakeX, shakeY);
                }
                Offset = baseOffset;
                break;
        }
    }

    public virtual float GetAnimationRotation()
    {
        if (!animationActive)
            return 0f;

        switch (AnimationStyle)
        {
            case AnimationType.Wag:
                var wagProgress = animationTimer / WAG_DURATION;
                var wagIntensity = WAG_INTENSITY * (1f - wagProgress * wagProgress);
                return (float)Math.Sin(wagProgress * Math.PI * 5) * wagIntensity;

            case AnimationType.Swivel:
                var swivelProgress = animationTimer / SWIVEL_DURATION;
                var swivelIntensity = SWIVEL_INTENSITY * (float)Math.Sin(swivelProgress * Math.PI);
                return swivelProgress * (float)Math.PI * 2 * swivelIntensity;

            default:
                return 0f;
        }
    }

    public virtual float GetAnimationScale()
    {
        var baseScale = 1f;

        if (IsHovering)
        {
            switch (AnimationStyle)
            {
                case AnimationType.Hover:
                    baseScale = 1.15f;
                    break;
                case AnimationType.Wag:
                    baseScale = 1.2f;
                    break;
                case AnimationType.Swivel:
                    baseScale = 1.1f;
                    break;
                case AnimationType.Shake:
                    baseScale = 1.05f;
                    break;
            }
        }

        return baseScale;
    }

    public virtual float GetHoverOpacity() => hoverFadeIn;

    protected virtual void PostUpdate() { }

    public bool CheckHovering()
    {
        if (!IsActive)
            return false;


        var worldPosWithOffset = WorldPosition + Offset;
        var screenPos = worldPosWithOffset - Main.screenPosition;

        var zoom = Main.GameViewMatrix.Zoom.X;
        var scaledWidth = (int)(Width * zoom);
        var scaledHeight = (int)(Height * zoom);

        var hitboxRect = new Rectangle(
            (int)screenPos.X - scaledWidth / 2,
            (int)screenPos.Y - scaledHeight / 2,
            scaledWidth,
            scaledHeight
        );

        return hitboxRect.Contains(Main.MouseScreen.ToPoint());
    }

    private void HandleMouseInput()
    {
        if (PlayerInput.IgnoreMouseInterface)
            return;

        if (!CheckHovering())
            return;

        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease)
            return;

        Main.mouseLeftRelease = false;

        OnClick?.Invoke();
    }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        if (!IsActive)
            return;

        if (hideFade <= 0f)
            return;

        var worldPosWithOffset = WorldPosition + Offset;
        var screenPos = worldPosWithOffset - Main.screenPosition;

        if (!PlayerInput.IgnoreMouseInterface && CheckHovering())
        {
            Main.LocalPlayer.mouseInterface = true;
            HandleMouseInput();
        }

        OnDrawWorld?.Invoke(spriteBatch, screenPos, hideFade);
    }

    // Helper method to convert world position to screen position for UI panels
    protected Vector2 GetScreenPosition()
    {
        var worldPosWithOffset = WorldPosition + Offset;
        return Vector2.Transform(worldPosWithOffset - Main.screenPosition, Main.GameViewMatrix.EffectMatrix);
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        Draw(spriteBatch);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom)
    {
        positionAnchorBottom = new Vector2(0);
    }
}

public class ScreenIndicatorManager : ModSystem
{
    public static ScreenIndicatorManager Instance { get; set; }
    public ScreenIndicatorManager() => Instance = this;

    private readonly List<ScreenIndicator> indicators = [];
    private readonly Dictionary<int, ScreenIndicator> npcIndicators = [];
    private readonly Dictionary<int, object> npcTracking = [];

    public override void Unload()
    {
        if (!Main.dedServ)
        {
            Instance = null;
        }
        indicators.Clear();
        npcIndicators.Clear();
        npcTracking.Clear();
    }

    public override void OnWorldUnload()
    {
        base.OnWorldUnload();
        indicators.Clear();
        npcIndicators.Clear();
        npcTracking.Clear();
    }

    public T CreateIndicator<T>(Vector2 worldPosition, params object[] args) where T : ScreenIndicator
    {
        var indicator = (T)Activator.CreateInstance(typeof(T), new object[] { worldPosition }.Concat(args).ToArray());
        indicators.Add(indicator);
        return indicator;
    }

    public T CreateIndicatorForNPC<T>(NPC npc, object trackingKey, params object[] args) where T : ScreenIndicator
    {
        int npcIndex = npc.whoAmI;

        if (npcIndicators.ContainsKey(npcIndex))
        {
            if (npcTracking.ContainsKey(npcIndex) && !npcTracking[npcIndex].Equals(trackingKey))
            {
                var oldIndicator = npcIndicators[npcIndex];
                indicators.Remove(oldIndicator);
                npcIndicators.Remove(npcIndex);

                var indicator = CreateIndicatorForNPCInternal<T>(npc, trackingKey, args);
                return indicator;
            }

            return (T)npcIndicators[npcIndex];
        }

        return CreateIndicatorForNPCInternal<T>(npc, trackingKey, args);
    }

    private T CreateIndicatorForNPCInternal<T>(NPC npc, object trackingKey, params object[] args) where T : ScreenIndicator
    {
        int npcIndex = npc.whoAmI;

        T indicator;
        if (typeof(T) == typeof(JobIndicator))
        {
            var job = (Job)args[0];
            var animationType = args.Length > 1 ? (AnimationType?)args[1] : null;
            indicator = (T)(object)JobIndicator.CreateForNPC(npc, job, animationType);
        }
        else
        {
            throw new NotSupportedException($"Indicator type {typeof(T).Name} not supported");
        }

        indicators.Add(indicator);
        npcIndicators[npcIndex] = indicator;
        npcTracking[npcIndex] = trackingKey;

        return indicator;
    }

    public void RemoveIndicatorForNPC(int npcIndex)
    {
        if (npcIndicators.TryGetValue(npcIndex, out ScreenIndicator indicator))
        {
            indicators.Remove(indicator);
            npcIndicators.Remove(npcIndex);
            npcTracking.Remove(npcIndex);
        }
    }

    public void RemoveIndicatorsForKey(object trackingKey)
    {
        var npcsToRemove = npcTracking
            .Where(kvp => kvp.Value.Equals(trackingKey))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var npcIndex in npcsToRemove)
        {
            RemoveIndicatorForNPC(npcIndex);
        }
    }

    public void RemoveIndicatorsOfType<T>() where T : ScreenIndicator
    {
        var toRemove = indicators.OfType<T>().ToList();
        foreach (var indicator in toRemove)
        {
            indicators.Remove(indicator);

            var npcEntry = npcIndicators.FirstOrDefault(kvp => kvp.Value == indicator);
            if (npcEntry.Key != 0)
            {
                npcIndicators.Remove(npcEntry.Key);
                npcTracking.Remove(npcEntry.Key);
            }
        }
    }

    public bool HasIndicatorForNPC(int npcIndex) => npcIndicators.ContainsKey(npcIndex);

    public T? GetIndicatorForNPC<T>(int npcIndex) where T : ScreenIndicator
    {
        return npcIndicators.TryGetValue(npcIndex, out var indicator) && indicator is T typed ? typed : null;
    }

    public object? GetTrackingKeyForNPC(int npcIndex)
    {
        return npcTracking.TryGetValue(npcIndex, out var key) ? key : null;
    }

    public override void PostUpdateEverything()
    {
        for (var i = indicators.Count - 1; i >= 0; i--)
        {
            var indicator = indicators[i];

            if (indicator.trackingEntity != null && !indicator.trackingEntity.active)
            {
                RemoveIndicatorFromCollections(indicator);
                continue;
            }

            indicator.Update();

            if (!indicator.IsActive)
            {
                RemoveIndicatorFromCollections(indicator);
            }
        }
    }

    private void RemoveIndicatorFromCollections(ScreenIndicator indicator)
    {
        indicators.Remove(indicator);

        var npcEntry = npcIndicators.FirstOrDefault(kvp => kvp.Value == indicator);
        if (npcEntry.Value != null)
        {
            npcIndicators.Remove(npcEntry.Key);
            npcTracking.Remove(npcEntry.Key);
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int insertIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Town NPC House Banners"));
        if (insertIndex != -1)
        {
            layers.Insert(insertIndex + 1, new LegacyGameInterfaceLayer(
                "TrelamiumIII: Job Indicators",
                delegate {
                    DrawIndicators(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game)
            );
        }
    }

    public void DrawIndicators(SpriteBatch spriteBatch)
    {
        foreach (var indicator in indicators)
        {
            indicator.DrawInGame(spriteBatch, Vector2.One);
        }
    }

    public void ClearAllIndicators()
    {
        indicators.Clear();
        npcIndicators.Clear();
        npcTracking.Clear();
    }

    public JobIndicator CreateJobIndicator(Vector2 worldPosition, Job job, AnimationType? animationType = null)
    {
        return CreateIndicator<JobIndicator>(worldPosition, job, animationType);
    }

    public JobIndicator CreateJobIndicatorForNPC(NPC npc, Job job, AnimationType? animationType = null)
    {
        var jobPlayer = Main.LocalPlayer.GetModPlayer<JobPlayer>();
        var localJob = jobPlayer.GetOrCreateJob(job.ID);

        if (localJob?.Progress != JobProgress.Inactive || localJob?.Status != JobStatus.Unlocked)
            return null;

        return CreateIndicatorForNPC<JobIndicator>(npc, job.ID, job, animationType);
    }
}