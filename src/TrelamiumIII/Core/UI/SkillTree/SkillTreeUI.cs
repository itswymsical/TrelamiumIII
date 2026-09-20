using Terraria.UI;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>
/// Places the skill tree panel. Fractions of the UI-space screen rather than pixels, so it keeps its
/// footprint at any resolution and UI scale and stays clear of the job sidebar on the right.
/// </summary>
internal sealed class SkillTreeUI : UIState
{
    private const float PANEL_LEFT = 0.077f;
    private const float PANEL_TOP = 0.08f;
    private const float PANEL_WIDTH = 0.61f;
    private const float PANEL_HEIGHT = 0.72f;

    /// <summary>Panel size floor and ceiling in UI pixels, so neither extreme resolution is unusable.</summary>
    private const float MIN_WIDTH = 420f;
    private const float MIN_HEIGHT = 280f;
    private const float MAX_WIDTH = 1100f;
    private const float MAX_HEIGHT = 780f;

    /// <summary>Pixels the panel starts below its resting place and slides up through the open transition.</summary>
    private const float SLIDE = 26f;

    private SkillTreePanel? _panel;

    public override void OnInitialize()
    {
        Width = Height = StyleDimension.Fill;

        _panel = new SkillTreePanel();
        _panel.SetPadding(0f);

        Append(_panel);
    }

    public void OnOpened() => _panel?.OnOpened();

    /// <summary>The panel moves while the transition runs, so its layout has to be resolved again each frame.</summary>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (SkillTreeSystem.Openness < 1f)
            Recalculate();
    }

    /// <summary>
    /// Sizes the panel before the layout runs. Percent dimensions alone cannot be clamped, so the
    /// fractions are resolved here against the screen the interface reports.
    /// </summary>
    /// <remarks>
    ///     The <see cref="UIState"/> constructor calls this before any field of this class is assigned,
    ///     so the panel has to be null-checked rather than trusted.
    /// </remarks>
    public override void Recalculate()
    {
        if (_panel is null)
        {
            base.Recalculate();

            return;
        }

        CalculatedStyle screen = UserInterface.ActiveInstance.GetDimensions();

        float width = Math.Clamp(screen.Width * PANEL_WIDTH, MIN_WIDTH, MAX_WIDTH);
        float height = Math.Clamp(screen.Height * PANEL_HEIGHT, MIN_HEIGHT, MAX_HEIGHT);

        float slide = SLIDE * (1f - SkillTreeSystem.Fade);

        _panel.Left.Set(MathF.Max(screen.Width * PANEL_LEFT, 0f), 0f);
        _panel.Top.Set(MathF.Max(screen.Height * PANEL_TOP, 0f) + slide, 0f);
        _panel.Width.Set(width, 0f);
        _panel.Height.Set(height, 0f);

        base.Recalculate();
    }

    /// <summary>Drawn after the panel so it is not clipped by the node field.</summary>
    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        _panel?.DrawTooltip(spriteBatch);
    }
}
