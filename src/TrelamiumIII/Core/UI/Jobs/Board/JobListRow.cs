using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     One job in the board's master list. Selecting a row only changes what the detail pane shows —
///     taking the job is a separate, deliberate step.
/// </summary>
internal sealed class JobListRow : UIElement
{
    private const float TEXT_SCALE = 0.85f;

    private readonly Job _job;
    private readonly Func<Job?> _selected;

    private float _hoverFade;

    public Action<Job>? OnSelected;
    public Func<float>? Opacity;

    public JobListRow(Job job, float width, float height, Func<Job> selected)
    {
        _job = job;
        _selected = selected;

        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(height);
    }

    private bool IsSelected => _selected?.Invoke() == _job;

    private float Alpha => Opacity?.Invoke() ?? 1f;

    public override void LeftClick(UIMouseEvent evt)
    {
        if (IsSelected)
            return;

        SoundEngine.PlaySound(SoundID.MenuTick);
        OnSelected?.Invoke(_job);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        _hoverFade = MathHelper.Clamp(_hoverFade + (IsMouseHovering ? 0.18f : -0.18f), 0f, 1f);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        float alpha = Alpha;
        if (alpha <= 0f)
            return;

        Rectangle bounds = GetDimensions().ToRectangle();
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        bool selected = IsSelected;

        // Hover and selection share one highlight so the row the cursor is on and the row being
        // previewed never look like two different kinds of state.
        float highlight = Math.Max(_hoverFade * 0.45f, selected ? 1f : 0f);

        if (highlight > 0f)
        {
            spriteBatch.Draw(pixel, bounds, new Color(90, 110, 175) * (0.55f * highlight * alpha));

            // A bar down the leading edge marks the selection itself, which reads at a glance even
            // when the hover tint is sitting on a neighbouring row.
            if (selected)
                spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height),
                    new Color(255, 215, 120) * alpha);
        }

        var font = FontAssets.MouseText.Value;
        Color text = selected ? new Color(255, 235, 175) : Color.Lerp(new Color(200, 200, 210), Color.White, _hoverFade);

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            font,
            Truncate(font, _job.Name, bounds.Width - 12f),
            bounds.X + 8,
            bounds.Y + (bounds.Height - font.MeasureString("A").Y * TEXT_SCALE) / 2f,
            text * alpha,
            Color.Black * alpha,
            Vector2.Zero,
            TEXT_SCALE);

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;
    }

    /// <summary>Clips an over-long job name to an ellipsis so it cannot run into the detail pane.</summary>
    private static string Truncate(ReLogic.Graphics.DynamicSpriteFont font, string text, float maxWidth)
    {
        if (font.MeasureString(text).X * TEXT_SCALE <= maxWidth)
            return text;

        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length] + "...";

            if (font.MeasureString(candidate).X * TEXT_SCALE <= maxWidth)
                return candidate;
        }

        return "...";
    }
}
