using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;
using TrelamiumIII.Core.ExperientialSystem.Acts;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// The one popup an act ever produces, in two modes.
///
/// <para>
/// <b>Milestone</b> is the quiet one: you have crossed a fraction of the way to the next tier, here is
/// the bar. <b>Complete</b> is the loud one: the tier paid, here is what it paid. They share a panel
/// because they are the same act saying the same kind of thing, and two separate popups for one event
/// meant a tier completion could land on top of its own progress notification.
/// </para>
/// </summary>
public sealed class ExpMilestoneUI
{
    private const float FADE_SPEED = 0.03f;
    private const int DISPLAY_DURATION = 180;

    /// <summary>A completed tier holds longer than a milestone - it is the payoff, not a progress ping.</summary>
    private const int COMPLETE_DURATION = 260;

    private const int PANEL_WIDTH = 250;
    private const int PANEL_HEIGHT = 80;
    private const int PROGRESS_BAR_WIDTH = 180;
    private const int PROGRESS_BAR_HEIGHT = 8;

    /// <summary>Frames the completion flourish runs for: the scale settle, the flash, and the bar slamming full.</summary>
    private const float PUNCH_FRAMES = 26f;

    /// <summary>How much larger the panel starts before easing back to its resting size.</summary>
    private const float PUNCH_SCALE = 1.14f;

    /// <summary>The default fill <see cref="DrawUtils.DrawPanel"/> uses, so an unflashed panel matches every other panel.</summary>
    private static readonly Color PanelBase = new Color(63, 65, 151, 255) * 0.785f;

    private static readonly Color CompleteGreen = new(120, 235, 130);
    private static readonly Color CompleteBar = new(70, 200, 95);

    private readonly Act act;
    private readonly bool isComplete;
    private readonly int currentProgress;
    private readonly int targetThreshold;
    private readonly float targetPercent;
    private readonly float fromPercent;
    private readonly int tierIndex;
    private readonly int exp;

    private float fadeInProgress;
    private float fadeoutProgress;
    private float alpha;
    private bool isFadingOut;
    private int displayTimer;
    private float punchTimer;

    public bool ShouldBeRemoved => isFadingOut && fadeoutProgress >= 1f;

    /// <summary>Whether this is the loud mode. Lets the manager refuse to stomp a completion with a milestone.</summary>
    public bool IsComplete => isComplete;

    private ExpMilestoneUI(Act act, bool isComplete, int currentProgress, int targetThreshold,
        float fromPercent, float targetPercent, int tierIndex, int exp)
    {
        this.act = act;
        this.isComplete = isComplete;
        this.currentProgress = currentProgress;
        this.targetThreshold = targetThreshold;
        this.fromPercent = fromPercent;
        this.targetPercent = targetPercent;
        this.tierIndex = tierIndex;
        this.exp = exp;

        displayTimer = isComplete ? COMPLETE_DURATION : DISPLAY_DURATION;

        SoundEngine.PlaySound(isComplete
            ? SoundID.AchievementComplete
            : SoundID.Research with { Volume = 0.25f });
    }

    /// <summary>Progress toward the next tier.</summary>
    public static ExpMilestoneUI Milestone(Act act, int progress, int nextThreshold, float fromPercent, float percent)
        => new(act, isComplete: false, progress, nextThreshold, fromPercent, percent, tierIndex: -1, exp: 0);

    /// <summary>A tier that has just paid out. <paramref name="exp"/> is already in the meter by now.</summary>
    public static ExpMilestoneUI TierComplete(Act act, int tierIndex, int threshold, int progress, float fromPercent, int exp)
        => new(act, isComplete: true, progress, threshold, fromPercent, 1f, tierIndex, exp);

    #region Animation

    /// <summary>0 at the moment of completion, 1 once the flourish has finished.</summary>
    private float Punch => Math.Clamp(punchTimer / PUNCH_FRAMES, 0f, 1f);

    /// <summary>Overshoots slightly past its target and settles back, so the panel lands rather than snaps.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private float PanelScale => isComplete ? MathHelper.Lerp(PUNCH_SCALE, 1f, EaseOutBack(Punch)) : 1f;

    /// <summary>White wash over the panel at the instant of completion, decaying fast.</summary>
    private float FlashAlpha
    {
        get
        {
            if (!isComplete)
                return 0f;

            float remaining = 1f - Punch;
            return remaining * remaining * 0.55f;
        }
    }

    private float BarPercent => isComplete
        ? MathHelper.Lerp(fromPercent, targetPercent, EaseOutCubic(Punch))
        : targetPercent;

    #endregion

    public void Update()
    {
        if (!isFadingOut && fadeInProgress < 1f)
            fadeInProgress = Math.Min(fadeInProgress + FADE_SPEED, 1f);

        if (punchTimer < PUNCH_FRAMES)
            punchTimer++;

        if (fadeInProgress >= 1f && !isFadingOut)
        {
            displayTimer--;

            if (displayTimer <= 0)
                isFadingOut = true;
        }

        if (isFadingOut)
            fadeoutProgress = Math.Min(fadeoutProgress + FADE_SPEED, 1f);

        alpha = MathHelper.Clamp(isFadingOut ? 1f - fadeoutProgress : fadeInProgress, 0f, 1f);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (alpha <= 0f)
            return;

        var font = FontAssets.MouseText.Value;

        // Under the map.
        int panelX = Main.screenWidth - PANEL_WIDTH - 44;
        int panelY = 348;

        // The flourish grows the panel about its own centre so it does not drift as it settles.
        float scale = PanelScale;
        int grownW = (int)(PANEL_WIDTH * scale);
        int grownH = (int)(PANEL_HEIGHT * scale);

        Rectangle panelRect = new(
            panelX - (grownW - PANEL_WIDTH) / 2,
            panelY - (grownH - PANEL_HEIGHT) / 2,
            grownW, grownH);

        // Tint the panel rather than overlaying a rectangle on it: the panel texture has worked corners,
        // and a square white wash over the top of them reads as a rendering fault rather than a flash.
        Color panelColor = FlashAlpha > 0f
            ? Color.Lerp(PanelBase, CompleteGreen, FlashAlpha)
            : PanelBase;

        DrawUtils.DrawPanel(spriteBatch, panelRect, panelColor, opacity: alpha);

        float innerX = panelRect.X + panelRect.Width / 12f;
        Color nameColor = (isComplete ? CompleteGreen : Color.Gold) * alpha;

        Vector2 namePos = new(innerX, panelRect.Y + panelRect.Height / 5.5f);
        string nameText = $"{act.Name} | {act.Category.DisplayName()}";

        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, nameText, namePos,
            nameColor, Color.Black * alpha, 0f, Vector2.Zero, new Vector2(0.7f * scale));

        // Milestones restate what the act is; a completion says which rung just paid.
        string subText = isComplete
            ? TrelamiumIII.Instance.GetLocalizationValue("ActLang.UI.TierComplete", tierIndex + 1)
            : act.Description;

        Vector2 subPos = new(innerX, panelRect.Y + panelRect.Height / 2.5f);

        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, subText, subPos,
            (isComplete ? CompleteGreen : Color.White) * alpha, Color.Black * alpha, 0f, Vector2.Zero,
            new Vector2(0.65f * scale));

        Vector2 barPos = new(innerX, panelRect.Y + panelRect.Height / 1.6f);
        DrawProgressBar(spriteBatch, barPos, scale);
    }

    private void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float scale)
    {
        int barWidth = (int)(PROGRESS_BAR_WIDTH * scale);
        int barHeight = (int)(PROGRESS_BAR_HEIGHT * scale);

        Rectangle barBg = new((int)position.X, (int)position.Y, barWidth, barHeight);
        Rectangle barFill = new((int)position.X, (int)position.Y, (int)(barWidth * BarPercent), barHeight);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, barBg, new Color(40, 40, 40) * alpha);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, barFill, FillColor * alpha);

        DrawBorder(spriteBatch, barBg, new Color(80, 80, 80) * alpha, 1);

        // The bar doubles as the reward readout on a completion, so the Exp lands where the eye already is.
        string barText = isComplete && exp > 0
            ? TrelamiumIII.Instance.GetLocalizationValue("ActLang.UI.ExpGained", exp)
            : $"{currentProgress} / {targetThreshold}";

        var font = FontAssets.MouseText.Value;
        Vector2 textSize = font.MeasureString(barText) * 0.6f;
        Vector2 textPos = position + new Vector2((barWidth - textSize.X) / 2f, (barHeight - textSize.Y) / 2f + 2f);

        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, barText, textPos,
            Color.White * alpha, Color.Black * alpha, 0f, Vector2.Zero, new Vector2(0.65f));
    }

    /// <summary>Green once the tier is in hand, otherwise brightening as the bar approaches the next rung.</summary>
    private Color FillColor
    {
        get
        {
            if (isComplete)
                return CompleteBar;

            return targetPercent switch
            {
                >= 0.75f => new Color(225, 225, 225),
                >= 0.50f => new Color(200, 200, 200),
                >= 0.25f => new Color(175, 175, 175),
                _ => new Color(150, 150, 150)
            };
        }
    }

    private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        var pixel = TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
    }
}

/// <summary>
/// Holds the single act popup on screen. One at a time on purpose: acts fire in bursts (one ore break can
/// advance an individual act and two groups), and a queue would leave the player reading notifications
/// about mining long after they had stopped.
/// </summary>
public sealed class ActTrackerManager : ModSystem
{
    public static ActTrackerManager? Instance { get; private set; }

    private ExpMilestoneUI? current;

    public ActTrackerManager() => Instance = this;

    public override void Unload()
    {
        Instance = null;
        current = null;
    }

    /// <summary>
    /// Shows a popup, replacing whatever was on screen. A completion outranks a milestone, so a quiet
    /// progress ping from another act cannot stomp the tier the player just earned.
    /// </summary>
    public void Show(ExpMilestoneUI tracker, bool isComplete = false)
    {
        if (!isComplete && current is { ShouldBeRemoved: false, IsComplete: true })
            return;

        current = tracker;
    }

    public void Clear() => current = null;

    public override void PostUpdateEverything()
    {
        if (current is null)
            return;

        current.Update();

        if (current.ShouldBeRemoved)
            current = null;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));

        if (resourceBarIndex == -1)
            return;

        layers.Insert(resourceBarIndex + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Act Tracker",
            delegate
            {
                if (current is not null && !Main.gameMenu)
                    current.Draw(Main.spriteBatch);

                return true;
            },
            InterfaceScaleType.UI));
    }
}
