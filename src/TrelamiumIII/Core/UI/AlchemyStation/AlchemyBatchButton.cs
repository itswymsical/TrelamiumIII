using Terraria.Audio;
using Terraria.UI;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>Batch size presets cycled by <see cref="AlchemyBatchButton" />.</summary>
internal static class AlchemyBatchSizes
{
    public static readonly int[] Presets = [1, 5, 10, 30, 100, 999, 9999];

    /// <summary>Preset <paramref name="index" /> clamped to 1..<paramref name="max" />.</summary>
    public static int Of(int index, int max) => Math.Clamp(Presets[index], 1, Math.Max(1, max));

    /// <summary>
    /// The preset after <paramref name="index" />. Returns to the first preset once the clamped size has reached
    /// <paramref name="max" />, so the presets above the max show it once instead of repeating it.
    /// </summary>
    public static int Next(int index, int max) => index >= Presets.Length - 1 || Presets[index] >= max ? 0 : index + 1;
}

/// <summary>Framed "Nx" batch size label that cycles its preset on click. The label rolls upward when the size changes.</summary>
internal class AlchemyBatchButton : UIElement
{
    private const int ROLL_TICKS = 9;
    private const float ROLL_DISTANCE = 6f;
    private const int FRAME_EDGE = 10;

    private static readonly Color CappedColor = new(255, 220, 150);

    private readonly float _textScale;

    public Action? OnClick;
    public Func<bool>? IsEnabled;
    public Func<int>? Count;

    /// <summary>True while <see cref="Count" /> is below the selected preset.</summary>
    public Func<bool>? IsCapped;

    private int _shown = 1;
    private int _previous = 1;
    private int _rollTicksLeft;

    public AlchemyBatchButton(float width, float height, float textScale)
    {
        _textScale = textScale;
        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(height);
    }

    private bool Enabled => IsEnabled?.Invoke() ?? true;

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

        if (_rollTicksLeft > 0)
            _rollTicksLeft--;

        int count = Count?.Invoke() ?? 1;

        if (count == _shown)
            return;

        _previous = _shown;
        _shown = count;
        _rollTicksLeft = ROLL_TICKS;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        bool enabled = Enabled;
        bool capped = IsCapped?.Invoke() ?? false;
        Rectangle box = GetDimensions().ToRectangle();

        Color frame = !enabled ? new Color(120, 120, 130) : IsMouseHovering ? Color.White : new Color(205, 205, 215);
        Utils.DrawSplicedPanel(spriteBatch, AlchemySlotArt.Bottle(inUse: true).Value, box.X, box.Y, box.Width, box.Height,
            FRAME_EDGE, FRAME_EDGE, FRAME_EDGE, FRAME_EDGE, frame);

        Color text = !enabled ? new Color(140, 140, 140) : capped ? CappedColor : Color.White;
        float roll = EaseFunction.EaseQuadOut.Ease(1f - _rollTicksLeft / (float)ROLL_TICKS);

        if (roll < 1f)
            DrawLabel(spriteBatch, box, _previous, -roll * ROLL_DISTANCE, text * (1f - roll));

        DrawLabel(spriteBatch, box, _shown, (1f - roll) * ROLL_DISTANCE, text * roll);

        if (!IsMouseHovering)
            return;

        Main.LocalPlayer.mouseInterface = true;
        Main.hoverItemName = capped ? "Batch size (max)\nClick to cycle" : "Batch size\nClick to cycle";
        Main.mouseText = true;
    }

    private void DrawLabel(SpriteBatch spriteBatch, Rectangle box, int count, float offsetY, Color color) =>
        Utils.DrawBorderString(spriteBatch, $"{count}x", new Vector2(box.Center.X, box.Center.Y + 1f + offsetY), color,
            _textScale, anchorx: 0.5f, anchory: 0.5f);
}
