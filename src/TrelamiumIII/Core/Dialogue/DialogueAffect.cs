using System;
using Terraria.Audio;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///  An affect applied to a dialogue line. Each affect adjusts the per-character tick state.
///  <para>
///  <see cref="OnReveal"/> adjusts the tick for each revealed character, while the line is being typed; and
///  </para>
///  <para>
///  <see cref="OnDraw"/> adjusts the per-glyph render state for span affects. Both are called for each affected character, but in practice most affects will only need to override one of them.
///  </para>
/// </summary>
public abstract class DialogueAffect
{
    public virtual void OnReveal(DialogueTick tick) { }
    public virtual void OnDraw(DialogueRenderContext render) { }
}

/// <summary>Where a glyph sits inside the span its affect covers.</summary>
public readonly struct SpanCursor
{
    /// <summary>Glyphs the span covers, never below 1.</summary>
    public int Length { get; }

    /// <summary>This glyph's position in the span, from 0 to <see cref="Length"/> - 1.</summary>
    public int Offset { get; }

    /// <summary>This glyph's position in the line.</summary>
    public int Index { get; }

    public SpanCursor(int index, int start, int end)
    {
        Length = Math.Max(end - start, 1);
        Offset = Math.Clamp(index - start, 0, Length - 1);
        Index = index;
    }

    /// <summary>0 on the span's first glyph, 1 on its last. A one-glyph span reads 1.</summary>
    public float Progress => Length > 1 ? Offset / (float)(Length - 1) : 1f;
}

/// <summary>Per-character state an affect adjusts as a glyph is revealed: typing speed, hold, pitch, one-shot sound.</summary>
public sealed class DialogueTick
{
    public int CharIndex { get; private set; }
    public float SpeedMultiplier { get; set; }
    public int HoldFrames { get; set; }
    public float Pitch { get; set; }
    public SoundStyle? Sound { get; set; }

    /// <summary>This glyph's place in the span of the affect being run.</summary>
    public SpanCursor Span { get; private set; }

    public void Reset(int charIndex)
    {
        CharIndex = charIndex;
        Span = new SpanCursor(charIndex, charIndex, charIndex + 1);
        SpeedMultiplier = 1f;
        HoldFrames = 0;
        Pitch = 1f;
        Sound = null;
    }

    internal void BeginSpan(int start, int end) => Span = new SpanCursor(CharIndex, start, end);
}

/// <summary>Per-glyph state an affect adjusts each frame while drawing: draw offset and color.</summary>
public sealed class DialogueRenderContext
{
    public int GlyphIndex { get; private set; }
    public Vector2 Offset { get; set; }
    public Color Color { get; set; }

    /// <summary>This glyph's place in the span of the affect being run.</summary>
    public SpanCursor Span { get; private set; }

    public void Reset(int glyphIndex, Color baseColor)
    {
        GlyphIndex = glyphIndex;
        Span = new SpanCursor(glyphIndex, glyphIndex, glyphIndex + 1);
        Offset = Vector2.Zero;
        Color = baseColor;
    }

    internal void BeginSpan(int start, int end) => Span = new SpanCursor(GlyphIndex, start, end);
}

/// <summary>Hold the typewriter for <c>frames</c> after this glyph (replaces &lt;pause&gt;).</summary>
public sealed class Pause(int frames = 60) : DialogueAffect
{
    public override void OnReveal(DialogueTick tick) => tick.HoldFrames = frames;
}

/// <summary>Type the covered span <c>factor</c>x slower (replaces &lt;slow&gt;).</summary>
public sealed class Slow(float factor = 2f) : DialogueAffect
{
    public override void OnReveal(DialogueTick tick) => tick.SpeedMultiplier = factor <= 0f ? 1f : 1f / factor;
}

/// <summary>Type the covered span <c>factor</c>x faster (replaces &lt;fast&gt;).</summary>
public sealed class Fast(float factor = 2f) : DialogueAffect
{
    public override void OnReveal(DialogueTick tick) => tick.SpeedMultiplier = factor <= 0f ? 1f : factor;
}

/// <summary>Shift the voice pitch across the covered span (replaces &lt;pitch&gt;; &lt;1 lower, &gt;1 higher).</summary>
public sealed class Pitch(float pitch) : DialogueAffect
{
    public override void OnReveal(DialogueTick tick) => tick.Pitch = pitch;
}

/// <summary>Play a one-shot sound as the covered glyph is revealed.</summary>
public sealed class Sound(SoundStyle sound) : DialogueAffect
{
    public override void OnReveal(DialogueTick tick) => tick.Sound = sound;
}

/// <summary>Jitter the covered glyphs (replaces &lt;shake&gt;).</summary>
public sealed class Shake(float intensity = 2f) : DialogueAffect
{
    public override void OnDraw(DialogueRenderContext r)
    {
        var time = Main.GameUpdateCount * 0.8f;
        int i = r.GlyphIndex;
        r.Offset += new Vector2(
            (float)(Math.Sin(time + i * 1.2f) * intensity + Math.Sin(time * 1.7f + i * 0.8f) * intensity * 0.6f),
            (float)(Math.Cos(time * 1.3f + i * 0.9f) * intensity * 0.8f + Math.Sin(time * 2.1f + i * 0.7f) * intensity * 0.4f));
    }
}

/// <summary>Sine-wave the covered glyphs up and down (replaces &lt;sine&gt;).</summary>
public sealed class Wave(float intensity = 2f) : DialogueAffect
{
    public override void OnDraw(DialogueRenderContext r)
    {
        var time = Main.GlobalTimeWrappedHourly * 3f;
        r.Offset += new Vector2(0f, (float)Math.Sin(time + r.GlyphIndex * 0.5f) * intensity * 1.5f);
    }
}
