using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;

namespace TrelamiumIII.Core.UI.AlchemyStation;

internal class AlchemyButton : UIElement
{
    private readonly string _label;
    private readonly float _scale;

    public Action OnClick;
    public Action OnRightClick;
    public Func<bool> IsEnabled;
    public Func<string> Tooltip;

    public AlchemyButton(string label, float width = 18f, float height = 18f, float scale = 0.85f)
    {
        _label = label;
        _scale = scale;
        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(height);
    }

    private bool Enabled => IsEnabled?.Invoke() ?? true;

    public override void LeftClick(UIMouseEvent evt)
    {
        if (!Enabled) return;
        SoundEngine.PlaySound(SoundID.MenuTick);
        OnClick?.Invoke();
    }

    public override void RightClick(UIMouseEvent evt)
    {
        if (!Enabled) return;
        if (OnRightClick == null) return;
        SoundEngine.PlaySound(SoundID.MenuTick);
        OnRightClick.Invoke();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        var bounds = GetDimensions().ToRectangle();
        var pixel = TextureAssets.MagicPixel.Value;
        bool enabled = Enabled;

        Color bg = !enabled
            ? new Color(20, 20, 20, 180)
            : IsMouseHovering
                ? new Color(85, 85, 95, 220)
                : new Color(45, 45, 55, 200);

        Color border = !enabled
            ? new Color(60, 60, 60, 200)
            : IsMouseHovering
                ? new Color(180, 180, 200, 230)
                : new Color(110, 110, 130, 220);

        var font = FontAssets.MouseText.Value;
        var size = font.MeasureString(_label) * _scale;
        var pos = new Vector2(
            bounds.X + (bounds.Width - size.X) * 0.5f,
            bounds.Y + (bounds.Height - size.Y) * 0.5f - 1f);

        Color textColor = enabled ? Color.White : new Color(140, 140, 140);
        Utils.DrawBorderString(spriteBatch, _label, pos, textColor, _scale);

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            var tip = Tooltip?.Invoke();
            if (!string.IsNullOrEmpty(tip))
            {
                Main.hoverItemName = tip;
                Main.mouseText = true;
            }
        }
    }
}
