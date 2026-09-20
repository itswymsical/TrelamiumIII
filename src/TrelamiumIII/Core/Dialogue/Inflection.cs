using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>How an <see cref="Inflection"/> travels between its base and target pitch across a span.</summary>
public enum InflectionShape
{
    /// <summary>Base pitch on the first glyph, target on the last.</summary>
    Glide,
    /// <summary>Target at the middle of the span, base pitch again at its end.</summary>
    Arc,
    /// <summary>Target on the first glyph, base pitch on the last.</summary>
    Settle,
    /// <summary>Target pitch on every glyph, with no travel.</summary>
    Flat
}

/// <summary>
///   Bends voice pitch across the covered span: <c>Did[n't] you?</c> tweens from <c>from</c>
///   to <c>to</c> over however many glyphs the anchor holds. Pitch is a multiplier on the
///   speaker's normal pitch, clamped to [0, 2] because FNA reaches one octave either way.
/// </summary>
public sealed class Inflection : DialogueAffect
{
    private const float MIN_PITCH = 0f;
    private const float MAX_PITCH = 2f;

    private readonly float to;
    private readonly float from;
    private readonly InflectionShape shape;
    private readonly EaseFunction ease;
    private readonly float vibrato;
    private readonly float vibratoRate;
    private readonly int steps;
    private readonly float jitter;
    private readonly float speed;
    private readonly float lift;

    /// <param name="to">Pitch the span bends toward. 1 is the speaker's normal pitch.</param>
    /// <param name="from">Pitch the span starts from.</param>
    /// <param name="shape">Path taken between the two pitches.</param>
    /// <param name="ease">Curve of that path; defaults to <see cref="EaseFunction.EaseQuadInOut"/>.</param>
    /// <param name="vibrato">Pitch wobble added on top of the curve, in pitch units.</param>
    /// <param name="vibratoRate">Wobble cycles per glyph; 0.5 alternates high and low glyph by glyph.</param>
    /// <param name="steps">Quantises the travel into this many pitches; 0 leaves it smooth.</param>
    /// <param name="jitter">Per-glyph random spread, in pitch units. Deterministic per glyph index.</param>
    /// <param name="speed">Typing speed multiplier over the span, as <see cref="Fast"/> applies it.</param>
    /// <param name="lift">Pixels the glyphs rise at full upward bend, and sink at a downward one.</param>
    public Inflection(
        float to = 1.25f,
        float from = 1f,
        InflectionShape shape = InflectionShape.Glide,
        EaseFunction? ease = null,
        float vibrato = 0f,
        float vibratoRate = 0.5f,
        int steps = 0,
        float jitter = 0f,
        float speed = 1f,
        float lift = 0f)
    {
        this.to = to;
        this.from = from;
        this.shape = shape;
        this.ease = ease ?? EaseFunction.EaseQuadInOut;
        this.vibrato = vibrato;
        this.vibratoRate = vibratoRate;
        this.steps = steps;
        this.jitter = jitter;
        this.speed = speed;
        this.lift = lift;
    }

    /// <summary>Rises to <paramref name="amount"/> above normal pitch by the end of the span.</summary>
    public static Inflection Up(float amount = 0.25f, InflectionShape shape = InflectionShape.Glide, EaseFunction? ease = null, float lift = 0f)
        => new(1f + amount, shape: shape, ease: ease, lift: lift);

    /// <summary>Falls to <paramref name="amount"/> below normal pitch by the end of the span.</summary>
    public static Inflection Down(float amount = 0.25f, InflectionShape shape = InflectionShape.Glide, EaseFunction? ease = null, float lift = 0f)
        => new(1f - amount, shape: shape, ease: ease, lift: lift);

    /// <summary>Late upward bend, the lift a question ends on.</summary>
    public static Inflection Question(float amount = 0.3f, float lift = 0f)
        => new(1f + amount, ease: EaseFunction.EaseQuadIn, lift: lift);

    /// <summary>Bends up and settles back, for a stressed word.</summary>
    public static Inflection Emphasis(float amount = 0.2f, float lift = 0f)
        => new(1f + amount, shape: InflectionShape.Arc, lift: lift);

    /// <summary>Unsteady pitch around normal, for a shaken or failing voice.</summary>
    public static Inflection Waver(float depth = 0.12f, float rate = 0.5f)
        => new(1f, shape: InflectionShape.Flat, vibrato: depth, vibratoRate: rate);

    public override void OnReveal(DialogueTick tick)
    {
        tick.Pitch = PitchAt(tick.Span);

        if (speed != 1f)
            tick.SpeedMultiplier = speed;
    }

    public override void OnDraw(DialogueRenderContext render)
    {
        if (lift == 0f)
            return;

        // Glyphs ride the same curve the voice does, normalised so lift reads in pixels.
        float reach = MathF.Abs(to - from);
        float deviation = PitchAt(render.Span) - from;

        render.Offset -= new Vector2(0f, (reach > 0.0001f ? deviation / reach : deviation) * lift);
    }

    private float PitchAt(SpanCursor span)
    {
        // Arc samples glyph centres, so even a two-glyph span reaches its peak instead of sitting on
        // both endpoints. The others run end to end, and a one-glyph span holds the target pitch.
        float weight = shape switch
        {
            InflectionShape.Arc => MathF.Sin(MathF.PI * (span.Offset + 0.5f) / span.Length),
            InflectionShape.Settle => span.Length > 1 ? 1f - span.Progress : 1f,
            InflectionShape.Flat => 1f,
            _ => span.Progress
        };

        weight = ease.Ease(weight);

        if (steps > 1)
            weight = MathF.Round(weight * (steps - 1)) / (steps - 1);

        float pitch = MathHelper.Lerp(from, to, weight);

        // Sampled half a glyph in: on whole glyphs the default rate of half a cycle each would
        // land on the zero crossing every time.
        if (vibrato != 0f)
            pitch += vibrato * MathF.Sin((span.Offset + 0.5f) * vibratoRate * MathHelper.TwoPi);

        // Keyed to the line index rather than the span's, so two jittered spans do not wobble alike.
        if (jitter != 0f)
            pitch += jitter * Spread(span.Index);

        return Math.Clamp(pitch, MIN_PITCH, MAX_PITCH);
    }

    /// <summary>Hashed noise in [-1, 1]. Hashed rather than rolled so a glyph keeps its offset across frames.</summary>
    private static float Spread(int index)
    {
        unchecked
        {
            int hash = index * 374761393 + 668265263;
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return ((hash ^ (hash >> 16)) & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
