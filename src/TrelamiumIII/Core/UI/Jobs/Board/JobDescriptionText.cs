using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     Job description text, word-wrapped to the element's width and drawn a character at a time so
///     the <c>shake</c> / <c>sine</c> / <c>glow</c> markup in the localisation files still animates.
/// </summary>
/// <remarks>
///     Wrapping has to happen after <see cref="JobTextParser" /> has stripped the markup, because
///     the effect table it returns is keyed by index into the <em>clean</em> text. Lines therefore
///     carry the index they start at, which is what keeps each character matched to its effect once
///     the text has been broken up.
/// </remarks>
internal sealed class JobDescriptionText : UIElement
{
    private const float TEXT_SCALE = 0.85f;
    private const float LINE_HEIGHT = 19f;

    private readonly List<(string Text, int Start)> _lines = [];

    private Dictionary<int, JobTextEffect> _effects = [];

    public Func<float>? Opacity;

    public Color BaseColor { get; set; } = new(215, 215, 225);

    /// <summary>
    ///     Most lines to show before the text is cut off with an ellipsis. 0 means no limit. The
    ///     panel sets this from the room it actually has, so a long brief runs out of space tidily
    ///     instead of overrunning the reward grid below it.
    /// </summary>
    public int MaxLines { get; set; }

    public JobDescriptionText(float width)
    {
        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(0f);
    }

    private float Alpha => Opacity?.Invoke() ?? 1f;

    public void SetText(string raw)
    {
        _lines.Clear();

        (string plain, Dictionary<int, JobTextEffect> effects) = JobTextParser.ParseDescription(raw ?? string.Empty);
        _effects = effects;

        Wrap(plain);
        Clamp();

        Height = StyleDimension.FromPixels(_lines.Count * LINE_HEIGHT);
        Recalculate();
    }

    /// <summary>
    ///     Greedy word wrap that also honours the line breaks already in the text. Tracks each
    ///     line's offset into the source string rather than rebuilding it, so effect indices survive.
    /// </summary>
    private void Wrap(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var font = FontAssets.MouseText.Value;
        float maxWidth = Width.Pixels;

        int lineStart = 0;
        int lastBreak = -1; // Index of the most recent space we could fall back to.

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lines.Add((text[lineStart..i], lineStart));
                lineStart = i + 1;
                lastBreak = -1;
                continue;
            }

            if (text[i] == ' ')
                lastBreak = i;

            float width = font.MeasureString(text[lineStart..(i + 1)]).X * TEXT_SCALE;
            if (width <= maxWidth)
                continue;

            // Break at the last space when there is one; otherwise hard-break a word too long to fit.
            int breakAt = lastBreak > lineStart ? lastBreak : i;

            _lines.Add((text[lineStart..breakAt], lineStart));
            lineStart = lastBreak > lineStart ? breakAt + 1 : breakAt;
            lastBreak = -1;
        }

        if (lineStart < text.Length)
            _lines.Add((text[lineStart..], lineStart));
    }

    /// <summary>
    ///     Drops anything past <see cref="MaxLines" /> and marks the cut with an ellipsis. The
    ///     trailing "..." is appended to the last line's text but keeps that line's start index, so
    ///     the extra characters simply fall off the end of the effect table and draw unstyled.
    /// </summary>
    private void Clamp()
    {
        if (MaxLines <= 0 || _lines.Count <= MaxLines)
            return;

        _lines.RemoveRange(MaxLines, _lines.Count - MaxLines);

        (string text, int start) = _lines[^1];
        _lines[^1] = (text.TrimEnd() + "...", start);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        float alpha = Alpha;
        if (alpha <= 0f || _lines.Count == 0)
            return;

        var font = FontAssets.MouseText.Value;
        Rectangle bounds = GetDimensions().ToRectangle();
        var time = (float)Main.timeForVisualEffects;

        for (int line = 0; line < _lines.Count; line++)
        {
            (string text, int start) = _lines[line];

            float x = bounds.X;
            float y = bounds.Y + line * LINE_HEIGHT;

            for (int i = 0; i < text.Length; i++)
            {
                string glyph = text[i].ToString();
                float advance = font.MeasureString(glyph).X * TEXT_SCALE;

                // Spaces have no ink; stepping over them saves a draw call each.
                if (text[i] != ' ')
                    DrawGlyph(spriteBatch, font, glyph, new Vector2(x, y), start + i, time, alpha);

                x += advance;
            }
        }
    }

    private void DrawGlyph(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font, string glyph,
        Vector2 position, int index, float time, float alpha)
    {
        Color color = BaseColor;
        Vector2 offset = Vector2.Zero;

        if (_effects.TryGetValue(index, out JobTextEffect effect))
        {
            offset = effect.Type switch
            {
                TextEffectType.Shake => new Vector2(
                    Main.rand.NextFloat(-effect.Intensity, effect.Intensity),
                    Main.rand.NextFloat(-effect.Intensity, effect.Intensity)),
                TextEffectType.Sine => new Vector2(0f, (float)Math.Sin(time * 0.1f + index * 0.5f) * effect.Intensity),
                _ => Vector2.Zero
            };

            if (effect.Type == TextEffectType.Glow)
            {
                color = Color.Lerp(
                    BaseColor,
                    effect.GlowColor ?? Color.Gold,
                    (float)Math.Sin(time * 0.1f + index * 0.3f) * 0.5f + 0.5f);
            }
        }

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            font,
            glyph,
            position.X + offset.X,
            position.Y + offset.Y,
            color * alpha,
            Color.Black * alpha,
            Vector2.Zero,
            TEXT_SCALE);
    }
}
