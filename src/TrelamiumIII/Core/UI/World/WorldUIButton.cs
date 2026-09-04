using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;

namespace TrelamiumIII.Core.UI.World;

/// <summary>
///     A labelled button for <see cref="WorldPanel" />s. Fades with the panel that owns it, which is
///     why it takes an opacity source rather than drawing at full strength like a docked UI button.
/// </summary>
internal sealed class WorldUIButton : UIElement
{
    private readonly float _scale;

    private float _hoverFade;

    public string Label { get; set; }

    public Action? OnClick;
    public Func<bool>? IsEnabled;
    public Func<float>? Opacity;

    /// <summary>Tints the button for weight — an accept reads differently to a cancel.</summary>
    public Color Accent { get; set; } = new(120, 160, 255);

    public WorldUIButton(string label, float width, float height, float scale = 0.9f)
    {
        Label = label;
        _scale = scale;

        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(height);
    }

    private bool Enabled => IsEnabled?.Invoke() ?? true;

    private float Alpha => Opacity?.Invoke() ?? 1f;

    public override void LeftClick(UIMouseEvent evt)
    {
        if (!Enabled)
            return;

        SoundEngine.PlaySound(SoundID.MenuTick);
        OnClick?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        _hoverFade = MathHelper.Clamp(_hoverFade + (IsMouseHovering && Enabled ? 0.15f : -0.15f), 0f, 1f);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        float alpha = Alpha;
        if (alpha <= 0f)
            return;

        Rectangle bounds = GetDimensions().ToRectangle();
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        bool enabled = Enabled;

        Color fill = enabled
            ? Color.Lerp(new Color(28, 30, 44), Accent * 0.55f, _hoverFade)
            : new Color(20, 20, 24);

        Color border = enabled
            ? Color.Lerp(Accent * 0.7f, Accent, _hoverFade)
            : new Color(60, 60, 66);

        spriteBatch.Draw(pixel, bounds, fill * (0.92f * alpha));
        DrawBorder(spriteBatch, pixel, bounds, border * alpha);

        var font = FontAssets.MouseText.Value;
        Vector2 size = font.MeasureString(Label) * _scale;
        var position = new Vector2(
            bounds.X + (bounds.Width - size.X) * 0.5f,
            bounds.Y + (bounds.Height - size.Y) * 0.5f - 1f);

        Color text = enabled ? Color.White : new Color(130, 130, 136);
        Utils.DrawBorderString(spriteBatch, Label, position, text * alpha, _scale);

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;
    }

    private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }
}
