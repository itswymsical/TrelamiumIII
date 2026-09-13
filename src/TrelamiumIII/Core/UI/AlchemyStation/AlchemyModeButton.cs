using ReLogic.Content;
using Terraria.Audio;
using Terraria.UI;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>Icon button that cycles a station's mode. Each accepted click plays one eased full rotation.</summary>
internal class AlchemyModeButton : UIElement
{
    private const int SPIN_TICKS = 24;

    private static Asset<Texture2D>? _icon;

    public Action? OnClick;
    public Func<bool>? IsEnabled;

    private int _spinTicksLeft;

    public AlchemyModeButton(float size)
    {
        Width = StyleDimension.FromPixels(size);
        Height = StyleDimension.FromPixels(size);
    }

    private bool Enabled => IsEnabled?.Invoke() ?? true;

    public override void LeftClick(UIMouseEvent evt)
    {
        if (!Enabled)
            return;

        SoundEngine.PlaySound(SoundID.MenuTick);
        _spinTicksLeft = SPIN_TICKS;
        OnClick?.Invoke();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (_spinTicksLeft > 0)
            _spinTicksLeft--;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        _icon ??= ModContent.Request<Texture2D>(Assets.Textures.UI.Jobs.CycleMenu.KEY, AssetRequestMode.ImmediateLoad);
        Texture2D icon = _icon.Value;

        CalculatedStyle dimensions = GetDimensions();
        float scale = Math.Min(dimensions.Width / icon.Width, dimensions.Height / icon.Height);
        float progress = 1f - _spinTicksLeft / (float)SPIN_TICKS;
        float rotation = EaseFunction.EaseCubicInOut.Ease(progress) * MathHelper.TwoPi;

        Color color = !Enabled ? new Color(110, 110, 110)
            : IsMouseHovering ? Color.White
            : new Color(205, 205, 215);

        spriteBatch.Draw(icon, dimensions.Center(), null, color, rotation, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);

        if (IsMouseHovering)
            Main.LocalPlayer.mouseInterface = true;
    }
}
