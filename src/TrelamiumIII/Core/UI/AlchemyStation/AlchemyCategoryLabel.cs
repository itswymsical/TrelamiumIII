using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Centered single-line label. A text change fades the current text out, then reveals the new text glyph by glyph.
/// Color changes apply immediately.
/// </summary>
internal class AlchemyCategoryLabel : UIElement
{
    private const int FADE_TICKS = 8;
    private const int REVEAL_TICKS = 14;
    private const float GLYPH_FADE_TICKS = 5f;
    private const float GLYPH_RISE = 4f;

    private readonly float _scale;

    private string _shown = string.Empty;
    private string _target = string.Empty;
    private Color _color = Color.White;

    private int _fadeTicksLeft;
    private int _revealTicks;

    public AlchemyCategoryLabel(float scale) => _scale = scale;

    public void SetText(string text, Color color)
    {
        _color = color;

        if (text == _target)
            return;

        _target = text;

        if (text == _shown)
        {
            _fadeTicksLeft = 0;
            return;
        }

        if (_fadeTicksLeft == 0)
            _fadeTicksLeft = _shown.Length == 0 ? 1 : FADE_TICKS;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (_fadeTicksLeft > 0)
        {
            if (--_fadeTicksLeft == 0)
            {
                _shown = _target;
                _revealTicks = 0;
            }

            return;
        }

        if (_revealTicks < REVEAL_TICKS + GLYPH_FADE_TICKS)
            _revealTicks++;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (_shown.Length == 0)
            return;

        var font = FontAssets.MouseText.Value;
        CalculatedStyle dimensions = GetDimensions();
        Vector2 size = font.MeasureString(_shown) * _scale;
        Vector2 origin = new(dimensions.X + (dimensions.Width - size.X) * 0.5f, dimensions.Y + (dimensions.Height - size.Y) * 0.5f);

        float fade = _fadeTicksLeft > 0 ? _fadeTicksLeft / (float)FADE_TICKS : 1f;
        float advance = 0f;

        for (int i = 0; i < _shown.Length; i++)
        {
            string glyph = _shown[i].ToString();
            float start = i * REVEAL_TICKS / (float)_shown.Length;
            float reveal = Math.Clamp((_revealTicks - start) / GLYPH_FADE_TICKS, 0f, 1f);
            float alpha = fade * reveal;

            if (alpha > 0f)
            {
                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, glyph,
                    origin + new Vector2(advance, (1f - reveal) * GLYPH_RISE), _color * alpha, 0f, Vector2.Zero, new Vector2(_scale));
            }

            advance += font.MeasureString(glyph).X * _scale;
        }
    }
}
